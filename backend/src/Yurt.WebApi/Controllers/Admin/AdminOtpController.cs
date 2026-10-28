using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth.Services;
using Yurt.Domain.Enums;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// WhatsApp OTP troubleshooting and audit:
///  - <c>summary</c>: instance state, counts per outcome, and people who requested a code
///    but never got through (the "tried to register but couldn't" list);
///  - <c>events</c>: the raw OTP history, filterable;
///  - <c>codes</c>: pending code requests with the current code in plain text, and
///    <c>codes/{id}/resend</c> to send a fresh one;
///  - <c>test</c>: live Green API checks with every raw response;
///  - <c>alerts/test</c>: sends a test message to the admin Telegram chat.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/otp")]
[Authorize(Policy = "SuperAdminOnly")]
public class AdminOtpController : ApiControllerBase
{
    private readonly IOtpDiagnostics _diagnostics;
    private readonly IApplicationDbContext _db;
    private readonly IAdminAlertSender _alerts;
    private readonly AuthService _auth;
    private readonly IAuditLogService _audit;

    public AdminOtpController(
        IOtpDiagnostics diagnostics, IApplicationDbContext db, IAdminAlertSender alerts,
        AuthService auth, IAuditLogService audit)
    {
        _diagnostics = diagnostics;
        _db = db;
        _alerts = alerts;
        _auth = auth;
        _audit = audit;
    }

    public record OtpTestRequest(string MobileNumber, bool SendTestMessage = true);

    /// <summary>
    /// Check the Green API setup for a phone number and, unless <c>sendTestMessage</c>
    /// is false, send it a test message and report WhatsApp's delivery status.
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] OtpTestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.MobileNumber))
            return BadRequest(new ProblemDetails { Status = 400, Title = "mobileNumber is required." });

        return Ok(await _diagnostics.RunAsync(request.MobileNumber, request.SendTestMessage, ct));
    }

    /// <summary>Send a test alert to the configured Telegram chat.</summary>
    [HttpPost("alerts/test")]
    public async Task<IActionResult> TestAlert(CancellationToken ct)
    {
        var sent = await _alerts.SendNowAsync("🔔 Test alert from the admin panel. Alerts are working.", ct);
        return sent
            ? Ok(new { sent })
            : StatusCode(502, new ProblemDetails
            {
                Status = 502,
                Title = "Alert not delivered. Check Alerts__TelegramBotToken / Alerts__TelegramChatId and the server log.",
            });
    }

    /// <summary>
    /// Outcome counts for the last <paramref name="hours"/> hours, the WhatsApp instance
    /// state, and every number whose latest code was never verified.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int hours = 24, CancellationToken ct = default)
    {
        hours = Math.Clamp(hours, 1, 24 * 30);
        var since = DateTime.UtcNow.AddHours(-hours);

        var events = await _db.OtpEvents
            .Where(e => e.CreatedAt >= since)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new { e.MobileNumber, e.Purpose, e.Type, e.Detail, e.CreatedAt })
            .ToListAsync(ct);

        var counts = Enum.GetValues<OtpEventType>()
            .ToDictionary(t => t.ToString(), t => events.Count(e => e.Type == t));

        // One row per (number, purpose) whose last event is anything but a successful verification.
        var notVerified = events
            .GroupBy(e => new { e.MobileNumber, e.Purpose })
            .Where(g => g.Last().Type != OtpEventType.Verified)
            .Select(g => new
            {
                g.Key.MobileNumber,
                Purpose = g.Key.Purpose.ToString(),
                LastEvent = g.Last().Type.ToString(),
                LastAt = g.Last().CreatedAt,
                CodesRequested = g.Count(e => e.Type is OtpEventType.CodeSent
                    or OtpEventType.SendFailed or OtpEventType.DeliveryDisabled),
                SendFailures = g.Count(e => e.Type == OtpEventType.SendFailed),
                WrongCodes = g.Count(e => e.Type == OtpEventType.WrongCode),
                LastError = g.LastOrDefault(e => e.Detail != null)?.Detail,
            })
            .OrderByDescending(x => x.LastAt)
            .ToList();

        var state = await _diagnostics.GetInstanceStateAsync(ct);

        return Ok(new { hours, whatsappState = state, counts, notVerified });
    }

    /// <summary>Raw OTP history, newest first.</summary>
    [HttpGet("events")]
    public async Task<IActionResult> Events(
        [FromQuery] string? mobileNumber,
        [FromQuery] OtpEventType? type,
        [FromQuery] VerificationPurpose? purpose,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var query = _db.OtpEvents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(mobileNumber))
        {
            var m = mobileNumber.Trim().Replace(" ", "").Replace("-", "");
            query = query.Where(e => e.MobileNumber.Contains(m));
        }
        if (type.HasValue) query = query.Where(e => e.Type == type.Value);
        if (purpose.HasValue) query = query.Where(e => e.Purpose == purpose.Value);
        if (from.HasValue) query = query.Where(e => e.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(e => e.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.MobileNumber,
                Purpose = e.Purpose.ToString(),
                Type = e.Type.ToString(),
                e.Detail,
                e.ProviderMessageId,
                e.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>
    /// Every number with a code request still pending (not yet confirmed), newest first,
    /// with the current code in plain text. Rows from before codes were kept in plain
    /// text have a null code until they are resent.
    /// </summary>
    [HttpGet("codes")]
    public async Task<IActionResult> Codes([FromQuery] string? mobileNumber, CancellationToken ct = default)
    {
        var query = _db.PhoneVerifications.AsQueryable();
        if (!string.IsNullOrWhiteSpace(mobileNumber))
        {
            var m = mobileNumber.Trim().Replace(" ", "").Replace("-", "");
            query = query.Where(v => v.MobileNumber.Contains(m));
        }

        var now = DateTime.UtcNow;
        var items = await query
            .OrderByDescending(v => v.LastSentAt)
            .Take(200)
            .Select(v => new
            {
                v.Id,
                v.MobileNumber,
                Purpose = v.Purpose.ToString(),
                v.Code,
                v.Attempts,
                v.LastSentAt,
                v.ExpiresAt,
                Expired = v.ExpiresAt <= now,
                v.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { items });
    }

    /// <summary>Generate a fresh code for a pending request and send it over WhatsApp again.</summary>
    [HttpPost("codes/{id:guid}/resend")]
    public async Task<IActionResult> Resend(Guid id, CancellationToken ct)
    {
        var result = await _auth.ResendCodeAsync(id, ct);
        if (result.Succeeded)
            await _audit.LogAsync("OtpResent", "PhoneVerification", id.ToString(),
                result.Data!.MobileNumber, ct);
        return ToResult(result);
    }
}
