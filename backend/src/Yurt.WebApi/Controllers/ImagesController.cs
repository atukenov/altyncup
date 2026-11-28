using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Asp.Versioning;
using Yurt.Application.Features.Images;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/images")]
[AllowAnonymous]
public class ImagesController : ApiControllerBase
{
    private readonly ImageService _images;

    public ImagesController(ImageService images) => _images = images;

    /// <summary>Serve an uploaded image. Images never change once stored, so caches may keep them forever.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var etag = new EntityTagHeaderValue($"\"{id:N}\"");
        if (Request.Headers.IfNoneMatch.Any(v => v == etag.Tag.Value))
            return StatusCode(StatusCodes.Status304NotModified);

        var image = await _images.GetAsync(id, ct);
        if (image is null)
            return NotFound();

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(image.Data, image.ContentType, lastModified: null, entityTag: etag);
    }
}
