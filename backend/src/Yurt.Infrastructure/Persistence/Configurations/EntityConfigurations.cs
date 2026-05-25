using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yurt.Domain.Entities;

namespace Yurt.Infrastructure.Persistence.Configurations;

public class CustomerUserConfiguration : IEntityTypeConfiguration<CustomerUser>
{
    public void Configure(EntityTypeBuilder<CustomerUser> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.MobileNumber).IsUnique();
        builder.Property(e => e.MobileNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PinHash).HasMaxLength(200).IsRequired();
    }
}

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Username).IsUnique();
        builder.Property(e => e.Username).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameRu).HasMaxLength(200);
        builder.Property(e => e.NameKk).HasMaxLength(200);
        builder.Property(e => e.Address).HasMaxLength(500).IsRequired();
        builder.Property(e => e.WorkingHours).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContactPhone).HasMaxLength(30).IsRequired();
    }
}

public class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.NameRu).HasMaxLength(100);
        builder.Property(e => e.NameKk).HasMaxLength(100);
    }
}

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameRu).HasMaxLength(200);
        builder.Property(e => e.NameKk).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.DescriptionRu).HasMaxLength(1000);
        builder.Property(e => e.DescriptionKk).HasMaxLength(1000);
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
        builder.HasOne(e => e.Category).WithMany(c => c.Items)
            .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CustomerUserId, e.MenuItemId }).IsUnique();
        builder.HasOne(e => e.CustomerUser).WithMany(c => c.Favorites)
            .HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.MenuItem).WithMany(m => m.Favorites)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DeclineReason).HasMaxLength(500);
        builder.Property(e => e.Subtotal).HasPrecision(10, 2);
        builder.Property(e => e.DiscountAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.Total).HasPrecision(10, 2);
        builder.HasOne(e => e.CustomerUser).WithMany(c => c.Orders)
            .HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location).WithMany(l => l.Orders)
            .HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DiscountCode).WithMany(d => d.Orders)
            .HasForeignKey(e => e.DiscountCodeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    public void Configure(EntityTypeBuilder<DiscountCode> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DiscountValue).HasPrecision(10, 2);
        builder.Property(e => e.MinOrderAmount).HasPrecision(10, 2);
        builder.Property(e => e.UsedCount).HasDefaultValue(0);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MenuItemName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.UnitPrice).HasPrecision(10, 2);
        builder.Property(e => e.LineTotal).HasPrecision(10, 2);
        builder.HasOne(e => e.Order).WithMany(o => o.Items)
            .HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.MenuItem).WithMany(m => m.OrderItems)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TitleRu).HasMaxLength(200);
        builder.Property(e => e.TitleKk).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.DescriptionRu).HasMaxLength(2000);
        builder.Property(e => e.DescriptionKk).HasMaxLength(2000);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
    }
}

public class MenuToppingConfiguration : IEntityTypeConfiguration<MenuTopping>
{
    public void Configure(EntityTypeBuilder<MenuTopping> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameRu).HasMaxLength(200);
        builder.Property(e => e.NameKk).HasMaxLength(200);
        builder.Property(e => e.Price).HasPrecision(10, 2);
    }
}

public class MenuToppingCategoryConfiguration : IEntityTypeConfiguration<MenuToppingCategory>
{
    public void Configure(EntityTypeBuilder<MenuToppingCategory> builder)
    {
        builder.HasKey(e => new { e.ToppingId, e.CategoryId });
        builder.HasOne(e => e.Topping).WithMany(t => t.ToppingCategories)
            .HasForeignKey(e => e.ToppingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Category).WithMany(c => c.ToppingLinks)
            .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemToppingConfiguration : IEntityTypeConfiguration<OrderItemTopping>
{
    public void Configure(EntityTypeBuilder<OrderItemTopping> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ToppingName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.HasOne(e => e.OrderItem).WithMany(oi => oi.Toppings)
            .HasForeignKey(e => e.OrderItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.InvoiceId).IsUnique();
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PaymentUrl).HasMaxLength(500).IsRequired();
        builder.Property(e => e.QrCode).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.RawResponse).HasMaxLength(4000);
        builder.Property(e => e.Amount).HasPrecision(10, 2);
        builder.HasOne(e => e.Order).WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentWebhookLogConfiguration : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Payload).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Headers).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Processed).HasDefaultValue(false);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Token).IsUnique();
        builder.Property(e => e.Token).HasMaxLength(128).IsRequired();
        builder.Property(e => e.UserType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ReplacedByToken).HasMaxLength(128);
        builder.Property(e => e.CreatedByIp).HasMaxLength(50);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(100);
        builder.Property(e => e.PerformedByUsername).HasMaxLength(200);
        builder.Property(e => e.Details).HasMaxLength(2000);
        builder.Property(e => e.IpAddress).HasMaxLength(50);
    }
}
