using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// WhatsApp OTP troubleshooting. Runs the Green API checks against the configured
/// instance and returns every raw response, so "the code never arrived" can be traced
/// to config, a logged-out instance, a number without WhatsApp, or a plan quota.
/// Works even when Otp:Enabled is false, so the setup can be verified before turning it on.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/otp")]
[Authorize(Policy = "SuperAdminOnly")]
public class AdminOtpController : ApiControllerBase
{
    private readonly IOtpDiagnostics _diagnostics;

    public AdminOtpController(IOtpDiagnostics diagnostics) => _diagnostics = diagnostics;

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
}
