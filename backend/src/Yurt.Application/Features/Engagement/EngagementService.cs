using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Push.Services;

namespace Yurt.Application.Features.Engagement;

/// <summary>
/// Birthday and win-back pushes. Each run is idempotent: a customer is stamped when
/// their push goes out, so running hourly (or after a restart) never double-sends.
/// </summary>
public class EngagementService
{
    private const int BatchSize = 200;

    private readonly IApplicationDbContext _db;
    private readonly PushNotificationService _push;
    private readonly EngagementOptions _options;
    private readonly ILogger<EngagementService> _logger;

    public EngagementService(
        IApplicationDbContext db,
        PushNotificationService push,
        EngagementOptions options,
        ILogger<EngagementService> logger)
    {
        _db = db;
        _push = push;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var local = utcNow.AddHours(_options.UtcOffsetHours);
        if (local.Hour < _options.SendFromLocalHour) return;

        if (_options.BirthdayPushEnabled) await SendBirthdayPushesAsync(DateOnly.FromDateTime(local), ct);
        if (_options.WinBackPushEnabled) await SendWinBackPushesAsync(utcNow, ct);
    }

    public async Task<int> SendBirthdayPushesAsync(DateOnly localToday, CancellationToken ct = default)
    {
        var customers = await _db.CustomerUsers
            .Where(c => c.IsActive && c.DateOfBirth != null
                && c.DateOfBirth.Value.Month == localToday.Month
                && c.DateOfBirth.Value.Day == localToday.Day
                && (c.LastBirthdayPushYear == null || c.LastBirthdayPushYear < localToday.Year)
                && c.DeviceTokens.Any())
            .Take(BatchSize)
            .ToListAsync(ct);

        var code = _options.BirthdayDiscountCode.Trim();
        foreach (var c in customers)
        {
            var name = string.IsNullOrWhiteSpace(c.FirstName) ? "" : $", {c.FirstName}";
            var body = string.IsNullOrEmpty(code)
                ? "Altyncup поздравляет вас с днём рождения! Заходите за любимым кофе ☕"
                : $"Altyncup поздравляет вас с днём рождения! Ваш подарок: промокод {code} ☕";
            await _push.SendOrderStatusPushAsync(c.Id, $"С днём рождения{name}! 🎂", body,
                new Dictionary<string, string> { ["type"] = "birthday" }, ct);
            c.LastBirthdayPushYear = localToday.Year;
        }

        if (customers.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Sent {Count} birthday push(es).", customers.Count);
        }
        return customers.Count;
    }

    public async Task<int> SendWinBackPushesAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var inactiveSince = utcNow.AddDays(-_options.WinBackAfterDays);
        var cooldownSince = utcNow.AddDays(-_options.WinBackCooldownDays);

        // Customers who have ordered before, but not recently, and weren't nudged lately.
        var customers = await _db.CustomerUsers
            .Where(c => c.IsActive
                && c.DeviceTokens.Any()
                && c.Orders.Any()
                && !c.Orders.Any(o => o.CreatedAt >= inactiveSince)
                && (c.LastWinBackPushAt == null || c.LastWinBackPushAt < cooldownSince))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var c in customers)
        {
            await _push.SendOrderStatusPushAsync(c.Id, "Мы скучаем по вам ☕",
                "Давно не виделись! Загляните в Altyncup, ваш любимый напиток ждёт.",
                new Dictionary<string, string> { ["type"] = "winback" }, ct);
            c.LastWinBackPushAt = utcNow;
        }

        if (customers.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Sent {Count} win-back push(es).", customers.Count);
        }
        return customers.Count;
    }
}
