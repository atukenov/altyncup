using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>DELETE admin/orders/{id}: super admin only hard delete.</summary>
[Collection("Integration")]
public class OrderDeletionTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private static async Task<Guid> PlaceOrderAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, string phone,
        string? discountCode = null, decimal? pointsToSpend = null)
    {
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, customerToken);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(appFactory.Services, "Espresso");
        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId           = LocationId,
            paymentMethod        = "Cash",
            discountCode,
            loyaltyPointsToSpend = pointsToSpend,
            items                = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        });
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        return order!.Id;
    }

    private static Task<string> SuperAdminTokenAsync(WebApplicationFactory<Program> appFactory, HttpClient client)
        => ApiHelpers.CreateAdminTokenAsync(
            appFactory.Services, client, username: "deletesuper", password: "Super@1234", role: AdminRole.SuperAdmin);

    [Theory]
    [InlineData(AdminRole.Worker, "deleteworker")]
    [InlineData(AdminRole.Admin, "deleteadmin")]
    public async Task NonSuperAdmin_Returns403_AndKeepsOrder(AdminRole role, string username)
    {
        var client  = factory.CreateClient();
        var orderId = await PlaceOrderAsync(factory, client, role == AdminRole.Worker ? "+77004000001" : "+77004000002");

        var token = await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, client, username: username, password: "Role@12345", role: role);
        ApiHelpers.Authorize(client, token);

        var resp = await client.DeleteAsync($"/api/v1/admin/orders/{orderId}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.True(await db.Orders.AnyAsync(o => o.Id == orderId));
    }

    [Fact]
    public async Task Customer_Returns403()
    {
        var client  = factory.CreateClient();
        var orderId = await PlaceOrderAsync(factory, client, "+77004000003");

        // Still authorized as the order's own customer
        var resp = await client.DeleteAsync($"/api/v1/admin/orders/{orderId}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_DeletesOrderWithItemsAndPayments_AndWritesAuditLog()
    {
        var client  = factory.CreateClient();
        var orderId = await PlaceOrderAsync(factory, client, "+77004000004");
        (await client.PostAsJsonAsync("/api/v1/payments/create", new
        {
            orderId, provider = "KaspiSandbox", sandboxBehavior = "Default"
        })).EnsureSuccessStatusCode();

        ApiHelpers.Authorize(client, await SuperAdminTokenAsync(factory, client));
        var resp = await client.DeleteAsync($"/api/v1/admin/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/admin/orders/{orderId}")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.False(await db.Orders.AnyAsync(o => o.Id == orderId));
        Assert.False(await db.OrderItems.AnyAsync(i => i.OrderId == orderId));
        Assert.False(await db.Payments.AnyAsync(p => p.OrderId == orderId));

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "OrderDeleted" && a.EntityId == orderId.ToString());
        Assert.Equal("deletesuper", audit.PerformedByUsername);
        Assert.Contains("+77004000004", audit.Details);
    }

    [Fact]
    public async Task SuperAdmin_UnknownOrder_Returns404()
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await SuperAdminTokenAsync(factory, client));

        var resp = await client.DeleteAsync($"/api/v1/admin/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_GivesBackDiscountCodeUse()
    {
        var code = await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "DELETEUSE",
            DiscountType.Percentage, 10m, maxUses: 1, expiresAt: DateTime.UtcNow.AddYears(1));
        var client  = factory.CreateClient();
        var orderId = await PlaceOrderAsync(factory, client, "+77004000005", discountCode: "DELETEUSE");

        ApiHelpers.Authorize(client, await SuperAdminTokenAsync(factory, client));
        (await client.DeleteAsync($"/api/v1/admin/orders/{orderId}")).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.Equal(0, (await db.DiscountCodes.SingleAsync(d => d.Id == code.Id)).UsedCount);
    }

    // ── Loyalty holds ────────────────────────────────────────────────────────

    private WebApplicationFactory<Program> LoyaltyFactory(IIikoApiClient client) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(new IikoOptions
                {
                    Enabled        = true,
                    EarnPercent    = 5m,
                    OrganizationId = Guid.NewGuid(),
                    ProgramId      = Guid.NewGuid(),
                });
                services.AddSingleton(client);
            }));

    [Fact]
    public async Task Delete_ReleasesLoyaltyHold()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77004000006", 100m);
        using var f = LoyaltyFactory(fake);
        var client  = f.CreateClient();
        var orderId = await PlaceOrderAsync(f, client, "+77004000006", pointsToSpend: 1m);
        Assert.Equal(1, fake.ActiveHoldCount);

        ApiHelpers.Authorize(client, await SuperAdminTokenAsync(f, client));
        var resp = await client.DeleteAsync($"/api/v1/admin/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Single(fake.CancelHoldCalls);
        Assert.Equal(0, fake.ActiveHoldCount);
    }

    [Fact]
    public async Task Delete_HoldReleaseFails_Returns409AndKeepsOrderForRetry()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77004000007", 100m);
        using var f = LoyaltyFactory(fake);
        var client  = f.CreateClient();
        var orderId = await PlaceOrderAsync(f, client, "+77004000007", pointsToSpend: 1m);
        fake.FailCancelHold = true;

        ApiHelpers.Authorize(client, await SuperAdminTokenAsync(f, client));
        var resp = await client.DeleteAsync($"/api/v1/admin/orders/{orderId}");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        await using var scope = f.Services.CreateAsyncScope();
        var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(LoyaltyPendingAction.Release, order.LoyaltyPendingAction);
    }
}
