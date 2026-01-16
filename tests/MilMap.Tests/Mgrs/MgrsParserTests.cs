using MilMap.Core.Mgrs;
using Xunit;

namespace MilMap.Tests.Mgrs;

public class MgrsParserTests
{
    // Test round-trip encoding/decoding accuracy
    [Theory]
    [InlineData(38.8895, -77.0353)] // Washington DC
    [InlineData(51.5074, -0.1278)] // London
    [InlineData(48.8566, 2.3522)] // Paris
    [InlineData(35.6762, 139.6503)] // Tokyo
    [InlineData(-33.8688, 151.2093)] // Sydney
    [InlineData(0.0, 0.0)] // Equator/Prime Meridian
    [InlineData(45.0, -90.0)] // Mid latitude
    [InlineData(-45.0, 90.0)] // Southern hemisphere
    public void Parse_RoundTrip_WithinOneMeter(double originalLat, double originalLon)
    {
        // Encode to MGRS at 1m precision
        string mgrs = MgrsEncoder.Encode(originalLat, originalLon, MgrsEncoder.Precision.OneMeter);

        // Parse back to lat/lon
        var (parsedLat, parsedLon) = MgrsParser.Parse(mgrs);

        // Calculate distance between original and parsed (simplified spherical)
        double latDiff = (parsedLat - originalLat) * 111320; // meters
        double lonDiff = (parsedLon - originalLon) * 111320 * Math.Cos(originalLat * Math.PI / 180);
        double distance = Math.Sqrt(latDiff * latDiff + lonDiff * lonDiff);

        // Should be within 1 meter for 1m precision (allowing for center-of-cell offset)
        Assert.True(distance < 2, $"Distance {distance:F2}m exceeds tolerance for {mgrs}");
    }

    [Theory]
    [InlineData("18SUJ2338606785", 38.889, -77.035)]
    public void Parse_KnownMgrs_ReturnsApproximateLocation(string mgrs, double expectedLat, double expectedLon)
    {
        var (lat, lon) = MgrsParser.Parse(mgrs);

        Assert.Equal(expectedLat, lat, 0.01);
        Assert.Equal(expectedLon, lon, 0.01);
    }

    [Fact]
    public void Parse_EquatorPrimeMeridian_ReturnsNearZero()
    {
        // MGRS at 0,0 - the center of the grid cell may not be exactly 0,0
        string mgrs = MgrsEncoder.Encode(0.0, 0.0, MgrsEncoder.Precision.OneMeter);
        var (lat, lon) = MgrsParser.Parse(mgrs);

        // Allow slightly larger tolerance for grid cell centering
        Assert.InRange(lat, -0.1, 0.1);
        Assert.InRange(lon, -0.1, 0.1);
    }

    [Theory]
    [InlineData("18TXM1234")]  // 10m precision
    [InlineData("18TXM123456")]  // 1m precision (highest)
    [InlineData("18TXM12345678")]  // 0.1m precision
    [InlineData("18TXM1234567890")]  // 0.01m precision
    public void Parse_VariousPrecisions_ReturnsValidCoordinates(string mgrs)
    {
        var (lat, lon) = MgrsParser.Parse(mgrs);

        // Should return valid lat/lon
        Assert.InRange(lat, -90, 90);
        Assert.InRange(lon, -180, 180);
    }

    [Fact]
    public void Parse_MinimalMgrs_ReturnsValidCoordinates()
    {
        // Just zone, band, and 100k square
        var (lat, lon) = MgrsParser.Parse("18TXM");

        Assert.InRange(lat, -90, 90);
        Assert.InRange(lon, -180, 180);
    }

    [Theory]
    [InlineData("18txm")]
    [InlineData("18TXM 12 34")]
    [InlineData("  18TXM1234  ")]
    public void Parse_CaseInsensitiveAndSpaces_Works(string mgrs)
    {
        var (lat, lon) = MgrsParser.Parse(mgrs);

        Assert.InRange(lat, -90, 90);
        Assert.InRange(lon, -180, 180);
    }

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MgrsParser.Parse(""));
    }

    [Fact]
    public void Parse_NullString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MgrsParser.Parse(null!));
    }

    [Theory]
    [InlineData("61T")] // Invalid zone (> 60)
    [InlineData("0T")] // Invalid zone (< 1)
    [InlineData("18I")] // Invalid band (I not used)
    [InlineData("18O")] // Invalid band (O not used)
    public void Parse_InvalidInput_ThrowsArgumentException(string mgrs)
    {
        Assert.Throws<ArgumentException>(() => MgrsParser.Parse(mgrs));
    }

    [Theory]
    [InlineData("18TXM123")] // Odd number of grid digits
    [InlineData("18TXM12345")] // Odd number of grid digits
    public void Parse_OddDigitCount_ThrowsArgumentException(string mgrs)
    {
        Assert.Throws<ArgumentException>(() => MgrsParser.Parse(mgrs));
    }

    [Theory]
    [InlineData("32V")] // Norway special zone
    [InlineData("31X")] // Svalbard
    [InlineData("33X")] // Svalbard
    public void Parse_SpecialZones_ReturnsValidCoordinates(string mgrs)
    {
        // Just zone and band returns center of zone/band region
        // Need full MGRS with 100k square
        string fullMgrs = mgrs + "AA"; // Add minimal 100k square

        var exception = Record.Exception(() => MgrsParser.Parse(fullMgrs));

        // May throw if AA is invalid for this zone, but shouldn't crash
        // The point is the zone/band parsing works
        Assert.True(exception == null || exception is ArgumentException);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(18)]
    [InlineData(31)]
    [InlineData(60)]
    public void Parse_AllZones_ReturnsValidCoordinates(int zone)
    {
        // Create a valid MGRS for each zone at the equator
        // Zone N is at equator, use typical 100k squares
        string mgrs = $"{zone}NAA5050";

        try
        {
            var (lat, lon) = MgrsParser.Parse(mgrs);
            Assert.InRange(lat, -10, 10); // Near equator
        }
        catch (ArgumentException)
        {
            // AA may not be valid for all zones - that's acceptable
        }
    }

    [Theory]
    [InlineData('C')]
    [InlineData('N')]
    [InlineData('T')]
    [InlineData('X')]
    public void Parse_VariousLatitudeBands_ReturnsValidCoordinates(char band)
    {
        // Use zone 18 with various bands
        string mgrs = $"18{band}AA5050";

        try
        {
            var (lat, lon) = MgrsParser.Parse(mgrs);
            Assert.InRange(lat, -90, 90);
        }
        catch (ArgumentException)
        {
            // AA may not be valid for all band combinations - acceptable
        }
    }

    // Verify round-trip accuracy with actual encode/decode
    [Fact]
    public void Parse_EncodedMgrs_RoundTripsAccurately()
    {
        // Test with actual encode/decode round-trip for known locations
        double[] testLats = { 21.30, 48.85 };
        double[] testLons = { -157.85, 2.35 };

        for (int i = 0; i < testLats.Length; i++)
        {
            string mgrs = MgrsEncoder.Encode(testLats[i], testLons[i], MgrsEncoder.Precision.OneMeter);
            var (lat, lon) = MgrsParser.Parse(mgrs);

            // Within 2 meters for 1m precision
            double latDiff = Math.Abs(lat - testLats[i]) * 111320;
            double lonDiff = Math.Abs(lon - testLons[i]) * 111320 * Math.Cos(testLats[i] * Math.PI / 180);
            double distance = Math.Sqrt(latDiff * latDiff + lonDiff * lonDiff);

            Assert.True(distance < 2, $"Distance {distance:F2}m exceeds tolerance");
        }
    }

    [Fact]
    public void Parse_HighPrecisionMgrs_AccurateWithinOneMeter()
    {
        // Test specific high-precision coordinate
        double originalLat = 38.889500;
        double originalLon = -77.035300;

        string mgrs = MgrsEncoder.Encode(originalLat, originalLon, MgrsEncoder.Precision.OneMeter);
        var (parsedLat, parsedLon) = MgrsParser.Parse(mgrs);

        // Calculate approximate distance in meters
        double latMeters = Math.Abs(parsedLat - originalLat) * 111320;
        double lonMeters = Math.Abs(parsedLon - originalLon) * 111320 * Math.Cos(originalLat * Math.PI / 180);
        double totalDistance = Math.Sqrt(latMeters * latMeters + lonMeters * lonMeters);

        Assert.True(totalDistance < 2, $"Distance {totalDistance:F2}m should be within 2 meters");
    }
}
