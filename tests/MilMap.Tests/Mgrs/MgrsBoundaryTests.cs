using MilMap.Core.Mgrs;
using Xunit;

namespace MilMap.Tests.Mgrs;

public class MgrsBoundaryTests
{
    [Theory]
    [InlineData(1, -180, -174)]
    [InlineData(10, -126, -120)]
    [InlineData(31, 0, 6)]
    [InlineData(60, 174, 180)]
    public void GetBounds_ZoneOnly_ReturnsCorrectLongitude(int zone, double expectedMinLon, double expectedMaxLon)
    {
        var bounds = MgrsBoundary.GetBounds(zone.ToString());

        Assert.Equal(expectedMinLon, bounds.MinLon, 0.1);
        Assert.Equal(expectedMaxLon, bounds.MaxLon, 0.1);
        Assert.Equal(-80.0, bounds.MinLat);
        Assert.Equal(84.0, bounds.MaxLat);
    }

    [Theory]
    [InlineData("18T", 32.0, 40.0)] // T is 32-40°N
    [InlineData("18C", -80.0, -72.0)] // C is first band
    [InlineData("31X", 72.0, 84.0)] // X extends to 84
    public void GetBounds_ZoneBand_ReturnsCorrectLatitude(string mgrs, double expectedMinLat, double expectedMaxLat)
    {
        var bounds = MgrsBoundary.GetBounds(mgrs);

        Assert.Equal(expectedMinLat, bounds.MinLat, 0.1);
        Assert.Equal(expectedMaxLat, bounds.MaxLat, 0.1);
    }

    [Fact]
    public void GetBounds_ZoneBand_ReturnsValidBox()
    {
        var bounds = MgrsBoundary.GetBounds("18T");

        Assert.True(bounds.MinLat < bounds.MaxLat);
        Assert.True(bounds.MinLon < bounds.MaxLon);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public void GetBounds_100KSquare_ReturnsSmallerThanZoneBand()
    {
        var zoneBandBounds = MgrsBoundary.GetBounds("18T");
        var squareBounds = MgrsBoundary.GetBounds("18TXM");

        Assert.True(squareBounds.Width < zoneBandBounds.Width);
        Assert.True(squareBounds.Height < zoneBandBounds.Height);
    }

    [Theory]
    [InlineData("18TXM1122")]
    [InlineData("18TXM112233")]
    [InlineData("18TXM11223344")]
    [InlineData("18TXM1122334455")]
    public void GetBounds_VariousPrecisions_ReturnsSmallerBounds(string mgrs)
    {
        var bounds = MgrsBoundary.GetBounds(mgrs);

        Assert.True(bounds.MinLat < bounds.MaxLat);
        Assert.True(bounds.MinLon < bounds.MaxLon);
    }

    [Fact]
    public void GetBounds_HigherPrecision_ReturnsSmallerArea()
    {
        var bounds1km = MgrsBoundary.GetBounds("18TXM1122");
        var bounds100m = MgrsBoundary.GetBounds("18TXM112233");

        Assert.True(bounds100m.Width < bounds1km.Width);
        Assert.True(bounds100m.Height < bounds1km.Height);
    }

    [Theory]
    [InlineData("32V")] // Norway zone
    [InlineData("31X")] // Svalbard
    [InlineData("33X")] // Svalbard
    [InlineData("35X")] // Svalbard
    public void GetBounds_SpecialZones_ReturnsValidBounds(string mgrs)
    {
        var bounds = MgrsBoundary.GetBounds(mgrs);

        Assert.True(bounds.MinLat < bounds.MaxLat);
        Assert.True(bounds.MinLon < bounds.MaxLon);
    }

    [Fact]
    public void GetBounds_NorwayZone32V_HasCorrectLongitude()
    {
        var bounds = MgrsBoundary.GetBounds("32V");

        Assert.Equal(3.0, bounds.MinLon, 0.1);
        Assert.Equal(12.0, bounds.MaxLon, 0.1);
    }

    [Theory]
    [InlineData("Y")] // North pole west
    [InlineData("Z")] // North pole east
    [InlineData("A")] // South pole west
    [InlineData("B")] // South pole east
    public void GetBounds_PolarRegions_ReturnsValidBounds(string mgrs)
    {
        var bounds = MgrsBoundary.GetBounds(mgrs);

        Assert.True(bounds.MinLat < bounds.MaxLat);
    }

    [Fact]
    public void GetBounds_NorthPolar_ReturnsCorrectLatitude()
    {
        var bounds = MgrsBoundary.GetBounds("Y");

        Assert.Equal(84.0, bounds.MinLat);
        Assert.Equal(90.0, bounds.MaxLat);
    }

    [Fact]
    public void GetBounds_SouthPolar_ReturnsCorrectLatitude()
    {
        var bounds = MgrsBoundary.GetBounds("A");

        Assert.Equal(-90.0, bounds.MinLat);
        Assert.Equal(-80.0, bounds.MaxLat);
    }

    [Fact]
    public void GetBounds_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MgrsBoundary.GetBounds(""));
    }

    [Fact]
    public void GetBounds_NullString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MgrsBoundary.GetBounds(null!));
    }

    [Theory]
    [InlineData("61T")] // Invalid zone
    [InlineData("0T")] // Invalid zone
    [InlineData("18I")] // Invalid band (I not used)
    [InlineData("18O")] // Invalid band (O not used)
    public void GetBounds_InvalidInput_ThrowsArgumentException(string mgrs)
    {
        Assert.Throws<ArgumentException>(() => MgrsBoundary.GetBounds(mgrs));
    }

    [Theory]
    [InlineData("18t")]
    [InlineData("18txm")]
    [InlineData("18TXM 12 34")]
    public void GetBounds_CaseInsensitiveAndSpaces_Works(string mgrs)
    {
        var bounds = MgrsBoundary.GetBounds(mgrs);

        Assert.True(bounds.MinLat < bounds.MaxLat);
    }

    [Fact]
    public void BoundingBox_PropertiesCalculatedCorrectly()
    {
        var box = new BoundingBox(10.0, 20.0, 30.0, 50.0);

        Assert.Equal(20.0, box.Width);
        Assert.Equal(10.0, box.Height);
        Assert.Equal(15.0, box.CenterLat);
        Assert.Equal(40.0, box.CenterLon);
    }
}
