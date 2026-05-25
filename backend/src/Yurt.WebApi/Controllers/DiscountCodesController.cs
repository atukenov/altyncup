using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.DiscountCodes.DTOs;
using Yurt.Application.Features.DiscountCodes.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/discount-codes")]
[Authorize(Policy = "CustomerOnly")]
public class DiscountCodesController : ApiControllerBase
{
    private readonly DiscountCodeService _service;

    public DiscountCodesController(DiscountCodeService service)
        => _service = service;

    /// <summary>Validate a discount code without consuming it.</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateDiscountCodeDto dto, CancellationToken ct)
        => Ok(await _service.ValidateAsync(dto.Code, dto.Subtotal, ct));
}
