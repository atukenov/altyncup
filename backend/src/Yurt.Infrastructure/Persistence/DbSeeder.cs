using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using BCrypt.Net;

namespace Yurt.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        await db.Database.MigrateAsync();

        await SeedLocationsAsync(db, logger);
        await SeedMenuAsync(db, logger);
        await SeedAdminsAsync(db, logger);
    }

    private static async Task SeedLocationsAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.Locations.AnyAsync()) return;

        db.Locations.AddRange(
            new Location
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                Name = "Yurt Downtown",
                Address = "123 Main Street, Downtown, City 10001",
                WorkingHours = "Mon–Fri 07:00–22:00 | Sat–Sun 08:00–23:00",
                ContactPhone = "+15551001001",
                IsActive = true
            },
            new Location
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000002"),
                Name = "Yurt Midtown",
                Address = "456 Park Avenue, Midtown, City 10022",
                WorkingHours = "Daily 08:00–21:00",
                ContactPhone = "+15551001002",
                IsActive = true
            },
            new Location
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000003"),
                Name = "Yurt Airport Terminal 2",
                Address = "Terminal 2, City International Airport",
                WorkingHours = "Daily 05:00–23:00",
                ContactPhone = "+15551001003",
                IsActive = false
            }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Locations seeded.");
    }

    private static async Task SeedMenuAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.MenuCategories.AnyAsync()) return;

        var catCoffee = new MenuCategory { Id = Guid.Parse("22222222-0000-0000-0000-000000000001"), Name = "Coffee", SortOrder = 1 };
        var catCold = new MenuCategory { Id = Guid.Parse("22222222-0000-0000-0000-000000000002"), Name = "Cold Drinks", SortOrder = 2 };
        var catFood = new MenuCategory { Id = Guid.Parse("22222222-0000-0000-0000-000000000003"), Name = "Food & Pastries", SortOrder = 3 };
        db.MenuCategories.AddRange(catCoffee, catCold, catFood);

        db.MenuItems.AddRange(
            // Coffee
            new MenuItem { CategoryId = catCoffee.Id, Name = "Espresso", Description = "Rich, concentrated single-shot espresso.", Price = 2.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1510707577719-ae7c14805e3a?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Americano", Description = "Espresso diluted with hot water for a smooth, bold cup.", Price = 3.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1551030173-122aabc4489c?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Cappuccino", Description = "Equal parts espresso, steamed milk, and thick foam.", Price = 3.75m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1553361371-9b22f78e8b1d?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Flat White", Description = "Double ristretto with velvety microfoam milk.", Price = 4.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1577968897966-3d4325b36b02?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Latte", Description = "Smooth espresso with lots of steamed milk and a small layer of foam.", Price = 4.25m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1561882468-9110e03e0f78?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Mocha", Description = "Espresso, chocolate sauce, steamed milk, and whipped cream.", Price = 4.75m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1572442388796-11668a67e53d?w=400" },
            // Cold
            new MenuItem { CategoryId = catCold.Id, Name = "Iced Latte", Description = "Chilled espresso over ice with cold milk.", Price = 4.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1461023058943-07fcbe16d735?w=400" },
            new MenuItem { CategoryId = catCold.Id, Name = "Cold Brew", Description = "Slow-steeped 18-hour cold brew concentrate.", Price = 4.75m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1517701604599-bb29b565090b?w=400" },
            new MenuItem { CategoryId = catCold.Id, Name = "Matcha Latte", Description = "Ceremonial-grade matcha with oat milk over ice.", Price = 5.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1536256263959-770b48d82b0a?w=400" },
            new MenuItem { CategoryId = catCold.Id, Name = "Fresh Lemonade", Description = "Hand-squeezed lemonade with a hint of mint.", Price = 3.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1613478223719-2ab802602423?w=400" },
            // Food
            new MenuItem { CategoryId = catFood.Id, Name = "Butter Croissant", Description = "Flaky, buttery, golden-baked classic French croissant.", Price = 3.25m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1555507036-ab1f4038808a?w=400" },
            new MenuItem { CategoryId = catFood.Id, Name = "Avocado Toast", Description = "Sourdough topped with smashed avocado, sea salt, and chilli flakes.", Price = 6.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1541519227354-08fa5d50c820?w=400" },
            new MenuItem { CategoryId = catFood.Id, Name = "Banana Bread", Description = "Moist, house-baked banana bread with walnut crunch.", Price = 4.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1588195538326-c5b1e9f80a1b?w=400" }
        );

        await db.SaveChangesAsync();
        logger.LogInformation("Menu seeded.");
    }

    private static async Task SeedAdminsAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.AdminUsers.AnyAsync()) return;

        db.AdminUsers.AddRange(
            new AdminUser
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!"),
                Role = AdminRole.Admin,
                IsActive = true
            },
            new AdminUser
            {
                Username = "worker1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Worker@123!"),
                Role = AdminRole.Worker,
                IsActive = true
            }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Admin users seeded.");
    }
}
