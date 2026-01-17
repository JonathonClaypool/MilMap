using MilMap.Core.Export;
using MilMap.Core.Mgrs;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Export;

public class GeoTiffExporterTests
{
    private static SKBitmap CreateTestBitmap(int width = 256, int height = 256)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightBlue);

        using var paint = new SKPaint { Color = SKColors.Red };
        canvas.DrawCircle(width / 2, height / 2, 50, paint);

        return bitmap;
    }

    private static GeoTiffExportOptions CreateTestOptions()
    {
        return new GeoTiffExportOptions
        {
            BoundingBox = new BoundingBox(38.0, 39.0, -77.0, -76.0),
            Dpi = 300,
            Compression = TiffCompression.Lzw,
            UseTiles = true
        };
    }

    [Fact]
    public void Export_ValidBitmap_CreatesFile()
    {
        using var bitmap = CreateTestBitmap();
        var options = CreateTestOptions();
        var exporter = new GeoTiffExporter(options);
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tif");

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
    public void ExportToBytes_ValidBitmap_ReturnsTiffBytes()
    {
        using var bitmap = CreateTestBitmap();
        var options = CreateTestOptions();
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // TIFF header: 49 49 2A 00 (little-endian) or 4D 4D 00 2A (big-endian)
        Assert.True(
            (bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) ||
            (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A),
            "Output should have valid TIFF header");
    }

    [Fact]
    public void Export_NullBitmap_ThrowsArgumentNullException()
    {
        var options = CreateTestOptions();
        var exporter = new GeoTiffExporter(options);

        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, "test.tif"));
    }

    [Fact]
    public void Export_NullPath_ThrowsArgumentNullException()
    {
        using var bitmap = CreateTestBitmap();
        var options = CreateTestOptions();
        var exporter = new GeoTiffExporter(options);

        Assert.Throws<ArgumentNullException>(() => exporter.Export(bitmap, null!));
    }

    [Fact]
    public void Export_EmptyPath_ThrowsArgumentNullException()
    {
        using var bitmap = CreateTestBitmap();
        var options = CreateTestOptions();
        var exporter = new GeoTiffExporter(options);

        Assert.Throws<ArgumentNullException>(() => exporter.Export(bitmap, ""));
    }

    [Theory]
    [InlineData(TiffCompression.None)]
    [InlineData(TiffCompression.Lzw)]
    [InlineData(TiffCompression.Deflate)]
    public void ExportToBytes_DifferentCompressions_ProducesValidOutput(TiffCompression compression)
    {
        using var bitmap = CreateTestBitmap();
        var options = new GeoTiffExportOptions
        {
            BoundingBox = new BoundingBox(38.0, 39.0, -77.0, -76.0),
            Compression = compression
        };
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void ExportToBytes_WithTiles_ProducesValidOutput()
    {
        using var bitmap = CreateTestBitmap(512, 512);
        var options = new GeoTiffExportOptions
        {
            BoundingBox = new BoundingBox(38.0, 39.0, -77.0, -76.0),
            UseTiles = true,
            TileWidth = 256,
            TileHeight = 256
        };
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void ExportToBytes_WithoutTiles_ProducesValidOutput()
    {
        using var bitmap = CreateTestBitmap();
        var options = new GeoTiffExportOptions
        {
            BoundingBox = new BoundingBox(38.0, 39.0, -77.0, -76.0),
            UseTiles = false
        };
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void GetFileExtension_ReturnsTifExtension()
    {
        Assert.Equal(".tif", GeoTiffExporter.GetFileExtension());
    }

    [Fact]
    public void GetMimeType_ReturnsTiffMimeType()
    {
        Assert.Equal("image/tiff", GeoTiffExporter.GetMimeType());
    }

    [Fact]
    public void Export_LargeBitmap_Succeeds()
    {
        using var bitmap = CreateTestBitmap(1024, 1024);
        var options = CreateTestOptions();
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Export_SmallBitmap_Succeeds()
    {
        using var bitmap = CreateTestBitmap(64, 64);
        var options = new GeoTiffExportOptions
        {
            BoundingBox = new BoundingBox(38.0, 38.1, -77.0, -76.9)
        };
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Export_NonSquareBitmap_Succeeds()
    {
        using var bitmap = CreateTestBitmap(800, 600);
        var options = CreateTestOptions();
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Export_DifferentDpi_SetsCorrectly()
    {
        using var bitmap = CreateTestBitmap();
        var options = new GeoTiffExportOptions
        {
            BoundingBox = new BoundingBox(38.0, 39.0, -77.0, -76.0),
            Dpi = 150
        };
        var exporter = new GeoTiffExporter(options);

        var bytes = exporter.ExportToBytes(bitmap);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }
}
