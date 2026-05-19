using Microsoft.AspNetCore.SignalR;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Application.Features.Orders.Services;

namespace Yurt.Infrastructure.Hubs;

public class OrdersHubService : IOrdersHubService
{
    private readonly IHubContext<OrdersHub> _hubContext;

    public OrdersHubService(IHubContext<OrdersHub> hubContext)
        => _hubContext = hubContext;

    public async Task NotifyOrderCreatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        // Notify all admins
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("OrderCreated", dto, ct);
        // Notify customer
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("OrderUpdated", dto, ct);
    }

    public async Task NotifyOrderUpdatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("OrderUpdated", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("OrderUpdated", dto, ct);
    }

    public async Task NotifyOrderDeclinedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("OrderDeclined", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("OrderDeclined", dto, ct);
    }

    public async Task NotifyPaymentUpdatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("PaymentUpdated", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentUpdated", dto, ct);
    }

    public async Task NotifyPaymentPendingAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("PaymentPending", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentPending", dto, ct);
    }

    public async Task NotifyPaymentSucceededAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("PaymentSucceeded", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentSucceeded", dto, ct);
    }

    public async Task NotifyPaymentFailedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("PaymentFailed", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentFailed", dto, ct);
    }
}
