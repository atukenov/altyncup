using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Yurt.Infrastructure.Messaging;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

/// <summary>
/// Receives Green API instance webhooks (set this URL as "webhookUrl" in the instance
/// settings). Secured by Otp:WebhookToken, which Green API sends back in the
/// Authorization header when it is set as the instance's "webhookUrlToken".
/// See docs/whatsapp-otp-troubleshooting.md.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/green-api")]
public class GreenApiWebhookController : ApiControllerBase
{
    private readonly GreenApiWebhookHandler _handler;
    private readonly ILogger<GreenApiWebhookController> _logger;

    public GreenApiWebhookController(GreenApiWebhookHandler handler, ILogger<GreenApiWebhookController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Receive([FromQuery] string? token, CancellationToken ct)
    {
        if (!_handler.IsAuthorized(Request.Headers.Authorization.ToString(), token))
        {
            _logger.LogWarning("Rejected Green API webhook: missing or wrong token (is Otp__WebhookToken set?)");
            return Unauthorized();
        }

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        // Always 200 once authorized: Green API retries anything else, and a bad payload won't get better.
        await _handler.HandleAsync(rawBody, ct);
        return Ok();
    }
}
