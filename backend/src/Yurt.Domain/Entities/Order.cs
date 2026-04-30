using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class Order : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public Guid LocationId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public string? DeclineReason { get; set; }
    public int? EtaMinutes { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public PaymentMethod? PaymentMethod { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }

    public CustomerUser CustomerUser { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
