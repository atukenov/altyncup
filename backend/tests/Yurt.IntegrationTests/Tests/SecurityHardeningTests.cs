using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Helpers;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class SecurityHardeningTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private record InvoiceResult(string InvoiceId, decimal Amount);

    private async Task<string> CreateInvoiceAsync(HttpClient client, string phone)
    {
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        var orderResp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        });
        orderResp.EnsureSuccessStatusCode();
        var order = await orderResp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);

        var payResp = await client.PostAsJsonAsync("/api/v1/payments/create", new
        {
            orderId = order!.Id, provider = "KaspiSandbox", sandboxBehavior = "Default"
        });
        payResp.EnsureSuccessStatusCode();
        var invoice = await payResp.Content.ReadFromJsonAsync<InvoiceResult>(ApiHelpers.JsonOpts);
        return invoice!.InvoiceId;
    }

    [Fact]
    public async Task PaymentStatus_OwnInvoice_200_WithoutRawResponse()
    {
        var client = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(client, "+77009100001");

        var resp = await client.GetAsync($"/api/v1/payments/status/{invoiceId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("rawResponse", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentStatus_OtherCustomersInvoice_404()
    {
        var owner = factory.CreateClient();
        var invoiceId = await CreateInvoiceAsync(owner, "+77009100002");

        var other = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(other, "+77009100003");
        ApiHelpers.Authorize(other, token);

        var resp = await other.GetAsync($"/api/v1/payments/status/{invoiceId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_FiveWrongPasswords_LocksAccount_423()
    {
        var client = factory.CreateClient();
        const string username = "lockoutadmin";
        const string password = "Lockout@123";
        await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username: username, password: password, role: AdminRole.Admin);

        for (var i = 0; i < 5; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { username, password = "wrong-pass" });
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        // Even the right password is refused while locked.
        var locked = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { username, password });
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_IsStoredHashed_AndLegacyPlaintextStillWorks()
    {
        var client = factory.CreateClient();
        var (_, userId) = await ApiHelpers.CreateCustomerAsync(client, "+77009100004");

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { mobileNumber = "+77009100004", pin4 = "1234" });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<ApiHelpers.AuthResult>(ApiHelpers.JsonOpts);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.Token == body!.RefreshToken));
        Assert.True(await db.RefreshTokens.AnyAsync(t => t.Token == TokenHasher.Hash(body!.RefreshToken)));

        // A token written before hashing (raw value in the column) must keep working.
        const string legacy = "legacy-plaintext-refresh-token-value";
        db.RefreshTokens.Add(new RefreshToken
        {
            Token = legacy, UserType = RefreshTokenUserType.Customer, UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = legacy });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task ReadinessProbe_ChecksDatabase_200()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Healthy", await resp.Content.ReadAsStringAsync());
    }
}
