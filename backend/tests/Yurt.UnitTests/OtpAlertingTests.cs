using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.NSubstitute;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Auth;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Application.Features.Auth.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Messaging;
using Yurt.Infrastructure.Services;

namespace Yurt.UnitTests;

/// <summary>
/// OTP audit trail and admin alerting: AuthService records every code outcome in
/// OtpEvents and alerts on send failures; the Telegram sender throttles bursts; the
/// monitor alerts when the WhatsApp instance goes down or codes stop being verified.
/// </summary>
public class OtpAlertingTests
{
    // ── AuthService ───────────────────────────────────────────────────────────

    private sealed class AuthHarness
    {
        public IApplicationDbContext Db { get; } = Substitute.For<IApplicationDbContext>();
        public IOtpSender Sender { get; } = Substitute.For<IOtpSender>();
        public IAdminAlertSender Alerts { get; } = Substitute.For<IAdminAlertSender>();
        public IPasswordHasher Hasher { get; } = Substitute.For<IPasswordHasher>();
        public List<OtpEvent> Events { get; } = new();
        public AuthService Service { get; }

        public AuthHarness(bool otpEnabled = true, params PhoneVerification[] pending)
        {
            var customers = new List<CustomerUser>().BuildMockDbSet();
            var verifications = pending.ToList().BuildMockDbSet();
            Db.CustomerUsers.Returns(customers);
            Db.PhoneVerifications.Returns(verifications);
            var events = Events.BuildMockDbSet();
            events.When(s => s.Add(Arg.Any<OtpEvent>())).Do(c => Events.Add(c.Arg<OtpEvent>()));
            Db.OtpEvents.Returns(events);
            Hasher.Hash(Arg.Any<string>()).Returns("hash");

            Service = new AuthService(Db, Substitute.For<ITokenService>(), Hasher, Sender,
                new OtpOptions { Enabled = otpEnabled }, Alerts, NullLogger<AuthService>.Instance);
        }
    }

    private static readonly CustomerRegisterDto Register = new("+77011234567", "1234", "A", "B");

    [Fact]
    public async Task StartRegistration_Sent_RecordsCodeSentWithMessageId()
    {
        var h = new AuthHarness();
        h.Sender.SendAsync("+77011234567", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("MSG1");

        var result = await h.Service.StartRegistrationAsync(Register);

        Assert.True(result.Succeeded);
        var e = Assert.Single(h.Events);
        Assert.Equal(OtpEventType.CodeSent, e.Type);
        Assert.Equal(VerificationPurpose.Registration, e.Purpose);
        Assert.Equal("MSG1", e.ProviderMessageId);
        await h.Alerts.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task StartRegistration_SendFails_Returns502_RecordsFailure_AndAlerts()
    {
        var h = new AuthHarness();
        h.Sender.SendAsync(default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OtpSendException("Green API sendMessage failed with 466"));

        var result = await h.Service.StartRegistrationAsync(Register);

        Assert.False(result.Succeeded);
        Assert.Equal(502, result.StatusCode);
        var e = Assert.Single(h.Events);
        Assert.Equal(OtpEventType.SendFailed, e.Type);
        Assert.Contains("466", e.Detail);
        await h.Alerts.Received(1).SendAsync("otp-send-failed",
            Arg.Is<string>(t => t.Contains("+77011234567") && t.Contains("466")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRegistration_DeliveryDisabled_RecordsDisabled_AndDoesNotSend()
    {
        var h = new AuthHarness(otpEnabled: false);

        var result = await h.Service.StartRegistrationAsync(Register);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data!.DevCode);
        Assert.Equal(OtpEventType.DeliveryDisabled, Assert.Single(h.Events).Type);
        await h.Sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task VerifyRegistration_WrongCode_RecordsWrongCode()
    {
        var pending = new PhoneVerification
        {
            MobileNumber = "+77011234567", Purpose = VerificationPurpose.Registration,
            CodeHash = "hash", ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        };
        var h = new AuthHarness(true, pending);
        h.Hasher.Verify("9999", "hash").Returns(false);

        var result = await h.Service.VerifyRegistrationAsync(new RegisterVerifyDto("+77011234567", "9999"));

        Assert.False(result.Succeeded);
        Assert.Equal(OtpEventType.WrongCode, Assert.Single(h.Events).Type);
    }

    [Fact]
    public async Task VerifyRegistration_Expired_RecordsExpired()
    {
        var pending = new PhoneVerification
        {
            MobileNumber = "+77011234567", Purpose = VerificationPurpose.Registration,
            CodeHash = "hash", ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        var h = new AuthHarness(true, pending);

        await h.Service.VerifyRegistrationAsync(new RegisterVerifyDto("+77011234567", "1234"));

        Assert.Equal(OtpEventType.Expired, Assert.Single(h.Events).Type);
    }

    // ── TelegramAlertSender ───────────────────────────────────────────────────

    private sealed class CountingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Requests.Add((r.RequestUri!, await r.Content!.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") };
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (TelegramAlertSender, CountingHandler, FixedClock) Telegram(AlertOptions? options = null)
    {
        var handler = new CountingHandler();
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero));
        var sender = new TelegramAlertSender(factory,
            options ?? new AlertOptions { TelegramBotToken = "123:abc", TelegramChatId = "-100", ThrottleMinutes = 30 },
            NullLogger<TelegramAlertSender>.Instance, clock);
        return (sender, handler, clock);
    }

    [Fact]
    public async Task Telegram_PostsToBotApi_WithChatIdAndText()
    {
        var (sender, handler, _) = Telegram();

        Assert.True(await sender.SendNowAsync("hello"));

        var req = Assert.Single(handler.Requests);
        Assert.Equal("/bot123:abc/sendMessage", req.Uri.AbsolutePath);
        Assert.Contains("\"chat_id\":\"-100\"", req.Body);
        Assert.Contains("hello", req.Body);
    }

    [Fact]
    public async Task Telegram_ThrottlesSameKey_UntilWindowPasses()
    {
        var (sender, handler, clock) = Telegram();

        await sender.SendAsync("otp-send-failed", "1");
        await sender.SendAsync("otp-send-failed", "2");
        await sender.SendAsync("other", "3");
        clock.Now = clock.Now.AddMinutes(31);
        await sender.SendAsync("otp-send-failed", "4");

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Telegram_NotConfigured_SendsNothing()
    {
        var (sender, handler, _) = Telegram(new AlertOptions());

        Assert.False(await sender.SendNowAsync("x"));
        await sender.SendAsync("k", "y");

        Assert.Empty(handler.Requests);
    }

    // ── OtpMonitorService ─────────────────────────────────────────────────────

    private static (OtpMonitorService, IAdminAlertSender, IOtpDiagnostics) Monitor(List<OtpEvent> events)
    {
        var diagnostics = Substitute.For<IOtpDiagnostics>();
        var db = Substitute.For<IApplicationDbContext>();
        var set = events.BuildMockDbSet();
        db.OtpEvents.Returns(set);
        var services = new ServiceCollection()
            .AddSingleton(diagnostics)
            .AddSingleton(db)
            .BuildServiceProvider();
        var alerts = Substitute.For<IAdminAlertSender>();
        var monitor = new OtpMonitorService(services, new OtpOptions { Enabled = true },
            new AlertOptions { UnverifiedCodesThreshold = 3 }, alerts, NullLogger<OtpMonitorService>.Instance);
        return (monitor, alerts, diagnostics);
    }

    [Fact]
    public async Task Monitor_AlertsOnceWhenInstanceLogsOut_AndAgainOnRecovery()
    {
        var (monitor, alerts, diagnostics) = Monitor(new());
        var now = DateTime.UtcNow;

        diagnostics.GetInstanceStateAsync(Arg.Any<CancellationToken>()).Returns("notAuthorized");
        await monitor.CheckOnceAsync(now);
        await monitor.CheckOnceAsync(now);
        diagnostics.GetInstanceStateAsync(Arg.Any<CancellationToken>()).Returns("authorized");
        await monitor.CheckOnceAsync(now);

        await alerts.Received(1).SendNowAsync(Arg.Is<string>(t => t.Contains("DOWN") && t.Contains("notAuthorized")), Arg.Any<CancellationToken>());
        await alerts.Received(1).SendNowAsync(Arg.Is<string>(t => t.Contains("back")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Monitor_AlertsWhenCodesSentButNoneVerified()
    {
        var now = DateTime.UtcNow;
        var events = Enumerable.Range(0, 3)
            .Select(i => new OtpEvent { Type = OtpEventType.CodeSent, CreatedAt = now.AddMinutes(-10 - i) })
            .ToList();
        var (monitor, alerts, diagnostics) = Monitor(events);
        diagnostics.GetInstanceStateAsync(Arg.Any<CancellationToken>()).Returns("authorized");

        await monitor.CheckOnceAsync(now);

        await alerts.Received(1).SendAsync("otp-unverified", Arg.Is<string>(t => t.Contains("3 WhatsApp codes")), Arg.Any<CancellationToken>());
        await alerts.DidNotReceiveWithAnyArgs().SendNowAsync(default!, default);
    }

    [Fact]
    public async Task Monitor_NoAlert_WhenSomeoneVerified_OrEventsAreOld()
    {
        var now = DateTime.UtcNow;
        var events = new List<OtpEvent>
        {
            new() { Type = OtpEventType.CodeSent, CreatedAt = now.AddMinutes(-5) },
            new() { Type = OtpEventType.CodeSent, CreatedAt = now.AddMinutes(-6) },
            new() { Type = OtpEventType.CodeSent, CreatedAt = now.AddMinutes(-7) },
            new() { Type = OtpEventType.Verified, CreatedAt = now.AddMinutes(-4) },
            new() { Type = OtpEventType.CodeSent, CreatedAt = now.AddHours(-3) },
        };
        var (monitor, alerts, diagnostics) = Monitor(events);
        diagnostics.GetInstanceStateAsync(Arg.Any<CancellationToken>()).Returns("authorized");

        await monitor.CheckOnceAsync(now);

        await alerts.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }
}
