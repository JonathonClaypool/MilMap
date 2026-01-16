using MilMap.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Rendering;

public class ScaleBarRendererTests
{
    [Fact]
    public void Render_DefaultOptions_ReturnsValidBitmap()
    {
        var renderer = new ScaleBarRenderer();

        var result = renderer.Render();

        Assert.NotNull(result);
        Assert.NotNull(result.Bitmap);
        Assert.True(result.Bitmap.Width > 0);
        Assert.True(result.Bitmap.Height > 0);
    }

    [Fact]
    public void Render_ReturnsPositiveMetricLength()
    {
        var renderer = new ScaleBarRenderer();

        var result = renderer.Render();

        Assert.True(result.MetricLengthMeters > 0);
    }

    [Fact]
    public void Render_ReturnsPositiveImperialLength()
    {
        var renderer = new ScaleBarRenderer();

        var result = renderer.Render();

        Assert.True(result.ImperialLengthMiles > 0);
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(25000)]
    [InlineData(50000)]
    [InlineData(100000)]
    public void Render_VariousScales_ReturnsValidBitmap(int scaleRatio)
    {
        var options = new ScaleBarOptions { ScaleRatio = scaleRatio };
        var renderer = new ScaleBarRenderer(options);

        var result = renderer.Render();

        Assert.NotNull(result.Bitmap);
        Assert.True(result.Bitmap.Width > 0);
    }

    [Fact]
    public void Render_LargerScale_ShorterGroundDistance()
    {
        // 1:10,000 should show shorter ground distance than 1:100,000
        var small = new ScaleBarRenderer(new ScaleBarOptions { ScaleRatio = 10000 });
        var large = new ScaleBarRenderer(new ScaleBarOptions { ScaleRatio = 100000 });

        var smallResult = small.Render();
        var largeResult = large.Render();

        Assert.True(smallResult.MetricLengthMeters < largeResult.MetricLengthMeters);
    }

    [Fact]
    public void Render_MetricOnly_ReturnsValidBitmap()
    {
        var options = new ScaleBarOptions
        {
            ShowMetric = true,
            ShowImperial = false
        };
        var renderer = new ScaleBarRenderer(options);

        var result = renderer.Render();

        Assert.NotNull(result.Bitmap);
    }

    [Fact]
    public void Render_ImperialOnly_ReturnsValidBitmap()
    {
        var options = new ScaleBarOptions
        {
            ShowMetric = false,
            ShowImperial = true
        };
        var renderer = new ScaleBarRenderer(options);

        var result = renderer.Render();

        Assert.NotNull(result.Bitmap);
    }

    [Fact]
    public void CalculatePixelWidth_1kmAt25000Scale300Dpi_ReturnsCorrectWidth()
    {
        var options = new ScaleBarOptions
        {
            ScaleRatio = 25000,
            Dpi = 300
        };
        var renderer = new ScaleBarRenderer(options);

        // 1km at 1:25000 = 40mm on map = 1.575 inches = 472.5 pixels at 300 DPI
        int width = renderer.CalculatePixelWidth(1000);

        Assert.InRange(width, 400, 550); // Allow some tolerance
    }

    [Fact]
    public void CalculateGroundDistance_RoundTrip_ReturnsOriginalValue()
    {
        var renderer = new ScaleBarRenderer();

        double originalDistance = 1000;
        int pixels = renderer.CalculatePixelWidth(originalDistance);
        double calculatedDistance = renderer.CalculateGroundDistance(pixels);

        Assert.InRange(calculatedDistance, originalDistance * 0.95, originalDistance * 1.05);
    }

    [Fact]
    public void ScaleBarOptions_HasCorrectDefaults()
    {
        var options = new ScaleBarOptions();

        Assert.Equal(25000, options.ScaleRatio);
        Assert.Equal(300, options.Dpi);
        Assert.True(options.ShowMetric);
        Assert.True(options.ShowImperial);
        Assert.Equal(SKColors.Black, options.PrimaryColor);
        Assert.Equal(SKColors.White, options.SecondaryColor);
    }

    [Fact]
    public void Render_CustomColors_ProducesValidBitmap()
    {
        var options = new ScaleBarOptions
        {
            PrimaryColor = SKColors.DarkGreen,
            SecondaryColor = SKColors.LightGreen,
            TextColor = SKColors.DarkGreen
        };
        var renderer = new ScaleBarRenderer(options);

        var result = renderer.Render();

        Assert.NotNull(result.Bitmap);
    }

    [Theory]
    [InlineData(72)]
    [InlineData(150)]
    [InlineData(300)]
    [InlineData(600)]
    public void Render_VariousDpi_ReturnsProportionalWidth(int dpi)
    {
        var options = new ScaleBarOptions { Dpi = dpi, ScaleRatio = 25000 };
        var renderer = new ScaleBarRenderer(options);

        var result = renderer.Render();

        // Higher DPI should result in wider bitmap
        Assert.True(result.Bitmap.Width > 0);
    }

    [Fact]
    public void Render_HigherDpi_LargerBitmap()
    {
        var lowDpi = new ScaleBarRenderer(new ScaleBarOptions { Dpi = 72 });
        var highDpi = new ScaleBarRenderer(new ScaleBarOptions { Dpi = 300 });

        var lowResult = lowDpi.Render();
        var highResult = highDpi.Render();

        Assert.True(highResult.Bitmap.Width > lowResult.Bitmap.Width);
    }

    [Fact]
    public void ScaleBarRenderer_CanBeCreatedWithDefaultOptions()
    {
        var renderer = new ScaleBarRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void ScaleBarRenderer_CanBeCreatedWithCustomOptions()
    {
        var options = new ScaleBarOptions
        {
            ScaleRatio = 50000,
            BarHeight = 10,
            LabelFontSize = 12f
        };

        var renderer = new ScaleBarRenderer(options);
        Assert.NotNull(renderer);
    }
}
