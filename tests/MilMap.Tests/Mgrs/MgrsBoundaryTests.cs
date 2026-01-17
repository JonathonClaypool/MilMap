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
    [InlineData("18T", 40.0, 48.0)] // T is 40-48°N
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

    // Cross-zone boundary tests

    [Theory]
    [InlineData(-179.0, 1)] // Zone 1
    [InlineData(-175.0, 1)] // Zone 1 (ends at -174)
    [InlineData(-121.0, 10)] // Zone 10 (boundaries -126 to -120)
    [InlineData(0.0, 31)] // Zone 31
    [InlineData(3.0, 31)] // Zone 31
    [InlineData(179.0, 60)] // Zone 60
    public void GetUtmZone_ReturnsCorrectZone(double longitude, int expectedZone)
    {
        int zone = MgrsBoundary.GetUtmZone(longitude);
        Assert.Equal(expectedZone, zone);
    }

    [Theory]
    [InlineData(60.0, 5.0, 32)] // Norway special zone
    [InlineData(75.0, 5.0, 31)] // Svalbard zone 31
    [InlineData(75.0, 15.0, 33)] // Svalbard zone 33
    public void GetUtmZone_WithLatitude_HandlesSpecialZones(double lat, double lon, int expectedZone)
    {
        int zone = MgrsBoundary.GetUtmZone(lat, lon);
        Assert.Equal(expectedZone, zone);
    }

    [Theory]
    [InlineData(1, -180, -174)]
    [InlineData(31, 0, 6)]
    [InlineData(60, 174, 180)]
    public void GetZoneLongitudeBounds_ReturnsCorrectBounds(int zone, double expectedMin, double expectedMax)
    {
        var (minLon, maxLon) = MgrsBoundary.GetZoneLongitudeBounds(zone);
        Assert.Equal(expectedMin, minLon);
        Assert.Equal(expectedMax, maxLon);
    }

    [Fact]
    public void GetZoneLongitudeBounds_InvalidZone_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MgrsBoundary.GetZoneLongitudeBounds(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MgrsBoundary.GetZoneLongitudeBounds(61));
    }

    [Theory]
    [InlineData(31, 3)]
    [InlineData(10, -123)]
    [InlineData(1, -177)]
    public void GetZoneCentralMeridian_ReturnsCorrectValue(int zone, double expectedMeridian)
    {
        double meridian = MgrsBoundary.GetZoneCentralMeridian(zone);
        Assert.Equal(expectedMeridian, meridian);
    }

    [Fact]
    public void GetZonesForBounds_SingleZone_ReturnsOneZone()
    {
        // Area entirely within zone 18 (around -75 to -78 lon)
        var zones = MgrsBoundary.GetZonesForBounds(40.0, 42.0, -77.0, -75.0);

        Assert.Single(zones);
        Assert.Equal(18, zones[0].Zone);
    }

    [Fact]
    public void GetZonesForBounds_MultipleZones_ReturnsAllZones()
    {
        // Area spanning zones 17 and 18 (boundary at -78)
        var zones = MgrsBoundary.GetZonesForBounds(40.0, 42.0, -80.0, -75.0);

        Assert.Equal(2, zones.Count);
        Assert.Equal(17, zones[0].Zone);
        Assert.Equal(18, zones[1].Zone);
    }

    [Fact]
    public void GetZonesForBounds_ThreeZones_ReturnsAllZones()
    {
        // Area spanning zones 30, 31, 32 (boundaries at 0 and 6)
        var zones = MgrsBoundary.GetZonesForBounds(45.0, 50.0, -2.0, 8.0);

        Assert.Equal(3, zones.Count);
        Assert.Equal(30, zones[0].Zone);
        Assert.Equal(31, zones[1].Zone);
        Assert.Equal(32, zones[2].Zone);
    }

    [Fact]
    public void GetZonesForBounds_ClipsToRequestedBounds()
    {
        // Request a small area in zone 31
        var zones = MgrsBoundary.GetZonesForBounds(48.0, 49.0, 2.0, 4.0);

        Assert.Single(zones);
        Assert.Equal(31, zones[0].Zone);
        Assert.Equal(2.0, zones[0].MinLon);
        Assert.Equal(4.0, zones[0].MaxLon);
    }

    [Fact]
    public void SpansMultipleZones_WithinSingleZone_ReturnsFalse()
    {
        bool spans = MgrsBoundary.SpansMultipleZones(-77.0, -75.0);
        Assert.False(spans);
    }

    [Fact]
    public void SpansMultipleZones_CrossingZoneBoundary_ReturnsTrue()
    {
        bool spans = MgrsBoundary.SpansMultipleZones(-80.0, -75.0);
        Assert.True(spans);
    }

    [Fact]
    public void GetZoneBoundariesInRange_NoBoundary_ReturnsEmpty()
    {
        var boundaries = MgrsBoundary.GetZoneBoundariesInRange(-77.0, -75.0);
        Assert.Empty(boundaries);
    }

    [Fact]
    public void GetZoneBoundariesInRange_OneBoundary_ReturnsOne()
    {
        // Zone 17/18 boundary is at -78
        var boundaries = MgrsBoundary.GetZoneBoundariesInRange(-80.0, -75.0);
        Assert.Single(boundaries);
        Assert.Equal(-78.0, boundaries[0]);
    }

    [Fact]
    public void GetZoneBoundariesInRange_TwoBoundaries_ReturnsTwo()
    {
        // Zones 30/31 boundary at 0, zones 31/32 boundary at 6
        var boundaries = MgrsBoundary.GetZoneBoundariesInRange(-2.0, 8.0);
        Assert.Equal(2, boundaries.Count);
        Assert.Equal(0.0, boundaries[0]);
        Assert.Equal(6.0, boundaries[1]);
    }
}
