namespace Yurt.Application.Features.Menu.DTOs;

public record MenuCategoryDto(Guid Id, string Name, int SortOrder);
public record MenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Description,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl,
    List<MenuToppingDto>? AvailableToppings = null);

public record MenuToppingDto(Guid Id, string Name, decimal Price, bool IsAvailable, List<Guid> CategoryIds, string? Group = null);
public record CreateToppingDto(string Name, decimal Price, bool IsAvailable, List<Guid> CategoryIds, string? Group = null);
public record UpdateToppingDto(string Name, decimal Price, bool IsAvailable, List<Guid> CategoryIds, string? Group = null);

public record CreateCategoryDto(string Name, int SortOrder);
public record UpdateCategoryDto(string Name, int SortOrder);

public record CreateMenuItemDto(
    Guid CategoryId,
    string Name,
    string Description,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl);

public record UpdateMenuItemDto(
    Guid CategoryId,
    string Name,
    string Description,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl);
