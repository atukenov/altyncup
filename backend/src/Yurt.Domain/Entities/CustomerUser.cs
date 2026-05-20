using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class CustomerUser : BaseEntity
{
    public string MobileNumber { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
