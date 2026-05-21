using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Menu.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[Route("api/menu")]
public class MenuController : ApiControllerBase
{
    private readonly MenuService _menuService;

    public MenuController(MenuService menuService) => _menuService = menuService;

    /// <summary>Get all menu categories.</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] string lang = "ru", CancellationToken ct = default)
        => Ok(await _menuService.GetCategoriesAsync(lang, ct));

    /// <summary>Get menu items, optionally filtered by categoryId and/or search term.</summary>
    [HttpGet]
    public async Task<IActionResult> GetItems(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] string lang = "ru",
        CancellationToken ct = default)
        => Ok(await _menuService.GetItemsAsync(categoryId, search, lang, ct));

    /// <summary>Get a single menu item by ID.</summary>
    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetItem(
        Guid id, [FromQuery] string lang = "ru", CancellationToken ct = default)
        => ToResult(await _menuService.GetItemByIdAsync(id, lang, ct));

    /// <summary>Get available toppings, optionally filtered by category.</summary>
    [HttpGet("toppings")]
    public async Task<IActionResult> GetToppings(
        [FromQuery] Guid? categoryId,
        [FromQuery] string lang = "ru",
        CancellationToken ct = default)
        => Ok(await _menuService.GetToppingsAsync(categoryId, lang, ct));
}
