using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Favorites.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize(Policy = "CustomerOnly")]
public class FavoritesController : ApiControllerBase
{
    private readonly FavoriteService _favoriteService;
    private readonly ICurrentUserService _currentUser;

    public FavoritesController(FavoriteService favoriteService, ICurrentUserService currentUser)
    {
        _favoriteService = favoriteService;
        _currentUser = currentUser;
    }

    /// <summary>Get customer's favorites.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _favoriteService.GetFavoritesAsync(_currentUser.UserId!.Value, ct));

    /// <summary>Add a menu item to favorites.</summary>
    [HttpPost("{menuItemId:guid}")]
    public async Task<IActionResult> Add(Guid menuItemId, CancellationToken ct)
        => ToResult(await _favoriteService.AddFavoriteAsync(_currentUser.UserId!.Value, menuItemId, ct));

    /// <summary>Remove a menu item from favorites.</summary>
    [HttpDelete("{menuItemId:guid}")]
    public async Task<IActionResult> Remove(Guid menuItemId, CancellationToken ct)
        => ToResult(await _favoriteService.RemoveFavoriteAsync(_currentUser.UserId!.Value, menuItemId, ct));
}
