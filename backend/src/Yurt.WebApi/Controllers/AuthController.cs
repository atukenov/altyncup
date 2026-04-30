using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Auth.Validators;
using Yurt.Application.Common.Interfaces;
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

    public AuthController(
        AuthService authService,
        ICurrentUserService currentUser,
        IApplicationDbContext db)
    {
        _authService = authService;
        _currentUser = currentUser;
        _db = db;
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
}
