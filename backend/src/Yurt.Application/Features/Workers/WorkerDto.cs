namespace Yurt.Application.Features.Workers;

public record WorkerDto(Guid Id, string Username, string Role, bool IsActive, DateTime CreatedAt);

public record CreateWorkerDto
{
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    /// <summary>Worker (default), Admin or SuperAdmin. Only a super admin may pick anything but Worker.</summary>
    public string? Role { get; init; }
}

public record UpdateWorkerDto
{
    public string Username { get; init; } = "";
    public bool IsActive { get; init; }
    /// <summary>Omit to keep the current role. Only a super admin may change it.</summary>
    public string? Role { get; init; }
}

public record ResetWorkerPasswordDto
{
    public string NewPassword { get; init; } = "";
}

public record SetWorkerActiveDto(bool IsActive);
