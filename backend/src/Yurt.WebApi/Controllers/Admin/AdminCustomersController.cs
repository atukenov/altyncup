using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Customers;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/customers")]
[Authorize(Policy = "AdminOnly")]
public class AdminCustomersController : ApiControllerBase
{
    private readonly CustomerService _customers;

    public AdminCustomersController(CustomerService customers) => _customers = customers;

    [HttpGet]
    public async Task<IActionResult> GetCustomers([FromQuery] string? phone, CancellationToken ct)
        => Ok(await _customers.GetCustomersAsync(phone, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomer(Guid id, CancellationToken ct)
    {
        var detail = await _customers.GetCustomerDetailAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveDto dto, CancellationToken ct)
    {
        var result = await _customers.SetActiveAsync(id, dto.IsActive, ct);
        return ToResult(result);
    }
}
