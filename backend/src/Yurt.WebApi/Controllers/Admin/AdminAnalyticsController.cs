using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Analytics.Services;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/analytics")]
[Authorize(Policy = "AdminRoleAdmin")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analyticsService;

    public AdminAnalyticsController(AnalyticsService analyticsService)
        => _analyticsService = analyticsService;

    /// <summary>Get analytics data for the specified period and optional location.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] string period = "month",
        [FromQuery] Guid? locationId = null,
        CancellationToken ct = default)
        => Ok(await _analyticsService.GetAnalyticsAsync(period, locationId, ct));

    /// <summary>Download the period's orders as a CSV file.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportOrders(
        [FromQuery] string period = "month",
        [FromQuery] Guid? locationId = null,
        CancellationToken ct = default)
    {
        var csv = await _analyticsService.ExportOrdersCsvAsync(period, locationId, ct);
        // UTF-8 BOM so Excel shows Cyrillic names correctly.
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"altyncup-orders-{period}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
