using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Auth;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Watches WhatsApp OTP delivery and alerts admins when it breaks silently:
///  - the Green API instance stops being "authorized" (and again when it recovers);
///  - codes were sent in the last hour but nobody verified any of them, which is what
///    users see when messages are queued but never delivered.
/// Send failures themselves are alerted immediately by AuthService.
/// </summary>
public class OtpMonitorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly OtpOptions _otpOptions;
    private readonly AlertOptions _alertOptions;
    private readonly IAdminAlertSender _alerts;
    private readonly ILogger<OtpMonitorService> _logger;

    // Last state we alerted about; starts as "authorized" so a bad state at startup alerts.
    private string _lastState = "authorized";

    public OtpMonitorService(
        IServiceProvider services, OtpOptions otpOptions, AlertOptions alertOptions,
        IAdminAlertSender alerts, ILogger<OtpMonitorService> logger)
    {
        _services = services;
        _otpOptions = otpOptions;
        _alertOptions = alertOptions;
        _alerts = alerts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_otpOptions.Enabled) return;

        var interval = TimeSpan.FromMinutes(Math.Max(1, _alertOptions.OtpMonitorIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP monitor check failed");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>One monitor pass. Public so it can be exercised directly in tests.</summary>
    public async Task CheckOnceAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var diagnostics = scope.ServiceProvider.GetRequiredService<IOtpDiagnostics>();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var state = await diagnostics.GetInstanceStateAsync(ct);
        if (state != _lastState)
        {
            if (state == "authorized")
                await _alerts.SendNowAsync("✅ WhatsApp OTP is back: Green API instance is authorized again.", ct);
            else
                await _alerts.SendNowAsync(
                    $"🚨 WhatsApp OTP is DOWN: Green API instance state is '{state}'. " +
                    "Customers cannot receive verification codes. " +
                    "If it says notAuthorized, scan the QR code in the Green API console.", ct);
            _lastState = state;
        }

        var since = nowUtc.AddHours(-1);
        var recent = await db.OtpEvents
            .Where(e => e.CreatedAt >= since
                && (e.Type == OtpEventType.CodeSent || e.Type == OtpEventType.Verified))
            .Select(e => e.Type)
            .ToListAsync(ct);
        var sent = recent.Count(t => t == OtpEventType.CodeSent);
        var verified = recent.Count(t => t == OtpEventType.Verified);

        if (sent >= Math.Max(1, _alertOptions.UnverifiedCodesThreshold) && verified == 0)
        {
            await _alerts.SendAsync("otp-unverified",
                $"⚠️ {sent} WhatsApp codes were sent in the last hour and none were verified. " +
                "Messages may not be arriving. Check the Green API console and /api/v1/admin/otp/summary.", ct);
        }
    }
}
