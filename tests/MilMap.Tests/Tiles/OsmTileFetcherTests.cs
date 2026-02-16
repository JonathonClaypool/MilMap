using MilMap.Core.Tiles;
using Xunit;

namespace MilMap.Tests.Tiles;

public class OsmTileFetcherTests
{
    [Theory]
    [InlineData(0, 0)]    // Zoom 0: entire world is 1 tile
    [InlineData(1, 0)]    // Zoom 1: 2x2 grid
    [InlineData(10, 0)]   // Zoom 10: 1024x1024 grid
    public void LonToTileX_ReturnsCorrectTile(int zoom, double lon)
    {
        var coordinates = OsmTileFetcher.CalculateTileCoordinates(0, 1, lon, lon + 0.001, zoom);

        // At least one tile returned
        Assert.NotEmpty(coordinates);
    }

    [Fact]
    public void CalculateTileCoordinates_SinglePoint_ReturnsSingleTile()
    {
        // A very small bounding box at zoom 10
        var coordinates = OsmTileFetcher.CalculateTileCoordinates(51.5, 51.501, -0.13, -0.129, 10);

        Assert.Single(coordinates);
    }

    [Fact]
    public void CalculateTileCoordinates_LargerArea_ReturnsMultipleTiles()
    {
        // London area at zoom 12
        var coordinates = OsmTileFetcher.CalculateTileCoordinates(51.4, 51.6, -0.3, 0.1, 12);

        Assert.True(coordinates.Count > 1);
    }

    [Fact]
    public void CalculateTileCoordinates_ZoomZero_ReturnsSingleTile()
    {
        // At zoom 0, the entire world is one tile
        var coordinates = OsmTileFetcher.CalculateTileCoordinates(-85, 85, -180, 179.99, 0);

        Assert.Single(coordinates);
        Assert.Equal((0, 0), coordinates[0]);
    }

    [Theory]
    [InlineData(0, 1)]    // Zoom 0: 1 tile total
    [InlineData(1, 4)]    // Zoom 1: 4 tiles total
    [InlineData(2, 16)]   // Zoom 2: 16 tiles total
    public void CalculateTileCoordinates_WorldBounds_ReturnsCorrectCount(int zoom, int expectedTileCount)
    {
        var coordinates = OsmTileFetcher.CalculateTileCoordinates(-85, 85, -180, 179.99, zoom);

        Assert.Equal(expectedTileCount, coordinates.Count);
    }

    [Fact]
    public void CalculateTileCoordinates_ValidatesMinMaxLat()
    {
        Assert.Throws<ArgumentException>(() =>
            OsmTileFetcher.CalculateTileCoordinates(50, 40, -1, 1, 10)); // min > max
    }

    [Fact]
    public void CalculateTileCoordinates_ValidatesMinMaxLon()
    {
        Assert.Throws<ArgumentException>(() =>
            OsmTileFetcher.CalculateTileCoordinates(40, 50, 1, -1, 10)); // min > max
    }

    [Fact]
    public void CalculateTileCoordinates_ValidatesLatitudeRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OsmTileFetcher.CalculateTileCoordinates(-90, 90, -1, 1, 10)); // Outside Web Mercator range
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(20)]
    public void CalculateTileCoordinates_InvalidZoom_ThrowsException(int zoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OsmTileFetcher.CalculateTileCoordinates(40, 50, -1, 1, zoom));
    }

    [Fact]
    public void TileFetcherOptions_HasCorrectDefaults()
    {
        var options = new TileFetcherOptions();

        Assert.Contains("opentopomap.org", options.TileServerUrl);
        Assert.Equal(2, options.MaxConcurrency);
        Assert.Equal(3, options.MaxRetries);
        Assert.NotEmpty(options.UserAgent);
    }

    [Fact]
    public void OsmTileFetcher_CanBeCreatedWithDefaultOptions()
    {
        using var fetcher = new OsmTileFetcher();
        Assert.NotNull(fetcher);
    }

    [Fact]
    public void OsmTileFetcher_CanBeCreatedWithCustomOptions()
    {
        var options = new TileFetcherOptions
        {
            TileServerUrl = "https://example.com/{z}/{x}/{y}.png",
            MaxConcurrency = 4,
            UserAgent = "TestAgent/1.0"
        };

        using var fetcher = new OsmTileFetcher(options);
        Assert.NotNull(fetcher);
    }

    [Fact]
    public void TileData_StoresCorrectValues()
    {
        byte[] imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var tile = new TileData(10, 20, 5, imageData);

        Assert.Equal(10, tile.X);
        Assert.Equal(20, tile.Y);
        Assert.Equal(5, tile.Zoom);
        Assert.Equal(imageData, tile.ImageData);
    }

    [Fact]
    public void TileFetchError_StoresCorrectValues()
    {
        var error = new TileFetchError(10, 20, 5, "Connection timeout");

        Assert.Equal(10, error.X);
        Assert.Equal(20, error.Y);
        Assert.Equal(5, error.Zoom);
        Assert.Equal("Connection timeout", error.ErrorMessage);
    }

    [Fact]
    public void TileFetchResult_StoresCorrectValues()
    {
        var tiles = new List<TileData> { new TileData(0, 0, 0, new byte[] { 1 }) };
        var errors = new List<TileFetchError> { new TileFetchError(1, 1, 0, "Error") };
        var result = new TileFetchResult(tiles, errors);

        Assert.Single(result.Tiles);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void CalculateTileCoordinates_WashingtonDC_ReturnsExpectedTiles()
    {
        // Washington DC area at zoom 14
        double minLat = 38.88, maxLat = 38.90;
        double minLon = -77.04, maxLon = -77.02;

        var coordinates = OsmTileFetcher.CalculateTileCoordinates(minLat, maxLat, minLon, maxLon, 14);

        Assert.True(coordinates.Count >= 1);
        Assert.True(coordinates.Count <= 9); // Should be a small number of tiles
    }

    [Fact]
    public void CalculateTileCoordinates_TilesCoverEntireArea()
    {
        // Verify that returned tiles actually cover the requested area
        double minLat = 40.0, maxLat = 41.0;
        double minLon = -75.0, maxLon = -74.0;
        int zoom = 10;

        var coordinates = OsmTileFetcher.CalculateTileCoordinates(minLat, maxLat, minLon, maxLon, zoom);

        // Get the min/max tile coordinates
        int minX = coordinates.Min(c => c.X);
        int maxX = coordinates.Max(c => c.X);
        int minY = coordinates.Min(c => c.Y);
        int maxY = coordinates.Max(c => c.Y);

        // Verify we have all tiles in the rectangle
        int expectedCount = (maxX - minX + 1) * (maxY - minY + 1);
        Assert.Equal(expectedCount, coordinates.Count);
    }
}
