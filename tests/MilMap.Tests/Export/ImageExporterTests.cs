using MilMap.Core.Export;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Export;

public class ImageExporterTests
{
    private static SKBitmap CreateTestBitmap(int width = 200, int height = 150)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightBlue);

        using var paint = new SKPaint { Color = SKColors.Red };
        canvas.DrawCircle(width / 2, height / 2, 50, paint);

        return bitmap;
    }

    [Fact]
    public void ExportToBytes_Png_ReturnsPngBytes()
    {
        using var bitmap = CreateTestBitmap();
        var exporter = new ImageExporter(new ImageExportOptions { Format = ImageFormat.Png });

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PNG header: 89 50 4E 47
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Fact]
    public void ExportToBytes_Jpeg_ReturnsValidImage()
    {
        using var bitmap = CreateTestBitmap();
        var exporter = new ImageExporter(new ImageExportOptions { Format = ImageFormat.Jpeg });

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void ExportToBytes_Webp_ReturnsValidImage()
    {
        using var bitmap = CreateTestBitmap();
        var exporter = new ImageExporter(new ImageExportOptions { Format = ImageFormat.Webp });

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void ExportToBytes_NullBitmap_ThrowsArgumentNullException()
    {
        var exporter = new ImageExporter();

        Assert.Throws<ArgumentNullException>(() => exporter.ExportToBytes(null!));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(95)]
    [InlineData(100)]
    public void ExportToBytes_VariousQuality_ReturnsValidImage(int quality)
    {
        using var bitmap = CreateTestBitmap();
        var exporter = new ImageExporter(new ImageExportOptions
        {
            Format = ImageFormat.Jpeg,
            Quality = quality
        });

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void ExportToBytes_LowerQuality_SmallerFile()
    {
        using var bitmap = CreateTestBitmap();
        var lowQuality = new ImageExporter(new ImageExportOptions { Format = ImageFormat.Jpeg, Quality = 10 });
        var highQuality = new ImageExporter(new ImageExportOptions { Format = ImageFormat.Jpeg, Quality = 100 });

        var lowBytes = lowQuality.ExportToBytes(bitmap);
        var highBytes = highQuality.ExportToBytes(bitmap);

        Assert.True(lowBytes.Length < highBytes.Length);
    }

    [Theory]
    [InlineData(ImageFormat.Png, ".png")]
    [InlineData(ImageFormat.Jpeg, ".jpg")]
    [InlineData(ImageFormat.Webp, ".webp")]
    public void GetFileExtension_ReturnsCorrectExtension(ImageFormat format, string expectedExtension)
    {
        var exporter = new ImageExporter(new ImageExportOptions { Format = format });

        var extension = exporter.GetFileExtension();

        Assert.Equal(expectedExtension, extension);
    }

    [Theory]
    [InlineData(ImageFormat.Png, "image/png")]
    [InlineData(ImageFormat.Jpeg, "image/jpeg")]
    [InlineData(ImageFormat.Webp, "image/webp")]
    public void GetMimeType_ReturnsCorrectMimeType(ImageFormat format, string expectedMimeType)
    {
        var exporter = new ImageExporter(new ImageExportOptions { Format = format });

        var mimeType = exporter.GetMimeType();

        Assert.Equal(expectedMimeType, mimeType);
    }

    [Fact]
    public void Resize_ValidInput_ReturnsResizedBitmap()
    {
        using var bitmap = CreateTestBitmap(200, 150);

        using var resized = ImageExporter.Resize(bitmap, 100, 75);

        Assert.Equal(100, resized.Width);
        Assert.Equal(75, resized.Height);
    }

    [Fact]
    public void Resize_NullBitmap_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ImageExporter.Resize(null!, 100, 100));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    public void Resize_InvalidDimensions_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        using var bitmap = CreateTestBitmap();

        Assert.Throws<ArgumentOutOfRangeException>(() => ImageExporter.Resize(bitmap, width, height));
    }

    [Fact]
    public void Scale_ValidInput_ReturnsScaledBitmap()
    {
        using var bitmap = CreateTestBitmap(100, 100);

        using var scaled = ImageExporter.Scale(bitmap, 2.0f);

        Assert.Equal(200, scaled.Width);
        Assert.Equal(200, scaled.Height);
    }

    [Fact]
    public void Scale_HalfSize_ReturnsHalfSizeBitmap()
    {
        using var bitmap = CreateTestBitmap(200, 100);

        using var scaled = ImageExporter.Scale(bitmap, 0.5f);

        Assert.Equal(100, scaled.Width);
        Assert.Equal(50, scaled.Height);
    }

    [Fact]
    public void Scale_ZeroScale_ThrowsArgumentOutOfRangeException()
    {
        using var bitmap = CreateTestBitmap();

        Assert.Throws<ArgumentOutOfRangeException>(() => ImageExporter.Scale(bitmap, 0));
    }

    [Fact]
    public void Export_CreatesFile()
    {
        using var bitmap = CreateTestBitmap();
        var exporter = new ImageExporter();

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_image_{Guid.NewGuid()}.png");
        try
        {
            exporter.Export(bitmap, tempPath);

            Assert.True(File.Exists(tempPath));
            var fileInfo = new FileInfo(tempPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExportToStream_WritesToStream()
    {
        using var bitmap = CreateTestBitmap();
        var exporter = new ImageExporter();
        using var stream = new MemoryStream();

        exporter.ExportToStream(bitmap, stream);

        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void ImageExportOptions_HasCorrectDefaults()
    {
        var options = new ImageExportOptions();

        Assert.Equal(ImageFormat.Png, options.Format);
        Assert.Equal(95, options.Quality);
        Assert.Equal(300, options.Dpi);
        Assert.False(options.IncludeAlpha);
        Assert.Equal(SKColors.White, options.BackgroundColor);
    }

    [Fact]
    public void ImageExporter_CanBeCreatedWithDefaultOptions()
    {
        var exporter = new ImageExporter();
        Assert.NotNull(exporter);
    }

    [Fact]
    public void ImageExporter_CanBeCreatedWithCustomOptions()
    {
        var options = new ImageExportOptions
        {
            Format = ImageFormat.Jpeg,
            Quality = 80,
            Dpi = 150
        };

        var exporter = new ImageExporter(options);
        Assert.NotNull(exporter);
    }
}
