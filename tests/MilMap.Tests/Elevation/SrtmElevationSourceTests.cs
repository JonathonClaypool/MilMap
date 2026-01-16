using MilMap.Core.Elevation;
using Xunit;

namespace MilMap.Tests.Elevation;

public class SrtmElevationSourceTests
{
    [Fact]
    public void ElevationDataSourceOptions_HasCorrectDefaults()
    {
        var options = new ElevationDataSourceOptions();

        Assert.Contains("elevation", options.CacheDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(options.TileServerUrl);
        Assert.NotEmpty(options.UserAgent);
        Assert.True(options.TimeoutSeconds > 0);
        Assert.True(options.UseSrtm1);
    }

    [Fact]
    public void SrtmElevationSource_CanBeCreatedWithDefaultOptions()
    {
        using var source = new SrtmElevationSource();
        Assert.NotNull(source);
    }

    [Fact]
    public void SrtmElevationSource_CanBeCreatedWithCustomOptions()
    {
        var options = new ElevationDataSourceOptions
        {
            CacheDirectory = Path.Combine(Path.GetTempPath(), "test-elevation-cache"),
            UserAgent = "TestAgent/1.0"
        };

        using var source = new SrtmElevationSource(options);
        Assert.NotNull(source);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public async Task GetElevationAsync_InvalidCoordinates_ThrowsException(double lat, double lon)
    {
        using var source = new SrtmElevationSource();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            source.GetElevationAsync(lat, lon));
    }

    [Fact]
    public async Task GetElevationGridAsync_InvalidBounds_ThrowsException()
    {
        using var source = new SrtmElevationSource();

        // minLat >= maxLat
        await Assert.ThrowsAsync<ArgumentException>(() =>
            source.GetElevationGridAsync(40, 30, -80, -70, 10, 10));

        // minLon >= maxLon
        await Assert.ThrowsAsync<ArgumentException>(() =>
            source.GetElevationGridAsync(30, 40, -70, -80, 10, 10));
    }

    [Fact]
    public async Task GetElevationGridAsync_TooSmallGrid_ThrowsException()
    {
        using var source = new SrtmElevationSource();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            source.GetElevationGridAsync(30, 31, -80, -79, 1, 10));
    }

    [Fact]
    public void ElevationGrid_CalculatesStepsCorrectly()
    {
        var grid = new ElevationGrid(
            MinLat: 30.0,
            MaxLat: 31.0,
            MinLon: -80.0,
            MaxLon: -79.0,
            Rows: 11,
            Cols: 11,
            Elevations: new double?[11, 11]);

        Assert.Equal(0.1, grid.LatStep, 5);
        Assert.Equal(0.1, grid.LonStep, 5);
    }

    [Fact]
    public void Srtm1Resolution_IsCorrect()
    {
        // SRTM1 is 1 arc-second, meaning 3601 samples per degree
        Assert.Equal(3601, SrtmElevationSource.Srtm1Resolution);
    }

    [Fact]
    public void Srtm3Resolution_IsCorrect()
    {
        // SRTM3 is 3 arc-seconds, meaning 1201 samples per degree
        Assert.Equal(1201, SrtmElevationSource.Srtm3Resolution);
    }

    [Fact]
    public void Srtm1FileSize_IsCorrect()
    {
        // SRTM1: 3601 * 3601 * 2 bytes = 25,934,402 bytes
        int expectedSize = SrtmElevationSource.Srtm1Resolution * SrtmElevationSource.Srtm1Resolution * 2;
        Assert.Equal(25934402, expectedSize);
    }

    [Fact]
    public void Srtm3FileSize_IsCorrect()
    {
        // SRTM3: 1201 * 1201 * 2 bytes = 2,884,802 bytes
        int expectedSize = SrtmElevationSource.Srtm3Resolution * SrtmElevationSource.Srtm3Resolution * 2;
        Assert.Equal(2884802, expectedSize);
    }

    [Fact]
    public void SrtmElevationSource_DisposesCorrectly()
    {
        var source = new SrtmElevationSource();
        source.Dispose();

        // Double dispose should not throw
        source.Dispose();
    }
}
