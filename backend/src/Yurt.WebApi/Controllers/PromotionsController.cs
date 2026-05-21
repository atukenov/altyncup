using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Promotions.Services;

namespace Yurt.WebApi.Controllers;

[ApiController]
[Route("api/promotions")]
public class PromotionsController : ControllerBase
{
    private readonly PromotionService _promotionService;

    public PromotionsController(PromotionService promotionService)
        => _promotionService = promotionService;

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(
        [FromQuery] string lang = "ru", CancellationToken ct = default)
        => Ok(await _promotionService.GetActivePromotionsAsync(lang, ct));
}
