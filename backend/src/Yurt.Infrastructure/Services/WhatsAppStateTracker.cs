using Yurt.Application.Common.Interfaces;
using Yurt.Infrastructure.Messaging;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Remembers the last known Green API instance state and alerts admins when it changes.
/// Both the <see cref="OtpMonitorService"/> poll and the Green API <c>stateInstanceChanged</c>
/// webhook report here, so whichever notices a change first alerts and the other stays
/// quiet. Registered as a singleton.
/// </summary>
public class WhatsAppStateTracker
{
    private readonly IAdminAlertSender _alerts;
    private readonly object _lock = new();

    // Starts as "authorized" so a bad state at startup alerts.
    private string _lastState = "authorized";

    public WhatsAppStateTracker(IAdminAlertSender alerts) => _alerts = alerts;

    public string LastState { get { lock (_lock) return _lastState; } }

    /// <summary>Record <paramref name="state"/>; alerts and returns true when it differs from the last one.</summary>
    public async Task<bool> ReportAsync(string state, bool fromWebhook, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (state == _lastState) return false;
            _lastState = state;
        }

        var source = fromWebhook ? " (reported by Green API)" : "";
        if (state == "authorized")
        {
            await _alerts.SendNowAsync($"✅ WhatsApp OTP is back: Green API instance is authorized again{source}.", ct);
        }
        else
        {
            var hint = GreenApi.HintForState(state);
            await _alerts.SendNowAsync(
                $"🚨 WhatsApp OTP is DOWN: Green API instance state is '{state}'{source}. " +
                "Customers cannot receive verification codes. " +
                (hint is null || state.StartsWith("error:") ? "If it says notAuthorized, scan the QR code in the Green API console." : hint),
                ct);
        }
        return true;
    }
}
