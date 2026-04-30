using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Locations.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationsController : ApiControllerBase
{
    private readonly LocationService _locationService;

    public LocationsController(LocationService locationService)
        => _locationService = locationService;

    /// <summary>Get all active locations.</summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct)
        => Ok(await _locationService.GetActiveLocationsAsync(ct));

    /// <summary>Get a specific location by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResult(await _locationService.GetByIdAsync(id, ct));
}
