using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class LoyaltyTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private record LoyaltyBalanceResult(
        bool Enabled, bool Available, bool Linked, decimal? Balance, decimal EarnPercent);

    private record CompletedOrder(Guid Id, decimal Total, string CustomerToken);

    // ── Feature flag off (default config) ────────────────────────────────────

    [Fact]
    public async Task LoyaltyDisabled_BalanceEndpoint_ReturnsDisabled()
    {
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000801");
        ApiHelpers.Authorize(client, token);

        var result = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            "/api/v1/loyalty/me", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        Assert.False(result.Enabled);
        Assert.Null(result.Balance);
    }

    [Fact]
    public async Task LoyaltyDisabled_CompletedOrder_DoesNotCreditPoints()
    {
        var client    = factory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(factory, client, "+77001000802");

        await using var scope = factory.Services.CreateAsyncScope();
        var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
        Assert.Null(order.LoyaltyPointsEarned);
    }

    // ── Feature flag on, iiko faked ──────────────────────────────────────────

    [Fact]
    public async Task LoyaltyEnabled_CompletedOrder_CreditsPointsOnceAndShowsBalance()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Last registration wins for both the options and the client
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client    = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000803");

        // Exactly one topup, 5% of the total, tagged with the order id
        var call = Assert.Single(fake.TopupCalls);
        Assert.Equal(FakeIikoApiClient.CustomerId, call.CustomerId);
        Assert.Equal(FakeIikoApiClient.WalletId, call.WalletId);
        Assert.Equal(Math.Round(completed.Total * 0.05m, 2, MidpointRounding.ToZero), call.Sum);
        Assert.Contains(completed.Id.ToString(), call.Comment);

        // Points recorded on the order, link ids persisted on the customer
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.Include(o => o.CustomerUser).FirstAsync(o => o.Id == completed.Id);
            Assert.Equal(call.Sum, order.LoyaltyPointsEarned);
            Assert.Equal(FakeIikoApiClient.CustomerId, order.CustomerUser.IikoCustomerId);
            Assert.Equal(FakeIikoApiClient.WalletId, order.CustomerUser.IikoWalletId);
        }

        // Balance endpoint reflects the credited points
        ApiHelpers.Authorize(client, completed.CustomerToken);
        var balance = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            "/api/v1/loyalty/me", ApiHelpers.JsonOpts);
        Assert.NotNull(balance);
        Assert.True(balance.Enabled);
        Assert.True(balance.Available);
        Assert.True(balance.Linked);
        Assert.Equal(call.Sum, balance.Balance);
        Assert.Equal(5m, balance.EarnPercent);
    }

    [Fact]
    public async Task LoyaltyEnabled_IikoDown_OrderCompletionStillSucceeds()
    {
        using var brokenFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(new ThrowingIikoApiClient());
            }));

        var client    = brokenFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(brokenFactory, client, "+77001000804");

        await using var scope = brokenFactory.Services.CreateAsyncScope();
        var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
        Assert.Equal("Completed", order.Status.ToString());
        Assert.Null(order.LoyaltyPointsEarned); // not credited, but completion succeeded
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IikoOptions EnabledOptions() => new()
    {
        Enabled        = true,
        EarnPercent    = 5m,
        OrganizationId = Guid.NewGuid(),
        ProgramId      = Guid.NewGuid(),
    };

    private static async Task<CompletedOrder> PlaceAndCompleteOrderAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, string phone)
    {
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, customerToken);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(appFactory.Services, "Cappuccino");
        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId = itemId, quantity = 2 } }
        });
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(appFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        // Orders can only be accepted after payment is confirmed
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment",
                new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 10 }))
            .EnsureSuccessStatusCode();
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/status", new { status }))
                .EnsureSuccessStatusCode();

        ApiHelpers.ClearAuth(client);
        return new CompletedOrder(order.Id, order.Total, customerToken);
    }

    private sealed class ThrowingIikoApiClient : IIikoApiClient
    {
        public Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<Guid> CreateOrUpdateCustomerAsync(string phone, string? name, string? surname, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
    }
}
