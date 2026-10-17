namespace Yurt.Application.Features.Engagement;

/// <summary>
/// Automated marketing pushes. Both are off by default: turning one on sends real
/// notifications to real customers, so it's an explicit ops decision.
/// </summary>
public class EngagementOptions
{
    /// <summary>Send a "happy birthday" push on the customer's birthday.</summary>
    public bool BirthdayPushEnabled { get; set; }

    /// <summary>Optional discount code (created in admin → Discount codes) named in the birthday push.</summary>
    public string BirthdayDiscountCode { get; set; } = "";

    /// <summary>Send a "we miss you" push to customers with no order for <see cref="WinBackAfterDays"/>.</summary>
    public bool WinBackPushEnabled { get; set; }

    public int WinBackAfterDays { get; set; } = 30;

    /// <summary>Don't send the same customer another win-back push within this many days.</summary>
    public int WinBackCooldownDays { get; set; } = 60;

    /// <summary>Local hour (cafe time zone) from which the daily pushes go out.</summary>
    public int SendFromLocalHour { get; set; } = 11;

    /// <summary>Cafe time zone offset from UTC, in hours (Kazakhstan = +5).</summary>
    public int UtcOffsetHours { get; set; } = 5;
}
