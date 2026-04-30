using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Orders.Services;

public class OrderService
{
    private readonly IApplicationDbContext _db;
    private readonly IOrdersHubService _hub;

    public OrderService(IApplicationDbContext db, IOrdersHubService hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<Result<OrderDto>> CreateOrderAsync(
        Guid customerId, CreateOrderDto dto, CancellationToken ct = default)
    {
        // Validate location
        var location = await _db.Locations
            .FirstOrDefaultAsync(l => l.Id == dto.LocationId && l.IsActive, ct);
        if (location == null)
            return Result<OrderDto>.Failure("Location not found or inactive.", 400);

        if (dto.Items == null || dto.Items.Count == 0)
            return Result<OrderDto>.Failure("Order must have at least one item.", 400);

        // Load menu items
        var itemIds = dto.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await _db.MenuItems
            .Where(m => itemIds.Contains(m.Id) && m.IsAvailable)
            .ToListAsync(ct);

        if (menuItems.Count != itemIds.Distinct().Count())
            return Result<OrderDto>.Failure("One or more menu items are unavailable.", 400);

        // Build order
        var order = new Order
        {
            CustomerUserId = customerId,
            LocationId = dto.LocationId,
            Status = OrderStatus.Created
        };

        var orderItems = dto.Items.Select(i =>
        {
            var menuItem = menuItems.First(m => m.Id == i.MenuItemId);
            var lineTotal = menuItem.Price * i.Quantity;
            return new OrderItem
            {
                OrderId = order.Id,
                MenuItemId = i.MenuItemId,
                MenuItemName = menuItem.Name,
                Quantity = i.Quantity,
                UnitPrice = menuItem.Price,
                LineTotal = lineTotal
            };
        }).ToList();

        order.Items = orderItems;
        order.Subtotal = orderItems.Sum(i => i.LineTotal);
        order.Total = order.Subtotal; // no tax for now

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Reload with location for DTO
        order.Location = location;
        await _hub.NotifyOrderCreatedAsync(order, ct);

        return Result<OrderDto>.Success(MapToDto(order), 201);
    }

    public async Task<Result<OrderDto>> GetOrderAsync(
        Guid orderId, Guid customerId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();
        if (order.CustomerUserId != customerId) return Result<OrderDto>.Forbidden();
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> GetOrderForAdminAsync(
        Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<List<OrderDto>> GetActiveOrdersAsync(
        Guid customerId, CancellationToken ct = default)
    {
        var activeStatuses = new[] { OrderStatus.Created, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready };
        return await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items)
            .Where(o => o.CustomerUserId == customerId && activeStatuses.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);
    }

    public async Task<List<OrderDto>> GetHistoryAsync(
        Guid customerId, CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items)
            .Where(o => o.CustomerUserId == customerId && o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);

    public async Task<List<OrderDto>> GetDeclinedOrdersAsync(
        Guid customerId, CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items)
            .Where(o => o.CustomerUserId == customerId && o.Status == OrderStatus.Declined)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);

    public async Task<List<OrderDto>> GetAdminOrdersAsync(
        OrderStatus? status, Guid? locationId, CancellationToken ct = default)
    {
        var query = _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items)
            .AsQueryable();

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (locationId.HasValue) query = query.Where(o => o.LocationId == locationId.Value);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);
    }

    public async Task<Result<OrderDto>> AcceptOrderAsync(
        Guid orderId, AcceptOrderDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        if (order.Status != OrderStatus.Created)
            return Result<OrderDto>.Failure("Order cannot be accepted in its current status.", 422);

        order.Status = OrderStatus.Accepted;
        order.EtaMinutes = dto.EtaMinutes;
        order.AcceptedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _hub.NotifyOrderUpdatedAsync(order, ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> DeclineOrderAsync(
        Guid orderId, DeclineOrderDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        if (order.Status != OrderStatus.Created)
            return Result<OrderDto>.Failure("Order cannot be declined in its current status.", 422);

        order.Status = OrderStatus.Declined;
        order.DeclineReason = dto.Reason;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _hub.NotifyOrderDeclinedAsync(order, ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> UpdateStatusAsync(
        Guid orderId, UpdateOrderStatusDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        var allowed = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Accepted] = [OrderStatus.Preparing],
            [OrderStatus.Preparing] = [OrderStatus.Ready],
            [OrderStatus.Ready] = [OrderStatus.Completed]
        };

        if (!allowed.TryGetValue(order.Status, out var next) || !next.Contains(dto.Status))
            return Result<OrderDto>.Failure(
                $"Cannot transition from {order.Status} to {dto.Status}.", 422);

        order.Status = dto.Status;
        if (dto.Status == OrderStatus.Completed) order.CompletedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _hub.NotifyOrderUpdatedAsync(order, ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> UpdatePaymentAsync(
        Guid orderId, UpdatePaymentDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        order.PaymentStatus = dto.PaymentStatus;
        order.PaymentMethod = dto.PaymentMethod;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _hub.NotifyPaymentUpdatedAsync(order, ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    private async Task<Order?> LoadOrderAsync(Guid id, CancellationToken ct)
        => await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public static OrderDto MapToDto(Order o)
        => new(
            o.Id,
            o.CustomerUserId,
            o.LocationId,
            o.Location?.Name ?? "",
            o.Status,
            o.DeclineReason,
            o.EtaMinutes,
            o.CreatedAt,
            o.AcceptedAt,
            o.CompletedAt,
            o.PaymentStatus,
            o.PaymentMethod,
            o.Subtotal,
            o.Total,
            o.Items.Select(i => new OrderItemDto(
                i.Id, i.MenuItemId, i.MenuItemName, i.Quantity, i.UnitPrice, i.LineTotal
            )).ToList());
}
