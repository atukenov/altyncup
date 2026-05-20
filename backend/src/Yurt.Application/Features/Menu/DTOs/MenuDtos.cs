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

public record CreateToppingDto
{
    public string Name { get; init; } = "";
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public List<Guid> CategoryIds { get; init; } = [];
    public string? Group { get; init; }
}

public record UpdateToppingDto
{
    public string Name { get; init; } = "";
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public List<Guid> CategoryIds { get; init; } = [];
    public string? Group { get; init; }
}

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
