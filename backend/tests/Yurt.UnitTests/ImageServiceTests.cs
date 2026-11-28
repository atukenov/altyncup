using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Images;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class ImageServiceTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] Webp = [.."RIFF"u8, 0x24, 0, 0, 0, .."WEBP"u8, .."VP8 "u8];

    private static (ImageService Svc, IApplicationDbContext Db) Build()
    {
        var db = Substitute.For<IApplicationDbContext>();
        var set = new List<StoredImage>().BuildMockDbSet();
        db.StoredImages.Returns(set);
        return (new ImageService(db), db);
    }

    [Fact]
    public void DetectContentType_RecognisesJpegPngWebp()
    {
        Assert.Equal("image/jpeg", ImageService.DetectContentType(Jpeg));
        Assert.Equal("image/png", ImageService.DetectContentType(Png));
        Assert.Equal("image/webp", ImageService.DetectContentType(Webp));
    }

    [Fact]
    public void DetectContentType_RejectsOtherFiles()
    {
        Assert.Null(ImageService.DetectContentType("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8));
        Assert.Null(ImageService.DetectContentType("GIF89a"u8));
        Assert.Null(ImageService.DetectContentType([0xFF]));
    }

    [Fact]
    public async Task Upload_StoresImageWithDetectedType()
    {
        var (svc, db) = Build();

        var result = await svc.UploadAsync(Png);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        db.StoredImages.Received(1).Add(Arg.Is<StoredImage>(i =>
            i.ContentType == "image/png" && i.SizeBytes == Png.Length && i.Id == result.Data!.Id));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_RejectsNonImage()
    {
        var (svc, db) = Build();

        var result = await svc.UploadAsync("<html>hi</html>"u8.ToArray());

        Assert.False(result.Succeeded);
        Assert.Equal(415, result.StatusCode);
        db.StoredImages.DidNotReceive().Add(Arg.Any<StoredImage>());
    }

    [Fact]
    public async Task Upload_RejectsTooLarge()
    {
        var (svc, _) = Build();
        var big = new byte[ImageService.MaxBytes + 1];
        Jpeg.CopyTo(big, 0);

        var result = await svc.UploadAsync(big);

        Assert.False(result.Succeeded);
        Assert.Equal(413, result.StatusCode);
    }
}
