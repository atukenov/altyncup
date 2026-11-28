using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class ImageUploadTests(YurtWebAppFactory factory)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Admin_UploadsImage_AndAnyoneCanFetchItWithLongCache()
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client));

        var res = await client.PostAsync("/api/v1/admin/images", Form(Png, "latte.png", "image/png"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("url").GetString()!;
        Assert.EndsWith($"/api/v1/images/{body.GetProperty("id").GetGuid()}", url);

        var anon = factory.CreateClient();
        var img = await anon.GetAsync(new Uri(url).PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, img.StatusCode);
        Assert.Equal("image/png", img.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await img.Content.ReadAsByteArrayAsync());
        Assert.Contains("immutable", img.Headers.CacheControl?.ToString());

        var cached = new HttpRequestMessage(HttpMethod.Get, new Uri(url).PathAndQuery);
        cached.Headers.IfNoneMatch.Add(img.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await anon.SendAsync(cached)).StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsFilesThatAreNotImages()
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client));

        var res = await client.PostAsync("/api/v1/admin/images",
            Form("<svg onload=alert(1)>"u8.ToArray(), "x.png", "image/png"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, res.StatusCode);
    }

    [Fact]
    public async Task Worker_And_Anonymous_CannotUpload()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsync("/api/v1/admin/images", Form(Png, "a.png", "image/png"))).StatusCode);

        var worker = factory.CreateClient();
        ApiHelpers.Authorize(worker, await ApiHelpers.CreateAdminTokenAsync(
            factory.Services, worker, username: "imageworker", password: "Worker@1234", role: AdminRole.Worker));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await worker.PostAsync("/api/v1/admin/images", Form(Png, "a.png", "image/png"))).StatusCode);
    }

    [Fact]
    public async Task UnknownImage_Returns404()
    {
        var res = await factory.CreateClient().GetAsync($"/api/v1/images/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
