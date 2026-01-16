using MilMap.Core.Input;
using Xunit;

namespace MilMap.Tests.Input;

public class LatLonInputHandlerTests
{
    [Fact]
    public void FromBounds_ValidInput_ReturnsBoundingBox()
    {
        var result = LatLonInputHandler.FromBounds(40.0, 41.0, -75.0, -74.0);

        Assert.NotNull(result);
        Assert.Equal(40.0, result.BoundingBox.MinLat);
        Assert.Equal(41.0, result.BoundingBox.MaxLat);
        Assert.Equal(-75.0, result.BoundingBox.MinLon);
        Assert.Equal(-74.0, result.BoundingBox.MaxLon);
    }

    [Fact]
    public void FromBounds_CenterIsCorrect()
    {
        var result = LatLonInputHandler.FromBounds(40.0, 42.0, -76.0, -74.0);

        Assert.Equal(41.0, result.CenterLat);
        Assert.Equal(-75.0, result.CenterLon);
    }

    [Theory]
    [InlineData(50, 40, -75, -74)] // minLat > maxLat
    [InlineData(40, 50, -70, -75)] // minLon > maxLon
    public void FromBounds_InvalidBounds_ThrowsArgumentException(double minLat, double maxLat, double minLon, double maxLon)
    {
        Assert.Throws<ArgumentException>(() =>
            LatLonInputHandler.FromBounds(minLat, maxLat, minLon, maxLon));
    }

    [Theory]
    [InlineData(-100, 50, -75, -74)] // Invalid minLat
    [InlineData(40, 100, -75, -74)]  // Invalid maxLat
    [InlineData(40, 50, -200, -74)]  // Invalid minLon
    [InlineData(40, 50, -75, 200)]   // Invalid maxLon
    public void FromBounds_OutOfRangeCoordinates_ThrowsArgumentOutOfRangeException(double minLat, double maxLat, double minLon, double maxLon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LatLonInputHandler.FromBounds(minLat, maxLat, minLon, maxLon));
    }

    [Fact]
    public void FromCenterRadius_ValidInput_ReturnsBoundingBox()
    {
        var result = LatLonInputHandler.FromCenterRadius(40.0, -75.0, 10, DistanceUnit.Kilometers);

        Assert.NotNull(result);
        Assert.Equal(40.0, result.CenterLat);
        Assert.Equal(-75.0, result.CenterLon);
        Assert.True(result.BoundingBox.MinLat < 40.0);
        Assert.True(result.BoundingBox.MaxLat > 40.0);
    }

    [Fact]
    public void FromCenterRadius_LargerRadius_LargerBoundingBox()
    {
        var small = LatLonInputHandler.FromCenterRadius(40.0, -75.0, 5, DistanceUnit.Kilometers);
        var large = LatLonInputHandler.FromCenterRadius(40.0, -75.0, 20, DistanceUnit.Kilometers);

        Assert.True(large.BoundingBox.Width > small.BoundingBox.Width);
        Assert.True(large.BoundingBox.Height > small.BoundingBox.Height);
    }

    [Fact]
    public void FromCenterRadius_ZeroRadius_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LatLonInputHandler.FromCenterRadius(40.0, -75.0, 0, DistanceUnit.Kilometers));
    }

    [Theory]
    [InlineData("40,-75,41,-74")]
    [InlineData("40, -75, 41, -74")]
    [InlineData("40.5,-75.5,41.5,-74.5")]
    public void Parse_CommaSeparated_ReturnsBoundingBox(string input)
    {
        var result = LatLonInputHandler.Parse(input);

        Assert.NotNull(result);
        Assert.True(result.BoundingBox.MinLat < result.BoundingBox.MaxLat);
        Assert.True(result.BoundingBox.MinLon < result.BoundingBox.MaxLon);
    }

    [Theory]
    [InlineData("40 -75 41 -74")]
    [InlineData("40.5 -75.5 41.5 -74.5")]
    public void Parse_SpaceSeparated_ReturnsBoundingBox(string input)
    {
        var result = LatLonInputHandler.Parse(input);

        Assert.NotNull(result);
        Assert.True(result.BoundingBox.MinLat < result.BoundingBox.MaxLat);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("40,75")] // Only 2 values
    public void Parse_InvalidInput_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => LatLonInputHandler.Parse(input));
    }

    [Theory]
    [InlineData("40.5,-75.5", 40.5, -75.5)]
    [InlineData("40.5 -75.5", 40.5, -75.5)]
    [InlineData("-33.8688,151.2093", -33.8688, 151.2093)]
    public void ParseCoordinate_ValidInput_ReturnsCorrectValues(string input, double expectedLat, double expectedLon)
    {
        var (lat, lon) = LatLonInputHandler.ParseCoordinate(input);

        Assert.Equal(expectedLat, lat);
        Assert.Equal(expectedLon, lon);
    }

    [Theory]
    [InlineData("")]
    [InlineData("40.5")] // Only one value
    [InlineData("abc,def")]
    public void ParseCoordinate_InvalidInput_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => LatLonInputHandler.ParseCoordinate(input));
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(0)]
    [InlineData(90)]
    public void ValidateLatitude_ValidValues_DoesNotThrow(double lat)
    {
        var exception = Record.Exception(() => LatLonInputHandler.ValidateLatitude(lat));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void ValidateLatitude_InvalidValues_ThrowsArgumentOutOfRangeException(double lat)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LatLonInputHandler.ValidateLatitude(lat));
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(0)]
    [InlineData(180)]
    public void ValidateLongitude_ValidValues_DoesNotThrow(double lon)
    {
        var exception = Record.Exception(() => LatLonInputHandler.ValidateLongitude(lon));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void ValidateLongitude_InvalidValues_ThrowsArgumentOutOfRangeException(double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LatLonInputHandler.ValidateLongitude(lon));
    }

    [Fact]
    public void LatLonInputResult_ContainsOriginalInput()
    {
        var result = LatLonInputHandler.FromCenterRadius(40.0, -75.0, 5, DistanceUnit.Kilometers);

        Assert.Contains("40", result.OriginalInput);
        Assert.Contains("-75", result.OriginalInput);
    }
}
