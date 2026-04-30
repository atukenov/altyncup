using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Menu.Services;

public class MenuService
{
    private readonly IApplicationDbContext _db;

    public MenuService(IApplicationDbContext db) => _db = db;

    public async Task<List<MenuCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
        => await _db.MenuCategories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new MenuCategoryDto(c.Id, c.Name, c.SortOrder))
            .ToListAsync(ct);

    public async Task<List<MenuItemDto>> GetItemsAsync(
        Guid? categoryId, string? search, CancellationToken ct = default)
    {
        var query = _db.MenuItems
            .Include(i => i.Category)
            .Where(i => i.IsAvailable)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(i => i.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i =>
                i.Name.Contains(search) || i.Description.Contains(search));

        return await query
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.Name)
            .Select(i => MapItemToDto(i))
            .ToListAsync(ct);
    }

    public async Task<Result<MenuItemDto>> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.MenuItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (item == null) return Result<MenuItemDto>.NotFound();
        return Result<MenuItemDto>.Success(MapItemToDto(item));
    }

    public async Task<Result<MenuCategoryDto>> CreateCategoryAsync(
        CreateCategoryDto dto, CancellationToken ct = default)
    {
        var cat = new MenuCategory { Name = dto.Name, SortOrder = dto.SortOrder };
        _db.MenuCategories.Add(cat);
        await _db.SaveChangesAsync(ct);
        return Result<MenuCategoryDto>.Success(new MenuCategoryDto(cat.Id, cat.Name, cat.SortOrder), 201);
    }

    public async Task<Result<MenuCategoryDto>> UpdateCategoryAsync(
        Guid id, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        var cat = await _db.MenuCategories.FindAsync([id], ct);
        if (cat == null) return Result<MenuCategoryDto>.NotFound();

        cat.Name = dto.Name;
        cat.SortOrder = dto.SortOrder;
        cat.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<MenuCategoryDto>.Success(new MenuCategoryDto(cat.Id, cat.Name, cat.SortOrder));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var cat = await _db.MenuCategories.FindAsync([id], ct);
        if (cat == null) return Result<bool>.NotFound();
        _db.MenuCategories.Remove(cat);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<MenuItemDto>> CreateItemAsync(
        CreateMenuItemDto dto, CancellationToken ct = default)
    {
        var cat = await _db.MenuCategories.FindAsync([dto.CategoryId], ct);
        if (cat == null)
            return Result<MenuItemDto>.Failure("Category not found.", 400);

        var item = new MenuItem
        {
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            IsAvailable = dto.IsAvailable,
            ImageUrl = dto.ImageUrl
        };
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync(ct);

        item.Category = cat;
        return Result<MenuItemDto>.Success(MapItemToDto(item), 201);
    }

    public async Task<Result<MenuItemDto>> UpdateItemAsync(
        Guid id, UpdateMenuItemDto dto, CancellationToken ct = default)
    {
        var item = await _db.MenuItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (item == null) return Result<MenuItemDto>.NotFound();

        item.CategoryId = dto.CategoryId;
        item.Name = dto.Name;
        item.Description = dto.Description;
        item.Price = dto.Price;
        item.IsAvailable = dto.IsAvailable;
        item.ImageUrl = dto.ImageUrl;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<MenuItemDto>.Success(MapItemToDto(item));
    }

    public async Task<Result<bool>> DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.MenuItems.FindAsync([id], ct);
        if (item == null) return Result<bool>.NotFound();
        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private static MenuItemDto MapItemToDto(MenuItem i)
        => new(i.Id, i.CategoryId, i.Category?.Name ?? "", i.Name, i.Description,
               i.Price, i.IsAvailable, i.ImageUrl);
}
