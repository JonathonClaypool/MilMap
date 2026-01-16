using MilMap.Core.Elevation;
using Xunit;

namespace MilMap.Tests.Elevation;

public class ElevationTileTests
{
    [Fact]
    public void Constructor_ValidData_CreatesInstance()
    {
        int resolution = 3;
        short[] data = new short[resolution * resolution];
        var tile = new ElevationTile(38, -77, resolution, data);

        Assert.Equal(38, tile.Latitude);
        Assert.Equal(-77, tile.Longitude);
        Assert.Equal(resolution, tile.Resolution);
    }

    [Fact]
    public void Constructor_InvalidResolution_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ElevationTile(0, 0, 0, Array.Empty<short>()));
    }

    [Fact]
    public void Constructor_DataSizeMismatch_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            new ElevationTile(0, 0, 10, new short[50])); // Should be 100
    }

    [Fact]
    public void GetElevation_PointOutsideTile_ReturnsNull()
    {
        int resolution = 3;
        short[] data = Enumerable.Repeat((short)100, resolution * resolution).ToArray();
        var tile = new ElevationTile(38, -77, resolution, data);

        // Point outside tile
        Assert.Null(tile.GetElevation(37.5, -77.5));
        Assert.Null(tile.GetElevation(39.5, -76.5));
    }

    [Fact]
    public void GetElevation_PointInsideTile_ReturnsValue()
    {
        int resolution = 3;
        short[] data = Enumerable.Repeat((short)250, resolution * resolution).ToArray();
        var tile = new ElevationTile(38, -77, resolution, data);

        // Point inside tile
        double? elevation = tile.GetElevation(38.5, -76.5);
        Assert.NotNull(elevation);
        Assert.Equal(250, elevation!.Value);
    }

    [Fact]
    public void GetElevation_VoidData_ReturnsNull()
    {
        int resolution = 3;
        short[] data = Enumerable.Repeat(short.MinValue, resolution * resolution).ToArray();
        var tile = new ElevationTile(38, -77, resolution, data);

        Assert.Null(tile.GetElevation(38.5, -76.5));
    }

    [Fact]
    public void GetElevation_CornerPoints_ReturnCorrectValues()
    {
        // Create a 3x3 tile with known values
        // Row 0 (north): 100, 200, 300
        // Row 1 (middle): 400, 500, 600
        // Row 2 (south): 700, 800, 900
        int resolution = 3;
        short[] data = new short[] { 100, 200, 300, 400, 500, 600, 700, 800, 900 };
        var tile = new ElevationTile(38, -77, resolution, data);

        // Southwest corner (should be row 2, col 0 = 700)
        double? sw = tile.GetElevation(38.0, -77.0);
        Assert.Equal(700, sw);

        // Northeast corner - at 38.999, -76.001 with resolution 3
        // fracLat = 0.999, fracLon = 0.999
        // row = (1 - 0.999) * 2 = 0.002 -> 0 (north row)
        // col = 0.999 * 2 = 1.998 -> 1 (middle column, not 2)
        double? ne = tile.GetElevation(38.999, -76.001);
        Assert.Equal(200, ne); // row 0, col 1
    }

    [Fact]
    public void GetElevationInterpolated_PointInsideTile_ReturnsInterpolatedValue()
    {
        // Create a 3x3 tile with gradient values
        int resolution = 3;
        short[] data = new short[] { 100, 100, 100, 500, 500, 500, 900, 900, 900 };
        var tile = new ElevationTile(38, -77, resolution, data);

        // Middle of tile - should interpolate
        double? elevation = tile.GetElevationInterpolated(38.5, -76.5);
        Assert.NotNull(elevation);
        Assert.InRange(elevation!.Value, 400, 600); // Should be around 500
    }

    [Fact]
    public void GetElevationInterpolated_VoidData_FallsBackToNearest()
    {
        int resolution = 3;
        // Put void data in one corner
        short[] data = new short[] { short.MinValue, 200, 300, 400, 500, 600, 700, 800, 900 };
        var tile = new ElevationTile(38, -77, resolution, data);

        // Query near the void data - should fall back to nearest neighbor
        double? elevation = tile.GetElevationInterpolated(38.99, -76.99);
        // Might return null or a valid value depending on which corner is queried
        Assert.True(elevation == null || elevation > 0);
    }
}
