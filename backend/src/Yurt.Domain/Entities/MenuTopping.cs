using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuTopping : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;

    public ICollection<MenuToppingCategory> ToppingCategories { get; set; } = new List<MenuToppingCategory>();
}
