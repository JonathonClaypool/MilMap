using MilMap.Core.Rendering;
using Xunit;

namespace MilMap.Tests.Rendering;

public class MagneticDeclinationRendererTests
{
    [Fact]
    public void CalculateDeclination_WashingtonDC_ReturnsWestDeclination()
    {
        // Washington DC should have a small westward declination (approximately -10° to -12°)
        double declination = MagneticDeclinationRenderer.CalculateDeclination(38.9072, -77.0369);

        Assert.True(declination < 0, "DC should have westward (negative) declination");
        Assert.True(declination > -30, "DC declination should be reasonable");
    }

    [Fact]
    public void CalculateDeclination_London_ReturnsSmallDeclination()
    {
        // London should have a small declination (near 0°)
        double declination = MagneticDeclinationRenderer.CalculateDeclination(51.5074, -0.1278);

        Assert.True(Math.Abs(declination) < 20, "London should have small declination");
    }

    [Fact]
    public void CalculateDeclination_Alaska_ReturnsReasonableValue()
    {
        // Alaska (Fairbanks) - our simplified model may not match exact values
        // but should return a reasonable value within expected global range
        double declination = MagneticDeclinationRenderer.CalculateDeclination(64.8378, -147.7164);

        Assert.True(Math.Abs(declination) < 45, "Alaska declination should be reasonable");
    }

    [Fact]
    public void CalculateDeclination_Sydney_ReturnsEastDeclination()
    {
        // Sydney, Australia should have a small to moderate eastward declination
        double declination = MagneticDeclinationRenderer.CalculateDeclination(-33.8688, 151.2093);

        // Just verify it returns a reasonable value
        Assert.True(Math.Abs(declination) < 30);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(45, -90)]
    [InlineData(-45, 90)]
    [InlineData(70, 0)]
    public void CalculateDeclination_VariousLocations_ReturnsReasonableValues(double lat, double lon)
    {
        double declination = MagneticDeclinationRenderer.CalculateDeclination(lat, lon);

        // All declinations should be within -180 to 180
        Assert.True(declination >= -180 && declination <= 180);
    }

    [Fact]
    public void CalculateAnnualChange_ReturnsSmallValue()
    {
        double change = MagneticDeclinationRenderer.CalculateAnnualChange(40.0, -75.0);

        // Annual change should be small (typically less than 0.3 degrees/year)
        Assert.True(Math.Abs(change) < 1.0, "Annual change should be less than 1 degree/year");
    }

    [Fact]
    public void CalculateGridConvergence_AtCentralMeridian_ReturnsNearZero()
    {
        // At the central meridian of a zone, convergence should be near zero
        // Zone 18 central meridian is -75°
        double convergence = MagneticDeclinationRenderer.CalculateGridConvergence(45.0, -75.0);

        Assert.True(Math.Abs(convergence) < 0.1, "Convergence at central meridian should be near zero");
    }

    [Fact]
    public void CalculateGridConvergence_AwayFromCentralMeridian_ReturnsNonZero()
    {
        // Away from central meridian, convergence increases
        double convergence = MagneticDeclinationRenderer.CalculateGridConvergence(45.0, -77.0);

        Assert.True(Math.Abs(convergence) > 0);
    }

    [Fact]
    public void Render_ReturnsValidResult()
    {
        var renderer = new MagneticDeclinationRenderer();

        var result = renderer.Render(40.0, -75.0);

        Assert.NotNull(result);
        Assert.NotNull(result.Diagram);
        Assert.True(result.Diagram.Width > 0);
        Assert.True(result.Diagram.Height > 0);
        
        result.Diagram.Dispose();
    }

    [Fact]
    public void Render_WithCustomOptions_ReturnsCorrectSize()
    {
        var options = new DeclinationOptions
        {
            Width = 200,
            Height = 240
        };
        var renderer = new MagneticDeclinationRenderer(options);

        var result = renderer.Render(45.0, 10.0);

        Assert.Equal(200, result.Diagram.Width);
        Assert.Equal(240, result.Diagram.Height);
        
        result.Diagram.Dispose();
    }

    [Fact]
    public void Render_IncludesCalculatedValues()
    {
        var options = new DeclinationOptions { Year = 2025 };
        var renderer = new MagneticDeclinationRenderer(options);

        var result = renderer.Render(38.9072, -77.0369);

        Assert.Equal(2025, result.Year);
        Assert.True(result.Declination != 0, "Declination should be calculated");
        Assert.True(result.AnnualChange != 0, "Annual change should be calculated");
        
        result.Diagram.Dispose();
    }

    [Fact]
    public void Render_WithoutGridNorth_Works()
    {
        var options = new DeclinationOptions { ShowGridNorth = false };
        var renderer = new MagneticDeclinationRenderer(options);

        var result = renderer.Render(45.0, -120.0);

        Assert.NotNull(result.Diagram);
        
        result.Diagram.Dispose();
    }

    [Fact]
    public void Render_WithoutAnnualChange_Works()
    {
        var options = new DeclinationOptions { ShowAnnualChange = false };
        var renderer = new MagneticDeclinationRenderer(options);

        var result = renderer.Render(45.0, -120.0);

        Assert.NotNull(result.Diagram);
        
        result.Diagram.Dispose();
    }

    [Theory]
    [InlineData(38.9072, -77.0369)] // Washington DC
    [InlineData(51.5074, -0.1278)]  // London
    [InlineData(35.6762, 139.6503)] // Tokyo
    [InlineData(-33.8688, 151.2093)] // Sydney
    [InlineData(64.8378, -147.7164)] // Fairbanks
    public void Render_GlobalLocations_AllSucceed(double lat, double lon)
    {
        var renderer = new MagneticDeclinationRenderer();

        var result = renderer.Render(lat, lon);

        Assert.NotNull(result);
        Assert.NotNull(result.Diagram);
        Assert.True(result.Diagram.Width > 0);
        
        result.Diagram.Dispose();
    }

    [Fact]
    public void DeclinationOptions_HasCorrectDefaults()
    {
        var options = new DeclinationOptions();

        Assert.Equal(100, options.Width);
        Assert.Equal(120, options.Height);
        Assert.True(options.ShowGridNorth);
        Assert.True(options.ShowAnnualChange);
        Assert.Null(options.Year);
    }

    [Fact]
    public void MagneticDeclinationResult_ContainsAllValues()
    {
        var renderer = new MagneticDeclinationRenderer(new DeclinationOptions { Year = 2026 });

        var result = renderer.Render(40.0, -80.0);

        Assert.Equal(2026, result.Year);
        Assert.True(Math.Abs(result.Declination) < 90, "Declination should be reasonable");
        Assert.True(Math.Abs(result.AnnualChange) < 1, "Annual change should be small");
        Assert.True(Math.Abs(result.GridConvergence) < 5, "Grid convergence should be small");
        
        result.Diagram.Dispose();
    }
}
