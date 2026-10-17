using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Engagement;
using Yurt.Application.Features.Push;
using Yurt.Application.Features.Push.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Runs the engagement queries against Postgres with a recording push sender.</summary>
[Collection("Integration")]
public class EngagementPushTests(YurtWebAppFactory factory)
{
    private sealed class RecordingSender : IPushNotificationSender
    {
        public List<(IReadOnlyList<string> Tokens, string Title)> Sent { get; } = [];
        public Task<PushSendResult> SendAsync(IReadOnlyList<string> tokens, string title, string body,
            IReadOnlyDictionary<string, string>? data, CancellationToken ct = default)
        {
            Sent.Add((tokens, title));
            return Task.FromResult(new PushSendResult([]));
        }
    }

    private static (EngagementService Svc, RecordingSender Sender) Build(IApplicationDbContext db, EngagementOptions opts)
    {
        var sender = new RecordingSender();
        var push = new PushNotificationService(db, sender, new FirebaseOptions { Enabled = true },
            NullLogger<PushNotificationService>.Instance);
        return (new EngagementService(db, push, opts, NullLogger<EngagementService>.Instance), sender);
    }

    private static CustomerUser Customer(string phone, DateOnly? dob = null) => new()
    {
        MobileNumber = phone, FirstName = "Aru", DateOfBirth = dob,
        DeviceTokens = [new CustomerDeviceToken { Token = "tok-" + phone, Platform = DevicePlatform.Android }]
    };

    [Fact]
    public async Task Birthday_SendsOncePerYear_OnlyToBirthdayCustomers()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var today = new DateOnly(2031, 3, 14);
        var birthday = Customer("+77009500001", new DateOnly(1995, 3, 14));
        var other = Customer("+77009500002", new DateOnly(1995, 3, 15));
        db.CustomerUsers.AddRange(birthday, other);
        await db.SaveChangesAsync();

        var (svc, sender) = Build(db, new EngagementOptions { BirthdayPushEnabled = true, BirthdayDiscountCode = "BDAY" });

        Assert.Equal(1, await svc.SendBirthdayPushesAsync(today));
        Assert.Contains(sender.Sent, s => s.Tokens.Contains("tok-+77009500001"));
        Assert.DoesNotContain(sender.Sent, s => s.Tokens.Contains("tok-+77009500002"));

        // Second run the same day (or after a restart) sends nothing.
        Assert.Equal(0, await svc.SendBirthdayPushesAsync(today));
    }

    [Fact]
    public async Task WinBack_TargetsLapsedCustomers_AndRespectsCooldown()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var locationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
        var now = new DateTime(2031, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        var lapsed = Customer("+77009500011");
        var recent = Customer("+77009500012");
        var never = Customer("+77009500013");
        db.CustomerUsers.AddRange(lapsed, recent, never);
        await db.SaveChangesAsync();
        db.Orders.AddRange(
            new Order { CustomerUserId = lapsed.Id, LocationId = locationId, Status = OrderStatus.Completed, CreatedAt = now.AddDays(-45) },
            new Order { CustomerUserId = recent.Id, LocationId = locationId, Status = OrderStatus.Completed, CreatedAt = now.AddDays(-3) });
        await db.SaveChangesAsync();

        var (svc, sender) = Build(db, new EngagementOptions { WinBackPushEnabled = true, WinBackAfterDays = 30 });
        await svc.SendWinBackPushesAsync(now);

        Assert.Contains(sender.Sent, s => s.Tokens.Contains("tok-+77009500011"));
        Assert.DoesNotContain(sender.Sent, s => s.Tokens.Contains("tok-+77009500012"));
        Assert.DoesNotContain(sender.Sent, s => s.Tokens.Contains("tok-+77009500013"));

        sender.Sent.Clear();
        await svc.SendWinBackPushesAsync(now.AddDays(1));
        Assert.DoesNotContain(sender.Sent, s => s.Tokens.Contains("tok-+77009500011"));
    }

    [Fact]
    public async Task RunAsync_BeforeSendHour_DoesNothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (svc, sender) = Build(db, new EngagementOptions { BirthdayPushEnabled = true, WinBackPushEnabled = true });
        // 03:00 UTC = 08:00 local (UTC+5), before the default 11:00 send hour.
        await svc.RunAsync(new DateTime(2031, 3, 14, 3, 0, 0, DateTimeKind.Utc));
        Assert.Empty(sender.Sent);
    }
}
