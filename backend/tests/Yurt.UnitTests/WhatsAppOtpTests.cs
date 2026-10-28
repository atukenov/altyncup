using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;
using Yurt.Infrastructure.Messaging;

namespace Yurt.UnitTests;

/// <summary>
/// Green API OTP delivery against a stubbed HTTP handler: what the sender posts, how it
/// reports failures, and what the admin diagnostics surface for the usual causes of
/// "the code never arrived".
/// </summary>
public class WhatsAppOtpTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses;
        public List<(string Method, string Path, string Body)> Requests { get; } = new();

        public StubHandler(Dictionary<string, (HttpStatusCode, string)> responses) => _responses = responses;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((request.Method.Method, path, body));

            // Path is /waInstance{id}/{method}/{token}
            var apiMethod = path.Split('/', StringSplitOptions.RemoveEmptyEntries)[1];
            var (status, json) = _responses.TryGetValue(apiMethod, out var r) ? r : (HttpStatusCode.NotFound, "");
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static OtpOptions Options() => new()
    {
        Enabled = true,
        GreenApiBaseUrl = "https://7103.api.greenapi.com",
        IdInstance = "7103000001",
        ApiTokenInstance = "secret-token",
    };

    private static WhatsAppOtpSender Sender(StubHandler handler, OtpOptions? options = null)
        => new(new HttpClient(handler), options ?? Options(), NullLogger<WhatsAppOtpSender>.Instance);

    private static WhatsAppOtpDiagnostics Diagnostics(StubHandler handler, OtpOptions? options = null)
        => new(new HttpClient(handler), options ?? Options()) { StatusCheckDelay = TimeSpan.Zero };

    [Fact]
    public async Task SendAsync_PostsChatIdAndCode_ToInstanceSendMessageUrl()
    {
        var handler = new StubHandler(new() { ["sendMessage"] = (HttpStatusCode.OK, """{"idMessage":"BAE5F4886F6F2D05"}""") });

        var idMessage = await Sender(handler).SendAsync("+7 701 123-45-67", "4821");

        Assert.Equal("BAE5F4886F6F2D05", idMessage);

        var req = Assert.Single(handler.Requests);
        Assert.Equal("POST", req.Method);
        Assert.Equal("/waInstance7103000001/sendMessage/secret-token", req.Path);
        Assert.Contains("\"chatId\":\"77011234567@c.us\"", req.Body);
        Assert.Contains("4821", req.Body);
    }

    [Theory]
    [InlineData("""{"existsWhatsapp":true}""", true)]
    [InlineData("""{"existsWhatsapp":false}""", false)]
    public async Task HasWhatsAppAsync_PostsNumericPhone_AndReadsExistsWhatsapp(string json, bool expected)
    {
        var handler = new StubHandler(new() { ["checkWhatsapp"] = (HttpStatusCode.OK, json) });

        var exists = await Sender(handler).HasWhatsAppAsync("+7 701 123-45-67");

        Assert.Equal(expected, exists);
        var req = Assert.Single(handler.Requests);
        Assert.Equal("/waInstance7103000001/checkWhatsapp/secret-token", req.Path);
        Assert.Contains("\"phoneNumber\":77011234567", req.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"message":"instance not authorized"}""")]
    [InlineData((HttpStatusCode)466, """{"message":"quota exceeded"}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task HasWhatsAppAsync_ProviderErrorOrOddBody_ReturnsNull(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(new() { ["checkWhatsapp"] = (status, body) });

        Assert.Null(await Sender(handler).HasWhatsAppAsync("+77011234567"));
    }

    [Fact]
    public async Task HasWhatsAppAsync_Timeout_ReturnsNull()
    {
        var sender = new WhatsAppOtpSender(new HttpClient(new HangingHandler()), Options(),
            NullLogger<WhatsAppOtpSender>.Instance) { CheckTimeout = TimeSpan.FromMilliseconds(50) };

        Assert.Null(await sender.HasWhatsAppAsync("+77011234567"));
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException();
        }
    }

    [Fact]
    public async Task SendAsync_QuotaExceeded_ThrowsWithStatusBodyAndHint()
    {
        var handler = new StubHandler(new() { ["sendMessage"] = ((HttpStatusCode)466, """{"message":"quota exceeded"}""") });

        var ex = await Assert.ThrowsAsync<OtpSendException>(() => Sender(handler).SendAsync("+77011234567", "4821"));

        Assert.Contains("466", ex.Message);
        Assert.Contains("quota exceeded", ex.Message);
        Assert.Contains("Developer plan", ex.Message);
        Assert.DoesNotContain("secret-token", ex.Message);
    }

    [Fact]
    public async Task SendAsync_MissingCredentials_ThrowsWithoutCallingGreenApi()
    {
        var handler = new StubHandler(new());
        var options = Options();
        options.ApiTokenInstance = "";

        var ex = await Assert.ThrowsAsync<OtpSendException>(() => Sender(handler, options).SendAsync("+77011234567", "4821"));

        Assert.Contains("Otp__ApiTokenInstance", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_NetworkError_IsWrappedWithoutLeakingToken()
    {
        var sender = new WhatsAppOtpSender(new HttpClient(new ThrowingHandler()), Options(), NullLogger<WhatsAppOtpSender>.Instance);

        var ex = await Assert.ThrowsAsync<OtpSendException>(() => sender.SendAsync("+77011234567", "4821"));

        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.DoesNotContain("secret-token", ex.Message);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("No such host is known.");
    }

    [Fact]
    public async Task Diagnostics_HealthySetup_AllStepsOk_AndReportsDeliveryStatus()
    {
        var handler = new StubHandler(new()
        {
            ["getStateInstance"] = (HttpStatusCode.OK, """{"stateInstance":"authorized"}"""),
            ["checkWhatsapp"] = (HttpStatusCode.OK, """{"existsWhatsapp":true}"""),
            ["sendMessage"] = (HttpStatusCode.OK, """{"idMessage":"BAE5F4886F6F2D05"}"""),
            ["getMessage"] = (HttpStatusCode.OK, """{"type":"outgoing","statusMessage":"delivered"}"""),
        });

        var report = await Diagnostics(handler).RunAsync("+77011234567", sendTestMessage: true);

        Assert.True(report.AllOk, string.Join("; ", report.ConfigWarnings.Concat(report.Steps.Select(s => s.Hint ?? ""))));
        Assert.Equal(new[] { "getStateInstance", "checkWhatsapp", "sendMessage", "getMessage" }, report.Steps.Select(s => s.Name));
        Assert.Equal("77011234567@c.us", report.ChatId);
        Assert.Contains("\"idMessage\":\"BAE5F4886F6F2D05\"", handler.Requests[3].Body);
    }

    [Fact]
    public async Task Diagnostics_NoSend_SkipsSendMessage()
    {
        var handler = new StubHandler(new()
        {
            ["getStateInstance"] = (HttpStatusCode.OK, """{"stateInstance":"authorized"}"""),
            ["checkWhatsapp"] = (HttpStatusCode.OK, """{"existsWhatsapp":true}"""),
        });

        var report = await Diagnostics(handler).RunAsync("+77011234567", sendTestMessage: false);

        Assert.Equal(new[] { "getStateInstance", "checkWhatsapp" }, report.Steps.Select(s => s.Name));
        Assert.DoesNotContain(handler.Requests, r => r.Path.Contains("sendMessage"));
    }

    [Fact]
    public async Task Diagnostics_LoggedOutInstance_NoWhatsApp_AndPendingMessage_AreFlagged()
    {
        var handler = new StubHandler(new()
        {
            ["getStateInstance"] = (HttpStatusCode.OK, """{"stateInstance":"notAuthorized"}"""),
            ["checkWhatsapp"] = (HttpStatusCode.OK, """{"existsWhatsapp":false}"""),
            ["sendMessage"] = (HttpStatusCode.OK, """{"idMessage":"X1"}"""),
            ["getMessage"] = (HttpStatusCode.OK, """{"statusMessage":"pending"}"""),
        });

        var report = await Diagnostics(handler).RunAsync("+77011234567", sendTestMessage: true);

        Assert.False(report.AllOk);
        var steps = report.Steps.ToDictionary(s => s.Name);
        Assert.Contains("QR code", steps["getStateInstance"].Hint);
        Assert.Contains("no WhatsApp", steps["checkWhatsapp"].Hint);
        Assert.True(steps["sendMessage"].Ok);
        Assert.Contains("queued", steps["getMessage"].Hint);
    }

    [Fact]
    public async Task Diagnostics_FlagsDisabledFlag_LegacyHost_AndLocalEightPrefix()
    {
        var handler = new StubHandler(new());
        var options = Options();
        options.Enabled = false;
        options.GreenApiBaseUrl = "https://api.green-api.com";
        options.IdInstance = "";

        var report = await Diagnostics(handler, options).RunAsync("87011234567", sendTestMessage: true);

        Assert.Contains(report.ConfigWarnings, w => w.Contains("Otp__Enabled=true"));
        Assert.Contains(report.ConfigWarnings, w => w.Contains("apiUrl"));
        Assert.Contains(report.ConfigWarnings, w => w.Contains("Otp__IdInstance"));
        Assert.Contains(report.ConfigWarnings, w => w.Contains("leading 8"));
        Assert.Empty(report.Steps); // no credentials, so no calls
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Diagnostics_UnauthorizedCredentials_ReturnsStatusBodyAndHint()
    {
        var handler = new StubHandler(new()
        {
            ["getStateInstance"] = (HttpStatusCode.Unauthorized, ""),
            ["checkWhatsapp"] = (HttpStatusCode.Unauthorized, ""),
        });

        var report = await Diagnostics(handler).RunAsync("+77011234567", sendTestMessage: false);

        var state = report.Steps[0];
        Assert.False(state.Ok);
        Assert.Equal(401, state.HttpStatus);
        Assert.Contains("IdInstance/ApiTokenInstance", state.Hint);
    }

    [Fact]
    public async Task GetInstanceState_ReturnsStateOrError()
    {
        var ok = new StubHandler(new() { ["getStateInstance"] = (HttpStatusCode.OK, """{"stateInstance":"notAuthorized"}""") });
        Assert.Equal("notAuthorized", await Diagnostics(ok).GetInstanceStateAsync());

        var bad = new StubHandler(new() { ["getStateInstance"] = (HttpStatusCode.Unauthorized, "") });
        Assert.StartsWith("error: HTTP 401", await Diagnostics(bad).GetInstanceStateAsync());
    }
}
