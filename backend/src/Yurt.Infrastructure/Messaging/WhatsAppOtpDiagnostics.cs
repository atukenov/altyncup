using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;

namespace Yurt.Infrastructure.Messaging;

/// <summary>
/// Green API implementation of <see cref="IOtpDiagnostics"/>: getStateInstance,
/// checkWhatsapp, and optionally sendMessage followed by getMessage to read the
/// delivery status WhatsApp reported for the test message.
/// </summary>
public class WhatsAppOtpDiagnostics : IOtpDiagnostics
{
    private readonly HttpClient _http;
    private readonly OtpOptions _options;

    /// <summary>How long to wait after sending before asking for the message status.</summary>
    public TimeSpan StatusCheckDelay { get; set; } = TimeSpan.FromSeconds(5);

    public WhatsAppOtpDiagnostics(HttpClient http, OtpOptions options)
    {
        _http = http;
        _options = options;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<OtpDiagnosticsReport> RunAsync(
        string mobileNumber, bool sendTestMessage, CancellationToken ct = default)
    {
        var chatId = GreenApi.ToChatId(mobileNumber);
        var warnings = ConfigWarnings(mobileNumber);
        var steps = new List<OtpDiagnosticsStep>();

        if (GreenApi.HasCredentials(_options))
        {
            steps.Add(await CheckStateAsync(ct));
            steps.Add(await CheckWhatsappAsync(mobileNumber, ct));

            if (sendTestMessage)
            {
                var (sendStep, idMessage) = await SendTestAsync(chatId, ct);
                steps.Add(sendStep);
                if (idMessage is not null)
                {
                    await Task.Delay(StatusCheckDelay, ct);
                    steps.Add(await CheckMessageStatusAsync(chatId, idMessage, ct));
                }
            }
        }

        return new OtpDiagnosticsReport(
            _options.Enabled,
            _options.GreenApiBaseUrl,
            _options.IdInstance,
            !string.IsNullOrWhiteSpace(_options.ApiTokenInstance),
            chatId,
            warnings,
            steps);
    }

    public async Task<string> GetInstanceStateAsync(CancellationToken ct = default)
    {
        if (!GreenApi.HasCredentials(_options))
            return "error: Green API credentials are not configured";

        var (status, body, error) = await CallAsync(HttpMethod.Get, "getStateInstance", null, ct);
        if (error is not null)
            return $"error: {error}";
        if (status is not 200)
            return $"error: HTTP {status} {GreenApi.HintForStatus(status ?? 0)}".TrimEnd();
        return ReadString(body!, "stateInstance") ?? $"error: unexpected response {body}";
    }

    public async Task<WhatsAppInstanceInfo> GetInstanceInfoAsync(CancellationToken ct = default)
    {
        var id = _options.IdInstance.Trim();
        if (!GreenApi.HasCredentials(_options))
            return new(null, null, null, null, id, "Green API credentials are not configured (Otp__IdInstance, Otp__ApiTokenInstance).");

        var (status, body, error) = await CallAsync(HttpMethod.Get, "getStateInstance", null, ct);
        if (error is not null || status is not 200)
            return new(null, null, null, null, id, error ?? $"HTTP {status} {GreenApi.HintForStatus(status ?? 0)}".TrimEnd());

        var state = ReadString(body!, "stateInstance");
        string? phone = null, avatar = null;
        // Settings are a nice-to-have: the card still shows the state if this call fails.
        var (sStatus, sBody, _) = await CallAsync(HttpMethod.Get, "getWaSettings", null, ct);
        if (sStatus is 200 && sBody is not null)
        {
            phone = NullIfEmpty(ReadString(sBody, "phone"));
            avatar = NullIfEmpty(ReadString(sBody, "avatar"));
        }

        return new(state, GreenApi.HintForState(state), phone, avatar, id, null);
    }

    public async Task<WhatsAppQr> GetQrAsync(CancellationToken ct = default)
    {
        if (!GreenApi.HasCredentials(_options))
            return new("error", null, "Green API credentials are not configured.");

        var (status, body, error) = await CallAsync(HttpMethod.Get, "qr", null, ct, truncate: false);
        if (error is not null)
            return new("error", null, error);
        if (status is not 200)
            return new("error", null, $"HTTP {status} {GreenApi.HintForStatus(status ?? 0)}".TrimEnd());

        var type = ReadString(body!, "type") ?? "error";
        var message = ReadString(body!, "message");
        return type == "qrCode"
            ? new WhatsAppQr(type, message, null)
            : new WhatsAppQr(type, null, type == "error" ? message ?? GreenApi.Truncate(body!) : null);
    }

    public async Task<WhatsAppAuthorizationCode> GetAuthorizationCodeAsync(string phoneNumber, CancellationToken ct = default)
    {
        if (!GreenApi.HasCredentials(_options))
            return new(false, null, "Green API credentials are not configured.");
        if (!long.TryParse(GreenApi.Digits(phoneNumber), out var digits))
            return new(false, null, "Phone number has no digits.");

        var (status, body, error) = await CallAsync(
            HttpMethod.Post, "getAuthorizationCode", new { phoneNumber = digits }, ct);
        if (error is not null)
            return new(false, null, error);
        if (status is not 200)
            return new(false, null, $"HTTP {status} {GreenApi.HintForStatus(status ?? 0)}".TrimEnd());

        var code = ReadString(body!, "code");
        return ReadBool(body!, "status") == true && !string.IsNullOrEmpty(code)
            ? new WhatsAppAuthorizationCode(true, code, null)
            : new WhatsAppAuthorizationCode(false, null, $"Green API did not return a code: {body}");
    }

    private List<string> ConfigWarnings(string mobileNumber)
    {
        var warnings = new List<string>();

        if (!_options.Enabled)
            warnings.Add("Otp:Enabled is false: the app never calls Green API and returns the code as devCode instead. Set Otp__Enabled=true.");
        if (string.IsNullOrWhiteSpace(_options.IdInstance))
            warnings.Add("Otp:IdInstance is empty. Set Otp__IdInstance.");
        if (string.IsNullOrWhiteSpace(_options.ApiTokenInstance))
            warnings.Add("Otp:ApiTokenInstance is empty. Set Otp__ApiTokenInstance.");

        if (!Uri.TryCreate(_options.GreenApiBaseUrl, UriKind.Absolute, out var baseUri))
            warnings.Add($"Otp:GreenApiBaseUrl '{_options.GreenApiBaseUrl}' is not a valid URL.");
        else if (baseUri.Host.Equals(GreenApi.LegacySharedHost, StringComparison.OrdinalIgnoreCase))
            warnings.Add("Otp:GreenApiBaseUrl is the shared api.green-api.com host. Use the instance's own apiUrl from the Green API console (e.g. https://7103.api.greenapi.com) via Otp__GreenApiBaseUrl.");

        var digits = GreenApi.Digits(mobileNumber);
        if (digits.Length != 11 || digits[0] != '7')
            warnings.Add($"Phone '{mobileNumber}' becomes chatId {digits}@c.us. Kazakh numbers need 11 digits starting with 7 (e.g. +77011234567); a leading 8 will not reach anyone.");

        return warnings;
    }

    private async Task<OtpDiagnosticsStep> CheckStateAsync(CancellationToken ct)
    {
        const string name = "getStateInstance";
        var (status, body, error) = await CallAsync(HttpMethod.Get, "getStateInstance", null, ct);
        if (error is not null || status is not 200)
            return Failed(name, status, body ?? error);

        var state = ReadString(body!, "stateInstance");
        var hint = GreenApi.HintForState(state);
        return new OtpDiagnosticsStep(name, hint is null, status, body, hint);
    }

    private async Task<OtpDiagnosticsStep> CheckWhatsappAsync(string mobileNumber, CancellationToken ct)
    {
        const string name = "checkWhatsapp";
        if (!long.TryParse(GreenApi.Digits(mobileNumber), out var phoneNumber))
            return new OtpDiagnosticsStep(name, false, null, null, "Phone number has no digits.");

        var (status, body, error) = await CallAsync(HttpMethod.Post, "checkWhatsapp", new { phoneNumber }, ct);
        if (error is not null || status is not 200)
            return Failed(name, status, body ?? error);

        var exists = ReadBool(body!, "existsWhatsapp");
        return new OtpDiagnosticsStep(name, exists == true, status, body,
            exists == true ? null : "This number has no WhatsApp account, so the OTP can never be delivered to it.");
    }

    private async Task<(OtpDiagnosticsStep, string?)> SendTestAsync(string chatId, CancellationToken ct)
    {
        const string name = "sendMessage";
        var message = _options.MessageTemplate.Replace("{code}", "0000") + " (test message, ignore)";
        var (status, body, error) = await CallAsync(HttpMethod.Post, "sendMessage", new { chatId, message }, ct);
        if (error is not null || status is not (>= 200 and < 300))
            return (Failed(name, status, body ?? error), null);

        var idMessage = ReadString(body!, "idMessage");
        return idMessage is null
            ? (new OtpDiagnosticsStep(name, false, status, body, "Response had no idMessage."), null)
            : (new OtpDiagnosticsStep(name, true, status, body, null), idMessage);
    }

    private async Task<OtpDiagnosticsStep> CheckMessageStatusAsync(string chatId, string idMessage, CancellationToken ct)
    {
        const string name = "getMessage";
        var (status, body, error) = await CallAsync(HttpMethod.Post, "getMessage", new { chatId, idMessage }, ct);
        if (error is not null || status is not 200)
            return Failed(name, status, body ?? error);

        var messageStatus = ReadString(body!, "statusMessage");
        var hint = GreenApi.HintForMessageStatus(messageStatus);
        return new OtpDiagnosticsStep(name, hint is null, status, body, hint);
    }

    private async Task<(int? Status, string? Body, string? Error)> CallAsync(
        HttpMethod method, string apiMethod, object? payload, CancellationToken ct, bool truncate = true)
    {
        try
        {
            using var request = new HttpRequestMessage(method, GreenApi.MethodUrl(_options, apiMethod));
            if (payload is not null)
                request.Content = JsonContent.Create(payload);
            using var resp = await _http.SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return ((int)resp.StatusCode, truncate ? GreenApi.Truncate(body) : body, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Never include the URL: its last segment is the API token.
            return (null, null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static OtpDiagnosticsStep Failed(string name, int? status, string? response)
        => new(name, false, status, response,
            status is int s ? GreenApi.HintForStatus(s) : "Could not reach Green API: check GreenApiBaseUrl and network access.");

    private static string? ReadString(string body, string property)
        => TryRead(body, property, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static bool? ReadBool(string body, string property)
        => TryRead(body, property, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? el.GetBoolean() : null;

    private static bool TryRead(string body, string property, out JsonElement value)
    {
        value = default;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(property, out var el))
                return false;
            value = el.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
