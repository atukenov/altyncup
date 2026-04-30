namespace Yurt.Application.Features.Locations.DTOs;

public record LocationDto(
    Guid Id,
    string Name,
    string Address,
    string WorkingHours,
    string ContactPhone,
    bool IsActive);

public record CreateLocationDto(
    string Name,
    string Address,
    string WorkingHours,
    string ContactPhone);

public record UpdateLocationDto(
    string Name,
    string Address,
    string WorkingHours,
    string ContactPhone,
    bool IsActive);
