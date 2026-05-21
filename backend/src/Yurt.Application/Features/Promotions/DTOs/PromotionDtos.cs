namespace Yurt.Application.Features.Promotions.DTOs;

// Customer-facing (localized title/description)
public record PromotionDto(
    Guid Id,
    string Title,
    string Description,
    string? ImageUrl,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

// Admin-facing (all language fields)
public record AdminPromotionDto(
    Guid Id,
    string Title,
    string? TitleRu,
    string? TitleKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    string? ImageUrl,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public record CreatePromotionDto(
    string Title,
    string? TitleRu,
    string? TitleKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    string? ImageUrl,
    DateTime? ExpiresAt);

public record UpdatePromotionDto(
    string Title,
    string? TitleRu,
    string? TitleKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    string? ImageUrl,
    bool IsActive,
    DateTime? ExpiresAt);
