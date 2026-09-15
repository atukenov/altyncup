namespace Yurt.Application.Features.AppUpdate;

public class AppUpdateInfoDto
{
    public string MinVersionIos { get; set; } = string.Empty;
    public string MinVersionAndroid { get; set; } = string.Empty;
    public string StoreUrlIos { get; set; } = string.Empty;
    public string StoreUrlAndroid { get; set; } = string.Empty;
}
