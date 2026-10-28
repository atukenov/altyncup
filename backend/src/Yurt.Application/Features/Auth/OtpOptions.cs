namespace Yurt.Application.Features.Auth;

/// <summary>
/// Configuration for the one-time-password used to prove a customer owns their phone number
/// ("Otp" section). Secrets (IdInstance, ApiTokenInstance) must come from environment
/// variables, never committed config.
/// </summary>
public class OtpOptions
{
    public const string WhatsAppProvider = "WhatsApp";
    public const string FirebaseProvider = "Firebase";

    /// <summary>Length of the codes this backend generates and sends over WhatsApp.</summary>
    public const int WhatsAppCodeLength = 4;

    /// <summary>Length of the SMS codes Firebase Phone Auth sends.</summary>
    public const int FirebaseCodeLength = 6;

    /// <summary>
    /// Who delivers the code (Otp__Provider): <c>WhatsApp</c> (default) sends a 4-digit code
    /// through Green API; <c>Firebase</c> lets the customer app send a 6-digit SMS through
    /// Firebase Phone Auth and the backend only checks the resulting Firebase ID token.
    /// App versions (and the web app) that can't do Firebase keep getting WhatsApp codes,
    /// so switching back to <c>WhatsApp</c> is always safe.
    /// </summary>
    public string Provider { get; set; } = WhatsAppProvider;

    public bool FirebaseEnabled
        => string.Equals(Provider?.Trim(), FirebaseProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// When false no WhatsApp message is sent — the generated code is logged and returned
    /// in the start response as <c>devCode</c> so local/dev registration still works.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Green API instance base URL, e.g. https://7201.api.green-api.com</summary>
    public string GreenApiBaseUrl { get; set; } = "https://api.green-api.com";

    /// <summary>Green API instance id (the number in <c>waInstance{IdInstance}</c>).</summary>
    public string IdInstance { get; set; } = string.Empty;

    /// <summary>Green API instance API token (the last path segment of the send URL).</summary>
    public string ApiTokenInstance { get; set; } = string.Empty;

    /// <summary>
    /// Call Green API checkWhatsapp before sending a code and refuse numbers that have no
    /// WhatsApp account. If the check itself fails the code is sent anyway.
    /// </summary>
    public bool CheckWhatsAppBeforeSend { get; set; } = true;

    /// <summary>How long a code stays valid, in minutes.</summary>
    public int CodeExpiryMinutes { get; set; } = 15;

    /// <summary>Minimum seconds between two code requests for the same number.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Maximum wrong-code attempts before the code is rejected outright.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Shared secret for the Green API webhook endpoint (Otp__WebhookToken). Set the same
    /// value as "webhookUrlToken" in the Green API instance settings. While it is empty the
    /// webhook endpoint rejects every call.
    /// </summary>
    public string WebhookToken { get; set; } = string.Empty;

    /// <summary><c>{code}</c> is replaced with the generated code.</summary>
    public string MessageTemplate { get; set; } = "Your Altyncup verification code: {code}";
}
