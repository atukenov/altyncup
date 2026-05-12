namespace Yurt.Application.Features.Analytics.DTOs;

public record AnalyticsResponse(
    KpiSummary Kpis,
    List<RevenueDataPoint> RevenueOverTime,
    List<TopItemDto> TopItems,
    List<LocationPerformanceDto> LocationPerformance,
    List<HourlyDistributionDto> HourlyDistribution,
    List<PaymentBreakdownDto> PaymentBreakdown,
    List<OrderStatusBreakdownDto> StatusBreakdown);

public record KpiSummary(
    decimal TotalRevenue,
    int TotalOrders,
    decimal AvgOrderValue,
    int CompletedOrders,
    int DeclinedOrders,
    double AvgPrepTimeMinutes,
    int UniqueCustomers);

public record RevenueDataPoint(string Label, decimal Revenue, int Orders);

public record TopItemDto(string Name, int QuantitySold, decimal Revenue);

public record LocationPerformanceDto(string LocationName, decimal Revenue, int Orders);

public record HourlyDistributionDto(int Hour, int Orders);

public record PaymentBreakdownDto(string Method, int Count, decimal Total);

public record OrderStatusBreakdownDto(string Status, int Count);
