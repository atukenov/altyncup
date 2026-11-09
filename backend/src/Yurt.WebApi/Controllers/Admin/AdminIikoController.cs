using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Read-only reference lookups against iiko, backing the admin menu-mapping and
/// location-routing pickers, plus a one-time webhook registration action. No general
/// write endpoints here — the "paid in app" payment type stays an appsettings-configured
/// value (IikoOptions.PaymentTypeId), consistent with every other global iiko setting;
/// this controller just helps the operator find the right ID to put there.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/iiko")]
[Authorize(Policy = "SuperAdminOnly")]
public class AdminIikoController : ApiControllerBase
{
    private readonly IIikoApiClient _iiko;
    private readonly IikoOptions _options;

    public AdminIikoController(IIikoApiClient iiko, IikoOptions options)
    {
        _iiko = iiko;
        _options = options;
    }

    /// <summary>Whether the iiko integration is switched on; lets the admin UI hide iiko-only hints.</summary>
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { enabled = _options.Enabled });

    [HttpGet("nomenclature")]
    public Task<IActionResult> GetNomenclature(CancellationToken ct)
        => LookupAsync(() => _iiko.GetNomenclatureAsync(ct));

    [HttpGet("payment-types")]
    public Task<IActionResult> GetPaymentTypes(CancellationToken ct)
        => LookupAsync(() => _iiko.GetPaymentTypesAsync(ct));

    [HttpGet("terminal-groups")]
    public Task<IActionResult> GetTerminalGroups(CancellationToken ct)
        => LookupAsync(() => _iiko.GetTerminalGroupsAsync(ct));

    /// <summary>
    /// Runs a reference lookup and turns the two common failures into a readable
    /// message for the admin picker, instead of a bare 500 the UI can only show as
    /// an empty list: the integration being switched off, and iiko rejecting the call
    /// (bad credentials, missing API scope, wrong organization id, iiko down).
    /// </summary>
    private async Task<IActionResult> LookupAsync<T>(Func<Task<T>> lookup)
    {
        if (!_options.Enabled)
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "iiko integration is turned off. Set Iiko:Enabled=true and the iiko credentials on the server.",
            });

        try
        {
            return Ok(await lookup());
        }
        catch (IikoApiException ex)
        {
            return StatusCode(502, new ProblemDetails
            {
                Status = 502,
                Title = "iiko rejected the request.",
                Detail = ex.Message,
            });
        }
        catch (TaskCanceledException ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new ProblemDetails
            {
                Status = 504,
                Title = "iiko did not answer in time.",
                Detail = ex.Message,
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new ProblemDetails
            {
                Status = 502,
                Title = "Could not reach iiko.",
                Detail = ex.Message,
            });
        }
    }

    /// <summary>
    /// Registers this backend's public URL with iiko as the delivery-order webhook
    /// target. Run once by an operator after deploying with a configured
    /// Iiko:WebhookAuthToken — not called automatically, since it needs the instance's
    /// real public URL and shouldn't silently re-run on every boot.
    /// </summary>
    [HttpPost("register-webhook")]
    public async Task<IActionResult> RegisterWebhook(
        [FromBody] RegisterWebhookRequest dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookAuthToken))
            return ValidationError("Set Iiko:WebhookAuthToken in configuration before registering the webhook.");
        if (string.IsNullOrWhiteSpace(dto.WebhookUrl))
            return ValidationError("WebhookUrl is required.");

        await _iiko.RegisterWebhookAsync(dto.WebhookUrl, _options.WebhookAuthToken, ct);
        return Ok(new { message = "Webhook registered." });
    }

    public record RegisterWebhookRequest(string WebhookUrl);
}
