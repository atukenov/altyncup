using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.AppUpdate;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/app")]
public class AppController : ApiControllerBase
{
    private readonly AppUpdateOptions _options;

    public AppController(AppUpdateOptions options) => _options = options;

    /// <summary>
    /// Minimum required app version per platform, and where to send the user to update.
    /// Public — the client must be able to check this before any login screen renders.
    /// </summary>
    [HttpGet("update-info")]
    public IActionResult GetUpdateInfo()
        => Ok(new AppUpdateInfoDto
        {
            MinVersionIos = _options.MinVersionIos,
            MinVersionAndroid = _options.MinVersionAndroid,
            StoreUrlIos = _options.StoreUrlIos,
            StoreUrlAndroid = _options.StoreUrlAndroid,
        });
}
