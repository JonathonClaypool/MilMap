using MilMap.Core.Osm;
using Xunit;

namespace MilMap.Tests.Osm;

public class OverpassClientTests
{
    [Fact]
    public void OverpassClientOptions_HasCorrectDefaults()
    {
        var options = new OverpassClientOptions();

        Assert.Contains("overpass-api.de", options.ApiUrl);
        Assert.Equal(180, options.TimeoutSeconds);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(1000, options.InitialRetryDelayMs);
        Assert.Equal(30000, options.MaxRetryDelayMs);
        Assert.Equal(1000, options.MinRequestIntervalMs);
        Assert.NotEmpty(options.UserAgent);
    }

    [Fact]
    public void OverpassClient_CanBeCreatedWithDefaultOptions()
    {
        using var client = new OverpassClient();
        Assert.NotNull(client);
    }

    [Fact]
    public void OverpassClient_CanBeCreatedWithCustomOptions()
    {
        var options = new OverpassClientOptions
        {
            ApiUrl = "https://example.com/api/interpreter",
            TimeoutSeconds = 60,
            UserAgent = "TestAgent/1.0"
        };

        using var client = new OverpassClient(options);
        Assert.NotNull(client);
    }

    [Fact]
    public void OverpassClient_ThrowsOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new OverpassClient(null!));
    }

    [Fact]
    public void OsmElement_DefaultValues()
    {
        var element = new OsmElement();

        Assert.Equal(string.Empty, element.Type);
        Assert.Equal(0, element.Id);
        Assert.Null(element.Lat);
        Assert.Null(element.Lon);
        Assert.Null(element.Tags);
        Assert.Null(element.Nodes);
        Assert.Null(element.Geometry);
    }

    [Fact]
    public void OsmElement_CanStoreValues()
    {
        var element = new OsmElement
        {
            Type = "way",
            Id = 12345,
            Tags = new Dictionary<string, string> { { "highway", "primary" } }
        };

        Assert.Equal("way", element.Type);
        Assert.Equal(12345, element.Id);
        Assert.Single(element.Tags);
        Assert.Equal("primary", element.Tags["highway"]);
    }

    [Fact]
    public void OsmGeometryPoint_StoresCoordinates()
    {
        var point = new OsmGeometryPoint { Lat = 51.5, Lon = -0.1 };

        Assert.Equal(51.5, point.Lat);
        Assert.Equal(-0.1, point.Lon);
    }

    [Fact]
    public void OverpassResponse_DefaultValues()
    {
        var response = new OverpassResponse();

        Assert.Equal(0, response.Version);
        Assert.Equal(string.Empty, response.Generator);
        Assert.Empty(response.Elements);
    }

    [Fact]
    public void OverpassQueryResult_SuccessResult()
    {
        var response = new OverpassResponse();
        var result = new OverpassQueryResult(response, true, null);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void OverpassQueryResult_FailureResult()
    {
        var result = new OverpassQueryResult(null, false, "Connection failed");

        Assert.False(result.Success);
        Assert.Null(result.Response);
        Assert.Equal("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public void OverpassRateLimitException_ContainsMessage()
    {
        var exception = new OverpassRateLimitException("Rate limit exceeded");
        Assert.Equal("Rate limit exceeded", exception.Message);
    }

    [Fact]
    public void OverpassServerException_ContainsMessage()
    {
        var exception = new OverpassServerException("Server error");
        Assert.Equal("Server error", exception.Message);
    }

    [Theory]
    [InlineData(50, 40, -1, 1)]   // minLat > maxLat
    [InlineData(40, 50, 1, -1)]   // minLon > maxLon
    public async Task QueryRoadsAsync_InvalidBoundingBox_ReturnsError(
        double minLat, double maxLat, double minLon, double maxLon)
    {
        using var client = new OverpassClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.QueryRoadsAsync(minLat, maxLat, minLon, maxLon));
    }

    [Theory]
    [InlineData(-100, 0, -1, 1)]  // latitude out of range
    [InlineData(0, 100, -1, 1)]   // latitude out of range
    [InlineData(40, 50, -200, 0)] // longitude out of range
    [InlineData(40, 50, 0, 200)]  // longitude out of range
    public async Task QueryRoadsAsync_OutOfRangeCoordinates_ReturnsError(
        double minLat, double maxLat, double minLon, double maxLon)
    {
        using var client = new OverpassClient();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.QueryRoadsAsync(minLat, maxLat, minLon, maxLon));
    }

    [Fact]
    public void OverpassClient_CanBeDisposedMultipleTimes()
    {
        var client = new OverpassClient();
        client.Dispose();
        client.Dispose(); // Should not throw
    }

    [Fact]
    public void OverpassClientOptions_RateLimitingOptionsAreConfigurable()
    {
        var options = new OverpassClientOptions
        {
            MinRequestIntervalMs = 2000,
            MaxRetries = 5,
            InitialRetryDelayMs = 500,
            MaxRetryDelayMs = 60000
        };

        Assert.Equal(2000, options.MinRequestIntervalMs);
        Assert.Equal(5, options.MaxRetries);
        Assert.Equal(500, options.InitialRetryDelayMs);
        Assert.Equal(60000, options.MaxRetryDelayMs);
    }

    [Fact]
    public void OverpassClientOptions_DefaultsRespectOsmPolicy()
    {
        var options = new OverpassClientOptions();

        // OSM policy requires at least 1 second between requests
        Assert.True(options.MinRequestIntervalMs >= 1000, 
            "Rate limiting should default to at least 1 second between requests");
        
        // Should have retry capability for transient failures
        Assert.True(options.MaxRetries > 0, 
            "Should have retry capability for transient failures");
        
        // Initial retry delay should be reasonable
        Assert.True(options.InitialRetryDelayMs >= 1000, 
            "Initial retry delay should be at least 1 second");
        
        // Max retry delay should support exponential backoff
        Assert.True(options.MaxRetryDelayMs >= options.InitialRetryDelayMs * 4, 
            "Max retry delay should support exponential backoff");
    }

    [Fact]
    public void OverpassClientOptions_UserAgentIsSet()
    {
        var options = new OverpassClientOptions();

        // OSM policy requires a valid User-Agent
        Assert.False(string.IsNullOrWhiteSpace(options.UserAgent), 
            "User-Agent is required by OSM policy");
        Assert.Contains("MilMap", options.UserAgent);
    }
}
