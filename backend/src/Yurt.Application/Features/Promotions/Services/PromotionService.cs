using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Promotions.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Promotions.Services;

public class PromotionService
{
    private readonly IApplicationDbContext _db;

    public PromotionService(IApplicationDbContext db) => _db = db;

    public async Task<List<PromotionDto>> GetActivePromotionsAsync(CancellationToken ct = default)
        => await _db.Promotions
            .Where(p => p.IsActive && (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync(ct);

    public async Task<List<PromotionDto>> GetAllPromotionsAsync(CancellationToken ct = default)
        => await _db.Promotions
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync(ct);

    public async Task<Result<PromotionDto>> CreateAsync(CreatePromotionDto dto, CancellationToken ct = default)
    {
        var promo = new Promotion
        {
            Title = dto.Title,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            ExpiresAt = dto.ExpiresAt
        };
        _db.Promotions.Add(promo);
        await _db.SaveChangesAsync(ct);
        return Result<PromotionDto>.Success(MapToDto(promo), 201);
    }

    public async Task<Result<PromotionDto>> UpdateAsync(Guid id, UpdatePromotionDto dto, CancellationToken ct = default)
    {
        var promo = await _db.Promotions.FindAsync([id], ct);
        if (promo == null) return Result<PromotionDto>.NotFound();

        promo.Title = dto.Title;
        promo.Description = dto.Description;
        promo.ImageUrl = dto.ImageUrl;
        promo.IsActive = dto.IsActive;
        promo.ExpiresAt = dto.ExpiresAt;

        await _db.SaveChangesAsync(ct);
        return Result<PromotionDto>.Success(MapToDto(promo));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var promo = await _db.Promotions.FindAsync([id], ct);
        if (promo == null) return Result<bool>.NotFound();
        _db.Promotions.Remove(promo);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private static PromotionDto MapToDto(Promotion p)
        => new(p.Id, p.Title, p.Description, p.ImageUrl, p.IsActive, p.ExpiresAt, p.CreatedAt);
}
