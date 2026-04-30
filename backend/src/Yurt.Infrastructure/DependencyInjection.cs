using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Favorites.Services;
using Yurt.Application.Features.Locations.Services;
using Yurt.Application.Features.Menu.Services;
using Yurt.Application.Features.Orders.Services;
using Yurt.Infrastructure.Hubs;
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
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
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

        // Application services
        services.AddScoped<AuthService>();
        services.AddScoped<LocationService>();
        services.AddScoped<MenuService>();
        services.AddScoped<OrderService>();
        services.AddScoped<FavoriteService>();

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
        services.AddSignalR().AddJsonProtocol(opts =>
        {
            opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            opts.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }
}
