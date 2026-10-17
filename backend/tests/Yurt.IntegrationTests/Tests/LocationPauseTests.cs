using System.Net;
using System.Net.Http.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class LocationPauseTests(YurtWebAppFactory factory)
{
    // A location of its own so pausing it can't affect orders in other tests.
    private async Task<Guid> CreateLocationAsync(HttpClient admin)
    {
        var resp = await admin.PostAsJsonAsync("/api/v1/admin/locations", new
        {
            name = "Pause Test " + Guid.NewGuid().ToString("N")[..6],
            address = "Test street 1", workingHours = "", contactPhone = "+77000000000"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdResult>(ApiHelpers.JsonOpts);
        return body!.Id;
    }

    private record IdResult(Guid Id, DateTime? OrdersPausedUntil);

    [Fact]
    public async Task WorkerPausesLocation_OrdersRejected_ThenResumeAllowsThem()
    {
        var admin = factory.CreateClient();
        ApiHelpers.Authorize(admin, await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, admin, username: "pauseadmin", password: "Pause@1234", role: AdminRole.Admin));
        var locationId = await CreateLocationAsync(admin);

        var worker = factory.CreateClient();
        ApiHelpers.Authorize(worker, await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, worker, username: "pauseworker", password: "Pause@1234", role: AdminRole.Worker));

        var pause = await worker.PostAsJsonAsync($"/api/v1/admin/locations/{locationId}/pause", new { minutes = 30 });
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);
        var paused = await pause.Content.ReadFromJsonAsync<IdResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(paused!.OrdersPausedUntil);

        var tooLong = await worker.PostAsJsonAsync($"/api/v1/admin/locations/{locationId}/pause", new { minutes = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77009400001");
        ApiHelpers.Authorize(customer, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        object Order() => new
        {
            locationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        };

        var rejected = await customer.PostAsJsonAsync("/api/v1/orders", Order());
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var resume = await worker.PostAsync($"/api/v1/admin/locations/{locationId}/resume", null);
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);

        var accepted = await customer.PostAsJsonAsync("/api/v1/orders", Order());
        Assert.NotEqual(HttpStatusCode.Conflict, accepted.StatusCode);
    }
}
