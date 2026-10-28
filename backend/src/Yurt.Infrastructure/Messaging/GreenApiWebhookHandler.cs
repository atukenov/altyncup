using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Services;

namespace Yurt.Infrastructure.Messaging;

/// <summary>
/// Handles the Green API instance webhooks that matter for OTP delivery:
///  - <c>outgoingMessageStatus</c>: WhatsApp's verdict on a sent code (sent → delivered → read,
///    or failed / noAccount), stored on the matching <see cref="PhoneVerification"/> row and
///    recorded as a <see cref="OtpEventType.DeliveryFailed"/> event when it didn't arrive;
///  - <c>stateInstanceChanged</c>: the instance logged out, got banned, etc. Passed to
///    <see cref="WhatsAppStateTracker"/>, which alerts admins right away.
/// Every other webhook type is acknowledged and ignored.
/// </summary>
public class GreenApiWebhookHandler
{
    private readonly IApplicationDbContext _db;
    private readonly OtpOptions _options;
    private readonly WhatsAppStateTracker _state;
    private readonly ILogger<GreenApiWebhookHandler> _logger;

    public GreenApiWebhookHandler(
        IApplicationDbContext db, OtpOptions options, WhatsAppStateTracker state,
        ILogger<GreenApiWebhookHandler> logger)
    {
        _db = db;
        _options = options;
        _state = state;
        _logger = logger;
    }

    /// <summary>
    /// True when the request carries the configured <see cref="OtpOptions.WebhookToken"/>,
    /// either as the Authorization header Green API sends for "webhookUrlToken"
    /// (<c>Bearer {token}</c>, or the bare token) or as a <c>?token=</c> query value.
    /// Always false while no token is configured.
    /// </summary>
    public bool IsAuthorized(string? authorizationHeader, string? queryToken)
    {
        var expected = _options.WebhookToken.Trim();
        if (expected.Length == 0) return false;

        var header = authorizationHeader?.Trim() ?? "";
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            header = header["Bearer ".Length..].Trim();

        return FixedTimeEquals(header, expected) || FixedTimeEquals(queryToken?.Trim() ?? "", expected);
    }

    private static bool FixedTimeEquals(string actual, string expected)
        => actual.Length > 0 && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));

    public async Task HandleAsync(string rawBody, CancellationToken ct = default)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            _logger.LogWarning("Malformed Green API webhook: {Body}", GreenApi.Truncate(rawBody, 500));
            return;
        }
        if (root.ValueKind != JsonValueKind.Object) return;

        // Ignore webhooks from another instance pointed at the same URL.
        if (root.TryGetProperty("instanceData", out var instance)
            && instance.ValueKind == JsonValueKind.Object
            && instance.TryGetProperty("idInstance", out var idInstance)
            && !string.IsNullOrWhiteSpace(_options.IdInstance)
            && idInstance.ToString() != _options.IdInstance.Trim())
        {
            _logger.LogWarning("Green API webhook for unexpected instance {IdInstance} ignored", idInstance.ToString());
            return;
        }

        switch (ReadString(root, "typeWebhook"))
        {
            case "outgoingMessageStatus":
                await HandleMessageStatusAsync(root, ct);
                break;
            case "stateInstanceChanged":
                var state = ReadString(root, "stateInstance");
                if (!string.IsNullOrWhiteSpace(state))
                {
                    _logger.LogInformation("Green API instance state changed to {State}", state);
                    await _state.ReportAsync(state, fromWebhook: true, ct);
                }
                break;
        }
    }

    /// <summary>Order of WhatsApp statuses. Webhooks can arrive out of order, so a status only replaces a lower one.</summary>
    public static int StatusRank(string? status) => status switch
    {
        null => 0,
        "sendFailed" => 0,
        "pending" => 1,
        "sent" => 2,
        "delivered" => 3,
        "read" => 4,
        "failed" or "noAccount" or "notInGroup" => 5,
        _ => 0,
    };

    private static bool IsFailure(string status) => status is "failed" or "noAccount" or "notInGroup";

    private async Task HandleMessageStatusAsync(JsonElement root, CancellationToken ct)
    {
        var idMessage = ReadString(root, "idMessage");
        var status = ReadString(root, "status");
        if (string.IsNullOrWhiteSpace(idMessage) || string.IsNullOrWhiteSpace(status)) return;

        var now = DateTime.UtcNow;
        var pending = await _db.PhoneVerifications
            .FirstOrDefaultAsync(v => v.ProviderMessageId == idMessage, ct);
        var changed = false;
        if (pending != null && StatusRank(status) > StatusRank(pending.DeliveryStatus))
        {
            pending.DeliveryStatus = status;
            pending.DeliveryStatusAt = now;
            changed = true;
        }

        if (IsFailure(status) && !await _db.OtpEvents.AnyAsync(
                e => e.ProviderMessageId == idMessage && e.Type == OtpEventType.DeliveryFailed, ct))
        {
            // The row may be gone (resent or replaced), so take the number from the send event.
            var sent = pending is null
                ? await _db.OtpEvents.FirstOrDefaultAsync(
                    e => e.ProviderMessageId == idMessage && e.Type == OtpEventType.CodeSent, ct)
                : null;
            var mobile = pending?.MobileNumber ?? sent?.MobileNumber;
            if (mobile != null)
            {
                var description = ReadString(root, "description");
                var detail = $"{status}: {GreenApi.HintForMessageStatus(status)}"
                    + (string.IsNullOrWhiteSpace(description) ? "" : $" ({description})");
                _db.OtpEvents.Add(new OtpEvent
                {
                    MobileNumber = mobile,
                    Purpose = pending?.Purpose ?? sent!.Purpose,
                    Type = OtpEventType.DeliveryFailed,
                    Detail = detail.Length > 1000 ? detail[..1000] : detail,
                    ProviderMessageId = idMessage,
                });
                changed = true;
                _logger.LogWarning("WhatsApp OTP message {IdMessage} to {Mobile} was not delivered: {Status}",
                    idMessage, mobile, status);
            }
        }

        if (changed)
            await _db.SaveChangesAsync(ct);
    }

    private static string? ReadString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
