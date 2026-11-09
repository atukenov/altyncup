using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain.Enums;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Read-only operational view of everything iiko-related for super admins: integration
/// settings (flags only, never secrets), order push status, location and menu mapping
/// coverage, loyalty links and the iiko audit trail. Live iiko lookups (terminal groups,
/// payment types) stay on <see cref="AdminIikoController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/iiko")]
[Authorize(Policy = "SuperAdminOnly")]
public class AdminIikoOverviewController(IApplicationDbContext db, IikoOptions options) : ControllerBase
{
    private const int MaxListed = 100;

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);

        var syncCounts = await db.Orders
            .Where(o => o.CreatedAt >= since && o.Status != OrderStatus.Created && o.Status != OrderStatus.Declined)
            .GroupBy(o => o.IikoOrderSyncStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pendingRetries = await db.Orders.CountAsync(
            o => o.IikoOrderSyncPendingAction != IikoOrderSyncPendingAction.None, ct);

        var locations = await db.Locations
            .OrderBy(l => l.Name)
            .Select(l => new { l.Id, l.Name, l.IsActive, l.IikoTerminalGroupId })
            .ToListAsync(ct);

        var totalItems = await db.MenuItems.CountAsync(ct);
        var variantsTotal = await db.MenuItemVariants.CountAsync(ct);
        var variantsOwnProduct = await db.MenuItemVariants.CountAsync(v => v.IikoProductId != null, ct);

        // An item is orderable to iiko when it has its own product, or every one of its
        // size variants has one (per-size products, item left unmapped).
        var unmappedItems = await db.MenuItems
            .Where(i => i.IikoProductId == null
                && (!i.Variants.Any() || i.Variants.Any(v => v.IikoProductId == null)))
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder)
            .Select(i => new
            {
                i.Id,
                i.Name,
                Category = i.Category.Name,
                i.IsAvailable,
                Sizes = i.Variants.Count,
                UnmappedSizes = i.Variants.Count(v => v.IikoProductId == null),
            })
            .Take(MaxListed)
            .ToListAsync(ct);
        var unmappedItemCount = await db.MenuItems.CountAsync(
            i => i.IikoProductId == null
                && (!i.Variants.Any() || i.Variants.Any(v => v.IikoProductId == null)), ct);

        var customersWithIiko = await db.CustomerUsers.CountAsync(c => c.IikoCustomerId != null, ct);
        var customersTotal = await db.CustomerUsers.CountAsync(ct);

        return Ok(new
        {
            config = new
            {
                enabled = options.Enabled,
                pushOrdersEnabled = options.PushOrdersEnabled,
                baseUrl = options.BaseUrl,
                organizationConfigured = options.OrganizationId != Guid.Empty,
                credentialsConfigured = !string.IsNullOrWhiteSpace(options.ApiKey)
                    || (!string.IsNullOrWhiteSpace(options.AppId) && !string.IsNullOrWhiteSpace(options.ClientSecret)),
                paymentTypeConfigured = options.PaymentTypeId != Guid.Empty,
                programConfigured = options.ProgramId != Guid.Empty,
                webhookTokenConfigured = !string.IsNullOrWhiteSpace(options.WebhookAuthToken),
                externalMenuConfigured = !string.IsNullOrWhiteSpace(options.ExternalMenuId),
                earnPercent = options.EarnPercent,
            },
            orders = new
            {
                days = 30,
                byStatus = syncCounts.ToDictionary(x => x.Status.ToString(), x => x.Count),
                pendingRetries,
            },
            locations = locations.Select(l => new
            {
                l.Id,
                l.Name,
                l.IsActive,
                terminalGroupId = l.IikoTerminalGroupId,
            }),
            menu = new
            {
                totalItems,
                unmappedItemCount,
                variantsTotal,
                variantsOwnProduct,
                unmappedItems,
            },
            loyalty = new { customersTotal, customersWithIiko },
        });
    }

    /// <summary>Orders as iiko sees them: sync status, iiko order id, retry state and the latest iiko audit note.</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] IikoOrderSyncStatus? syncStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 25;

        // Orders that reached the kitchen flow, or have any iiko sync state of their own.
        var query = db.Orders.Where(o =>
            (o.Status != OrderStatus.Created && o.Status != OrderStatus.Declined)
            || o.IikoOrderSyncStatus != IikoOrderSyncStatus.NotPushed);

        if (syncStatus.HasValue)
            query = query.Where(o => o.IikoOrderSyncStatus == syncStatus.Value);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.CreatedAt,
                o.Status,
                o.Total,
                o.PaymentStatus,
                LocationId = o.LocationId,
                Location = o.Location.Name,
                HasTerminalGroup = o.Location.IikoTerminalGroupId != null,
                CustomerPhone = o.CustomerUser.MobileNumber,
                ItemCount = o.Items.Sum(i => (int?)i.Quantity) ?? 0,
                o.IikoOrderSyncStatus,
                o.IikoOrderSyncPendingAction,
                o.IikoDeliveryOrderId,
            })
            .ToListAsync(ct);

        // Latest iiko audit note per order (e.g. why it was skipped), in one query.
        var ids = rows.Select(r => r.Id.ToString()).ToList();
        var notes = await db.AuditLogs
            .Where(a => a.EntityType == "Order" && a.Action.StartsWith("Iiko") && ids.Contains(a.EntityId!))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.EntityId, a.Action, a.Details, a.CreatedAt })
            .ToListAsync(ct);
        var latestNote = notes.GroupBy(n => n.EntityId!).ToDictionary(g => g.Key, g => g.First());

        var items = rows.Select(r =>
        {
            latestNote.TryGetValue(r.Id.ToString(), out var note);
            return new
            {
                r.Id,
                r.CreatedAt,
                status = r.Status.ToString(),
                r.Total,
                paymentStatus = r.PaymentStatus.ToString(),
                r.LocationId,
                r.Location,
                r.HasTerminalGroup,
                r.CustomerPhone,
                r.ItemCount,
                syncStatus = r.IikoOrderSyncStatus.ToString(),
                pendingAction = r.IikoOrderSyncPendingAction.ToString(),
                iikoOrderId = r.IikoDeliveryOrderId,
                lastEvent = note?.Action,
                lastEventDetails = note?.Details,
                lastEventAt = note?.CreatedAt,
            };
        });

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Audit trail of iiko actions (pushes, skips, closes, webhook problems, mapping edits).</summary>
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 30;

        var query = db.AuditLogs.Where(a => a.Action.StartsWith("Iiko"));
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new { a.Id, a.Action, a.EntityType, a.EntityId, a.Details, a.PerformedByUsername, a.CreatedAt })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }
}
