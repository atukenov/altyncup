using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Workers;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

public class WorkerServiceTests
{
    private static readonly WorkerActor SuperAdmin = new(Guid.NewGuid(), IsSuperAdmin: true);
    private static readonly WorkerActor CafeAdmin = new(Guid.NewGuid(), IsSuperAdmin: false);

    private static (WorkerService svc, IApplicationDbContext db) Build(IEnumerable<AdminUser>? workers = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var hasher = Substitute.For<IPasswordHasher>();
        var audit = Substitute.For<IAuditLogService>();
        hasher.Hash(Arg.Any<string>()).Returns(ci => $"hashed:{ci.Arg<string>()}");

        var set = (workers ?? []).ToList().BuildMockDbSet();
        db.AdminUsers.Returns(set);

        return (new WorkerService(db, hasher, audit), db);
    }

    [Fact]
    public async Task GetWorkersAsync_SortsByUsername()
    {
        var z = new AdminUser { Username = "zeke", Role = AdminRole.Worker };
        var a = new AdminUser { Username = "amina", Role = AdminRole.Worker };
        var (svc, _) = Build([z, a]);

        var result = await svc.GetWorkersAsync(SuperAdmin);

        Assert.Equal(["amina", "zeke"], result.Select(r => r.Username));
    }

    [Fact]
    public async Task CreateWorkerAsync_DuplicateUsername_ReturnsConflict()
    {
        var existing = new AdminUser { Username = "taken", Role = AdminRole.Worker };
        var (svc, _) = Build([existing]);

        var result = await svc.CreateWorkerAsync(SuperAdmin, new CreateWorkerDto { Username = "taken", Password = "pw" });

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task CreateWorkerAsync_NewUsername_HashesPasswordAndSaves()
    {
        var (svc, db) = Build();

        var result = await svc.CreateWorkerAsync(SuperAdmin, new CreateWorkerDto { Username = "newhire", Password = "plainpw" });

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        db.AdminUsers.Received(1).Add(Arg.Is<AdminUser>(u =>
            u.Username == "newhire" && u.PasswordHash == "hashed:plainpw" && u.IsActive));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkerAsync_UnknownId_ReturnsNotFound()
    {
        var (svc, _) = Build();

        var result = await svc.UpdateWorkerAsync(SuperAdmin, Guid.NewGuid(), new UpdateWorkerDto { Username = "x", IsActive = true });

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkerAsync_RenamingToAnotherWorkersUsername_ReturnsConflict()
    {
        var worker = new AdminUser { Username = "worker1", Role = AdminRole.Worker };
        var other = new AdminUser { Username = "worker2", Role = AdminRole.Worker };
        var (svc, _) = Build([worker, other]);

        var result = await svc.UpdateWorkerAsync(SuperAdmin, worker.Id, new UpdateWorkerDto { Username = "worker2", IsActive = true });

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkerAsync_ValidChange_UpdatesAndSaves()
    {
        var worker = new AdminUser { Username = "old", IsActive = true, Role = AdminRole.Worker };
        var (svc, db) = Build([worker]);

        var result = await svc.UpdateWorkerAsync(SuperAdmin, worker.Id, new UpdateWorkerDto { Username = "new", IsActive = false });

        Assert.True(result.Succeeded);
        Assert.Equal("new", worker.Username);
        Assert.False(worker.IsActive);
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveAsync_UnknownId_ReturnsNotFound()
    {
        var (svc, _) = Build();
        var result = await svc.SetActiveAsync(SuperAdmin, Guid.NewGuid(), false);
        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task SetActiveAsync_TogglesFlag()
    {
        var worker = new AdminUser { Username = "w", IsActive = true, Role = AdminRole.Worker };
        var (svc, _) = Build([worker]);

        var result = await svc.SetActiveAsync(SuperAdmin, worker.Id, false);

        Assert.True(result.Succeeded);
        Assert.False(worker.IsActive);
    }

    [Fact]
    public async Task ResetPasswordAsync_HashesNewPassword()
    {
        var worker = new AdminUser { Username = "w", PasswordHash = "old-hash", Role = AdminRole.Worker };
        var (svc, _) = Build([worker]);

        var result = await svc.ResetPasswordAsync(SuperAdmin, worker.Id, new ResetWorkerPasswordDto { NewPassword = "newpw" });

        Assert.True(result.Succeeded);
        Assert.Equal("hashed:newpw", worker.PasswordHash);
    }

    // ── Super admin vs cafe admin ────────────────────────────────────────────

    [Fact]
    public async Task GetWorkersAsync_CafeAdmin_SeesOnlyWorkers()
    {
        var worker = new AdminUser { Username = "barista", Role = AdminRole.Worker };
        var admin = new AdminUser { Username = "manager", Role = AdminRole.Admin };
        var super = new AdminUser { Username = "owner", Role = AdminRole.SuperAdmin };
        var (svc, _) = Build([worker, admin, super]);

        Assert.Equal(["barista"], (await svc.GetWorkersAsync(CafeAdmin)).Select(r => r.Username));
        Assert.Equal(3, (await svc.GetWorkersAsync(SuperAdmin)).Count);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task CreateWorkerAsync_CafeAdminCreatingAdminRole_ReturnsForbidden(string role)
    {
        var (svc, db) = Build();

        var result = await svc.CreateWorkerAsync(CafeAdmin, new CreateWorkerDto { Username = "x", Password = "pw1234", Role = role });

        Assert.Equal(403, result.StatusCode);
        db.AdminUsers.DidNotReceive().Add(Arg.Any<AdminUser>());
    }

    [Fact]
    public async Task CreateWorkerAsync_SuperAdmin_CanCreateAdmin()
    {
        var (svc, db) = Build();

        var result = await svc.CreateWorkerAsync(SuperAdmin, new CreateWorkerDto { Username = "mgr", Password = "pw1234", Role = "Admin" });

        Assert.True(result.Succeeded);
        Assert.Equal("Admin", result.Data!.Role);
        db.AdminUsers.Received(1).Add(Arg.Is<AdminUser>(u => u.Role == AdminRole.Admin));
    }

    [Fact]
    public async Task CreateWorkerAsync_InvalidRole_ReturnsBadRequest()
    {
        var (svc, _) = Build();
        var result = await svc.CreateWorkerAsync(SuperAdmin, new CreateWorkerDto { Username = "x", Password = "pw1234", Role = "Owner" });
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CafeAdmin_CannotTouchAdminAccounts()
    {
        var admin = new AdminUser { Username = "manager", Role = AdminRole.Admin, IsActive = true };
        var (svc, _) = Build([admin]);

        Assert.Equal(403, (await svc.UpdateWorkerAsync(CafeAdmin, admin.Id, new UpdateWorkerDto { Username = "m", IsActive = true })).StatusCode);
        Assert.Equal(403, (await svc.SetActiveAsync(CafeAdmin, admin.Id, false)).StatusCode);
        Assert.Equal(403, (await svc.ResetPasswordAsync(CafeAdmin, admin.Id, new ResetWorkerPasswordDto { NewPassword = "newpw1" })).StatusCode);
        Assert.True(admin.IsActive);
        Assert.Equal("manager", admin.Username);
    }

    [Fact]
    public async Task UpdateWorkerAsync_CafeAdminPromotingWorker_ReturnsForbidden()
    {
        var worker = new AdminUser { Username = "w", Role = AdminRole.Worker, IsActive = true };
        var (svc, _) = Build([worker]);

        var result = await svc.UpdateWorkerAsync(CafeAdmin, worker.Id, new UpdateWorkerDto { Username = "w", IsActive = true, Role = "Admin" });

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(AdminRole.Worker, worker.Role);
    }

    [Fact]
    public async Task UpdateWorkerAsync_SuperAdmin_CanPromoteWorker()
    {
        var worker = new AdminUser { Username = "w", Role = AdminRole.Worker, IsActive = true };
        var (svc, _) = Build([worker]);

        var result = await svc.UpdateWorkerAsync(SuperAdmin, worker.Id, new UpdateWorkerDto { Username = "w", IsActive = true, Role = "Admin" });

        Assert.True(result.Succeeded);
        Assert.Equal(AdminRole.Admin, worker.Role);
    }

    [Fact]
    public async Task SuperAdmin_CannotDemoteOrDeactivateSelf()
    {
        var me = new AdminUser { Username = "owner", Role = AdminRole.SuperAdmin, IsActive = true };
        var (svc, _) = Build([me]);
        var self = new WorkerActor(me.Id, IsSuperAdmin: true);

        Assert.Equal(400, (await svc.UpdateWorkerAsync(self, me.Id, new UpdateWorkerDto { Username = "owner", IsActive = true, Role = "Admin" })).StatusCode);
        Assert.Equal(400, (await svc.SetActiveAsync(self, me.Id, false)).StatusCode);
        Assert.Equal(AdminRole.SuperAdmin, me.Role);
        Assert.True(me.IsActive);
    }
}
