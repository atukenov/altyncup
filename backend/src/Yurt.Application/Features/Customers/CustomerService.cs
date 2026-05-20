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

    public async Task<CustomerDetailDto?> GetCustomerDetailAsync(Guid id, CancellationToken ct = default)
        => await _db.CustomerUsers
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetailDto(
                c.Id,
                c.MobileNumber,
                (c.FirstName + " " + c.LastName).Trim(),
                c.CreatedAt,
                c.IsActive,
                c.Orders.Count(o => !o.IsArchived),
                c.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => (decimal?)o.Total) ?? 0,
                c.Orders
                    .Where(o => !o.IsArchived)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(50)
                    .Select(o => new CustomerOrderSummaryDto(
                        o.Id,
                        o.CreatedAt,
                        o.Status,
                        o.Total,
                        o.Location.Name,
                        o.Items.Count))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
}
