using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Locations.DTOs;
using Yurt.Application.Features.Locations.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/locations")]
[Authorize(Policy = "AdminOnly")] // list is shared with workers (orders page location filter)
public class AdminLocationsController : ApiControllerBase
{
    private readonly LocationService _locationService;

    public AdminLocationsController(LocationService locationService)
        => _locationService = locationService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _locationService.GetAllLocationsAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResult(await _locationService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateLocationDto dto, CancellationToken ct)
        => ToResult(await _locationService.CreateAsync(dto, User.IsInRole("SuperAdmin"), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateLocationDto dto, CancellationToken ct)
        => ToResult(await _locationService.UpdateAsync(id, dto, User.IsInRole("SuperAdmin"), ct));

    /// <summary>Pause new orders at a location (rush, machine down). Open to all staff,
    /// since it's an orders-board action. Minutes = 0 resumes.</summary>
    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, [FromBody] PauseOrdersDto dto, CancellationToken ct)
        => ToResult(await _locationService.SetOrdersPausedAsync(id, dto.Minutes, ct));

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
        => ToResult(await _locationService.SetOrdersPausedAsync(id, 0, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await _locationService.DeleteAsync(id, ct));
}
