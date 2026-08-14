using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Features.Loyalty;
using Yurt.Infrastructure.Iiko;

namespace Yurt.UnitTests;

/// <summary>
/// Regression coverage for the wallet-id bug: iiko's customer/program/add returns both
/// userWalletId (the customer's own balance-holding wallet) and walletId (the shared,
/// non-balance-holding program wallet definition). We must always resolve to
/// userWalletId — using walletId instead makes every balance lookup and wallet
/// operation target a wallet the customer doesn't actually hold points in.
/// </summary>
public class IikoApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Guid _userWalletId;
        private readonly Guid _walletId;

        public StubHandler(Guid userWalletId, Guid walletId)
        {
            _userWalletId = userWalletId;
            _walletId = walletId;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("access_token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { correlationId = "c1", token = "fake-token" })
                });
            }

            if (request.RequestUri.AbsolutePath.EndsWith("customer/program/add"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { userWalletId = _userWalletId, walletId = _walletId })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task AddCustomerToProgramAsync_PrefersUserWalletId_OverSharedProgramWalletId()
    {
        var userWalletId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var programWalletId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var httpClient = new HttpClient(new StubHandler(userWalletId, programWalletId));
        var options = new IikoOptions
        {
            Enabled = true,
            BaseUrl = "https://iiko.test",
            ApiKey = "key",
            AppId = "app",
            ClientSecret = "secret",
            OrganizationId = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
        };
        var client = new IikoApiClient(
            httpClient, options, new IikoTokenStore(), NullLogger<IikoApiClient>.Instance);

        var resolved = await client.AddCustomerToProgramAsync(Guid.NewGuid());

        Assert.Equal(userWalletId, resolved);
        Assert.NotEqual(programWalletId, resolved);
    }
}
