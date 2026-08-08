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

public record CreateLocationDto
{
    public string Name { get; init; } = string.Empty;
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public string Address { get; init; } = string.Empty;
    public string WorkingHours { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
}

public record UpdateLocationDto
{
    public string Name { get; init; } = string.Empty;
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public string Address { get; init; } = string.Empty;
    public string WorkingHours { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
