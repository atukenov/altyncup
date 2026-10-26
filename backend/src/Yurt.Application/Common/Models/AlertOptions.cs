namespace Yurt.Application.Common.Models;

/// <summary>
/// Admin alerting ("Alerts" section). Alerts go to a Telegram chat through a bot; leave
/// the token or chat id empty to turn alerts off. Secrets come from env vars.
/// </summary>
public class AlertOptions
{
    /// <summary>Bot token from @BotFather (Alerts__TelegramBotToken).</summary>
    public string TelegramBotToken { get; set; } = string.Empty;

    /// <summary>Chat (or group) id the bot posts to (Alerts__TelegramChatId).</summary>
    public string TelegramChatId { get; set; } = string.Empty;

    /// <summary>Minimum minutes between two alerts of the same kind.</summary>
    public int ThrottleMinutes { get; set; } = 30;

    /// <summary>How often the OTP monitor checks the WhatsApp instance and recent codes.</summary>
    public int OtpMonitorIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// The monitor alerts when at least this many codes were sent in the last hour
    /// and none of them were verified (codes probably are not arriving).
    /// </summary>
    public int UnverifiedCodesThreshold { get; set; } = 3;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TelegramBotToken) && !string.IsNullOrWhiteSpace(TelegramChatId);
}
