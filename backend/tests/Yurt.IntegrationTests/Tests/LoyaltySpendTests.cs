using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Phase 2 of the iiko loyalty integration: paying with points at checkout.</summary>
[Collection("Integration")]
public class LoyaltySpendTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private record OrderWithLoyalty(
        Guid Id, decimal Total, string Status, string PaymentStatus,
        decimal? LoyaltyPointsSpent, decimal? LoyaltyPointsEarned);

    private static IikoOptions EnabledOptions() => new()
    {
        Enabled        = true,
        EarnPercent    = 5m,
        OrganizationId = Guid.NewGuid(),
        ProgramId      = Guid.NewGuid(),
    };

    private WebApplicationFactory<Program> EnabledFactory(IIikoApiClient client) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton(client);
            }));

    private static async Task<(OrderWithLoyalty Order, string CustomerToken)> PlaceOrderAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, string phone, decimal? pointsToSpend)
    {
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, customerToken);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(appFactory.Services, "Cappuccino");
        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId          = LocationId,
            paymentMethod       = "Cash",
            loyaltyPointsToSpend = pointsToSpend,
            items               = new[] { new { menuItemId = itemId, quantity = 2 } }
        });
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content.ReadFromJsonAsync<OrderWithLoyalty>(ApiHelpers.JsonOpts);
        return (order!, customerToken);
    }

    private static async Task AdvanceToCompletedAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, Guid orderId, bool markPaid = true)
    {
        var adminToken = await ApiHelpers.CreateAdminTokenAsync(appFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        if (markPaid)
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment",
                    new { paymentStatus = "Paid", paymentMethod = "Cash" }))
                .EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/accept", new { etaMinutes = 10 }))
            .EnsureSuccessStatusCode();
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status }))
                .EnsureSuccessStatusCode();
    }

    // ── Placement ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PartialSpend_HoldsRequestedPoints()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000901", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000901", 5m);

        Assert.Equal(5m, order.LoyaltyPointsSpent);
        Assert.Equal("Unpaid", order.PaymentStatus); // partial — remainder still due
        var hold = Assert.Single(fake.HoldCalls);
        Assert.Equal(5m, hold.Sum);
        Assert.Contains(order.Id.ToString(), hold.Comment);
        Assert.Equal(1, fake.ActiveHoldCount);
    }

    [Fact]
    public async Task SpendMoreThanBalance_ClampsToAvailableBalance()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000902", 2m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000902", 999m);

        Assert.Equal(2m, order.LoyaltyPointsSpent);
        Assert.Equal(2m, Assert.Single(fake.HoldCalls).Sum);
    }

    [Fact]
    public async Task FullCover_MarksOrderPaid()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000903", 1000m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000903", 1000m);

        Assert.Equal(order.Total, order.LoyaltyPointsSpent);
        Assert.Equal("Paid", order.PaymentStatus);

        // Fully points-paid order can be accepted without a manual payment step
        await AdvanceToCompletedAsync(f, client, order.Id, markPaid: false);

        Assert.Equal(order.Total, Assert.Single(fake.ChargeoffCalls).Sum);
        Assert.Empty(fake.TopupCalls); // earn base is zero — no points on points
    }

    [Fact]
    public async Task IikoDownAtPlacement_OrderStillCreatedWithoutPoints()
    {
        using var f = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(new AlwaysThrowIikoClient());
            }));
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000904", 50m);

        Assert.Null(order.LoyaltyPointsSpent);
        Assert.Equal("Unpaid", order.PaymentStatus);
    }

    // ── Completion ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Completion_ReleasesHoldChargesOffAndEarnsOnMoneyPortionOnly()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000905", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000905", 5m);
        await AdvanceToCompletedAsync(f, client, order.Id);

        Assert.Single(fake.CancelHoldCalls);
        Assert.Equal(0, fake.ActiveHoldCount);
        Assert.Equal(5m, Assert.Single(fake.ChargeoffCalls).Sum);

        // Earn 5% of the money portion (total − points), truncated to 2 dp
        var expectedEarn = Math.Round((order.Total - 5m) * 0.05m, 2, MidpointRounding.ToZero);
        Assert.Equal(expectedEarn, Assert.Single(fake.TopupCalls).Sum);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        Assert.Equal(5m, dbOrder.LoyaltyPointsSpent);
        Assert.Equal(expectedEarn, dbOrder.LoyaltyPointsEarned);
        Assert.Null(dbOrder.LoyaltyHoldTransactionId);
        Assert.Equal(LoyaltyPendingAction.None, dbOrder.LoyaltyPendingAction);
    }

    // ── Decline ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Decline_ReleasesHoldAndClearsSpentPoints()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000906", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000906", 5m);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(f.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/decline", new { reason = "Out of milk" }))
            .EnsureSuccessStatusCode();

        Assert.Single(fake.CancelHoldCalls);
        Assert.Equal(0, fake.ActiveHoldCount);
        Assert.Empty(fake.ChargeoffCalls);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        Assert.Null(dbOrder.LoyaltyPointsSpent);
        Assert.Null(dbOrder.LoyaltyHoldTransactionId);
    }

    [Fact]
    public async Task DeclineWhileIikoDown_MarksPendingRelease_RetrySweepReleasesHold()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000907", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000907", 5m);

        // iiko goes down before the decline
        fake.FailCancelHold = true;
        var adminToken = await ApiHelpers.CreateAdminTokenAsync(f.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/decline", new { reason = "Closed" }))
            .EnsureSuccessStatusCode(); // decline itself must not fail

        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.Release, dbOrder.LoyaltyPendingAction);
            Assert.NotNull(dbOrder.LoyaltyHoldTransactionId); // hold still active in iiko
        }

        // iiko comes back — the background sweep releases the hold
        fake.FailCancelHold = false;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.RetryPendingAsync();
        }

        Assert.Equal(0, fake.ActiveHoldCount);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.None, dbOrder.LoyaltyPendingAction);
            Assert.Null(dbOrder.LoyaltyHoldTransactionId);
        }
    }

    [Fact]
    public async Task ChargeoffFailsAtCompletion_RetrySweepChargesOff()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000908", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000908", 5m);

        fake.FailChargeoff = true;
        await AdvanceToCompletedAsync(f, client, order.Id); // completion must not fail

        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.ChargeOff, dbOrder.LoyaltyPendingAction);
        }

        fake.FailChargeoff = false;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.RetryPendingAsync();
        }

        Assert.Equal(5m, Assert.Single(fake.ChargeoffCalls).Sum);
        await using (var scopeAfter = f.Services.CreateAsyncScope())
        {
            var db = scopeAfter.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.None, dbOrder.LoyaltyPendingAction);
        }
    }

    private sealed class AlwaysThrowIikoClient : IIikoApiClient
    {
        public Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<Guid> CreateOrUpdateCustomerAsync(string phone, string? name, string? surname, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
            => throw new IikoApiException("down");
    }
}
