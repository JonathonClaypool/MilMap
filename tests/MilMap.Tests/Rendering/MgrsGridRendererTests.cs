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

    // Cross-zone boundary tests

    [Fact]
    public void DrawGrid_CrossZoneBoundary_ProducesValidOutput()
    {
        using var baseMap = CreateTestBitmap();
        var renderer = new MgrsGridRenderer();

        // Area spanning zones 17 and 18 (boundary at -78°)
        using var result = renderer.DrawGrid(baseMap, 40.0, 41.0, -80.0, -76.0);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    public void DrawGrid_CrossMultipleZones_ProducesValidOutput()
    {
        using var baseMap = CreateTestBitmap(1024, 512);
        var renderer = new MgrsGridRenderer();

        // Area spanning zones 30, 31, 32 (boundaries at 0° and 6°)
        using var result = renderer.DrawGrid(baseMap, 48.0, 50.0, -2.0, 8.0);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    public void DrawGrid_ZoneBoundaryAtGreenwich_ProducesValidOutput()
    {
        using var baseMap = CreateTestBitmap();
        var renderer = new MgrsGridRenderer();

        // Area spanning zone 30/31 boundary at 0° (Greenwich)
        using var result = renderer.DrawGrid(baseMap, 51.0, 52.0, -1.0, 1.0);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    public void DrawGrid_NorwaySpecialZone_ProducesValidOutput()
    {
        using var baseMap = CreateTestBitmap();
        var renderer = new MgrsGridRenderer();

        // Area in Norway's special zone 32V
        using var result = renderer.DrawGrid(baseMap, 58.0, 62.0, 4.0, 10.0);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    public void DrawGrid_WideAreaSpanningManyZones_ProducesValidOutput()
    {
        using var baseMap = CreateTestBitmap(2048, 512);
        var options = new MgrsGridOptions { Scale = MapScale.Scale1To100000 };
        var renderer = new MgrsGridRenderer(options);

        // Wide area spanning ~4 zones (each zone is 6 degrees)
        using var result = renderer.DrawGrid(baseMap, 45.0, 47.0, -95.0, -70.0);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }
}
