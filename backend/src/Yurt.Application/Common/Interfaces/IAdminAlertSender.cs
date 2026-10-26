namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Pushes an operational alert to the admins (Telegram). Never throws: an alert that
/// cannot be delivered is logged and dropped so it can't break the request that raised it.
/// </summary>
public interface IAdminAlertSender
{
    /// <summary>
    /// Send <paramref name="text"/>. Alerts sharing a <paramref name="throttleKey"/> are sent
    /// at most once per throttle window, so a burst of failures produces one message.
    /// </summary>
    Task SendAsync(string throttleKey, string text, CancellationToken ct = default);

    /// <summary>Send immediately, ignoring throttling. Returns false when it could not be delivered.</summary>
    Task<bool> SendNowAsync(string text, CancellationToken ct = default);
}
