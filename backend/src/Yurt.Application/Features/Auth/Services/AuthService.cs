using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Auth.Services;

public class AuthService
{
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(
        IApplicationDbContext db,
        ITokenService tokenService,
        IPasswordHasher passwordHasher)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResponseDto>> RegisterCustomerAsync(
        CustomerRegisterDto dto, CancellationToken ct = default)
    {
        var normalizedMobile = NormalizeMobile(dto.MobileNumber);
        var exists = await _db.CustomerUsers
            .AnyAsync(u => u.MobileNumber == normalizedMobile, ct);

        if (exists)
            return Result<AuthResponseDto>.Failure("Mobile number already registered.", 409);

        var user = new CustomerUser
        {
            MobileNumber = normalizedMobile,
            PinHash = _passwordHasher.Hash(dto.Pin4),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim()
        };

        _db.CustomerUsers.Add(user);
        await _db.SaveChangesAsync(ct);

        var displayName = string.IsNullOrWhiteSpace(user.FirstName)
            ? user.MobileNumber
            : $"{user.FirstName} {user.LastName}".Trim();
        var token = _tokenService.GenerateCustomerToken(user.Id, user.MobileNumber);
        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(token, "customer", user.Id, displayName), 201);
    }

    public async Task<Result<AuthResponseDto>> LoginCustomerAsync(
        CustomerLoginDto dto, CancellationToken ct = default)
    {
        var normalizedMobile = NormalizeMobile(dto.MobileNumber);
        var user = await _db.CustomerUsers
            .FirstOrDefaultAsync(u => u.MobileNumber == normalizedMobile, ct);

        if (user == null)
            return Result<AuthResponseDto>.Failure("Invalid credentials.", 401);

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            return Result<AuthResponseDto>.Failure(
                $"Account locked. Try again after {user.LockoutEnd.Value:HH:mm} UTC.", 423);

        if (!_passwordHasher.Verify(dto.Pin4, user.PinHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginAttempts = 0;
            }
            await _db.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Failure("Invalid credentials.", 401);
        }

        // Reset on success
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _db.SaveChangesAsync(ct);

        var token = _tokenService.GenerateCustomerToken(user.Id, user.MobileNumber);
        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(token, "customer", user.Id, user.MobileNumber));
    }

    public async Task<Result<AuthResponseDto>> LoginAdminAsync(
        AdminLoginDto dto, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive, ct);

        if (admin == null || !_passwordHasher.Verify(dto.Password, admin.PasswordHash))
            return Result<AuthResponseDto>.Failure("Invalid credentials.", 401);

        var token = _tokenService.GenerateAdminToken(admin.Id, admin.Username, admin.Role.ToString());
        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(token, "admin", admin.Id, admin.Username));
    }

    private static string NormalizeMobile(string mobile)
        => mobile.Trim().Replace(" ", "").Replace("-", "");

    public async Task<Result<CustomerProfileDto>> UpdateProfileAsync(
        Guid userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null)
            return Result<CustomerProfileDto>.NotFound("User not found.");

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        await _db.SaveChangesAsync(ct);

        return Result<CustomerProfileDto>.Success(
            new CustomerProfileDto(user.Id, user.MobileNumber, user.FirstName, user.LastName, user.CreatedAt));
    }
}
