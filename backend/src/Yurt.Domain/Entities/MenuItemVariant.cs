using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuItemVariant : BaseEntity
{
    public Guid MenuItemId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? LabelRu { get; set; }
    public string? LabelKk { get; set; }
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }

    // iiko mapping (order-push feature). A variant can point at its own iiko product (e.g. a
    // separate "Americano 350" product); otherwise the parent item's product is used, with
    // IikoProductSizeId selecting a size on that product's scale.
    public Guid? IikoProductId { get; set; }
    public Guid? IikoProductSizeId { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
}
