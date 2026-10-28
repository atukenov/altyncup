using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Messaging;
using Yurt.Infrastructure.Services;

namespace Yurt.UnitTests;

/// <summary>
/// Green API webhooks: token check, delivery status on the pending code row (never going
/// backwards), DeliveryFailed events, and instant alerts on instance state changes that
/// the poll then doesn't repeat.
/// </summary>
public class GreenApiWebhookTests
{
    private sealed class Harness
    {
        public IApplicationDbContext Db { get; } = Substitute.For<IApplicationDbContext>();
        public IAdminAlertSender Alerts { get; } = Substitute.For<IAdminAlertSender>();
        public List<OtpEvent> Events { get; }
        public WhatsAppStateTracker State { get; }
        public GreenApiWebhookHandler Handler { get; }

        public Harness(List<PhoneVerification>? pending = null, List<OtpEvent>? events = null, string token = "s3cret")
        {
            Events = events ?? new();
            var verifications = (pending ?? new()).BuildMockDbSet();
            Db.PhoneVerifications.Returns(verifications);
            var set = Events.BuildMockDbSet();
            set.When(s => s.Add(Arg.Any<OtpEvent>())).Do(c => Events.Add(c.Arg<OtpEvent>()));
            Db.OtpEvents.Returns(set);
            State = new WhatsAppStateTracker(Alerts);
            Handler = new GreenApiWebhookHandler(Db,
                new OtpOptions { IdInstance = "7103000001", WebhookToken = token }, State,
                NullLogger<GreenApiWebhookHandler>.Instance);
        }
    }

    private static string Status(string idMessage, string status, long idInstance = 7103000001) => $$"""
        {"typeWebhook":"outgoingMessageStatus","instanceData":{"idInstance":{{idInstance}},"wid":"77000000000@c.us","typeInstance":"whatsapp"},
         "timestamp":1761670000,"idMessage":"{{idMessage}}","status":"{{status}}","chatId":"77011234567@c.us","sendByApi":true}
        """;

    private static string State(string state) => $$"""
        {"typeWebhook":"stateInstanceChanged","instanceData":{"idInstance":7103000001},"timestamp":1761670000,"stateInstance":"{{state}}"}
        """;

    private static PhoneVerification Row(string? messageId = "MSG1", string? status = null) => new()
    {
        MobileNumber = "+77011234567", Purpose = VerificationPurpose.Registration,
        ProviderMessageId = messageId, DeliveryStatus = status,
    };

    [Theory]
    [InlineData("Bearer s3cret", null, true)]
    [InlineData("s3cret", null, true)]
    [InlineData(null, "s3cret", true)]
    [InlineData("Bearer wrong", null, false)]
    [InlineData(null, null, false)]
    public void IsAuthorized_ChecksHeaderOrQueryToken(string? header, string? query, bool expected)
        => Assert.Equal(expected, new Harness().Handler.IsAuthorized(header, query));

    [Fact]
    public void IsAuthorized_RejectsEverything_WhenNoTokenConfigured()
    {
        var h = new Harness(token: "");
        Assert.False(h.Handler.IsAuthorized("Bearer ", ""));
        Assert.False(h.Handler.IsAuthorized("", null));
    }

    [Fact]
    public async Task MessageStatus_UpdatesMatchingRow()
    {
        var row = Row();
        var h = new Harness(new() { row });

        await h.Handler.HandleAsync(Status("MSG1", "delivered"));

        Assert.Equal("delivered", row.DeliveryStatus);
        Assert.NotNull(row.DeliveryStatusAt);
        Assert.Empty(h.Events);
    }

    [Fact]
    public async Task MessageStatus_NeverGoesBackwards()
    {
        var row = Row(status: "read");
        var h = new Harness(new() { row });

        await h.Handler.HandleAsync(Status("MSG1", "sent"));
        await h.Handler.HandleAsync(Status("MSG1", "delivered"));

        Assert.Equal("read", row.DeliveryStatus);
    }

    [Fact]
    public async Task MessageStatus_NoAccount_MarksRowAndRecordsDeliveryFailedOnce()
    {
        var row = Row(status: "sent");
        var h = new Harness(new() { row });

        await h.Handler.HandleAsync(Status("MSG1", "noAccount"));
        await h.Handler.HandleAsync(Status("MSG1", "noAccount"));

        Assert.Equal("noAccount", row.DeliveryStatus);
        var e = Assert.Single(h.Events);
        Assert.Equal(OtpEventType.DeliveryFailed, e.Type);
        Assert.Equal("+77011234567", e.MobileNumber);
        Assert.Equal("MSG1", e.ProviderMessageId);
        Assert.Contains("no WhatsApp", e.Detail);
    }

    [Fact]
    public async Task MessageStatus_Failed_ForReplacedCode_StillRecordedFromSendEvent()
    {
        var sent = new OtpEvent
        {
            MobileNumber = "+77019999999", Purpose = VerificationPurpose.PinReset,
            Type = OtpEventType.CodeSent, ProviderMessageId = "OLD",
        };
        var h = new Harness(new() { Row("NEW") }, new() { sent });

        await h.Handler.HandleAsync(Status("OLD", "failed"));

        var e = h.Events.Single(x => x.Type == OtpEventType.DeliveryFailed);
        Assert.Equal("+77019999999", e.MobileNumber);
        Assert.Equal(VerificationPurpose.PinReset, e.Purpose);
    }

    [Fact]
    public async Task MessageStatus_FromOtherInstance_IsIgnored()
    {
        var row = Row();
        var h = new Harness(new() { row });

        await h.Handler.HandleAsync(Status("MSG1", "delivered", idInstance: 1100000000));

        Assert.Null(row.DeliveryStatus);
    }

    [Fact]
    public async Task MalformedOrUnknownWebhook_IsIgnored()
    {
        var h = new Harness();

        await h.Handler.HandleAsync("not json");
        await h.Handler.HandleAsync("""{"typeWebhook":"incomingMessageReceived"}""");

        await h.Db.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await h.Alerts.DidNotReceiveWithAnyArgs().SendNowAsync(default!, default);
    }

    [Fact]
    public async Task StateChanged_AlertsImmediately_AndAgainOnRecovery()
    {
        var h = new Harness();

        await h.Handler.HandleAsync(State("notAuthorized"));
        await h.Handler.HandleAsync(State("notAuthorized"));
        await h.Handler.HandleAsync(State("authorized"));

        await h.Alerts.Received(1).SendNowAsync(
            Arg.Is<string>(t => t.Contains("DOWN") && t.Contains("notAuthorized") && t.Contains("QR")),
            Arg.Any<CancellationToken>());
        await h.Alerts.Received(1).SendNowAsync(Arg.Is<string>(t => t.Contains("back")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StateTracker_PollAfterWebhook_DoesNotAlertTwice()
    {
        var h = new Harness();

        await h.Handler.HandleAsync(State("blocked"));
        var changed = await h.State.ReportAsync("blocked", fromWebhook: false);

        Assert.False(changed);
        await h.Alerts.Received(1).SendNowAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
