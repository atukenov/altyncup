using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Images;

public record UploadedImageDto(Guid Id);

public record StoredImageContent(string ContentType, byte[] Data);

/// <summary>Stores admin-uploaded images (menu items, promotions) in the database.</summary>
public class ImageService
{
    /// <summary>The admin panel shrinks photos before upload, so real files are a few hundred KB.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    private readonly IApplicationDbContext _db;

    public ImageService(IApplicationDbContext db) => _db = db;

    public async Task<Result<UploadedImageDto>> UploadAsync(byte[] data, CancellationToken ct = default)
    {
        if (data.Length == 0)
            return Result<UploadedImageDto>.Failure("The file is empty.");
        if (data.Length > MaxBytes)
            return Result<UploadedImageDto>.Failure("The image must be 5 MB or smaller.", 413);

        // Trust the bytes, not the file name or the browser's Content-Type.
        var contentType = DetectContentType(data);
        if (contentType is null)
            return Result<UploadedImageDto>.Failure("Only JPEG, PNG or WebP images can be uploaded.", 415);

        var image = new StoredImage { ContentType = contentType, Data = data, SizeBytes = data.Length };
        _db.StoredImages.Add(image);
        await _db.SaveChangesAsync(ct);
        return Result<UploadedImageDto>.Success(new UploadedImageDto(image.Id), 201);
    }

    public async Task<StoredImageContent?> GetAsync(Guid id, CancellationToken ct = default)
        => await _db.StoredImages.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new StoredImageContent(i.ContentType, i.Data))
            .FirstOrDefaultAsync(ct);

    public static string? DetectContentType(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return "image/jpeg";
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }
}
