namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Checks the WhatsApp OTP setup end to end (config, instance state, recipient, optional
/// test message) and returns every provider response, so delivery problems are visible.
/// </summary>
public interface IOtpDiagnostics
{
    Task<OtpDiagnosticsReport> RunAsync(string mobileNumber, bool sendTestMessage, CancellationToken ct = default);

    /// <summary>
    /// The WhatsApp instance state ("authorized", "notAuthorized", ...), or an
    /// "error: ..." string when Green API could not be asked.
    /// </summary>
    Task<string> GetInstanceStateAsync(CancellationToken ct = default);

    /// <summary>State plus the linked WhatsApp account (getStateInstance + getWaSettings).</summary>
    Task<WhatsAppInstanceInfo> GetInstanceInfoAsync(CancellationToken ct = default);

    /// <summary>A fresh QR code for linking WhatsApp to the instance (Green API <c>qr</c>).</summary>
    Task<WhatsAppQr> GetQrAsync(CancellationToken ct = default);

    /// <summary>
    /// An 8-character pairing code for linking by phone number instead of scanning a QR
    /// (Green API <c>getAuthorizationCode</c>).
    /// </summary>
    Task<WhatsAppAuthorizationCode> GetAuthorizationCodeAsync(string phoneNumber, CancellationToken ct = default);
}

/// <param name="State">Green API stateInstance, e.g. "authorized" or "notAuthorized"; null when it could not be read.</param>
/// <param name="Hint">What the state means and what to do, when it is not "authorized".</param>
/// <param name="Phone">The linked WhatsApp number (digits), when known.</param>
/// <param name="Avatar">URL of the linked account's profile picture, when it has one.</param>
/// <param name="Error">Why Green API could not be asked, if it could not.</param>
public record WhatsAppInstanceInfo(
    string? State, string? Hint, string? Phone, string? Avatar, string IdInstance, string? Error);

/// <param name="Type">"qrCode" (Image is set), "alreadyLogged", or "error".</param>
/// <param name="Image">Base64 PNG of the QR code when <paramref name="Type"/> is "qrCode".</param>
/// <param name="Message">Error text when <paramref name="Type"/> is "error".</param>
public record WhatsAppQr(string Type, string? Image, string? Message);

public record WhatsAppAuthorizationCode(bool Ok, string? Code, string? Error);

/// <param name="Ok">True when this step found nothing that would stop delivery.</param>
/// <param name="Response">Raw provider response body (truncated), or the error.</param>
/// <param name="Hint">What to fix when <paramref name="Ok"/> is false.</param>
public record OtpDiagnosticsStep(string Name, bool Ok, int? HttpStatus, string? Response, string? Hint);

public record OtpDiagnosticsReport(
    bool Enabled,
    string BaseUrl,
    string IdInstance,
    bool TokenConfigured,
    string ChatId,
    IReadOnlyList<string> ConfigWarnings,
    IReadOnlyList<OtpDiagnosticsStep> Steps)
{
    public bool AllOk => ConfigWarnings.Count == 0 && Steps.All(s => s.Ok);
}
