namespace Yurt.Application.Features.Menu.DTOs;

// ── Customer-facing DTOs (names already localized by service) ────────────────

public record MenuCategoryDto(Guid Id, string Name, int SortOrder);

public record MenuItemVariantDto(Guid Id, string Label, decimal Price, int SortOrder, bool IsDefault);

public record MenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Description,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl,
    List<Guid>? LocationIds = null,
    List<MenuToppingDto>? AvailableToppings = null,
    List<MenuItemVariantDto>? Variants = null);

public record MenuToppingDto(Guid Id, string Name, decimal Price, bool IsAvailable, List<Guid> CategoryIds, string? Group = null);

// ── Admin-facing read DTOs (all language fields exposed) ─────────────────────

public record AdminMenuCategoryDto(Guid Id, string Name, string? NameRu, string? NameKk, int SortOrder);

public record AdminMenuItemVariantDto(Guid Id, string Label, string? LabelRu, string? LabelKk, decimal Price, int SortOrder, bool IsDefault);
public record CreateMenuItemVariantDto(string Label, string? LabelRu, string? LabelKk, decimal Price, int SortOrder, bool IsDefault);

public record AdminMenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? NameRu,
    string? NameKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl,
    List<Guid>? LocationIds = null,
    List<MenuToppingDto>? AvailableToppings = null,
    List<AdminMenuItemVariantDto>? Variants = null);

public record AdminMenuToppingDto(
    Guid Id,
    string Name,
    string? NameRu,
    string? NameKk,
    decimal Price,
    bool IsAvailable,
    List<Guid> CategoryIds,
    string? Group = null);

// ── Admin write DTOs ──────────────────────────────────────────────────────────

public record CreateCategoryDto(string Name, string? NameRu, string? NameKk, int SortOrder);
public record UpdateCategoryDto(string Name, string? NameRu, string? NameKk, int SortOrder);

public record CreateMenuItemDto(
    Guid CategoryId,
    string Name,
    string? NameRu,
    string? NameKk,
    string? Description,
    string? DescriptionRu,
    string? DescriptionKk,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl,
    List<Guid>? LocationIds = null,
    List<CreateMenuItemVariantDto>? Variants = null);

public record UpdateMenuItemDto(
    Guid CategoryId,
    string Name,
    string? NameRu,
    string? NameKk,
    string? Description,
    string? DescriptionRu,
    string? DescriptionKk,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl,
    List<Guid>? LocationIds = null,
    List<CreateMenuItemVariantDto>? Variants = null);

public record CreateToppingDto
{
    public string Name { get; init; } = "";
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public List<Guid> CategoryIds { get; init; } = [];
    public string? Group { get; init; }
}

public record UpdateToppingDto
{
    public string Name { get; init; } = "";
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public List<Guid> CategoryIds { get; init; } = [];
    public string? Group { get; init; }
}
