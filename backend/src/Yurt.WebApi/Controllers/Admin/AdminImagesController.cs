using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Images;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

public record UploadImageResponse(Guid Id, string Url);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/images")]
[Authorize(Policy = "AdminRoleAdmin")]
public class AdminImagesController : ApiControllerBase
{
    private readonly ImageService _images;
    private readonly IConfiguration _config;

    public AdminImagesController(ImageService images, IConfiguration config)
    {
        _images = images;
        _config = config;
    }

    /// <summary>
    /// Upload a menu or promotion picture (multipart field "file"). Returns the public URL
    /// to save in the item's ImageUrl, so the customer apps need no changes.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(ImageService.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageService.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return ValidationError("Choose an image file to upload.");
        if (file.Length > ImageService.MaxBytes)
            return StatusCode(413, new ProblemDetails { Status = 413, Title = "The image must be 5 MB or smaller." });

        using var ms = new MemoryStream((int)file.Length);
        await file.CopyToAsync(ms, ct);

        var result = await _images.UploadAsync(ms.ToArray(), ct);
        if (!result.Succeeded)
            return ToResult(result);

        var id = result.Data!.Id;
        return StatusCode(201, new UploadImageResponse(id, $"{PublicBaseUrl()}/api/v1/images/{id}"));
    }

    // Images:PublicBaseUrl wins when set; otherwise use the address the request came in on
    // (Railway forwards the original host and https scheme, which UseForwardedHeaders applies).
    private string PublicBaseUrl()
    {
        var configured = _config["Images:PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(configured)
            ? $"{Request.Scheme}://{Request.Host}"
            : configured.TrimEnd('/');
    }
}
