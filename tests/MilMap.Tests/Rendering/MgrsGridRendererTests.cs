using MilMap.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Rendering;

public class MgrsGridRendererTests
{
    private static SKBitmap CreateTestBitmap(int width = 512, int height = 512)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightGray);
        return bitmap;
    }

    [Fact]
    public void DrawGrid_ReturnsLargerBitmapWithMargins()
    {
        using var baseMap = CreateTestBitmap();
        var renderer = new MgrsGridRenderer();

        using var result = renderer.DrawGrid(baseMap, 40.0, 40.1, -75.0, -74.9);

        // Should be larger due to margins
        Assert.True(result.Width > baseMap.Width);
        Assert.True(result.Height > baseMap.Height);
    }

    [Fact]
    public void DrawGrid_NoLabels_ReturnsSameSizeBitmap()
    {
        using var baseMap = CreateTestBitmap();
        var options = new MgrsGridOptions { ShowLabels = false };
        var renderer = new MgrsGridRenderer(options);

        using var result = renderer.DrawGrid(baseMap, 40.0, 40.1, -75.0, -74.9);

        Assert.Equal(baseMap.Width, result.Width);
        Assert.Equal(baseMap.Height, result.Height);
    }

    [Fact]
    public void GetGridInterval_Scale1To25000_Returns1000()
    {
        var options = new MgrsGridOptions { Scale = MapScale.Scale1To25000 };
        var renderer = new MgrsGridRenderer(options);

        Assert.Equal(1000, renderer.GetGridInterval());
    }

    [Fact]
    public void GetGridInterval_Scale1To100000_Returns10000()
    {
        var options = new MgrsGridOptions { Scale = MapScale.Scale1To100000 };
        var renderer = new MgrsGridRenderer(options);

        Assert.Equal(10000, renderer.GetGridInterval());
    }

    [Fact]
    public void GetGridInterval_CustomInterval_OverridesScale()
    {
        var options = new MgrsGridOptions
        {
            Scale = MapScale.Scale1To25000,
            GridIntervalMeters = 500
        };
        var renderer = new MgrsGridRenderer(options);

        Assert.Equal(500, renderer.GetGridInterval());
    }

    [Fact]
    public void GetGridConvergence_AtCentralMeridian_ReturnsZero()
    {
        var renderer = new MgrsGridRenderer();

        // At the central meridian of a zone, convergence should be near zero
        double convergence = renderer.GetGridConvergence(45.0, -75.0); // Zone 18, CM = -75

        Assert.True(Math.Abs(convergence) < 0.1);
    }

    [Fact]
    public void GetGridConvergence_AwayFromCentralMeridian_ReturnsNonZero()
    {
        var renderer = new MgrsGridRenderer();

        // Away from central meridian, convergence increases
        double convergence = renderer.GetGridConvergence(45.0, -77.0);

        Assert.True(Math.Abs(convergence) > 0);
    }

    [Fact]
    public void MgrsGridOptions_HasCorrectDefaults()
    {
        var options = new MgrsGridOptions();

        Assert.Equal(MapScale.Scale1To25000, options.Scale);
        Assert.Equal(SKColors.Black, options.GridLineColor);
        Assert.Equal(1.0f, options.GridLineWidth);
        Assert.True(options.ShowLabels);
        Assert.Equal(30, options.MarginPixels);
    }

    [Fact]
    public void DrawGrid_DifferentAreas_ProducesValidOutput()
    {
        using var baseMap = CreateTestBitmap();
        var renderer = new MgrsGridRenderer();

        // Washington DC area
        using var dcResult = renderer.DrawGrid(baseMap, 38.8, 38.95, -77.1, -76.9);
        Assert.NotNull(dcResult);
        Assert.True(dcResult.Width > 0);
        Assert.True(dcResult.Height > 0);

        // London area
        using var londonResult = renderer.DrawGrid(baseMap, 51.4, 51.6, -0.3, 0.1);
        Assert.NotNull(londonResult);

        // Sydney area (southern hemisphere)
        using var sydneyResult = renderer.DrawGrid(baseMap, -34.0, -33.7, 151.0, 151.3);
        Assert.NotNull(sydneyResult);
    }

    [Theory]
    [InlineData(MapScale.Scale1To10000)]
    [InlineData(MapScale.Scale1To25000)]
    [InlineData(MapScale.Scale1To50000)]
    [InlineData(MapScale.Scale1To100000)]
    public void DrawGrid_AllScales_ProducesValidOutput(MapScale scale)
    {
        using var baseMap = CreateTestBitmap();
        var options = new MgrsGridOptions { Scale = scale };
        var renderer = new MgrsGridRenderer(options);

        using var result = renderer.DrawGrid(baseMap, 40.0, 40.1, -75.0, -74.9);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    public void DrawGrid_CustomColors_AppliesCorrectly()
    {
        using var baseMap = CreateTestBitmap();
        var options = new MgrsGridOptions
        {
            GridLineColor = SKColors.Red,
            LabelColor = SKColors.Blue
        };
        var renderer = new MgrsGridRenderer(options);

        using var result = renderer.DrawGrid(baseMap, 40.0, 40.1, -75.0, -74.9);

        // Just verify it doesn't crash with custom colors
        Assert.NotNull(result);
    }

    [Fact]
    public void MgrsGridRenderer_CanBeCreatedWithDefaultOptions()
    {
        var renderer = new MgrsGridRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void MgrsGridRenderer_CanBeCreatedWithCustomOptions()
    {
        var options = new MgrsGridOptions
        {
            Scale = MapScale.Scale1To50000,
            GridLineWidth = 2.0f,
            LabelFontSize = 12f
        };

        var renderer = new MgrsGridRenderer(options);
        Assert.NotNull(renderer);
    }
}
