using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Analytics.DTOs;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Analytics.Services;

public class AnalyticsService
{
    private readonly IApplicationDbContext _db;

    public AnalyticsService(IApplicationDbContext db) => _db = db;

    public async Task<AnalyticsResponse> GetAnalyticsAsync(
        string period, CancellationToken ct = default)
    {
        var fromDate = GetFromDate(period);

        var orders = _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items)
            .Where(o => o.CreatedAt >= fromDate)
            .AsQueryable();

        var allOrders = await orders.ToListAsync(ct);

        var completedOrders = allOrders
            .Where(o => o.Status == OrderStatus.Completed).ToList();

        // ── KPIs ─────────────────────────────────────────────────────────────
        var totalRevenue = completedOrders.Sum(o => o.Total);
        var totalOrders = allOrders.Count;
        var avgOrderValue = completedOrders.Count > 0
            ? completedOrders.Average(o => o.Total) : 0;
        var completedCount = completedOrders.Count;
        var declinedCount = allOrders.Count(o => o.Status == OrderStatus.Declined);

        var prepTimes = completedOrders
            .Where(o => o.AcceptedAt.HasValue && o.CompletedAt.HasValue)
            .Select(o => (o.CompletedAt!.Value - o.AcceptedAt!.Value).TotalMinutes)
            .ToList();
        var avgPrepTime = prepTimes.Count > 0 ? prepTimes.Average() : 0;

        var uniqueCustomers = allOrders
            .Select(o => o.CustomerUserId).Distinct().Count();

        var kpis = new KpiSummary(
            totalRevenue, totalOrders, avgOrderValue,
            completedCount, declinedCount,
            Math.Round(avgPrepTime, 1), uniqueCustomers);

        // ── Revenue over time ────────────────────────────────────────────────
        var revenueOverTime = GetRevenueOverTime(completedOrders, period, fromDate);

        // ── Top items ────────────────────────────────────────────────────────
        var topItems = completedOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.MenuItemName)
            .Select(g => new TopItemDto(
                g.Key,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.LineTotal)))
            .OrderByDescending(t => t.QuantitySold)
            .Take(10)
            .ToList();

        // ── Location performance ─────────────────────────────────────────────
        var locationPerf = completedOrders
            .GroupBy(o => o.Location?.Name ?? "Unknown")
            .Select(g => new LocationPerformanceDto(
                g.Key,
                g.Sum(o => o.Total),
                g.Count()))
            .OrderByDescending(l => l.Revenue)
            .ToList();

        // ── Hourly distribution ──────────────────────────────────────────────
        var hourly = allOrders
            .Where(o => o.Status != OrderStatus.Declined)
            .GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new HourlyDistributionDto(g.Key, g.Count()))
            .ToList();

        // Fill missing hours with 0
        var allHours = Enumerable.Range(0, 24)
            .Select(h => hourly.FirstOrDefault(x => x.Hour == h)
                         ?? new HourlyDistributionDto(h, 0))
            .ToList();

        // ── Payment breakdown ────────────────────────────────────────────────
        var paymentBreakdown = completedOrders
            .Where(o => o.PaymentMethod.HasValue)
            .GroupBy(o => o.PaymentMethod!.Value.ToString())
            .Select(g => new PaymentBreakdownDto(
                g.Key,
                g.Count(),
                g.Sum(o => o.Total)))
            .OrderByDescending(p => p.Count)
            .ToList();

        // Add unpaid/unknown
        var unpaidCount = completedOrders.Count(o => !o.PaymentMethod.HasValue);
        if (unpaidCount > 0)
        {
            paymentBreakdown.Add(new PaymentBreakdownDto(
                "Unknown",
                unpaidCount,
                completedOrders.Where(o => !o.PaymentMethod.HasValue).Sum(o => o.Total)));
        }

        // ── Status breakdown ─────────────────────────────────────────────────
        var statusBreakdown = allOrders
            .GroupBy(o => o.Status.ToString())
            .Select(g => new OrderStatusBreakdownDto(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        return new AnalyticsResponse(
            kpis, revenueOverTime, topItems, locationPerf,
            allHours, paymentBreakdown, statusBreakdown);
    }

    private static DateTime GetFromDate(string period) => period.ToLowerInvariant() switch
    {
        "week" => DateTime.UtcNow.AddDays(-7),
        "month" => DateTime.UtcNow.AddMonths(-1),
        "6months" => DateTime.UtcNow.AddMonths(-6),
        "year" => DateTime.UtcNow.AddYears(-1),
        "all" => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => DateTime.UtcNow.AddMonths(-1)
    };

    private static List<RevenueDataPoint> GetRevenueOverTime(
        List<Domain.Entities.Order> completedOrders, string period, DateTime fromDate)
    {
        return period.ToLowerInvariant() switch
        {
            "week" or "month" => completedOrders
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new RevenueDataPoint(
                    g.Key.ToString("MMM dd"),
                    g.Sum(o => o.Total),
                    g.Count()))
                .OrderBy(r => r.Label)
                .ToList(),

            "6months" => completedOrders
                .GroupBy(o => GetWeekStart(o.CreatedAt))
                .Select(g => new RevenueDataPoint(
                    g.Key.ToString("MMM dd"),
                    g.Sum(o => o.Total),
                    g.Count()))
                .OrderBy(r => r.Label)
                .ToList(),

            _ => completedOrders
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                .Select(g => new RevenueDataPoint(
                    new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    g.Sum(o => o.Total),
                    g.Count()))
                .OrderBy(r => r.Label)
                .ToList()
        };
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var tomorrow = today.AddDays(1);

        var todayOrders = await _db.Orders
            .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow && !o.IsArchived)
            .ToListAsync(ct);

        var pendingCount = await _db.Orders
            .CountAsync(o => o.Status == OrderStatus.Created && !o.IsArchived, ct);

        var completed = todayOrders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var revenueToday = completed.Sum(o => o.Total);
        var avgOrderValue = completed.Count > 0 ? revenueToday / completed.Count : 0m;

        var hourlyRaw = todayOrders
            .Where(o => o.Status == OrderStatus.Completed)
            .GroupBy(o => o.CreatedAt.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        var hourlyOrders = Enumerable.Range(0, 24)
            .Select(h => new HourlyOrderCount(h, hourlyRaw.GetValueOrDefault(h, 0)))
            .ToList();

        return new DashboardDto(
            todayOrders.Count,
            revenueToday,
            avgOrderValue,
            pendingCount,
            hourlyOrders);
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}
