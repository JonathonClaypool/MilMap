using MilMap.Core.Rendering;
using MilMap.Core.Tiles;
using Xunit;

namespace MilMap.Tests.Rendering;

public class BaseMapRendererTests
{
    private static byte[] CreateTestPngTile()
    {
        // Create a simple 256x256 PNG image using SkiaSharp
        using var bitmap = new SkiaSharp.SKBitmap(256, 256);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.LightGray);

        // Draw a simple grid pattern
        using var paint = new SkiaSharp.SKPaint
        {
            Color = SkiaSharp.SKColors.DarkGray,
            StrokeWidth = 1
        };

        for (int i = 0; i <= 256; i += 32)
        {
            canvas.DrawLine(i, 0, i, 256, paint);
            canvas.DrawLine(0, i, 256, i, paint);
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void RenderMap_SingleTile_ReturnsValidImage()
    {
        using var renderer = new BaseMapRenderer();
        var tileData = CreateTestPngTile();
        var tiles = new List<TileData>
        {
            new TileData(0, 0, 10, tileData)
        };

        // Use a bounding box that roughly corresponds to tile 0,0 at zoom 10
        var result = renderer.RenderMap(tiles, -85, 85, -180, -179.6, 10);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // PNG header check
        Assert.Equal(0x89, result[0]);
        Assert.Equal(0x50, result[1]); // 'P'
        Assert.Equal(0x4E, result[2]); // 'N'
        Assert.Equal(0x47, result[3]); // 'G'
    }

    [Fact]
    public void RenderMap_MultipleTiles_StitchesTogether()
    {
        using var renderer = new BaseMapRenderer();
        var tileData = CreateTestPngTile();

        var tiles = new List<TileData>
        {
            new TileData(0, 0, 2, tileData),
            new TileData(1, 0, 2, tileData),
            new TileData(0, 1, 2, tileData),
            new TileData(1, 1, 2, tileData)
        };

        var result = renderer.RenderMap(tiles, -85, 85, -180, 180, 2);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void RenderMap_NoTiles_ThrowsArgumentException()
    {
        using var renderer = new BaseMapRenderer();
        var tiles = new List<TileData>();

        Assert.Throws<ArgumentException>(() =>
            renderer.RenderMap(tiles, 40, 50, -1, 1, 10));
    }

    [Fact]
    public void RenderMap_NullTiles_ThrowsArgumentException()
    {
        using var renderer = new BaseMapRenderer();

        Assert.Throws<ArgumentException>(() =>
            renderer.RenderMap(null!, 40, 50, -1, 1, 10));
    }

    [Fact]
    public void RenderMapAsJpeg_ReturnsValidImage()
    {
        using var renderer = new BaseMapRenderer();
        var tileData = CreateTestPngTile();
        var tiles = new List<TileData>
        {
            new TileData(0, 0, 10, tileData)
        };

        var result = renderer.RenderMapAsJpeg(tiles, -85, 85, -180, -179.6, 10);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // Check for valid image format (either JPEG or PNG fallback)
        bool isJpeg = result[0] == 0xFF && result[1] == 0xD8;
        bool isPng = result[0] == 0x89 && result[1] == 0x50;
        Assert.True(isJpeg || isPng, "Result should be either JPEG or PNG format");
    }

    [Fact]
    public void CalculateOutputDimensions_ReturnsPositiveDimensions()
    {
        using var renderer = new BaseMapRenderer();

        var (width, height) = renderer.CalculateOutputDimensions(40, 41, -75, -74, 10);

        Assert.True(width > 0);
        Assert.True(height > 0);
    }

    [Fact]
    public void CalculateOutputDimensions_LargerArea_ReturnsLargerDimensions()
    {
        using var renderer = new BaseMapRenderer();

        var (smallWidth, smallHeight) = renderer.CalculateOutputDimensions(40, 40.5, -75, -74.5, 10);
        var (largeWidth, largeHeight) = renderer.CalculateOutputDimensions(40, 41, -75, -74, 10);

        Assert.True(largeWidth >= smallWidth);
        Assert.True(largeHeight >= smallHeight);
    }

    [Fact]
    public void CalculateOutputDimensions_HigherZoom_ReturnsLargerDimensions()
    {
        using var renderer = new BaseMapRenderer();

        var (lowZoomWidth, lowZoomHeight) = renderer.CalculateOutputDimensions(40, 41, -75, -74, 8);
        var (highZoomWidth, highZoomHeight) = renderer.CalculateOutputDimensions(40, 41, -75, -74, 12);

        Assert.True(highZoomWidth > lowZoomWidth);
        Assert.True(highZoomHeight > lowZoomHeight);
    }

    [Fact]
    public void MapRenderOptions_HasCorrectDefaults()
    {
        var options = new MapRenderOptions();

        Assert.Equal(300, options.Dpi);
        Assert.Equal(95, options.JpegQuality);
        Assert.Equal(6, options.PngCompressionLevel);
    }

    [Fact]
    public void BaseMapRenderer_CanBeCreatedWithDefaultOptions()
    {
        using var renderer = new BaseMapRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void BaseMapRenderer_CanBeCreatedWithCustomOptions()
    {
        var options = new MapRenderOptions
        {
            Dpi = 150,
            JpegQuality = 80
        };

        using var renderer = new BaseMapRenderer(options);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RenderMap_NonContiguousTiles_HandlesGaps()
    {
        using var renderer = new BaseMapRenderer();
        var tileData = CreateTestPngTile();

        // Create tiles with a gap
        var tiles = new List<TileData>
        {
            new TileData(0, 0, 2, tileData),
            new TileData(2, 0, 2, tileData), // Gap at x=1
            new TileData(0, 2, 2, tileData),
            new TileData(2, 2, 2, tileData)
        };

        // Should still render without crashing
        var result = renderer.RenderMap(tiles, -85, 85, -180, 180, 2);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }
}
