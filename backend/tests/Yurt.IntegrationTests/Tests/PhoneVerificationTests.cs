using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class PhoneVerificationTests(YurtWebAppFactory factory)
{
    private record StartResult(string MobileNumber, DateTime ExpiresAt, string? DevCode);
    private record Profile(Guid Id, string MobileNumber, bool IsPhoneVerified);

    [Fact]
    public async Task Registration_MarksPhoneVerified()
    {
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77009600001");
        ApiHelpers.Authorize(client, token);

        var me = await client.GetFromJsonAsync<Profile>("/api/v1/auth/me", ApiHelpers.JsonOpts);
        Assert.True(me!.IsPhoneVerified);

        var start = await client.PostAsync("/api/v1/auth/me/verify-phone/start", null);
        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
    }

    [Fact]
    public async Task UnverifiedCustomer_ConfirmsCode_BecomesVerified()
    {
        var client = factory.CreateClient();
        var (token, userId) = await ApiHelpers.CreateCustomerAsync(client, "+77009600002");
        await SetVerifiedAsync(userId, false);
        ApiHelpers.Authorize(client, token);

        var me = await client.GetFromJsonAsync<Profile>("/api/v1/auth/me", ApiHelpers.JsonOpts);
        Assert.False(me!.IsPhoneVerified);

        var start = await client.PostAsync("/api/v1/auth/me/verify-phone/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var startBody = await start.Content.ReadFromJsonAsync<StartResult>(ApiHelpers.JsonOpts);
        Assert.Equal("+77009600002", startBody!.MobileNumber);
        Assert.NotNull(startBody.DevCode);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/me/verify-phone/confirm",
            new { code = startBody.DevCode == "0000" ? "1111" : "0000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var confirm = await client.PostAsJsonAsync("/api/v1/auth/me/verify-phone/confirm",
            new { code = startBody.DevCode });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var profile = await confirm.Content.ReadFromJsonAsync<Profile>(ApiHelpers.JsonOpts);
        Assert.True(profile!.IsPhoneVerified);

        me = await client.GetFromJsonAsync<Profile>("/api/v1/auth/me", ApiHelpers.JsonOpts);
        Assert.True(me!.IsPhoneVerified);
    }

    private async Task SetVerifiedAsync(Guid userId, bool verified)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = await db.CustomerUsers.FirstAsync(u => u.Id == userId);
        user.IsPhoneVerified = verified;
        user.PhoneVerifiedAt = null;
        await db.SaveChangesAsync();
    }
}
