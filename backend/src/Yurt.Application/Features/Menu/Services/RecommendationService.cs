using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Menu.Services;

/// <summary>
/// "Frequently added together" suggestions for the cart. Ranks menu items by how often
/// they appeared in the same completed orders as what is already in the cart, boosts
/// items the customer has ordered before, and fills any remaining slots with the
/// location's recent best sellers.
/// </summary>
public class RecommendationService
{
    internal const int MaxLimit = 4;
    internal const int MinPairSupport = 3;          // pairs seen in fewer orders are noise
    internal static readonly TimeSpan PairWindow = TimeSpan.FromDays(90);
    internal static readonly TimeSpan PopularWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan StatsTtl = TimeSpan.FromMinutes(10);
    private const double PersonalBoostPerOrder = 0.25;
    private const double PersonalBoostCap = 0.75;

    private readonly IApplicationDbContext _db;
    private readonly IMenuCacheService _menuCache;

    public RecommendationService(IApplicationDbContext db, IMenuCacheService menuCache)
    {
        _db = db;
        _menuCache = menuCache;
    }

    public async Task<List<MenuItemDto>> GetCartRecommendationsAsync(
        IReadOnlyCollection<Guid> cartItemIds, Guid? locationId, Guid? customerId,
        string lang = "ru", int limit = MaxLimit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, MaxLimit);
        var inCart = cartItemIds.Distinct().ToList();

        var candidatesQuery = _db.MenuItems
            .Include(i => i.Category)
            .Include(i => i.MenuItemLocations)
            .Include(i => i.Variants)
            .Where(i => i.IsAvailable && !inCart.Contains(i.Id));
        if (locationId.HasValue)
            candidatesQuery = candidatesQuery.Where(i =>
                !i.MenuItemLocations.Any() ||
                i.MenuItemLocations.Any(l => l.LocationId == locationId.Value));
        var candidates = await candidatesQuery.ToListAsync(ct);
        if (candidates.Count == 0) return [];

        var stats = await GetStatsAsync(locationId, ct);
        var personal = customerId.HasValue
            ? await GetPersonalCountsAsync(customerId.Value, ct)
            : new Dictionary<Guid, int>();

        double Score(Guid id)
        {
            // Sum of P(candidate | cart item) over the cart, counting only well-supported pairs.
            double score = 0;
            foreach (var cartId in inCart)
            {
                if (!stats.Pairs.TryGetValue(cartId, out var partners)) continue;
                if (!partners.TryGetValue(id, out var together) || together < MinPairSupport) continue;
                score += (double)together / stats.OrdersWith[cartId];
            }
            if (personal.TryGetValue(id, out var mine))
                score += Math.Min(PersonalBoostCap, mine * PersonalBoostPerOrder);
            return score;
        }

        int Popularity(Guid id) => stats.Popular.TryGetValue(id, out var n) ? n : 0;

        var scored = candidates
            .Select(i => (Item: i, Score: Score(i.Id), Popularity: Popularity(i.Id)))
            .ToList();

        var picked = scored
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Popularity)
            .ThenBy(s => s.Item.SortOrder)
            .Take(limit)
            .Select(s => s.Item)
            .ToList();

        if (picked.Count < limit)
        {
            var pickedIds = picked.Select(i => i.Id).ToHashSet();
            picked.AddRange(scored
                .Where(s => s.Popularity > 0 && !pickedIds.Contains(s.Item.Id))
                .OrderByDescending(s => s.Popularity)
                .ThenBy(s => s.Item.SortOrder)
                .Take(limit - picked.Count)
                .Select(s => s.Item));
        }

        return picked.Select(i => MenuService.MapItemToDto(i, lang)).ToList();
    }

    /// <summary>Order-history statistics for a location; cached because every cart view asks.</summary>
    private async Task<RecommendationStats> GetStatsAsync(Guid? locationId, CancellationToken ct)
    {
        var cacheKey = $"recs:stats:{locationId}";
        var cached = await _menuCache.GetAsync<RecommendationStats>(cacheKey);
        if (cached is not null) return cached;

        var now = DateTime.UtcNow;
        var pairSince = now - PairWindow;
        var popularSince = now - PopularWindow;

        var query = _db.OrderItems
            .Where(oi => oi.Order.Status == OrderStatus.Completed && oi.Order.CreatedAt >= pairSince);
        if (locationId.HasValue)
            query = query.Where(oi => oi.Order.LocationId == locationId.Value);

        var rows = await query
            .Select(oi => new { oi.OrderId, oi.MenuItemId, oi.Quantity, oi.Order.CreatedAt })
            .ToListAsync(ct);

        var stats = new RecommendationStats();
        foreach (var r in rows.Where(r => r.CreatedAt >= popularSince))
            stats.Popular[r.MenuItemId] = stats.Popular.GetValueOrDefault(r.MenuItemId) + r.Quantity;

        foreach (var order in rows.GroupBy(r => r.OrderId))
        {
            var ids = order.Select(r => r.MenuItemId).Distinct().ToList();
            foreach (var a in ids)
            {
                stats.OrdersWith[a] = stats.OrdersWith.GetValueOrDefault(a) + 1;
                if (!stats.Pairs.TryGetValue(a, out var partners))
                    stats.Pairs[a] = partners = new Dictionary<Guid, int>();
                foreach (var b in ids)
                    if (a != b) partners[b] = partners.GetValueOrDefault(b) + 1;
            }
        }

        await _menuCache.SetAsync(cacheKey, stats, StatsTtl);
        return stats;
    }

    /// <summary>How many of the customer's completed orders contained each item.</summary>
    private async Task<Dictionary<Guid, int>> GetPersonalCountsAsync(Guid customerId, CancellationToken ct)
    {
        var rows = await _db.OrderItems
            .Where(oi => oi.Order.CustomerUserId == customerId && oi.Order.Status == OrderStatus.Completed)
            .Select(oi => new { oi.OrderId, oi.MenuItemId })
            .ToListAsync(ct);

        return rows.Distinct()
            .GroupBy(r => r.MenuItemId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private sealed class RecommendationStats
    {
        public Dictionary<Guid, Dictionary<Guid, int>> Pairs { get; } = new();
        public Dictionary<Guid, int> OrdersWith { get; } = new();
        public Dictionary<Guid, int> Popular { get; } = new();
    }
}
