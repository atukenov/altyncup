using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Orders.DTOs;

public record CreateOrderDto(Guid LocationId, List<OrderItemInputDto> Items);
public record OrderItemInputDto(Guid MenuItemId, int Quantity);

public record AcceptOrderDto(int EtaMinutes);
public record DeclineOrderDto(string Reason);
public record UpdateOrderStatusDto(OrderStatus Status);
public record UpdatePaymentDto(PaymentStatus PaymentStatus, PaymentMethod PaymentMethod);

public record OrderDto(
    Guid Id,
    Guid CustomerUserId,
    Guid LocationId,
    string LocationName,
    OrderStatus Status,
    string? DeclineReason,
    int? EtaMinutes,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    DateTime? CompletedAt,
    PaymentStatus PaymentStatus,
    PaymentMethod? PaymentMethod,
    decimal Subtotal,
    decimal Total,
    List<OrderItemDto> Items);

public record OrderItemDto(
    Guid Id,
    Guid MenuItemId,
    string MenuItemName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
