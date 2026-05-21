using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Helpers;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Locations.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Locations.Services;

public class LocationService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public LocationService(IApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<LocationDto>> GetActiveLocationsAsync(string lang = "ru", CancellationToken ct = default)
        => await _db.Locations
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto(
                l.Id,
                LocalizationHelper.Localize(l.Name, l.NameRu, l.NameKk, lang),
                l.Address, l.WorkingHours, l.ContactPhone, l.IsActive))
            .ToListAsync(ct);

    public async Task<List<AdminLocationDto>> GetAllLocationsAsync(CancellationToken ct = default)
        => await _db.Locations
            .OrderBy(l => l.Name)
            .Select(l => new AdminLocationDto(
                l.Id, l.Name, l.NameRu, l.NameKk,
                l.Address, l.WorkingHours, l.ContactPhone, l.IsActive))
            .ToListAsync(ct);

    public async Task<Result<AdminLocationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<AdminLocationDto>.NotFound();
        return Result<AdminLocationDto>.Success(MapToAdminDto(loc));
    }

    public async Task<Result<AdminLocationDto>> CreateAsync(CreateLocationDto dto, CancellationToken ct = default)
    {
        var loc = new Location
        {
            Name = dto.Name,
            NameRu = dto.NameRu,
            NameKk = dto.NameKk,
            Address = dto.Address,
            WorkingHours = dto.WorkingHours,
            ContactPhone = dto.ContactPhone
        };
        _db.Locations.Add(loc);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LocationCreated", "Location", loc.Id.ToString(), loc.Name, ct);
        return Result<AdminLocationDto>.Success(MapToAdminDto(loc), 201);
    }

    public async Task<Result<AdminLocationDto>> UpdateAsync(Guid id, UpdateLocationDto dto, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<AdminLocationDto>.NotFound();

        loc.Name = dto.Name;
        loc.NameRu = dto.NameRu;
        loc.NameKk = dto.NameKk;
        loc.Address = dto.Address;
        loc.WorkingHours = dto.WorkingHours;
        loc.ContactPhone = dto.ContactPhone;
        loc.IsActive = dto.IsActive;
        loc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LocationUpdated", "Location", id.ToString(), loc.Name, ct);
        return Result<AdminLocationDto>.Success(MapToAdminDto(loc));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<bool>.NotFound();
        var name = loc.Name;
        _db.Locations.Remove(loc);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LocationDeleted", "Location", id.ToString(), name, ct);
        return Result<bool>.Success(true);
    }

    private static AdminLocationDto MapToAdminDto(Location l)
        => new(l.Id, l.Name, l.NameRu, l.NameKk, l.Address, l.WorkingHours, l.ContactPhone, l.IsActive);
}
