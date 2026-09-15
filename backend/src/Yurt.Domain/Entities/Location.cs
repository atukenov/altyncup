using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class Location : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // iiko terminal group this location's orders are pushed to. Null = don't push
    // orders for this location (fail closed, same principle as an unmapped menu item).
    public Guid? IikoTerminalGroupId { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
