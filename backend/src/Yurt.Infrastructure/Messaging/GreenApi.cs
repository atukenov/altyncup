using Yurt.Application.Features.Auth;

namespace Yurt.Infrastructure.Messaging;

/// <summary>URL building and error hints shared by the Green API OTP sender and diagnostics.</summary>
internal static class GreenApi
{
    /// <summary>The old shared host. Instances created on newer servers only answer on their own apiUrl.</summary>
    public const string LegacySharedHost = "api.green-api.com";

    public static bool HasCredentials(OtpOptions o)
        => !string.IsNullOrWhiteSpace(o.IdInstance) && !string.IsNullOrWhiteSpace(o.ApiTokenInstance);

    public static string MethodUrl(OtpOptions o, string method)
        => $"{o.GreenApiBaseUrl.TrimEnd('/')}/waInstance{o.IdInstance.Trim()}/{method}/{o.ApiTokenInstance.Trim()}";

    /// <summary>Green API expects <c>{digits}@c.us</c> with no leading + or spaces.</summary>
    public static string ToChatId(string mobileNumber) => $"{Digits(mobileNumber)}@c.us";

    public static string Digits(string mobileNumber) => new(mobileNumber.Where(char.IsDigit).ToArray());

    /// <summary>What a non-2xx sendMessage status usually means, so the log says what to fix.</summary>
    public static string? HintForStatus(int status) => status switch
    {
        400 => "Bad request: check the chatId format (7XXXXXXXXXX@c.us) and the message body.",
        401 or 403 => "Unauthorized: IdInstance/ApiTokenInstance are wrong, or the instance is on a different apiUrl host.",
        404 => "Not found: GreenApiBaseUrl is probably not this instance's apiUrl (see the Green API console).",
        429 => "Rate limited by Green API: too many requests.",
        466 => "Quota exceeded: the free Developer plan only lets the instance message a few numbers per month. Upgrade the plan or test with an allowed number.",
        >= 500 => "Green API server error: retry later and check status.green-api.com.",
        _ => null,
    };

    /// <summary>Hint for a <c>stateInstance</c> value returned by getStateInstance.</summary>
    public static string? HintForState(string? state) => state switch
    {
        "authorized" => null,
        "notAuthorized" => "The WhatsApp account is logged out. Re-link it with the QR code on the admin WhatsApp codes page (or in the Green API console); messages sent meanwhile sit in a queue and are dropped after 24h.",
        "blocked" => "The WhatsApp account is banned. Use another number for the instance.",
        "yellowCard" => "WhatsApp flagged the account for suspected spam; sending is paused.",
        "starting" => "The instance is starting up; try again in a few minutes.",
        "sleepMode" => "The phone is offline (sleep mode). Open WhatsApp on the phone.",
        _ => $"Unexpected instance state '{state}'.",
    };

    /// <summary>Hint for an outgoing message's <c>statusMessage</c> returned by getMessage.</summary>
    public static string? HintForMessageStatus(string? status) => status switch
    {
        "sent" or "delivered" or "read" => null,
        "pending" => "Still queued. If it stays pending the instance is not authorized or the phone is offline.",
        "failed" => "WhatsApp rejected the message.",
        "noAccount" => "This phone number has no WhatsApp account.",
        "notInGroup" => "Not a member of the target group.",
        _ => $"Unexpected message status '{status}'.",
    };

    public static string Truncate(string s, int max = 1000) => s.Length <= max ? s : s[..max];
}
