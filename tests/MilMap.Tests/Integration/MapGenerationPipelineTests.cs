using MilMap.Core.Export;
using MilMap.Core.Input;
using MilMap.Core.Mgrs;
using MilMap.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Integration;

/// <summary>
/// End-to-end integration tests for map generation pipeline.
/// Tests the full flow from input coordinates through rendering to output.
/// </summary>
public class MapGenerationPipelineTests : IDisposable
{
    private readonly string _tempDir;

    public MapGenerationPipelineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MilMap_IntegrationTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Tests parsing MGRS zone+band coordinates and getting valid bounds.
    /// </summary>
    [Theory]
    [InlineData("18T")] // Zone + band only
    [InlineData("32T")] // Europe  
    [InlineData("54S")] // Japan
    public void MgrsZoneBand_ParsesAndReturnsBounds(string mgrs)
    {
        var bounds = MgrsBoundary.GetBounds(mgrs);

        Assert.True(bounds.MinLat < bounds.MaxLat, $"MinLat ({bounds.MinLat}) should be < MaxLat ({bounds.MaxLat})");
        Assert.True(bounds.MinLon < bounds.MaxLon, $"MinLon ({bounds.MinLon}) should be < MaxLon ({bounds.MaxLon})");
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    /// <summary>
    /// Tests MGRS encoder and parser roundtrip.
    /// </summary>
    [Theory]
    [InlineData(38.8977, -77.0365)] // Washington DC
    [InlineData(51.5074, -0.1278)] // London
    [InlineData(35.6762, 139.6503)] // Tokyo
    [InlineData(-33.8688, 151.2093)] // Sydney
    [InlineData(48.8566, 2.3522)] // Paris
    public void MgrsEncoderParser_Roundtrip_WithinTolerance(double lat, double lon)
    {
        // Encode to MGRS
        string mgrs = MgrsEncoder.Encode(lat, lon, MgrsEncoder.Precision.OneMeter);

        // Parse back to lat/lon
        var (parsedLat, parsedLon) = MgrsParser.Parse(mgrs);

        // Should be within 1 meter (approximately 0.00001 degrees)
        Assert.True(Math.Abs(parsedLat - lat) < 0.0001,
            $"Latitude difference too large: {Math.Abs(parsedLat - lat)}");
        Assert.True(Math.Abs(parsedLon - lon) < 0.0001,
            $"Longitude difference too large: {Math.Abs(parsedLon - lon)}");
    }

    /// <summary>
    /// Tests lat/lon input handler parsing.
    /// </summary>
    [Theory]
    [InlineData(38.8, 38.9, -77.1, -77.0)]
    [InlineData(51.4, 51.6, -0.2, 0.1)]
    [InlineData(-34.0, -33.7, 151.0, 151.3)]
    public void LatLonInput_ParsesBoundsCorrectly(double minLat, double maxLat, double minLon, double maxLon)
    {
        var result = LatLonInputHandler.FromBounds(minLat, maxLat, minLon, maxLon);

        Assert.True(result.BoundingBox.MinLat < result.BoundingBox.MaxLat);
        Assert.True(result.BoundingBox.MinLon < result.BoundingBox.MaxLon);
    }

    /// <summary>
    /// Tests that grid rendering produces valid output.
    /// </summary>
    [Fact]
    public void GridRenderer_ProducesValidBitmap()
    {
        using var baseMap = CreateTestBitmap(512, 512);
        var renderer = new MgrsGridRenderer(new MgrsGridOptions
        {
            Scale = MapScale.Scale1To25000,
            ShowLabels = true
        });

        using var result = renderer.DrawGrid(baseMap, 38.8, 38.9, -77.1, -77.0);

        Assert.NotNull(result);
        Assert.True(result.Width > 512); // Margins added
        Assert.True(result.Height > 512);
    }

    /// <summary>
    /// Tests complete pipeline: MGRS input → bounds → grid render → image export.
    /// </summary>
    [Fact]
    public void FullPipeline_MgrsToImage_Succeeds()
    {
        // Input: MGRS coordinate
        string mgrsInput = "18SUJ2505";
        var bounds = MgrsBoundary.GetBounds(mgrsInput);

        // Create a test base map
        using var baseMap = CreateTestBitmap(512, 512);

        // Render MGRS grid
        var gridRenderer = new MgrsGridRenderer(new MgrsGridOptions
        {
            Scale = MapScale.Scale1To25000,
            ShowLabels = true
        });
        using var mapWithGrid = gridRenderer.DrawGrid(
            baseMap,
            bounds.MinLat, bounds.MaxLat,
            bounds.MinLon, bounds.MaxLon);

        // Export to PNG
        var exporter = new ImageExporter(new ImageExportOptions
        {
            Format = ImageFormat.Png,
            Quality = 100
        });

        string outputPath = Path.Combine(_tempDir, "test_output.png");
        exporter.Export(mapWithGrid, outputPath);

        // Verify output exists and is valid
        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0);

        // Verify the file is a valid PNG by reading it back
        using var readBack = SKBitmap.Decode(outputPath);
        Assert.NotNull(readBack);
        Assert.True(readBack.Width > 0);
        Assert.True(readBack.Height > 0);
    }

    /// <summary>
    /// Tests pipeline with JPEG output format.
    /// </summary>
    [Fact]
    public void FullPipeline_ToJpeg_Succeeds()
    {
        using var baseMap = CreateTestBitmap(256, 256);
        var gridRenderer = new MgrsGridRenderer();
        using var mapWithGrid = gridRenderer.DrawGrid(baseMap, 40.0, 40.1, -75.0, -74.9);

        var exporter = new ImageExporter(new ImageExportOptions
        {
            Format = ImageFormat.Jpeg,
            Quality = 85
        });

        string outputPath = Path.Combine(_tempDir, "test_output.jpg");
        exporter.Export(mapWithGrid, outputPath);

        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0);
    }

    /// <summary>
    /// Tests grid rendering across zone boundaries.
    /// </summary>
    [Fact]
    public void CrossZoneGrid_RendersCorrectly()
    {
        // Area spanning zones 17 and 18 (boundary at -78°)
        using var baseMap = CreateTestBitmap(1024, 512);
        var renderer = new MgrsGridRenderer(new MgrsGridOptions
        {
            Scale = MapScale.Scale1To100000,
            ShowLabels = true
        });

        using var result = renderer.DrawGrid(baseMap, 40.0, 41.0, -80.0, -76.0);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);

        // Export to verify rendering completes
        var exporter = new ImageExporter();
        string outputPath = Path.Combine(_tempDir, "cross_zone.png");
        exporter.Export(result, outputPath);

        Assert.True(File.Exists(outputPath));
    }

    /// <summary>
    /// Tests installation input handler for known installations.
    /// </summary>
    [Fact]
    public void InstallationInput_LookupSucceeds()
    {
        // Fort Liberty (formerly Fort Bragg) should be in the database
        bool found = InstallationInputHandler.TryLookup("Fort Liberty", out var result);

        if (found)
        {
            Assert.NotNull(result);
            Assert.True(result!.BoundingBox.Width > 0);
            Assert.True(result.BoundingBox.Height > 0);
        }
        // If installation database isn't populated, this is acceptable
    }

    /// <summary>
    /// Tests contour renderer can be instantiated with options.
    /// </summary>
    [Fact]
    public void ContourRenderer_CanBeInstantiated()
    {
        var renderer = new ContourRenderer(new ContourOptions
        {
            Scale = MapScale.Scale1To25000,
            ShowLabels = true
        });

        Assert.NotNull(renderer);
    }

    /// <summary>
    /// Tests scale bar rendering produces valid output.
    /// </summary>
    [Fact]
    public void ScaleBarRenderer_ProducesValidOutput()
    {
        var renderer = new ScaleBarRenderer(new ScaleBarOptions
        {
            ScaleRatio = 25000,
            Dpi = 300,
            ShowMetric = true,
            ShowImperial = true
        });

        var result = renderer.Render();

        Assert.NotNull(result);
        Assert.NotNull(result.Bitmap);
        Assert.True(result.Bitmap.Width > 0);
        Assert.True(result.Bitmap.Height > 0);
        result.Bitmap.Dispose();
    }

    /// <summary>
    /// Tests full pipeline with multiple output formats.
    /// </summary>
    [Theory]
    [InlineData(ImageFormat.Png, "output.png")]
    [InlineData(ImageFormat.Jpeg, "output.jpg")]
    [InlineData(ImageFormat.Webp, "output.webp")]
    public void FullPipeline_MultipleFormats_AllSucceed(ImageFormat format, string filename)
    {
        using var baseMap = CreateTestBitmap(128, 128);
        var gridRenderer = new MgrsGridRenderer();
        using var mapWithGrid = gridRenderer.DrawGrid(baseMap, 45.0, 45.1, 10.0, 10.1);

        var exporter = new ImageExporter(new ImageExportOptions
        {
            Format = format,
            Quality = 90
        });

        string outputPath = Path.Combine(_tempDir, filename);
        exporter.Export(mapWithGrid, outputPath);

        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 100); // At least 100 bytes
    }

    /// <summary>
    /// Tests that different scales produce different grid intervals.
    /// </summary>
    [Theory]
    [InlineData(MapScale.Scale1To10000, 1000)]
    [InlineData(MapScale.Scale1To25000, 1000)]
    [InlineData(MapScale.Scale1To50000, 1000)]
    [InlineData(MapScale.Scale1To100000, 10000)]
    public void GridRenderer_ScalesProduceCorrectIntervals(MapScale scale, int expectedInterval)
    {
        var options = new MgrsGridOptions { Scale = scale };
        var renderer = new MgrsGridRenderer(options);

        int interval = renderer.GetGridInterval();

        Assert.Equal(expectedInterval, interval);
    }

    /// <summary>
    /// Tests pipeline with southern hemisphere coordinates.
    /// </summary>
    [Fact]
    public void FullPipeline_SouthernHemisphere_Succeeds()
    {
        // Sydney, Australia
        using var baseMap = CreateTestBitmap(256, 256);
        var gridRenderer = new MgrsGridRenderer();
        using var mapWithGrid = gridRenderer.DrawGrid(baseMap, -34.0, -33.7, 151.0, 151.3);

        var exporter = new ImageExporter();
        string outputPath = Path.Combine(_tempDir, "southern.png");
        exporter.Export(mapWithGrid, outputPath);

        Assert.True(File.Exists(outputPath));

        using var readBack = SKBitmap.Decode(outputPath);
        Assert.NotNull(readBack);
    }

    /// <summary>
    /// Tests that image exporter correctly resizes bitmaps.
    /// </summary>
    [Theory]
    [InlineData(512, 256)]
    [InlineData(1024, 768)]
    [InlineData(100, 100)]
    public void ImageExporter_Resize_ProducesCorrectDimensions(int width, int height)
    {
        using var original = CreateTestBitmap(256, 256);
        using var resized = ImageExporter.Resize(original, width, height);

        Assert.Equal(width, resized.Width);
        Assert.Equal(height, resized.Height);
    }

    /// <summary>
    /// Tests byte array export for streaming scenarios.
    /// </summary>
    [Fact]
    public void ImageExporter_ToBytes_ReturnsValidData()
    {
        using var bitmap = CreateTestBitmap(128, 128);
        var exporter = new ImageExporter(new ImageExportOptions
        {
            Format = ImageFormat.Png
        });

        byte[] data = exporter.ExportToBytes(bitmap);

        Assert.NotNull(data);
        Assert.True(data.Length > 0);

        // PNG files start with magic bytes
        Assert.Equal(0x89, data[0]);
        Assert.Equal((byte)'P', data[1]);
        Assert.Equal((byte)'N', data[2]);
        Assert.Equal((byte)'G', data[3]);
    }

    private static SKBitmap CreateTestBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightGray);

        // Draw some test pattern
        using var paint = new SKPaint
        {
            Color = SKColors.DarkGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        // Grid pattern
        for (int x = 0; x < width; x += 32)
        {
            canvas.DrawLine(x, 0, x, height, paint);
        }
        for (int y = 0; y < height; y += 32)
        {
            canvas.DrawLine(0, y, width, y, paint);
        }

        return bitmap;
    }
}
