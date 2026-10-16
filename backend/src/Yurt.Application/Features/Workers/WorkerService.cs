using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Workers;

/// <summary>Who is calling the staff-account endpoints. A cafe Admin only manages Worker accounts;
/// a SuperAdmin manages every account and can grant the Admin/SuperAdmin roles.</summary>
public record WorkerActor(Guid? UserId, bool IsSuperAdmin);

public class WorkerService
{
    private const string AdminAccountsForbidden = "Only a super admin can manage admin accounts.";

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogService _audit;

    public WorkerService(IApplicationDbContext db, IPasswordHasher hasher, IAuditLogService audit)
    {
        _db = db;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<List<WorkerDto>> GetWorkersAsync(WorkerActor actor, CancellationToken ct = default)
        => await _db.AdminUsers
            .Where(u => actor.IsSuperAdmin || u.Role == AdminRole.Worker)
            .OrderBy(u => u.Username)
            .Select(u => new WorkerDto(u.Id, u.Username, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToListAsync(ct);

    public async Task<Result<WorkerDto>> CreateWorkerAsync(WorkerActor actor, CreateWorkerDto dto, CancellationToken ct = default)
    {
        if (!TryParseRole(dto.Role, out var role))
            return Result<WorkerDto>.Failure("Invalid role.", 400);
        if (role != AdminRole.Worker && !actor.IsSuperAdmin)
            return Result<WorkerDto>.Forbidden(AdminAccountsForbidden);

        if (await _db.AdminUsers.AnyAsync(u => u.Username == dto.Username, ct))
            return Result<WorkerDto>.Failure("Username already taken.", 409);

        var worker = new AdminUser
        {
            Username = dto.Username,
            PasswordHash = _hasher.Hash(dto.Password),
            Role = role,
            IsActive = true
        };
        _db.AdminUsers.Add(worker);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("WorkerCreated", "AdminUser", worker.Id.ToString(), $"{worker.Username} ({role})", ct);
        return Result<WorkerDto>.Success(ToDto(worker), 201);
    }

    public async Task<Result<WorkerDto>> UpdateWorkerAsync(WorkerActor actor, Guid id, UpdateWorkerDto dto, CancellationToken ct = default)
    {
        var worker = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker == null) return Result<WorkerDto>.NotFound();
        if (!CanManage(actor, worker)) return Result<WorkerDto>.Forbidden(AdminAccountsForbidden);

        if (!TryParseRole(dto.Role ?? worker.Role.ToString(), out var role))
            return Result<WorkerDto>.Failure("Invalid role.", 400);
        if (role != worker.Role && !actor.IsSuperAdmin)
            return Result<WorkerDto>.Forbidden(AdminAccountsForbidden);
        if (IsSelf(actor, worker) && (role != worker.Role || !dto.IsActive))
            return Result<WorkerDto>.Failure("You can't change your own role or deactivate yourself.", 400);

        if (worker.Username != dto.Username &&
            await _db.AdminUsers.AnyAsync(u => u.Username == dto.Username, ct))
            return Result<WorkerDto>.Failure("Username already taken.", 409);

        var roleChanged = role != worker.Role;
        worker.Username = dto.Username;
        worker.IsActive = dto.IsActive;
        worker.Role = role;
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var action = dto.IsActive ? "WorkerActivated" : "WorkerDeactivated";
        await _audit.LogAsync(action, "AdminUser", id.ToString(), worker.Username, ct);
        if (roleChanged)
            await _audit.LogAsync("WorkerRoleChanged", "AdminUser", id.ToString(), $"{worker.Username} ({role})", ct);
        return Result<WorkerDto>.Success(ToDto(worker));
    }

    public async Task<Result<WorkerDto>> SetActiveAsync(WorkerActor actor, Guid id, bool isActive, CancellationToken ct = default)
    {
        var worker = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker is null) return Result<WorkerDto>.NotFound();
        if (!CanManage(actor, worker)) return Result<WorkerDto>.Forbidden(AdminAccountsForbidden);
        if (IsSelf(actor, worker) && !isActive)
            return Result<WorkerDto>.Failure("You can't deactivate yourself.", 400);

        worker.IsActive = isActive;
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var action = isActive ? "WorkerActivated" : "WorkerDeactivated";
        await _audit.LogAsync(action, "AdminUser", id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(ToDto(worker));
    }

    public async Task<Result<WorkerDto>> ResetPasswordAsync(WorkerActor actor, Guid id, ResetWorkerPasswordDto dto, CancellationToken ct = default)
    {
        var worker = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker == null) return Result<WorkerDto>.NotFound();
        if (!CanManage(actor, worker)) return Result<WorkerDto>.Forbidden(AdminAccountsForbidden);

        worker.PasswordHash = _hasher.Hash(dto.NewPassword);
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("WorkerPasswordReset", "AdminUser", id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(ToDto(worker));
    }

    private static bool CanManage(WorkerActor actor, AdminUser target)
        => actor.IsSuperAdmin || target.Role == AdminRole.Worker;

    private static bool IsSelf(WorkerActor actor, AdminUser target)
        => actor.UserId is { } uid && uid == target.Id;

    private static bool TryParseRole(string? value, out AdminRole role)
    {
        if (string.IsNullOrWhiteSpace(value)) { role = AdminRole.Worker; return true; }
        return Enum.TryParse(value, ignoreCase: true, out role) && Enum.IsDefined(role);
    }

    private static WorkerDto ToDto(AdminUser u) => new(u.Id, u.Username, u.Role.ToString(), u.IsActive, u.CreatedAt);
}
