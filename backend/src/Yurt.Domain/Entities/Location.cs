using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class Location : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameRu { get; set; }
    public string? NameKk { get; set; }
    public string Address { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
