using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Customers;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[Route("api/admin/customers")]
[Authorize(Policy = "AdminOnly")]
public class AdminCustomersController : ApiControllerBase
{
    private readonly CustomerService _customers;

    public AdminCustomersController(CustomerService customers) => _customers = customers;

    [HttpGet]
    public async Task<IActionResult> GetCustomers([FromQuery] string? phone, CancellationToken ct)
        => Ok(await _customers.GetCustomersAsync(phone, ct));
}
