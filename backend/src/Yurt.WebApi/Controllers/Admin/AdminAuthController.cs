using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Auth.Validators;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ApiControllerBase
{
    private readonly AuthService _authService;

    public AdminAuthController(AuthService authService) => _authService = authService;

    /// <summary>Admin login with username and password.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginDto dto, CancellationToken ct)
    {
        var validator = new AdminLoginValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var result = await _authService.LoginAdminAsync(dto, ct);
        return ToResult(result);
    }
}
