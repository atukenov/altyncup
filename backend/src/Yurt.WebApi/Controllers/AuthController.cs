using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Auth.Validators;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;
using Yurt.WebApi.Common;
using Microsoft.AspNetCore.Authorization;

namespace Yurt.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly AuthService _authService;
    private readonly ICurrentUserService _currentUser;
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AuthController(
        AuthService authService,
        ICurrentUserService currentUser,
        IApplicationDbContext db,
        IPasswordHasher hasher)
    {
        _authService = authService;
        _currentUser = currentUser;
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Register a new customer using mobile number and 4-digit PIN.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] CustomerRegisterDto dto, CancellationToken ct)
    {
        var validator = new CustomerRegisterValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var result = await _authService.RegisterCustomerAsync(dto, ct);
        return ToResult(result);
    }

    /// <summary>Login with mobile number and 4-digit PIN.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] CustomerLoginDto dto, CancellationToken ct)
    {
        var validator = new CustomerLoginValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var result = await _authService.LoginCustomerAsync(dto, ct);
        return ToResult(result);
    }

    /// <summary>Issue a fresh 7-day token for the current customer (sliding expiration).</summary>
    [HttpPost("refresh")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var result = await _authService.RefreshCustomerTokenAsync(userId, ct);
        return ToResult(result);
    }

    /// <summary>Get current customer profile.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.MobileNumber, user.FirstName, user.LastName, user.CreatedAt });
    }

    /// <summary>Update current customer's name.</summary>
    [HttpPut("me")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileDto dto, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var result = await _authService.UpdateProfileAsync(userId, dto, ct);
        return ToResult(result);
    }

    /// <summary>Change customer's 4-digit PIN.</summary>
    [HttpPut("pin")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> ChangePin(
        [FromBody] ChangePinDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrentPin) || dto.CurrentPin.Length != 4 ||
            string.IsNullOrWhiteSpace(dto.NewPin) || dto.NewPin.Length != 4)
            return ValidationError("Current PIN and new PIN must each be exactly 4 digits.");

        var userId = _currentUser.UserId!.Value;
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null) return NotFound();

        if (!_hasher.Verify(dto.CurrentPin, user.PinHash))
            return Unauthorized(new ProblemDetails { Title = "Current PIN is incorrect." });

        user.PinHash = _hasher.Hash(dto.NewPin);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Delete current customer account (soft delete).</summary>
    [HttpDelete("me")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var user = await _db.CustomerUsers.FindAsync([userId], ct);
        if (user == null) return NotFound();

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Get lifetime order stats for the current customer.</summary>
    [HttpGet("me/stats")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var orders = await _db.Orders
            .Where(o => o.CustomerUserId == userId && o.Status == OrderStatus.Completed)
            .ToListAsync(ct);

        return Ok(new
        {
            totalOrders = orders.Count,
            totalSpent = orders.Sum(o => o.Total)
        });
    }
}
