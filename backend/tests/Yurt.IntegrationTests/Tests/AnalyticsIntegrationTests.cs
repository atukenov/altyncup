using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Runs the analytics projections against real Postgres, so a query EF can't
/// translate fails here rather than on the admin dashboard.
/// </summary>
[Collection("Integration")]
public class AnalyticsIntegrationTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("today", "+77009200001")]
    [InlineData("month", "+77009200002")]
    [InlineData("all", "+77009200003")]
    public async Task Analytics_WithOrders_Returns200(string period, string phone)
    {
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, phone);
        ApiHelpers.Authorize(customer, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        var order = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity = 2, toppings = Array.Empty<object>() } }
        });
        order.EnsureSuccessStatusCode();

        var admin = factory.CreateClient();
        ApiHelpers.Authorize(admin, await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, admin, username: "analyticsadmin", password: "Analytics@123", role: AdminRole.Admin));

        var resp = await admin.GetAsync($"/api/v1/admin/analytics?period={period}&locationId={LocationId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("kpis").GetProperty("totalOrders").GetInt32() >= 1);

        var dash = await admin.GetAsync("/api/v1/admin/dashboard");
        Assert.Equal(HttpStatusCode.OK, dash.StatusCode);
    }

    [Fact]
    public async Task ExportCsv_ReturnsOrdersWithHeader_AndNoPhoneNumbers()
    {
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77009200010");
        ApiHelpers.Authorize(customer, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        (await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        })).EnsureSuccessStatusCode();

        var admin = factory.CreateClient();
        ApiHelpers.Authorize(admin, await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, admin, username: "exportadmin", password: "Export@1234", role: AdminRole.Admin));

        var resp = await admin.GetAsync($"/api/v1/admin/analytics/export?period=month&locationId={LocationId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/csv", resp.Content.Headers.ContentType?.MediaType);
        var csv = await resp.Content.ReadAsStringAsync();
        Assert.StartsWith("Order,Created (UTC),Location", csv.TrimStart('\uFEFF'));
        Assert.Contains("Espresso", csv);
        Assert.DoesNotContain("77009200010", csv);
    }

    [Fact]
    public async Task ExportCsv_Worker_403()
    {
        var worker = factory.CreateClient();
        ApiHelpers.Authorize(worker, await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, worker, username: "exportworker", password: "Export@1234", role: AdminRole.Worker));
        var resp = await worker.GetAsync("/api/v1/admin/analytics/export");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
