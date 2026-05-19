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
    public async Task<IActionResult> GetCategories(CancellationToken ct)
        => Ok(await _menuService.GetCategoriesAsync(ct));

    /// <summary>Get menu items, optionally filtered by categoryId and/or search term.</summary>
    [HttpGet]
    public async Task<IActionResult> GetItems(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await _menuService.GetItemsAsync(categoryId, search, ct));

    /// <summary>Get a single menu item by ID.</summary>
    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
        => ToResult(await _menuService.GetItemByIdAsync(id, ct));

    /// <summary>Get available toppings, optionally filtered by category.</summary>
    [HttpGet("toppings")]
    public async Task<IActionResult> GetToppings(
        [FromQuery] Guid? categoryId, CancellationToken ct)
        => Ok(await _menuService.GetToppingsAsync(categoryId, ct));
}
