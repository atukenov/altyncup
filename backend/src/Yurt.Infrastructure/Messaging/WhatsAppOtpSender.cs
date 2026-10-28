using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;

namespace Yurt.Infrastructure.Messaging;

/// <summary>
/// Sends the registration OTP as a WhatsApp message through the Green API
/// (<c>{BaseUrl}/waInstance{IdInstance}/sendMessage/{ApiTokenInstance}</c>).
/// </summary>
/// <remarks>
/// A 200 from sendMessage only means Green API queued the message. It is still dropped
/// when the instance is logged out or the number has no WhatsApp, so the returned
/// idMessage is logged to look the message up later (admin OTP diagnostics endpoint).
/// </remarks>
public class WhatsAppOtpSender : IOtpSender
{
    private readonly HttpClient _http;
    private readonly OtpOptions _options;
    private readonly ILogger<WhatsAppOtpSender> _logger;

    public WhatsAppOtpSender(HttpClient http, OtpOptions options, ILogger<WhatsAppOtpSender> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    private record SendMessageRequest(string ChatId, string Message);
    private record CheckWhatsappRequest(long PhoneNumber);

    /// <summary>The pre-check must not hold up the code for long, so it gets its own short timeout.</summary>
    public TimeSpan CheckTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public async Task<string?> SendAsync(string mobileNumber, string code, CancellationToken ct = default)
    {
        if (!GreenApi.HasCredentials(_options))
            throw new OtpSendException(
                "Green API credentials are missing: set Otp__IdInstance and Otp__ApiTokenInstance.");

        var url = GreenApi.MethodUrl(_options, "sendMessage");
        var chatId = GreenApi.ToChatId(mobileNumber);
        var message = _options.MessageTemplate.Replace("{code}", code);

        HttpResponseMessage resp;
        string body;
        try
        {
            resp = await _http.PostAsJsonAsync(url, new SendMessageRequest(chatId, message), ct);
            body = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Never include the URL: its last segment is the API token.
            throw new OtpSendException(
                $"Green API sendMessage request to {_options.GreenApiBaseUrl} failed: {ex.GetType().Name}: {ex.Message}", ex);
        }

        var status = (int)resp.StatusCode;
        if (!resp.IsSuccessStatusCode)
        {
            var hint = GreenApi.HintForStatus(status);
            throw new OtpSendException(
                $"Green API sendMessage failed with {status} for {chatId}: {GreenApi.Truncate(body, 500)}" +
                (hint is null ? "" : $" Hint: {hint}"));
        }

        var idMessage = ReadIdMessage(body);
        if (idMessage is null)
        {
            _logger.LogWarning(
                "Green API sendMessage returned {Status} for {ChatId} without an idMessage: {Body}",
                status, chatId, GreenApi.Truncate(body, 500));
            return null;
        }

        _logger.LogInformation(
            "Green API queued OTP for {ChatId} as message {IdMessage}", chatId, idMessage);
        return idMessage;
    }

    public async Task<bool?> HasWhatsAppAsync(string mobileNumber, CancellationToken ct = default)
    {
        if (!GreenApi.HasCredentials(_options)
            || !long.TryParse(GreenApi.Digits(mobileNumber), out var phoneNumber))
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CheckTimeout);

        try
        {
            var resp = await _http.PostAsJsonAsync(
                GreenApi.MethodUrl(_options, "checkWhatsapp"), new CheckWhatsappRequest(phoneNumber), timeout.Token);
            var body = await resp.Content.ReadAsStringAsync(timeout.Token);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Green API checkWhatsapp returned {Status} for {Phone}; sending the code anyway: {Body}",
                    (int)resp.StatusCode, phoneNumber, GreenApi.Truncate(body, 500));
                return null;
            }

            var exists = ReadExistsWhatsapp(body);
            if (exists is null)
                _logger.LogWarning(
                    "Green API checkWhatsapp gave no existsWhatsapp for {Phone}; sending the code anyway: {Body}",
                    phoneNumber, GreenApi.Truncate(body, 500));
            return exists;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Never log the URL: its last segment is the API token.
            _logger.LogWarning(
                "Green API checkWhatsapp failed for {Phone}; sending the code anyway: {Error}: {Message}",
                phoneNumber, ex.GetType().Name, ex.Message);
            return null;
        }
    }

    private static bool? ReadExistsWhatsapp(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("existsWhatsapp", out var exists)
                && exists.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? exists.GetBoolean()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadIdMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("idMessage", out var id)
                && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
