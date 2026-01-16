using MilMap.Core.Mgrs;
using Xunit;

namespace MilMap.Tests.Mgrs;

public class MgrsEncoderTests
{
    [Theory]
    [InlineData(38.8895, -77.0353, "18SUJ2338606785")] // Washington DC (White House area)
    [InlineData(51.5074, -0.1278, "30UXC9193334064")] // London
    [InlineData(48.8566, 2.3522, "31UDQ4823520870")] // Paris
    [InlineData(35.6762, 139.6503, "54SUE0937149618")] // Tokyo
    [InlineData(-33.8688, 151.2093, "56HLH3394852474")] // Sydney
    [InlineData(0.0, 0.0, "31NAA6602109998")] // Equator/Prime Meridian
    public void Encode_KnownLocations_ReturnsExpectedMgrs(double lat, double lon, string expectedPrefix)
    {
        var result = MgrsEncoder.Encode(lat, lon, MgrsEncoder.Precision.OneMeter);
        
        // Check that zone, band and 100k square match (first 5 characters)
        Assert.StartsWith(expectedPrefix[..5], result);
    }

    [Theory]
    [InlineData(38.8895, -77.0353, MgrsEncoder.Precision.TenKilometers, 7)] // 4 (zone+band) + 2 (100k) + 1+1 (grid)
    [InlineData(38.8895, -77.0353, MgrsEncoder.Precision.OneKilometer, 9)]
    [InlineData(38.8895, -77.0353, MgrsEncoder.Precision.HundredMeters, 11)]
    [InlineData(38.8895, -77.0353, MgrsEncoder.Precision.TenMeters, 13)]
    [InlineData(38.8895, -77.0353, MgrsEncoder.Precision.OneMeter, 15)]
    public void Encode_VariousPrecisions_ReturnsCorrectLength(double lat, double lon, MgrsEncoder.Precision precision, int expectedLength)
    {
        var result = MgrsEncoder.Encode(lat, lon, precision);
        
        Assert.Equal(expectedLength, result.Length);
    }

    [Fact]
    public void Encode_EquatorPrimeMeridian_ReturnsZone31()
    {
        var result = MgrsEncoder.Encode(0.0, 0.0);
        
        Assert.StartsWith("31N", result);
    }

    [Fact]
    public void Encode_SouthernHemisphere_ReturnsCorrectBand()
    {
        var result = MgrsEncoder.Encode(-33.8688, 151.2093);
        
        Assert.StartsWith("56H", result);
    }

    [Theory]
    [InlineData(60.0, 5.0, "32V")] // Norway special zone
    [InlineData(76.0, 10.0, "33X")] // Svalbard
    public void Encode_SpecialZones_HandledCorrectly(double lat, double lon, string expectedZoneBand)
    {
        var result = MgrsEncoder.Encode(lat, lon);
        
        Assert.StartsWith(expectedZoneBand, result);
    }

    [Theory]
    [InlineData(85.0, 0.0)] // North pole region
    [InlineData(88.0, 45.0)] // Deep in north polar region
    [InlineData(-82.0, 0.0)] // South pole region
    [InlineData(-88.0, -90.0)] // Deep in south polar region
    public void Encode_PolarRegions_ReturnsUpsCoordinates(double lat, double lon)
    {
        var result = MgrsEncoder.Encode(lat, lon);
        
        // UPS coordinates start with Y/Z (north) or A/B (south), not a zone number
        char firstChar = result[0];
        if (lat >= 84.0)
        {
            Assert.True(firstChar == 'Y' || firstChar == 'Z', $"Expected Y or Z for north polar, got {firstChar}");
        }
        else
        {
            Assert.True(firstChar == 'A' || firstChar == 'B', $"Expected A or B for south polar, got {firstChar}");
        }
    }

    [Theory]
    [InlineData(-91.0, 0.0)]
    [InlineData(91.0, 0.0)]
    public void Encode_InvalidLatitude_ThrowsArgumentOutOfRangeException(double lat, double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MgrsEncoder.Encode(lat, lon));
    }

    [Theory]
    [InlineData(0.0, -181.0)]
    [InlineData(0.0, 181.0)]
    public void Encode_InvalidLongitude_ThrowsArgumentOutOfRangeException(double lat, double lon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MgrsEncoder.Encode(lat, lon));
    }

    [Fact]
    public void Encode_ExtremeSouthernLatitude_ReturnsValidMgrs()
    {
        var result = MgrsEncoder.Encode(-79.9, 0.0);
        
        Assert.StartsWith("31C", result);
    }

    [Fact]
    public void Encode_ExtremeNorthernLatitude_ReturnsValidMgrs()
    {
        var result = MgrsEncoder.Encode(83.9, 0.0);
        
        Assert.StartsWith("31X", result);
    }

    [Theory]
    [InlineData(-180.0)]
    [InlineData(-90.0)]
    [InlineData(0.0)]
    [InlineData(90.0)]
    [InlineData(180.0)]
    public void Encode_AllLongitudes_ReturnsValidMgrs(double lon)
    {
        var result = MgrsEncoder.Encode(45.0, lon);
        
        Assert.NotNull(result);
        Assert.True(result.Length >= 7);
    }
}
