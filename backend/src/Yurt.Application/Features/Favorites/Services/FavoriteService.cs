using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Menu.DTOs;

namespace Yurt.Application.Features.Favorites.Services;

public class FavoriteService
{
    private readonly IApplicationDbContext _db;

    public FavoriteService(IApplicationDbContext db) => _db = db;

    public async Task<List<MenuItemDto>> GetFavoritesAsync(
        Guid customerId, CancellationToken ct = default)
        => await _db.Favorites
            .Where(f => f.CustomerUserId == customerId)
            .Select(f => new MenuItemDto(
                f.MenuItem.Id,
                f.MenuItem.CategoryId,
                f.MenuItem.Category.Name,
                f.MenuItem.Name,
                f.MenuItem.Description,
                f.MenuItem.Price,
                f.MenuItem.IsAvailable,
                f.MenuItem.ImageUrl))
            .ToListAsync(ct);

    public async Task<Result<bool>> AddFavoriteAsync(
        Guid customerId, Guid menuItemId, CancellationToken ct = default)
    {
        var item = await _db.MenuItems.FindAsync([menuItemId], ct);
        if (item == null) return Result<bool>.NotFound("Menu item not found.");

        var exists = await _db.Favorites
            .AnyAsync(f => f.CustomerUserId == customerId && f.MenuItemId == menuItemId, ct);
        if (exists) return Result<bool>.Success(true); // idempotent

        _db.Favorites.Add(new Domain.Entities.Favorite
        {
            CustomerUserId = customerId,
            MenuItemId = menuItemId
        });
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true, 201);
    }

    public async Task<Result<bool>> RemoveFavoriteAsync(
        Guid customerId, Guid menuItemId, CancellationToken ct = default)
    {
        var fav = await _db.Favorites
            .FirstOrDefaultAsync(f => f.CustomerUserId == customerId && f.MenuItemId == menuItemId, ct);

        if (fav == null) return Result<bool>.Success(true); // idempotent
        _db.Favorites.Remove(fav);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
