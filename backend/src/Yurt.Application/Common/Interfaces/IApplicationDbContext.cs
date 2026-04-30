using Microsoft.EntityFrameworkCore;
using Yurt.Domain.Entities;

namespace Yurt.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<CustomerUser> CustomerUsers { get; }
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<Location> Locations { get; }
    DbSet<MenuCategory> MenuCategories { get; }
    DbSet<MenuItem> MenuItems { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
