using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Favorites.Services;
using Yurt.Application.Features.Locations.Services;
using Yurt.Application.Features.Menu.Services;
using Yurt.Application.Features.Analytics.Services;
using Yurt.Application.Features.Orders.Services;
using Yurt.Application.Features.Customers;
using Yurt.Application.Features.GroupOrders;
using Yurt.Application.Features.Workers;
using Yurt.Application.Features.Payments.Services;
using Yurt.Application.Features.Promotions.Services;
using Yurt.Application.Features.DiscountCodes.Services;
using Yurt.Application.Features.AuditLog.Services;
using Yurt.Application.Features.Reports;
using Yurt.Infrastructure.Hubs;
using Yurt.Infrastructure.Payments;
using Yurt.Infrastructure.Persistence;
using Yurt.Infrastructure.Services;

namespace Yurt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                GetPostgreSqlConnectionString(configuration),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // HTTP context
        services.AddHttpContextAccessor();

        // Services
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IOrdersHubService, OrdersHubService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Payment services
        services.Configure<PaymentOptions>(configuration.GetSection("Payment"));
        services.AddHttpClient();
        services.AddScoped<IPaymentProvider, KaspiSandboxPaymentProvider>();
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();
        services.AddScoped<IPaymentWebhookValidator, PaymentWebhookValidator>();

        // Application services
        services.AddScoped<AnalyticsService>();
        services.AddScoped<AuthService>();
        services.AddScoped<LocationService>();
        services.AddScoped<MenuService>();
        services.AddScoped<OrderService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<PromotionService>();
        services.AddScoped<DiscountCodeService>();
        services.AddScoped<FavoriteService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<WorkerService>();
        services.AddScoped<GroupOrderService>();
        services.AddScoped<ReportService>();

        // Background services
        services.AddHostedService<OrderArchivalService>();

        // JWT Auth
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret))
                };

                // Allow SignalR to use token from query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireClaim("user_type", "admin"));
            options.AddPolicy("CustomerOnly", policy =>
                policy.RequireClaim("user_type", "customer"));
            options.AddPolicy("AdminRoleAdmin", policy =>
            {
                policy.RequireClaim("user_type", "admin");
                policy.RequireRole("Admin");
            });
        });

        // SignalR
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
        }).AddJsonProtocol(opts =>
        {
            opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            opts.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }

    private static string GetPostgreSqlConnectionString(IConfiguration configuration)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("RAILWAY_DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("PostgreSQL connection string must be configured via DATABASE_URL, RAILWAY_DATABASE_URL, or DefaultConnection.");
        }

        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(databaseUrl);
            var userInfoParts = uri.UserInfo.Split(':', 2);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = userInfoParts.Length > 0 ? userInfoParts[0] : string.Empty,
                Password = userInfoParts.Length > 1 ? userInfoParts[1] : string.Empty,
                SslMode = SslMode.Prefer
            };
            return builder.ConnectionString;
        }

        return databaseUrl;
    }
}
