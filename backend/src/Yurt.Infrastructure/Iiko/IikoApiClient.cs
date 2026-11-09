using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        // userWalletId is the customer's own balance-holding wallet (matches the ids
        // returned by customer/info's walletBalances); walletId is only the shared
        // program-level wallet definition and never holds an individual balance.
        return resp.UserWalletId
            ?? resp.WalletId
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

    // ── Order push (reporting/kitchen-routing side-channel) ─────────────────────

    // iiko switched off api/1/nomenclature (410 METHOD_DISCONTINUED). The catalog now comes
    // from the external menus built in iikoWeb: api/2/menu lists them, and
    // api/menu/v3/by_id returns one menu's groups and items. Item productIds are the
    // same nomenclature product ids that deliveries/create expects.
    private record ExternalMenuListResponse(List<ExternalMenuDto>? ExternalMenus);
    private record ExternalMenuDto(string Id, string? Name);
    private record MenuV3Request(string ExternalMenuId, Guid OrganizationId, Guid? PriceCategoryId);
    private record MenuV3SizePriceDto(string? SizeId, string? SizeName, bool? IsDefault, decimal? Price, bool? IsHidden, string? Sku);
    private record MenuV3ItemDto(string? ProductId, string? Name, bool? IsHidden, string? ItemType, List<MenuV3SizePriceDto>? SizePrices);
    private record MenuV3GroupDto(string? Name, bool? IsHidden, List<MenuV3ItemDto>? Items);
    private record MenuV3ProductDto(string? Id, string? Sku);
    private record MenuV3Response(List<MenuV3GroupDto>? ItemsGroups, List<MenuV3ProductDto>? Products);

    public async Task<List<IikoNomenclatureProduct>> GetNomenclatureAsync(CancellationToken ct = default)
    {
        List<string> menuIds;
        if (!string.IsNullOrWhiteSpace(_options.ExternalMenuId))
        {
            menuIds = [_options.ExternalMenuId.Trim()];
        }
        else
        {
            var list = await PostAsync<ExternalMenuListResponse>("api/2/menu", new { }, ct);
            menuIds = (list.ExternalMenus ?? []).Select(m => m.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            // No external menu built in iikoWeb: fall back to the full nomenclature.
            if (menuIds.Count == 0)
                return await GetNomenclatureFromCatalogAsync(ct);
        }

        var priceCategoryId = _options.PriceCategoryId == Guid.Empty ? (Guid?)null : _options.PriceCategoryId;
        var byId = new Dictionary<Guid, IikoNomenclatureProduct>();
        foreach (var menuId in menuIds)
        {
            var menu = await PostAsync<MenuV3Response>(
                "api/menu/v3/by_id", new MenuV3Request(menuId, _options.OrganizationId, priceCategoryId), ct);

            var skus = (menu.Products ?? [])
                .Where(p => p.Id != null)
                .GroupBy(p => p.Id!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Sku, StringComparer.OrdinalIgnoreCase);

            foreach (var group in menu.ItemsGroups ?? [])
            foreach (var item in group.Items ?? [])
            {
                // Combos have no productId and can't be pushed as a single order line.
                if (item.ItemType != null && !string.Equals(item.ItemType, "PRODUCT", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Guid.TryParse(item.ProductId, out var productId) || byId.ContainsKey(productId)) continue;

                var prices = item.SizePrices ?? [];
                var sizes = prices
                    .Where(sp => Guid.TryParse(sp.SizeId, out _))
                    .Select(sp => new IikoNomenclatureSize(Guid.Parse(sp.SizeId!), sp.SizeName))
                    .ToList();
                var price = (prices.FirstOrDefault(sp => sp.IsDefault == true) ?? prices.FirstOrDefault())?.Price ?? 0m;
                var code = skus.GetValueOrDefault(item.ProductId!);

                byId[productId] = new IikoNomenclatureProduct(
                    productId, item.Name ?? "", price, sizes,
                    string.IsNullOrWhiteSpace(code) ? null : code,
                    string.IsNullOrWhiteSpace(group.Name) ? null : group.Name);
            }
        }

        return byId.Values
            .OrderBy(p => p.GroupName == null) // ungrouped products last
            .ThenBy(p => p.GroupName ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // ── Nomenclature v1 fallback (no external menu configured in iikoWeb) ─────────

    private record CatalogFilter(
        string Field, string Op,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Value = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? Values = null);
    private record CatalogListRequest(List<CatalogFilter> Filters, int Limit, int Offset);
    private record CatalogProductDto(
        string? ProductId, string? Name, string? Type, bool? Deleted, string? ProductArticle, string? Code,
        string? ParentGroupId, string? ProductScaleId, List<string>? DisabledProductSizeIds, decimal? DefaultSalePrice);
    private record CatalogProductPage(List<CatalogProductDto>? Items, int? TotalCount);
    private record CatalogGroupDto(string? GroupId, string? Name, bool? IsDeleted);
    private record CatalogGroupPage(List<CatalogGroupDto>? Groups);
    private record CatalogSizeDto(string? Id, string? Name, bool? Deleted, int? Priority);
    private record CatalogScaleDto(string? Id, bool? Deleted, List<CatalogSizeDto>? ProductSizes);
    private record CatalogScalePage(List<CatalogScaleDto>? Items);

    private const int CatalogPageSize = 1000;
    private const int CatalogMaxPages = 20;

    /// <summary>
    /// Reads dishes and goods straight from the iiko nomenclature (api/nomenclature/v1/*),
    /// with group names from group/list and size names from product-scale/list. Used when
    /// the account has no external menu, so the mapping picker still works.
    /// </summary>
    private async Task<List<IikoNomenclatureProduct>> GetNomenclatureFromCatalogAsync(CancellationToken ct)
    {
        var typeFilter = new CatalogFilter("type", "in", Values: ["DISH", "GOODS"]);
        var products = new List<CatalogProductDto>();
        for (var page = 0; page < CatalogMaxPages; page++)
        {
            var resp = await PostAsync<CatalogProductPage>("api/nomenclature/v1/product/list",
                new CatalogListRequest([typeFilter], CatalogPageSize, page * CatalogPageSize), ct);
            var items = resp.Items ?? [];
            products.AddRange(items);
            if (items.Count < CatalogPageSize || (resp.TotalCount is int total && products.Count >= total)) break;
        }

        var groups = (await PostAsync<CatalogGroupPage>("api/nomenclature/v1/group/list",
                new CatalogListRequest([], 0, 0), ct)).Groups ?? [];
        var groupNames = groups
            .Where(g => g.GroupId != null && g.IsDeleted != true)
            .GroupBy(g => g.GroupId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        var scales = (await PostAsync<CatalogScalePage>("api/nomenclature/v1/product-scale/list",
                new CatalogListRequest([], 0, 0), ct)).Items ?? [];
        var scaleSizes = scales
            .Where(sc => sc.Id != null && sc.Deleted != true)
            .GroupBy(sc => sc.Id!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ProductSizes ?? [], StringComparer.OrdinalIgnoreCase);

        return products
            .Where(p => p.Deleted != true
                && (string.Equals(p.Type, "DISH", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Type, "GOODS", StringComparison.OrdinalIgnoreCase)))
            .Where(p => Guid.TryParse(p.ProductId, out _))
            .GroupBy(p => Guid.Parse(p.ProductId!))
            .Select(g =>
            {
                var p = g.First();
                var disabled = new HashSet<string>(p.DisabledProductSizeIds ?? [], StringComparer.OrdinalIgnoreCase);
                List<CatalogSizeDto> sizes =
                    p.ProductScaleId != null && scaleSizes.TryGetValue(p.ProductScaleId, out var scaleList) ? scaleList : [];
                var sizeList = sizes
                    .Where(sz => sz.Deleted != true && sz.Id != null && !disabled.Contains(sz.Id) && Guid.TryParse(sz.Id, out _))
                    .OrderBy(sz => sz.Priority ?? int.MaxValue)
                    .Select(sz => new IikoNomenclatureSize(Guid.Parse(sz.Id!), sz.Name))
                    .ToList();
                var code = string.IsNullOrWhiteSpace(p.ProductArticle) ? p.Code : p.ProductArticle;
                var groupName = p.ParentGroupId != null ? groupNames.GetValueOrDefault(p.ParentGroupId) : null;
                return new IikoNomenclatureProduct(
                    g.Key, p.Name ?? "", p.DefaultSalePrice ?? 0m, sizeList,
                    string.IsNullOrWhiteSpace(code) ? null : code,
                    string.IsNullOrWhiteSpace(groupName) ? null : groupName);
            })
            .OrderBy(p => p.GroupName == null) // ungrouped products last
            .ThenBy(p => p.GroupName ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private record PaymentTypesRequest(List<Guid> OrganizationIds);
    private record PaymentTypeDto(Guid? Id, string? Name);
    private record PaymentTypesResponse(List<PaymentTypeDto>? PaymentTypes);

    public async Task<List<IikoPaymentType>> GetPaymentTypesAsync(CancellationToken ct = default)
    {
        var resp = await PostAsync<PaymentTypesResponse>(
            "api/1/payment_types", new PaymentTypesRequest([_options.OrganizationId]), ct);

        return (resp.PaymentTypes ?? [])
            .Where(p => p.Id.HasValue)
            .Select(p => new IikoPaymentType(p.Id!.Value, p.Name ?? ""))
            .ToList();
    }

    private record TerminalGroupsRequest(List<Guid> OrganizationIds);
    private record TerminalGroupDto(Guid Id, string Name);
    private record TerminalGroupsWrapperDto(Guid OrganizationId, List<TerminalGroupDto>? Items);
    private record TerminalGroupsResponse(List<TerminalGroupsWrapperDto>? TerminalGroups);

    public async Task<List<IikoTerminalGroup>> GetTerminalGroupsAsync(CancellationToken ct = default)
    {
        var resp = await PostAsync<TerminalGroupsResponse>(
            "api/1/terminal_groups", new TerminalGroupsRequest([_options.OrganizationId]), ct);

        return (resp.TerminalGroups ?? [])
            .SelectMany(w => w.Items ?? [])
            .Select(g => new IikoTerminalGroup(g.Id, g.Name))
            .ToList();
    }

    private record CreateOrderCustomer(string Type, string Name);
    private record CreateOrderItem(string Type, decimal Amount, Guid ProductId, Guid? ProductSizeId, decimal Price, string? Comment);
    private record CreateOrderPayment(string PaymentTypeKind, decimal Sum, Guid PaymentTypeId, bool IsProcessedExternally);
    private record CreateDeliveryOrder(
        string Phone, string OrderServiceType, CreateOrderCustomer Customer,
        List<CreateOrderItem> Items, List<CreateOrderPayment> Payments, string? Comment);
    private record CreateDeliveryOrderRequest(Guid OrganizationId, Guid TerminalGroupId, CreateDeliveryOrder Order);
    private record CreationErrorDto(string? Message);
    private record CreateOrderInfoDto(Guid Id, string CreationStatus, CreationErrorDto? ErrorInfo);
    private record CreateDeliveryOrderResponse(Guid CorrelationId, CreateOrderInfoDto OrderInfo);
    private record CommandStatusRequest(Guid OrganizationId, Guid CorrelationId);
    private record CommandStatusResponse(string State);

    public async Task<Guid> CreateDeliveryOrderAsync(IikoCreateOrderRequest request, CancellationToken ct = default)
    {
        var order = new CreateDeliveryOrder(
            Phone: request.CustomerPhone,
            OrderServiceType: "DeliveryByClient",
            // One-time customer: kept out of iiko's own loyalty processing entirely — the
            // wallet earn/spend flow in this client already owns that, and double-binding
            // the order to the loyalty customer would double-credit bonus on close.
            Customer: new CreateOrderCustomer("one-time", request.CustomerName ?? request.CustomerPhone),
            Items: request.Items.Select(i => new CreateOrderItem(
                "Product", i.Amount, i.ProductId, i.ProductSizeId, i.Price, i.Comment)).ToList(),
            Payments: [new CreateOrderPayment("External", request.PaymentSum, _options.PaymentTypeId, true)],
            Comment: request.Comment);

        var resp = await PostAsync<CreateDeliveryOrderResponse>(
            "api/1/deliveries/create",
            new CreateDeliveryOrderRequest(_options.OrganizationId, request.TerminalGroupId, order), ct);

        var info = resp.OrderInfo;
        if (info.CreationStatus == "InProgress")
            info = await PollCommandStatusAsync(resp.CorrelationId, info, ct);

        if (info.CreationStatus != "Success")
            throw new IikoApiException(
                $"iiko deliveries/create did not succeed: {info.CreationStatus} — {info.ErrorInfo?.Message}");

        return info.Id;
    }

    private async Task<CreateOrderInfoDto> PollCommandStatusAsync(
        Guid correlationId, CreateOrderInfoDto lastKnown, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            var status = await PostAsync<CommandStatusResponse>(
                "api/1/commands/status",
                new CommandStatusRequest(_options.OrganizationId, correlationId), ct);
            if (status.State != "InProgress")
                return lastKnown with { CreationStatus = status.State };
        }
        return lastKnown; // still InProgress after the bounded wait — caller treats as failure/retry
    }

    private record CloseOrderRequest(Guid OrganizationId, Guid OrderId);

    public async Task CloseDeliveryOrderAsync(Guid iikoOrderId, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/deliveries/close",
            new CloseOrderRequest(_options.OrganizationId, iikoOrderId), ct);
        await EnsureSuccessAsync(resp, "deliveries/close", ct);
    }

    // ── Transaction history (issue #13) ─────────────────────────────────────

    private record TransactionsByPeriodRequest(
        Guid CustomerId, string DateFrom, string DateTo, int PageNumber, int PageSize, Guid OrganizationId);
    private record TransactionReportItemDto(
        Guid Id, string WhenCreated, decimal Sum, decimal? OrderSum, int? OrderNumber, Guid? PosOrderId,
        string? TypeName, bool? IsDelivery, decimal? BalanceBefore, decimal? BalanceAfter, string? Comment);
    private record TransactionsByPeriodResponse(List<TransactionReportItemDto>? Transactions);

    public async Task<List<IikoTransaction>> GetCustomerTransactionsAsync(
        Guid iikoCustomerId, DateTime dateFromUtc, DateTime dateToUtc,
        int pageSize = 200, CancellationToken ct = default)
    {
        var resp = await PostAsync<TransactionsByPeriodResponse>(
            "api/1/loyalty/iiko/customer/transactions/by_date",
            new TransactionsByPeriodRequest(
                iikoCustomerId, FormatIikoDate(dateFromUtc), FormatIikoDate(dateToUtc),
                PageNumber: 0, pageSize, _options.OrganizationId), ct);

        return (resp.Transactions ?? [])
            .Select(t => new IikoTransaction(
                t.Id, ParseIikoDate(t.WhenCreated), t.Sum, t.OrderSum, t.OrderNumber, t.PosOrderId,
                t.TypeName, t.IsDelivery, t.BalanceBefore, t.BalanceAfter, t.Comment))
            .OrderByDescending(t => t.WhenCreated)
            .ToList();
    }

    private static string FormatIikoDate(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm:ss.fff");

    private static DateTime ParseIikoDate(string s) => DateTime.Parse(
        s, System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

    private record WebhookFilter(List<string> WebHooksEventType);
    private record RegisterWebhookRequest(Guid OrganizationId, string WebHooksUri, string AuthToken, WebhookFilter WebHooksFilter);

    public async Task RegisterWebhookAsync(string webhookUrl, string authToken, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/webhooks/update_settings",
            new RegisterWebhookRequest(
                _options.OrganizationId, webhookUrl, authToken,
                new WebhookFilter(["DeliveryOrderUpdate", "DeliveryOrderError"])), ct);
        await EnsureSuccessAsync(resp, "webhooks/update_settings", ct);
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
