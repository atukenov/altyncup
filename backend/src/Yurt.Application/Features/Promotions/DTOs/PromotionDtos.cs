namespace Yurt.Application.Features.Promotions.DTOs;

public record PromotionDto(
    Guid Id,
    string Title,
    string Description,
    string? ImageUrl,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public record CreatePromotionDto(
    string Title,
    string Description,
    string? ImageUrl,
    DateTime? ExpiresAt);

public record UpdatePromotionDto(
    string Title,
    string Description,
    string? ImageUrl,
    bool IsActive,
    DateTime? ExpiresAt);
