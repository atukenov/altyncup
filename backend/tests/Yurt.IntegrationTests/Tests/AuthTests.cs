using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class AuthTests(YurtWebAppFactory factory)
{
    // ── PIN lockout ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_FiveWrongPins_LocksAccountWith423AndMinutesRemaining()
    {
        var client = factory.CreateClient();
        var phone  = "+77006000001";
        await ApiHelpers.CreateCustomerAsync(client, phone, "9999");

        // Wrong PIN five times
        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { mobileNumber = phone, pin4 = "0000" });
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        // Account must now be locked
        var lockedResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { mobileNumber = phone, pin4 = "9999" });

        Assert.Equal((HttpStatusCode)423, lockedResp.StatusCode);

        var body = await lockedResp.Content.ReadFromJsonAsync<ProblemBody>(ApiHelpers.JsonOpts);
        Assert.NotNull(body);
        Assert.True(body.MinutesRemaining > 0,
            $"Expected minutesRemaining > 0, got {body.MinutesRemaining}");
    }

    // ── Refresh token rotation ───────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_RotatesTokenAndRevokesOld()
    {
        var client = factory.CreateClient();
        var (_, userId) = await ApiHelpers.CreateCustomerAsync(client, "+77006000002");
        // CreateCustomerAsync calls register then login; grab tokens from a fresh login
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { mobileNumber = "+77006000002", pin4 = "1234" });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<ApiHelpers.AuthResult>(ApiHelpers.JsonOpts);
        var oldRefreshToken = loginBody!.RefreshToken;

        // Rotate
        var refreshResp = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = oldRefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<ApiHelpers.AuthResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(refreshBody);
        Assert.NotEqual(oldRefreshToken, refreshBody.RefreshToken);

        // Old token must be revoked in DB
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == oldRefreshToken);
        Assert.NotNull(stored);
        Assert.True(stored.IsRevoked);

        // New access token must be accepted
        ApiHelpers.Authorize(client, refreshBody.AccessToken);
        var meResp = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReuseOfRevokedToken_RevokesAllTokensAnd401()
    {
        var client = factory.CreateClient();
        await ApiHelpers.CreateCustomerAsync(client, "+77006000003");

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { mobileNumber = "+77006000003", pin4 = "1234" });
        loginResp.EnsureSuccessStatusCode();
        var loginBody  = await loginResp.Content.ReadFromJsonAsync<ApiHelpers.AuthResult>(ApiHelpers.JsonOpts);
        var token1 = loginBody!.RefreshToken;

        // First rotation: token1 → token2 (token1 is now revoked)
        var r1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token1 });
        r1.EnsureSuccessStatusCode();
        var body1  = await r1.Content.ReadFromJsonAsync<ApiHelpers.AuthResult>(ApiHelpers.JsonOpts);
        var token2 = body1!.RefreshToken;

        // Reuse token1 (already revoked) → must revoke all and return 401
        var reuseResp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token1 });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResp.StatusCode);

        // token2 must also be revoked now
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var t2 = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token2);
        Assert.NotNull(t2);
        Assert.True(t2.IsRevoked, "token2 should have been revoked by reuse detection");
    }

    // ── Authorization policy enforcement ────────────────────────────────────

    [Fact]
    public async Task AnonymousRequest_CustomerOnlyEndpoint_Returns401()
    {
        var client = factory.CreateClient();
        ApiHelpers.ClearAuth(client);

        var resp = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CustomerToken_AdminOnlyEndpoint_Returns403()
    {
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77006000004");
        ApiHelpers.Authorize(client, token);

        // /api/v1/admin/locations is AdminOnly
        var resp = await client.GetAsync("/api/v1/admin/locations");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AdminWithMustChangePassword_AdminOnlyEndpoint_Returns403()
    {
        var client = factory.CreateClient();
        // MustChangePassword=true — admin is forced to change password before accessing anything
        var token = await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, client,
            username: "mustchangeadmin", password: "ChangeMe@123",
            mustChangePassword: true);

        ApiHelpers.Authorize(client, token);
        var resp = await client.GetAsync("/api/v1/admin/locations");

        // MustChangePassword middleware should block with 403
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task WorkerToken_AdminRoleAdminEndpoint_Returns403()
    {
        var client = factory.CreateClient();

        // Create a Worker-role admin
        await using var scope = factory.Services.CreateAsyncScope();
        var db     = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        const string workerUser = "workeradmin";
        const string workerPass = "Worker@123";
        if (!await db.AdminUsers.AnyAsync(a => a.Username == workerUser))
        {
            db.AdminUsers.Add(new AdminUser
            {
                Username           = workerUser,
                PasswordHash       = hasher.Hash(workerPass),
                Role               = AdminRole.Worker,
                IsActive           = true,
                MustChangePassword = false
            });
            await db.SaveChangesAsync();
        }

        var loginResp = await client.PostAsJsonAsync("/api/v1/admin/auth/login",
            new { username = workerUser, password = workerPass });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<ApiHelpers.AuthResult>(ApiHelpers.JsonOpts);
        ApiHelpers.Authorize(client, loginBody!.AccessToken);

        // /api/v1/admin/workers requires AdminRoleAdmin, worker role should be denied
        var resp = await client.GetAsync("/api/v1/admin/workers");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Local shape ──────────────────────────────────────────────────────────

    private record ProblemBody(int Status, string Title, int MinutesRemaining);
}
