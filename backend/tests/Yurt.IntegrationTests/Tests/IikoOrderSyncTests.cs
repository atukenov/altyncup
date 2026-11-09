using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.IikoIntegration.Services;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

// Every test creates its own dedicated Location + MenuItem rather than mutating the
// shared seeded "Cappuccino" item / seeded LocationId that other test files (and other
// tests in this file) also rely on — those writes are otherwise never rolled back and
// leak across tests sharing the same collection-fixture database, e.g. one test's
// IikoTerminalGroupId/IikoProductId assignment silently "unblocking" a later test that
// deliberately wants an unmapped item or location.
[Collection("Integration")]
public class IikoOrderSyncTests(YurtWebAppFactory factory)
{
    private record PlacedOrder(Guid Id, string CustomerToken);

    // ── Push when preparation starts ───────────────────────────────────────────────────────

    [Fact]
    public async Task MappedItem_OnStartPreparing_PushesOrderToIikoAndRecordsId()
    {
        var fake = new FakeIikoApiClient();
        var terminalGroupId = Guid.NewGuid();
        var iikoProductId = Guid.NewGuid();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, terminalGroupId);
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, iikoProductId);

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000801", locationId, menuItemId);

        var call = Assert.Single(fake.PushCalls);
        Assert.Equal(terminalGroupId, call.TerminalGroupId);
        Assert.Equal(iikoProductId, call.Items.Single().ProductId);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Pushed, order.IikoOrderSyncStatus);
        Assert.Equal(fake.NextIikoOrderId, order.IikoDeliveryOrderId);
        Assert.Equal(IikoOrderSyncPendingAction.None, order.IikoOrderSyncPendingAction);
    }

    [Fact]
    public async Task PushOrdersDisabled_OnStartPreparing_NeverCallsIiko()
    {
        var fake = new FakeIikoApiClient();
        var options = EnabledOptions();
        options.PushOrdersEnabled = false; // loyalty on, order-push off
        using var factoryWithLoyaltyOnly = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(factoryWithLoyaltyOnly.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(factoryWithLoyaltyOnly.Services, Guid.NewGuid());

        var client = factoryWithLoyaltyOnly.CreateClient();
        await PlaceAndAcceptOrderAsync(factoryWithLoyaltyOnly, client, "+77002000802", locationId, menuItemId);

        Assert.Empty(fake.PushCalls);
    }

    [Fact]
    public async Task UnmappedMenuItem_OnStartPreparing_SkipsPushAndLogsAudit()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        // Terminal group configured, but the menu item is left unmapped (no IikoProductId).
        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, iikoProductId: null);

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000803", locationId, menuItemId);

        Assert.Empty(fake.PushCalls);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.SkippedUnmapped, order.IikoOrderSyncStatus);
        Assert.Equal(IikoOrderSyncPendingAction.None, order.IikoOrderSyncPendingAction);
        Assert.True(await db.AuditLogs.AnyAsync(a =>
            a.Action == "IikoOrderSkippedUnmapped" && a.EntityId == placed.Id.ToString()));
    }

    [Fact]
    public async Task LocationWithNoTerminalGroup_OnStartPreparing_SkipsPush()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        // Menu item mapped, but the location is deliberately left with no terminal group.
        var locationId = await CreateTestLocationAsync(enabledFactory.Services, terminalGroupId: null);
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000804", locationId, menuItemId);

        Assert.Empty(fake.PushCalls);
        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.SkippedUnmapped, order.IikoOrderSyncStatus);
    }

    [Fact]
    public async Task AcceptOnly_DoesNotPushUntilPreparationStarts()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(
            enabledFactory, client, "+77002000809", locationId, menuItemId, startPreparing: false);
        Assert.Empty(fake.PushCalls);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{placed.Id}/status", new { status = "Preparing" }))
            .EnsureSuccessStatusCode();

        Assert.Single(fake.PushCalls);
    }

    [Fact]
    public async Task VariantWithoutIikoSize_PushesProductWithoutSize_AndNamesSizeInComment()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var iikoProductId = Guid.NewGuid();
        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var (menuItemId, variantId) = await CreateTestMenuItemWithVariantAsync(
            enabledFactory.Services, iikoProductId, variantLabel: "350ml");

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(
            enabledFactory, client, "+77002000810", locationId, menuItemId, variantId: variantId);

        var item = Assert.Single(Assert.Single(fake.PushCalls).Items);
        Assert.Equal(iikoProductId, item.ProductId);
        Assert.Null(item.ProductSizeId);
        Assert.Contains("350ml", item.Comment);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.Equal(IikoOrderSyncStatus.Pushed, (await db.Orders.FirstAsync(o => o.Id == placed.Id)).IikoOrderSyncStatus);
    }

    [Fact]
    public async Task VariantMappedToOwnIikoProduct_PushesThatProduct_EvenWhenItemIsUnmapped()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var americano350 = Guid.NewGuid();
        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var (menuItemId, variantId) = await CreateTestMenuItemWithVariantAsync(
            enabledFactory.Services, iikoProductId: null, variantLabel: "350", variantIikoProductId: americano350);

        var client = enabledFactory.CreateClient();
        await PlaceAndAcceptOrderAsync(
            enabledFactory, client, "+77002000812", locationId, menuItemId, variantId: variantId);

        var item = Assert.Single(Assert.Single(fake.PushCalls).Items);
        Assert.Equal(americano350, item.ProductId);
        Assert.Null(item.ProductSizeId);
        Assert.True(item.Comment == null || !item.Comment.Contains("350"));
    }

    [Fact]
    public async Task RetryIikoPush_AfterMappingFixed_PushesSkippedOrder()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, iikoProductId: null);

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000811", locationId, menuItemId);
        Assert.Empty(fake.PushCalls);

        var superToken = await ApiHelpers.CreateAdminTokenAsync(
            enabledFactory.Services, client, username: "iikoretrysuper", password: "Super@1234", role: AdminRole.SuperAdmin);
        ApiHelpers.Authorize(client, superToken);

        // Still unmapped: 422 with the reason.
        var stillUnmapped = await client.PostAsync($"/api/v1/admin/orders/{placed.Id}/iiko/push", null);
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, stillUnmapped.StatusCode);
        Assert.Contains("no iiko product mapping", await stillUnmapped.Content.ReadAsStringAsync());

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var item = await db.MenuItems.FirstAsync(m => m.Id == menuItemId);
            item.IikoProductId = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        (await client.PostAsync($"/api/v1/admin/orders/{placed.Id}/iiko/push", null)).EnsureSuccessStatusCode();
        Assert.Single(fake.PushCalls);

        await using var verifyScope = enabledFactory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.Equal(IikoOrderSyncStatus.Pushed, (await verifyDb.Orders.FirstAsync(o => o.Id == placed.Id)).IikoOrderSyncStatus);
    }

    // ── Retry ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushFails_MarksPendingPush_RetrySweepRecoversIt()
    {
        var fake = new FakeIikoApiClient { FailPush = true };
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000805", locationId, menuItemId);

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
            Assert.Equal(IikoOrderSyncStatus.NotPushed, order.IikoOrderSyncStatus);
            Assert.Equal(IikoOrderSyncPendingAction.Push, order.IikoOrderSyncPendingAction);
        }

        fake.FailPush = false;
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var sync = scope.ServiceProvider.GetRequiredService<IikoOrderSyncService>();
            await sync.RetryPendingAsync();
        }

        Assert.Single(fake.PushCalls);
        await using var verifyScope = enabledFactory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var verified = await verifyDb.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Pushed, verified.IikoOrderSyncStatus);
        Assert.Equal(IikoOrderSyncPendingAction.None, verified.IikoOrderSyncPendingAction);
    }

    // ── Close on completion ──────────────────────────────────────────────────

    [Fact]
    public async Task CompletedOrder_PreviouslyPushed_ClosesIikoOrder()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000806", locationId, menuItemId);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        foreach (var status in new[] { "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{placed.Id}/status", new { status }))
                .EnsureSuccessStatusCode();

        Assert.Equal(fake.NextIikoOrderId, Assert.Single(fake.CloseCalls));

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Closed, order.IikoOrderSyncStatus);
    }

    [Fact]
    public async Task CompletedOrder_NeverPushed_DoesNotCallClose()
    {
        // Unmapped item — never pushed when preparation started — must not attempt a close either.
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, iikoProductId: null);

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000807", locationId, menuItemId);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        foreach (var status in new[] { "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{placed.Id}/status", new { status }))
                .EnsureSuccessStatusCode();

        Assert.Empty(fake.CloseCalls);
    }

    // ── Webhook ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_DeliveryOrderErrorEvent_FlagsOrderFailed()
    {
        var fake = new FakeIikoApiClient();
        var options = EnabledOptions();
        options.WebhookAuthToken = "test-webhook-token";
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000808", locationId, menuItemId);
        var iikoOrderId = fake.PushCalls.Count > 0 ? fake.NextIikoOrderId : Guid.Empty;
        Assert.NotEqual(Guid.Empty, iikoOrderId);

        var payload = new[]
        {
            new
            {
                eventType = "DeliveryOrderError",
                eventInfo = new { id = iikoOrderId, order = (object?)null }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/iiko")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Authorization", "test-webhook-token");
        var resp = await client.SendAsync(request);
        resp.EnsureSuccessStatusCode();

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Failed, order.IikoOrderSyncStatus);
    }

    [Fact]
    public async Task Webhook_WrongAuthToken_ReturnsUnauthorized()
    {
        var options = EnabledOptions();
        options.WebhookAuthToken = "correct-token";
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IIikoApiClient>(new FakeIikoApiClient());
            }));

        var client = enabledFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/iiko")
        {
            Content = JsonContent.Create(Array.Empty<object>())
        };
        request.Headers.Add("Authorization", "wrong-token");
        var resp = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IikoOptions EnabledOptions() => new()
    {
        Enabled          = true,
        PushOrdersEnabled = true,
        EarnPercent      = 0m, // isolate order-sync assertions from loyalty topup side effects
        OrganizationId   = Guid.NewGuid(),
        ProgramId        = Guid.NewGuid(),
        PaymentTypeId    = Guid.NewGuid(),
    };

    // Dedicated Location per test — never reuse/mutate the shared seeded location, since
    // that write would leak into every other test (in this file and others) that reads it.
    private static async Task<Guid> CreateTestLocationAsync(IServiceProvider services, Guid? terminalGroupId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var location = new Location
        {
            Name = $"iiko sync test location {Guid.NewGuid():N}",
            Address = "Test address",
            WorkingHours = "",
            ContactPhone = "",
            IsActive = true,
            IikoTerminalGroupId = terminalGroupId,
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location.Id;
    }

    // Dedicated MenuItem per test — never reuse/mutate the shared seeded "Cappuccino"
    // item, for the same reason as CreateTestLocationAsync above.
    private static async Task<Guid> CreateTestMenuItemAsync(IServiceProvider services, Guid? iikoProductId)
        => (await CreateTestMenuItemWithVariantAsync(services, iikoProductId, variantLabel: null)).MenuItemId;

    private static async Task<(Guid MenuItemId, Guid? VariantId)> CreateTestMenuItemWithVariantAsync(
        IServiceProvider services, Guid? iikoProductId, string? variantLabel, Guid? variantIikoProductId = null)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem
        {
            CategoryId = category.Id,
            Name = $"iiko sync test item {Guid.NewGuid():N}",
            Description = "",
            Price = 500,
            IsAvailable = true,
            IikoProductId = iikoProductId,
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        if (variantLabel == null) return (item.Id, null);

        var variant = new MenuItemVariant
        {
            MenuItemId = item.Id, Label = variantLabel, Price = 600, SortOrder = 0, IsDefault = true,
            IikoProductId = variantIikoProductId,
        };
        db.MenuItemVariants.Add(variant);
        await db.SaveChangesAsync();
        return (item.Id, variant.Id);
    }

    private static async Task<PlacedOrder> PlaceAndAcceptOrderAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, string phone,
        Guid locationId, Guid menuItemId, bool startPreparing = true, Guid? variantId = null)
    {
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, customerToken);

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId, quantity = 1, variantId } }
        });
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(appFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment",
                new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 10 }))
            .EnsureSuccessStatusCode();
        if (startPreparing)
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/status", new { status = "Preparing" }))
                .EnsureSuccessStatusCode();

        ApiHelpers.ClearAuth(client);
        return new PlacedOrder(order.Id, customerToken);
    }
}
