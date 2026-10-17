using System.Net;
using System.Net.Http.Json;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class PinResetTests(YurtWebAppFactory factory)
{
    private record StartResult(string MobileNumber, DateTime ExpiresAt, string? DevCode);

    [Fact]
    public async Task PinReset_FullFlow_SetsNewPin_RevokesOldSessions_AndUnlocks()
    {
        var client = factory.CreateClient();
        const string phone = "+77009300001";
        await ApiHelpers.CreateCustomerAsync(client, phone);

        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { mobileNumber = phone, pin4 = "1234" });
        var oldTokens = await oldLogin.Content.ReadFromJsonAsync<ApiHelpers.AuthResult>(ApiHelpers.JsonOpts);

        // Lock the account with wrong PINs first: reset must still work and clear the lock.
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/v1/auth/login", new { mobileNumber = phone, pin4 = "0000" });

        var start = await client.PostAsJsonAsync("/api/v1/auth/pin-reset/start", new { mobileNumber = phone });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var startBody = await start.Content.ReadFromJsonAsync<StartResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(startBody!.DevCode);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/pin-reset/verify",
            new { mobileNumber = phone, code = startBody.DevCode == "0000" ? "1111" : "0000", newPin4 = "5678" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/pin-reset/verify",
            new { mobileNumber = phone, code = startBody.DevCode, newPin4 = "5678" });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { mobileNumber = phone, pin4 = "5678" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var oldPin = await client.PostAsJsonAsync("/api/v1/auth/login", new { mobileNumber = phone, pin4 = "1234" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPin.StatusCode);

        var oldRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = oldTokens!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
    }

    [Fact]
    public async Task PinReset_UnknownNumber_LooksLikeSuccess_ButSendsNothing()
    {
        var client = factory.CreateClient();
        var start = await client.PostAsJsonAsync("/api/v1/auth/pin-reset/start", new { mobileNumber = "+77009399999" });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var body = await start.Content.ReadFromJsonAsync<StartResult>(ApiHelpers.JsonOpts);
        Assert.Null(body!.DevCode);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/pin-reset/verify",
            new { mobileNumber = "+77009399999", code = "1234", newPin4 = "5678" });
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }
}
