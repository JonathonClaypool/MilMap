using MilMap.Core.Tiles;
using Xunit;

namespace MilMap.Tests.Tiles;

public class ZoomLevelCalculatorTests
{
    [Fact]
    public void CalculateZoom_1To25000At300Dpi_ReturnsReasonableZoom()
    {
        var result = ZoomLevelCalculator.CalculateZoom(25000, 300);

        // 1:25000 at 300 DPI should be around Z15-Z17
        Assert.InRange(result.Zoom, 14, 18);
        Assert.True(result.MetersPerPixel > 0);
    }

    [Fact]
    public void CalculateZoom_1To10000At300Dpi_ReturnsHigherZoom()
    {
        var result = ZoomLevelCalculator.CalculateZoom(10000, 300);

        // 1:10000 needs more detail than 1:25000
        var result25k = ZoomLevelCalculator.CalculateZoom(25000, 300);
        Assert.True(result.Zoom >= result25k.Zoom);
    }

    [Fact]
    public void CalculateZoom_1To50000At300Dpi_ReturnsLowerZoom()
    {
        var result = ZoomLevelCalculator.CalculateZoom(50000, 300);

        // 1:50000 needs less detail than 1:25000
        var result25k = ZoomLevelCalculator.CalculateZoom(25000, 300);
        Assert.True(result.Zoom <= result25k.Zoom);
    }

    [Theory]
    [InlineData(25000, 300)]  // Standard military topo
    [InlineData(50000, 300)]  // Standard military topo
    [InlineData(100000, 300)] // Medium scale
    [InlineData(10000, 150)]  // Large scale, low DPI
    public void CalculateZoom_CommonScaleDpiCombinations_ReturnsValidZoom(int scale, int dpi)
    {
        var result = ZoomLevelCalculator.CalculateZoom(scale, dpi);

        Assert.InRange(result.Zoom, ZoomLevelCalculator.MinZoom, ZoomLevelCalculator.MaxZoom);
        Assert.True(result.MetersPerPixel > 0);
        Assert.True(result.ActualScale > 0);
    }

    [Fact]
    public void CalculateZoom_HigherDpi_MayRequireHigherZoom()
    {
        var result300 = ZoomLevelCalculator.CalculateZoom(25000, 300);
        var result600 = ZoomLevelCalculator.CalculateZoom(25000, 600);

        // Higher DPI means smaller pixels, so we need more detail
        Assert.True(result600.Zoom >= result300.Zoom);
    }

    [Fact]
    public void CalculateZoom_VeryHighResolution_ReturnsWarning()
    {
        // Very fine scale with high DPI that exceeds Z18 capability
        var result = ZoomLevelCalculator.CalculateZoom(1000, 600);

        // Should return max zoom with warning
        Assert.Equal(ZoomLevelCalculator.MaxZoom, result.Zoom);
        Assert.NotNull(result.Warning);
        Assert.Contains("higher resolution", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalculateZoom_InvalidScale_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZoomLevelCalculator.CalculateZoom(0, 300));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZoomLevelCalculator.CalculateZoom(-1, 300));
    }

    [Fact]
    public void CalculateZoom_InvalidDpi_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZoomLevelCalculator.CalculateZoom(25000, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZoomLevelCalculator.CalculateZoom(25000, -1));
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(90)]
    public void CalculateZoom_InvalidLatitude_ThrowsException(double latitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZoomLevelCalculator.CalculateZoom(25000, 300, latitude));
    }

    [Theory]
    [InlineData(0, 156543.03)]   // Equator, Z0
    [InlineData(10, 152.87)]     // Equator, Z10
    [InlineData(15, 4.77)]       // Equator, Z15
    [InlineData(18, 0.596)]      // Equator, Z18
    public void GetMetersPerPixel_AtEquator_ReturnsExpectedValues(int zoom, double expectedApprox)
    {
        double result = ZoomLevelCalculator.GetMetersPerPixel(zoom, 0);

        // Allow 5% tolerance for rounding
        Assert.InRange(result, expectedApprox * 0.95, expectedApprox * 1.05);
    }

    [Fact]
    public void GetMetersPerPixel_HigherLatitude_ReturnsSmallerValue()
    {
        double equator = ZoomLevelCalculator.GetMetersPerPixel(10, 0);
        double midLatitude = ZoomLevelCalculator.GetMetersPerPixel(10, 45);
        double highLatitude = ZoomLevelCalculator.GetMetersPerPixel(10, 60);

        Assert.True(midLatitude < equator);
        Assert.True(highLatitude < midLatitude);
    }

    [Fact]
    public void GetMetersPerPixel_ZoomIncrease_HalvesResolution()
    {
        double z10 = ZoomLevelCalculator.GetMetersPerPixel(10, 0);
        double z11 = ZoomLevelCalculator.GetMetersPerPixel(11, 0);

        // Each zoom level halves the meters per pixel
        Assert.InRange(z11, z10 * 0.49, z10 * 0.51);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(19)]
    public void GetMetersPerPixel_InvalidZoom_ThrowsException(int zoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZoomLevelCalculator.GetMetersPerPixel(zoom, 0));
    }

    [Fact]
    public void CalculateActualScale_ReversesFormulaCorrectly()
    {
        int targetScale = 25000;
        int dpi = 300;

        // Calculate target meters per pixel
        double targetMpp = (targetScale * 0.0254) / dpi;

        // Reverse to get scale
        double calculatedScale = ZoomLevelCalculator.CalculateActualScale(targetMpp, dpi);

        Assert.InRange(calculatedScale, targetScale * 0.99, targetScale * 1.01);
    }

    [Fact]
    public void GetResolutionTable_ReturnsCorrectLength()
    {
        var table = ZoomLevelCalculator.GetResolutionTable(0);

        Assert.Equal(ZoomLevelCalculator.MaxZoom + 1, table.Length);
    }

    [Fact]
    public void GetResolutionTable_ValuesDecreaseWithZoom()
    {
        var table = ZoomLevelCalculator.GetResolutionTable(0);

        for (int i = 1; i < table.Length; i++)
        {
            Assert.True(table[i] < table[i - 1]);
        }
    }

    [Fact]
    public void RecommendZoom_ReturnsZoomFromCalculation()
    {
        int zoom = ZoomLevelCalculator.RecommendZoom(25000, 300, 0);
        var result = ZoomLevelCalculator.CalculateZoom(25000, 300, 0);

        Assert.Equal(result.Zoom, zoom);
    }

    [Fact]
    public void CalculateZoom_1To25000At300Dpi_FormulaCorrect()
    {
        // Verify the formula: meters_per_pixel = (scale * 0.0254) / DPI
        int scale = 25000;
        int dpi = 300;

        double expectedMpp = (scale * 0.0254) / dpi; // ~2.117 meters per pixel

        var result = ZoomLevelCalculator.CalculateZoom(scale, dpi);

        // The actual MPP should be close to our target
        // (it won't be exact since zoom levels are discrete)
        double ratio = result.MetersPerPixel / expectedMpp;
        Assert.InRange(ratio, 0.5, 2.0); // Within a factor of 2
    }

    [Fact]
    public void CalculateZoom_IsApproximate_SetCorrectly()
    {
        var result = ZoomLevelCalculator.CalculateZoom(25000, 300);

        // If actual scale differs by more than 5%, IsApproximate should be true
        double ratio = Math.Abs(result.ActualScale - 25000) / 25000.0;
        bool expectedApproximate = ratio > 0.05;

        Assert.Equal(expectedApproximate, result.IsApproximate);
    }
}
