using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;

namespace Yurt.Infrastructure.Iiko;

/// <summary>
/// iikoCloud (iikoTransport) API client.
/// Authorization uses /api/v2/access_token (apiKey + appId + clientSecret) — NOT the legacy v1 apiLogin flow.
/// </summary>
public class IikoApiClient : IIikoApiClient
{
    private readonly HttpClient _http;
    private readonly IikoOptions _options;
    private readonly IikoTokenStore _tokenStore;
    private readonly ILogger<IikoApiClient> _logger;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IikoApiClient(
        HttpClient http, IikoOptions options, IikoTokenStore tokenStore, ILogger<IikoApiClient> logger)
    {
        _http = http;
        _options = options;
        _tokenStore = tokenStore;
        _logger = logger;
        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    // ── Auth (v2) ────────────────────────────────────────────────────────────

    private record TokenV2Request(string ApiKey, string AppId, string ClientSecret);
    private record TokenV2Response(string? CorrelationId, string? Token);

    private async Task<string> FetchTokenAsync(CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("api/v2/access_token",
            new TokenV2Request(_options.ApiKey, _options.AppId, _options.ClientSecret), Json, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new IikoApiException(
                $"iiko v2 access_token failed with {(int)resp.StatusCode}: {Truncate(body)}", (int)resp.StatusCode);
        }

        var token = (await resp.Content.ReadFromJsonAsync<TokenV2Response>(Json, ct))?.Token;
        if (string.IsNullOrWhiteSpace(token))
            throw new IikoApiException("iiko v2 access_token returned an empty token.");
        return token;
    }

    private Task<string> GetTokenAsync(bool forceRefresh, CancellationToken ct) =>
        _tokenStore.GetOrRefreshAsync(
            FetchTokenAsync, TimeSpan.FromMinutes(_options.TokenLifetimeMinutes), forceRefresh, ct);

    // ── Core send with one retry on 401 ──────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(string path, object body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await GetTokenAsync(forceRefresh: attempt > 0, ct);

            using var req = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body, options: Json)
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _http.SendAsync(req, ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                _logger.LogInformation("iiko session token expired; refreshing and retrying {Path}", path);
                _tokenStore.Invalidate();
                resp.Dispose();
                continue;
            }

            return resp;
        }
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        var resp = await SendAsync(path, body, ct);
        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct);
                throw new IikoApiException(
                    $"iiko {path} failed with {(int)resp.StatusCode}: {Truncate(errBody)}", (int)resp.StatusCode);
            }

            var result = await resp.Content.ReadFromJsonAsync<T>(Json, ct);
            return result ?? throw new IikoApiException($"iiko {path} returned an empty body.");
        }
    }

    // ── Customers / wallet ───────────────────────────────────────────────────

    private record CustomerInfoByPhoneRequest(string Type, string Phone, Guid OrganizationId);
    private record WalletBalanceDto(Guid Id, string? Name, int Type, decimal Balance);
    private record CustomerInfoResponse(Guid Id, string? Phone, List<WalletBalanceDto>? WalletBalances);

    public async Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/info",
            new CustomerInfoByPhoneRequest("phone", phone, _options.OrganizationId), ct);
        using (resp)
        {
            // iiko answers 400 for unknown customers — treat as "not registered", not an error
            if (resp.StatusCode == HttpStatusCode.BadRequest || resp.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new IikoApiException(
                    $"iiko customer/info failed with {(int)resp.StatusCode}: {Truncate(body)}", (int)resp.StatusCode);
            }

            var info = await resp.Content.ReadFromJsonAsync<CustomerInfoResponse>(Json, ct);
            if (info == null) return null;

            return new IikoCustomerInfo(
                info.Id,
                info.Phone,
                (info.WalletBalances ?? []).Select(w => new IikoWalletBalance(w.Id, w.Name, w.Type, w.Balance)).ToList());
        }
    }

    private record CreateOrUpdateCustomerRequest(string Phone, string? Name, string? SurName, Guid OrganizationId);
    private record CreateOrUpdateCustomerResponse(Guid Id);

    public async Task<Guid> CreateOrUpdateCustomerAsync(
        string phone, string? name, string? surname, CancellationToken ct = default)
    {
        var resp = await PostAsync<CreateOrUpdateCustomerResponse>(
            "api/1/loyalty/iiko/customer/create_or_update",
            new CreateOrUpdateCustomerRequest(phone, name, surname, _options.OrganizationId), ct);
        return resp.Id;
    }

    private record AddToProgramRequest(Guid CustomerId, Guid ProgramId, Guid OrganizationId);
    private record AddToProgramResponse(Guid? UserWalletId, Guid? WalletId);

    public async Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
    {
        var resp = await PostAsync<AddToProgramResponse>(
            "api/1/loyalty/iiko/customer/program/add",
            new AddToProgramRequest(iikoCustomerId, _options.ProgramId, _options.OrganizationId), ct);

        return resp.WalletId
            ?? resp.UserWalletId
            ?? throw new IikoApiException("iiko program/add returned no wallet id.");
    }

    private record ChangeBalanceRequest(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment, Guid OrganizationId);

    public async Task TopupAsync(
        Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/wallet/topup",
            new ChangeBalanceRequest(iikoCustomerId, walletId, sum, comment, _options.OrganizationId), ct);
        await EnsureSuccessAsync(resp, "wallet/topup", ct);
    }

    public async Task ChargeoffAsync(
        Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/wallet/chargeoff",
            new ChangeBalanceRequest(iikoCustomerId, walletId, sum, comment, _options.OrganizationId), ct);
        await EnsureSuccessAsync(resp, "wallet/chargeoff", ct);
    }

    private record HoldRequest(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment, Guid OrganizationId);
    private record HoldResponse(Guid TransactionId);

    public async Task<Guid> HoldAsync(
        Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var resp = await PostAsync<HoldResponse>("api/1/loyalty/iiko/customer/wallet/hold",
            new HoldRequest(iikoCustomerId, walletId, sum, comment, _options.OrganizationId), ct);
        return resp.TransactionId;
    }

    private record CancelHoldRequest(Guid TransactionId, Guid OrganizationId);

    public async Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/wallet/cancel_hold",
            new CancelHoldRequest(holdTransactionId, _options.OrganizationId), ct);
        await EnsureSuccessAsync(resp, "wallet/cancel_hold", ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, string op, CancellationToken ct)
    {
        using (resp)
        {
            if (resp.IsSuccessStatusCode) return;
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new IikoApiException(
                $"iiko {op} failed with {(int)resp.StatusCode}: {Truncate(body)}", (int)resp.StatusCode);
        }
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}
