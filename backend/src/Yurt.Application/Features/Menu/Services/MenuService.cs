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

        var items = await query
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.Name)
            .ToListAsync(ct);

        var categoryIds = items.Select(i => i.CategoryId).Distinct().ToList();
        var toppingsByCategory = await GetToppingsByCategoryIdsAsync(categoryIds, ct);

        return items.Select(i => MapItemToDto(i,
            toppingsByCategory.TryGetValue(i.CategoryId, out var t) ? t : null)).ToList();
    }

    public async Task<Result<MenuItemDto>> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.MenuItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (item == null) return Result<MenuItemDto>.NotFound();

        var toppingsByCategory = await GetToppingsByCategoryIdsAsync([item.CategoryId], ct);
        toppingsByCategory.TryGetValue(item.CategoryId, out var toppings);
        return Result<MenuItemDto>.Success(MapItemToDto(item, toppings));
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

    // ── Toppings ─────────────────────────────────────────────────────────────────

    public async Task<List<MenuToppingDto>> GetToppingsAsync(
        Guid? categoryId, CancellationToken ct = default)
    {
        var query = _db.MenuToppings
            .Include(t => t.ToppingCategories)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(t =>
                t.ToppingCategories.Any(tc => tc.CategoryId == categoryId.Value));

        return await query
            .OrderBy(t => t.Group).ThenBy(t => t.Name)
            .Select(t => new MenuToppingDto(
                t.Id, t.Name, t.Price, t.IsAvailable,
                t.ToppingCategories.Select(tc => tc.CategoryId).ToList(), t.Group))
            .ToListAsync(ct);
    }

    public async Task<Result<MenuToppingDto>> CreateToppingAsync(
        CreateToppingDto dto, CancellationToken ct = default)
    {
        var topping = new MenuTopping
        {
            Name = dto.Name,
            Price = dto.Price,
            IsAvailable = dto.IsAvailable,
            Group = string.IsNullOrWhiteSpace(dto.Group) ? null : dto.Group
        };
        _db.MenuToppings.Add(topping);

        foreach (var catId in dto.CategoryIds.Distinct())
            _db.MenuToppingCategories.Add(new MenuToppingCategory
            {
                ToppingId = topping.Id,
                CategoryId = catId
            });

        await _db.SaveChangesAsync(ct);
        return Result<MenuToppingDto>.Success(
            new MenuToppingDto(topping.Id, topping.Name, topping.Price, topping.IsAvailable,
                dto.CategoryIds.Distinct().ToList(), topping.Group), 201);
    }

    public async Task<Result<MenuToppingDto>> UpdateToppingAsync(
        Guid id, UpdateToppingDto dto, CancellationToken ct = default)
    {
        var topping = await _db.MenuToppings
            .Include(t => t.ToppingCategories)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (topping == null) return Result<MenuToppingDto>.NotFound();

        topping.Name = dto.Name;
        topping.Price = dto.Price;
        topping.IsAvailable = dto.IsAvailable;
        topping.Group = string.IsNullOrWhiteSpace(dto.Group) ? null : dto.Group;
        topping.UpdatedAt = DateTime.UtcNow;

        // Replace category assignments
        foreach (var link in topping.ToppingCategories.ToList())
            _db.MenuToppingCategories.Remove(link);

        foreach (var catId in dto.CategoryIds.Distinct())
            _db.MenuToppingCategories.Add(new MenuToppingCategory
            {
                ToppingId = topping.Id,
                CategoryId = catId
            });

        await _db.SaveChangesAsync(ct);
        return Result<MenuToppingDto>.Success(
            new MenuToppingDto(topping.Id, topping.Name, topping.Price, topping.IsAvailable,
                dto.CategoryIds.Distinct().ToList(), topping.Group));
    }

    public async Task<Result<bool>> DeleteToppingAsync(Guid id, CancellationToken ct = default)
    {
        var topping = await _db.MenuToppings.FindAsync([id], ct);
        if (topping == null) return Result<bool>.NotFound();
        _db.MenuToppings.Remove(topping);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private async Task<Dictionary<Guid, List<MenuToppingDto>>> GetToppingsByCategoryIdsAsync(
        List<Guid> categoryIds, CancellationToken ct)
    {
        var links = await _db.MenuToppingCategories
            .Include(tc => tc.Topping)
            .Where(tc => categoryIds.Contains(tc.CategoryId) && tc.Topping.IsAvailable)
            .ToListAsync(ct);

        var result = new Dictionary<Guid, List<MenuToppingDto>>();
        foreach (var link in links)
        {
            if (!result.TryGetValue(link.CategoryId, out var list))
            {
                list = new List<MenuToppingDto>();
                result[link.CategoryId] = list;
            }

            var allCatIds = link.Topping.ToppingCategories?
                .Select(tc => tc.CategoryId).ToList() ?? [link.CategoryId];
            list.Add(new MenuToppingDto(
                link.Topping.Id, link.Topping.Name, link.Topping.Price,
                link.Topping.IsAvailable, allCatIds, link.Topping.Group));
        }
        return result;
    }

    private static MenuItemDto MapItemToDto(MenuItem i, List<MenuToppingDto>? toppings = null)
        => new(i.Id, i.CategoryId, i.Category?.Name ?? "", i.Name, i.Description,
               i.Price, i.IsAvailable, i.ImageUrl, toppings);
}
