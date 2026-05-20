using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Customers;

public class CustomerService
{
    private readonly IApplicationDbContext _db;

    public CustomerService(IApplicationDbContext db) => _db = db;

    public async Task<List<CustomerSummaryDto>> GetCustomersAsync(string? phone, CancellationToken ct = default)
    {
        var query = _db.CustomerUsers
            .Include(u => u.Orders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(u => u.MobileNumber.Contains(phone));

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new CustomerSummaryDto(
                u.Id,
                u.MobileNumber,
                (u.FirstName + " " + u.LastName).Trim(),
                u.CreatedAt,
                u.Orders.Count(o => o.Status == OrderStatus.Completed),
                u.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total)))
            .ToListAsync(ct);
    }
}
