using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// An image uploaded from the admin panel (menu items, promotions). The bytes live in
/// Postgres so they survive redeploys without extra storage; they are served by
/// GET /api/v1/images/{id} with long-lived cache headers. Rows are never updated:
/// replacing a picture uploads a new row, so the id-based URL can be cached forever.
/// </summary>
public class StoredImage : BaseEntity
{
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public int SizeBytes { get; set; }
}
