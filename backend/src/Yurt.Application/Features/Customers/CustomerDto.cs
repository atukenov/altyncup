namespace Yurt.Application.Features.Customers;

public record CustomerSummaryDto(
    Guid Id,
    string Phone,
    string DisplayName,
    DateTime RegisteredAt,
    int OrderCount,
    decimal TotalSpent);
