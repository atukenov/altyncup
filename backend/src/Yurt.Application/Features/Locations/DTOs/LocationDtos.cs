namespace Yurt.Application.Features.Locations.DTOs;

// Customer-facing (localized name)
public record LocationDto(
    Guid Id,
    string Name,
    string Address,
    string WorkingHours,
    string ContactPhone,
    bool IsActive);

// Admin-facing (all language fields)
public record AdminLocationDto(
    Guid Id,
    string Name,
    string? NameRu,
    string? NameKk,
    string Address,
    string WorkingHours,
    string ContactPhone,
    bool IsActive);

public record CreateLocationDto(
    string Name,
    string? NameRu,
    string? NameKk,
    string Address,
    string WorkingHours,
    string ContactPhone);

public record UpdateLocationDto(
    string Name,
    string? NameRu,
    string? NameKk,
    string Address,
    string WorkingHours,
    string ContactPhone,
    bool IsActive);
