using MilMap.Core.Input;
using Xunit;

namespace MilMap.Tests.Input;

public class MgrsInputHandlerTests
{
    [Theory]
    [InlineData("18SUJ2338606785")]
    [InlineData("18TXM")]
    [InlineData("18T")]
    [InlineData("31NAA6602109998")]
    public void ValidateMgrs_ValidInput_DoesNotThrow(string mgrs)
    {
        var exception = Record.Exception(() => MgrsInputHandler.ValidateMgrs(mgrs));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("61T")] // Invalid zone
    [InlineData("0T")]  // Invalid zone
    public void ValidateMgrs_InvalidInput_ThrowsArgumentException(string mgrs)
    {
        Assert.Throws<ArgumentException>(() => MgrsInputHandler.ValidateMgrs(mgrs));
    }

    [Theory]
    [InlineData("18SUJ2338606785", true)]
    [InlineData("18TXM", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void IsValidMgrs_ReturnsCorrectResult(string mgrs, bool expected)
    {
        var result = MgrsInputHandler.IsValidMgrs(mgrs);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromMgrsWithRadius_ValidInput_ReturnsBoundingBox()
    {
        var result = MgrsInputHandler.FromMgrsWithRadius("18SUJ2338606785", 5, DistanceUnit.Kilometers);

        Assert.NotNull(result);
        Assert.True(result.BoundingBox.MinLat < result.BoundingBox.MaxLat);
        Assert.True(result.BoundingBox.MinLon < result.BoundingBox.MaxLon);
        Assert.True(result.BoundingBox.Width > 0);
        Assert.True(result.BoundingBox.Height > 0);
    }

    [Fact]
    public void FromMgrsWithRadius_CenterIsWithinBounds()
    {
        var result = MgrsInputHandler.FromMgrsWithRadius("18SUJ2338606785", 5, DistanceUnit.Kilometers);

        Assert.InRange(result.CenterLat, result.BoundingBox.MinLat, result.BoundingBox.MaxLat);
        Assert.InRange(result.CenterLon, result.BoundingBox.MinLon, result.BoundingBox.MaxLon);
    }

    [Theory]
    [InlineData(1, DistanceUnit.Kilometers)]
    [InlineData(5, DistanceUnit.Kilometers)]
    [InlineData(1000, DistanceUnit.Meters)]
    [InlineData(1, DistanceUnit.Miles)]
    public void FromMgrsWithRadius_VariousRadii_ReturnsBoundingBox(double radius, DistanceUnit unit)
    {
        var result = MgrsInputHandler.FromMgrsWithRadius("18TXM5050", radius, unit);

        Assert.NotNull(result);
        Assert.True(result.BoundingBox.Width > 0);
        Assert.True(result.BoundingBox.Height > 0);
    }

    [Fact]
    public void FromMgrsWithRadius_LargerRadius_LargerBoundingBox()
    {
        var small = MgrsInputHandler.FromMgrsWithRadius("18TXM5050", 1, DistanceUnit.Kilometers);
        var large = MgrsInputHandler.FromMgrsWithRadius("18TXM5050", 10, DistanceUnit.Kilometers);

        Assert.True(large.BoundingBox.Width > small.BoundingBox.Width);
        Assert.True(large.BoundingBox.Height > small.BoundingBox.Height);
    }

    [Fact]
    public void FromMgrsCorners_ValidInput_ReturnsBoundingBox()
    {
        var result = MgrsInputHandler.FromMgrsCorners("18TXM0000", "18TXM9999");

        Assert.NotNull(result);
        Assert.True(result.BoundingBox.MinLat < result.BoundingBox.MaxLat);
        Assert.True(result.BoundingBox.MinLon < result.BoundingBox.MaxLon);
    }

    [Fact]
    public void FromMgrsGridSquare_ZoneBand_ReturnsLargeBoundingBox()
    {
        var result = MgrsInputHandler.FromMgrsGridSquare("18T");

        Assert.NotNull(result);
        // Zone + band should cover about 6° x 8°
        Assert.True(result.BoundingBox.Width >= 5);
        Assert.True(result.BoundingBox.Height >= 7);
    }

    [Fact]
    public void FromMgrsGridSquare_100KSquare_ReturnsSmallerBoundingBox()
    {
        var zoneBand = MgrsInputHandler.FromMgrsGridSquare("18T");
        var square100K = MgrsInputHandler.FromMgrsGridSquare("18TXM");

        Assert.True(square100K.BoundingBox.Width < zoneBand.BoundingBox.Width);
        Assert.True(square100K.BoundingBox.Height < zoneBand.BoundingBox.Height);
    }

    [Fact]
    public void Parse_PartialMgrs_UsesGridSquare()
    {
        var result = MgrsInputHandler.Parse("18TXM");

        Assert.NotNull(result);
        Assert.Equal("18TXM", result.OriginalInput);
    }

    [Fact]
    public void Parse_FullMgrs_UsesRadiusApproach()
    {
        var result = MgrsInputHandler.Parse("18TXM5050", defaultRadiusKm: 2.0);

        Assert.NotNull(result);
        // Should be roughly 4km wide (2km radius each direction)
        Assert.True(result.BoundingBox.Width < 1); // degrees, approx 80km per degree
    }

    [Theory]
    [InlineData("5km", 5, DistanceUnit.Kilometers)]
    [InlineData("1000m", 1000, DistanceUnit.Meters)]
    [InlineData("3mi", 3, DistanceUnit.Miles)]
    [InlineData("100ft", 100, DistanceUnit.Feet)]
    [InlineData("10", 10, DistanceUnit.Kilometers)] // Default to km
    public void ParseRadius_ValidInput_ReturnsCorrectValues(string input, double expectedValue, DistanceUnit expectedUnit)
    {
        var (value, unit) = MgrsInputHandler.ParseRadius(input);

        Assert.Equal(expectedValue, value);
        Assert.Equal(expectedUnit, unit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("km5")]
    public void ParseRadius_InvalidInput_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => MgrsInputHandler.ParseRadius(input));
    }

    [Fact]
    public void MgrsInputResult_ContainsCorrectData()
    {
        var result = MgrsInputHandler.FromMgrsWithRadius("18SUJ2338606785", 5, DistanceUnit.Kilometers);

        Assert.Equal("18SUJ2338606785", result.OriginalInput);
        Assert.InRange(result.CenterLat, 38, 40); // Approx Washington DC
        Assert.InRange(result.CenterLon, -78, -76);
    }
}
