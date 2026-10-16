using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/menu")]
public class MenuController : ApiControllerBase
{
    private readonly MenuService _menuService;
    private readonly RecommendationService _recommendations;
    private readonly ICurrentUserService _currentUser;

    public MenuController(
        MenuService menuService, RecommendationService recommendations, ICurrentUserService currentUser)
    {
        _menuService = menuService;
        _recommendations = recommendations;
        _currentUser = currentUser;
    }

    /// <summary>Get all menu categories, optionally filtered to those with items at a location.</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] string lang = "ru", [FromQuery] Guid? locationId = null, CancellationToken ct = default)
        => Ok(await _menuService.GetCategoriesAsync(lang, locationId, ct));

    /// <summary>Get menu items, optionally filtered by categoryId, search term, and locationId.</summary>
    [HttpGet]
    public async Task<IActionResult> GetItems(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] string lang = "ru",
        [FromQuery] Guid? locationId = null,
        CancellationToken ct = default)
        => Ok(await _menuService.GetItemsAsync(categoryId, search, lang, locationId, ct));

    /// <summary>Get a single menu item by ID.</summary>
    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetItem(
        Guid id, [FromQuery] string lang = "ru", [FromQuery] Guid? locationId = null, CancellationToken ct = default)
        => ToResult(await _menuService.GetItemByIdAsync(id, lang, locationId, ct));

    /// <summary>Get available toppings, optionally filtered by category.</summary>
    [HttpGet("toppings")]
    public async Task<IActionResult> GetToppings(
        [FromQuery] Guid? categoryId,
        [FromQuery] string lang = "ru",
        CancellationToken ct = default)
        => Ok(await _menuService.GetToppingsAsync(categoryId, lang, ct));

    /// <summary>
    /// "Frequently added together" suggestions for the given cart items. Anonymous callers get
    /// order-history and popularity based picks; signed-in customers also get their own habits.
    /// </summary>
    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations(
        [FromQuery] List<Guid>? itemIds,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string lang = "ru",
        [FromQuery] int limit = 4,
        CancellationToken ct = default)
    {
        var customerId = _currentUser.UserType == "customer" ? _currentUser.UserId : null;
        return Ok(await _recommendations.GetCartRecommendationsAsync(
            itemIds ?? [], locationId, customerId, lang, limit, ct));
    }
}
