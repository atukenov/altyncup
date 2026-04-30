using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Locations.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Locations.Services;

public class LocationService
{
    private readonly IApplicationDbContext _db;

    public LocationService(IApplicationDbContext db) => _db = db;

    public async Task<List<LocationDto>> GetActiveLocationsAsync(CancellationToken ct = default)
        => await _db.Locations
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => MapToDto(l))
            .ToListAsync(ct);

    public async Task<List<LocationDto>> GetAllLocationsAsync(CancellationToken ct = default)
        => await _db.Locations
            .OrderBy(l => l.Name)
            .Select(l => MapToDto(l))
            .ToListAsync(ct);

    public async Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<LocationDto>.NotFound();
        return Result<LocationDto>.Success(MapToDto(loc));
    }

    public async Task<Result<LocationDto>> CreateAsync(CreateLocationDto dto, CancellationToken ct = default)
    {
        var loc = new Location
        {
            Name = dto.Name,
            Address = dto.Address,
            WorkingHours = dto.WorkingHours,
            ContactPhone = dto.ContactPhone
        };
        _db.Locations.Add(loc);
        await _db.SaveChangesAsync(ct);
        return Result<LocationDto>.Success(MapToDto(loc), 201);
    }

    public async Task<Result<LocationDto>> UpdateAsync(Guid id, UpdateLocationDto dto, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<LocationDto>.NotFound();

        loc.Name = dto.Name;
        loc.Address = dto.Address;
        loc.WorkingHours = dto.WorkingHours;
        loc.ContactPhone = dto.ContactPhone;
        loc.IsActive = dto.IsActive;
        loc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<LocationDto>.Success(MapToDto(loc));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<bool>.NotFound();
        _db.Locations.Remove(loc);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private static LocationDto MapToDto(Location l)
        => new(l.Id, l.Name, l.Address, l.WorkingHours, l.ContactPhone, l.IsActive);
}
