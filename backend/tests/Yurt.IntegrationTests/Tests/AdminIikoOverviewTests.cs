using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class AdminIikoOverviewTests(YurtWebAppFactory factory)
{
    private static readonly string[] Endpoints =
    [
        "/api/v1/admin/iiko/overview",
        "/api/v1/admin/iiko/orders",
        "/api/v1/admin/iiko/activity",
        "/api/v1/admin/iiko/status",
    ];

    [Fact]
    public async Task SuperAdmin_CanReadOverviewOrdersAndActivity()
    {
        var client = factory.CreateClient();
        var token = await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, client, username: "iikooverviewsuper", password: "Super@1234", role: AdminRole.SuperAdmin);
        ApiHelpers.Authorize(client, token);

        foreach (var url in Endpoints)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/iiko/overview");
        Assert.False(overview.GetProperty("config").GetProperty("enabled").GetBoolean());
        Assert.True(overview.GetProperty("menu").GetProperty("totalItems").GetInt32() >= 0);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/iiko/status");
        Assert.False(status.GetProperty("enabled").GetBoolean());
        // Secrets must never be part of the response.
        var raw = overview.GetRawText();
        Assert.DoesNotContain("apiKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CafeAdmin_IsForbidden()
    {
        var client = factory.CreateClient();
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, client);
        ApiHelpers.Authorize(client, token);

        foreach (var url in Endpoints)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }
}
