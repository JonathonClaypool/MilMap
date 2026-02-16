using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MilMap.Core.Osm;

/// <summary>
/// Represents a geographic element from the OSM Overpass API.
/// </summary>
public record OsmElement
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("lat")]
    public double? Lat { get; init; }

    [JsonPropertyName("lon")]
    public double? Lon { get; init; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; init; }

    [JsonPropertyName("nodes")]
    public long[]? Nodes { get; init; }

    [JsonPropertyName("geometry")]
    public OsmGeometryPoint[]? Geometry { get; init; }

    [JsonPropertyName("members")]
    public OsmMember[]? Members { get; init; }
}

/// <summary>
/// Represents a member of an OSM relation.
/// </summary>
public record OsmMember
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("ref")]
    public long Ref { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("geometry")]
    public OsmGeometryPoint[]? Geometry { get; init; }
}

/// <summary>
/// Represents a point in OSM geometry.
/// </summary>
public record OsmGeometryPoint
{
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lon")]
    public double Lon { get; init; }
}

/// <summary>
/// Response from the Overpass API.
/// </summary>
public record OverpassResponse
{
    [JsonPropertyName("version")]
    public double Version { get; init; }

    [JsonPropertyName("generator")]
    public string Generator { get; init; } = string.Empty;

    [JsonPropertyName("elements")]
    public OsmElement[] Elements { get; init; } = Array.Empty<OsmElement>();
}

/// <summary>
/// Result of an Overpass query operation.
/// </summary>
public record OverpassQueryResult(
    OverpassResponse? Response,
    bool Success,
    string? ErrorMessage);

/// <summary>
/// Configuration options for the Overpass client.
/// </summary>
public class OverpassClientOptions
{
    /// <summary>
    /// Base URL for the Overpass API endpoint.
    /// </summary>
    public string ApiUrl { get; set; } = "https://overpass-api.de/api/interpreter";

    /// <summary>
    /// User-Agent header for requests. Required by OSM usage policy.
    /// </summary>
    public string UserAgent { get; set; } = "MilMap/1.0 (https://github.com/milmap/milmap)";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// Maximum number of retry attempts for failed requests.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before first retry.
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds between retries.
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;

    /// <summary>
    /// Minimum time in milliseconds between requests (rate limiting).
    /// </summary>
    public int MinRequestIntervalMs { get; set; } = 1000;
}

/// <summary>
/// Client for querying the OSM Overpass API for vector features.
/// </summary>
public class OverpassClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly OverpassClientOptions _options;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private bool _disposed;

    public OverpassClient() : this(new OverpassClientOptions()) { }

    public OverpassClient(OverpassClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
    }

    /// <summary>
    /// Queries for roads within a bounding box.
    /// </summary>
    public async Task<OverpassQueryResult> QueryRoadsAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        string query = BuildQuery(minLat, maxLat, minLon, maxLon, "highway");
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    /// <summary>
    /// Queries for buildings within a bounding box.
    /// </summary>
    public async Task<OverpassQueryResult> QueryBuildingsAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        string query = BuildQuery(minLat, maxLat, minLon, maxLon, "building");
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    /// <summary>
    /// Queries for waterways within a bounding box.
    /// </summary>
    public async Task<OverpassQueryResult> QueryWaterwaysAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        string query = BuildQuery(minLat, maxLat, minLon, maxLon, "waterway");
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    /// <summary>
    /// Queries for natural features (water bodies, forests, etc.) within a bounding box.
    /// </summary>
    public async Task<OverpassQueryResult> QueryNaturalFeaturesAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        string query = BuildQuery(minLat, maxLat, minLon, maxLon, "natural");
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    /// <summary>
    /// Queries for contour lines within a bounding box.
    /// </summary>
    public async Task<OverpassQueryResult> QueryContoursAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        string query = BuildQuery(minLat, maxLat, minLon, maxLon, "contour");
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    /// <summary>
    /// Queries for landuse areas within a bounding box.
    /// </summary>
    public async Task<OverpassQueryResult> QueryLanduseAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        string query = BuildQuery(minLat, maxLat, minLon, maxLon, "landuse");
        return await ExecuteQueryAsync(query, cancellationToken);
    }

    /// <summary>
    /// Queries for all military-relevant features within a bounding box.
    /// Includes roads, buildings, waterways, natural features, and landuse.
    /// </summary>
    public async Task<OverpassQueryResult> QueryAllFeaturesAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        ValidateBoundingBox(minLat, maxLat, minLon, maxLon);

        string bbox = $"{minLat},{minLon},{maxLat},{maxLon}";
        string query = $@"[out:json][timeout:{_options.TimeoutSeconds}];
(
  way[""highway""]({bbox});
  way[""building""]({bbox});
  way[""waterway""]({bbox});
  way[""natural""]({bbox});
  way[""landuse""]({bbox});
  relation[""natural""=""water""]({bbox});
  relation[""landuse""]({bbox});
);
out body;
>;
out skel qt;";

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    /// <summary>
    /// Executes a raw Overpass QL query.
    /// </summary>
    public async Task<OverpassQueryResult> ExecuteQueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            await RateLimitAsync(cancellationToken);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("data", query)
            });

            var response = await _httpClient.PostAsync(_options.ApiUrl, content, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new OverpassRateLimitException("Rate limit exceeded");
            }

            if (response.StatusCode == HttpStatusCode.GatewayTimeout ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new OverpassServerException($"Server returned {response.StatusCode}");
            }

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            var overpassResponse = JsonSerializer.Deserialize<OverpassResponse>(json);

            return new OverpassQueryResult(overpassResponse, true, null);
        }, cancellationToken);
    }

    private string BuildQuery(double minLat, double maxLat, double minLon, double maxLon, string tag)
    {
        ValidateBoundingBox(minLat, maxLat, minLon, maxLon);

        string bbox = $"{minLat},{minLon},{maxLat},{maxLon}";
        return $@"[out:json][timeout:{_options.TimeoutSeconds}];
(
  way[""{tag}""]({bbox});
);
out body;
>;
out skel qt;";
    }

    private async Task RateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var requiredDelay = TimeSpan.FromMilliseconds(_options.MinRequestIntervalMs) - timeSinceLastRequest;

            if (requiredDelay > TimeSpan.Zero)
            {
                await Task.Delay(requiredDelay, cancellationToken);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task<OverpassQueryResult> ExecuteWithRetryAsync(
        Func<Task<OverpassQueryResult>> operation,
        CancellationToken cancellationToken)
    {
        int attempt = 0;
        int delay = _options.InitialRetryDelayMs;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRetryableException(ex) && attempt < _options.MaxRetries)
            {
                attempt++;
                await Task.Delay(delay, cancellationToken);
                delay = Math.Min(delay * 2, _options.MaxRetryDelayMs);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new OverpassQueryResult(null, false, ex.Message);
            }
        }
    }

    private static bool IsRetryableException(Exception ex)
    {
        return ex is HttpRequestException or
               OverpassRateLimitException or
               OverpassServerException or
               TaskCanceledException;
    }

    private static void ValidateBoundingBox(double minLat, double maxLat, double minLon, double maxLon)
    {
        if (minLat < -90 || maxLat > 90)
            throw new ArgumentOutOfRangeException(nameof(minLat), "Latitude must be between -90 and 90");
        if (minLon < -180 || maxLon > 180)
            throw new ArgumentOutOfRangeException(nameof(minLon), "Longitude must be between -180 and 180");
        if (minLat >= maxLat)
            throw new ArgumentException("minLat must be less than maxLat");
        if (minLon >= maxLon)
            throw new ArgumentException("minLon must be less than maxLon");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _rateLimiter.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Exception thrown when the Overpass API rate limit is exceeded.
/// </summary>
public class OverpassRateLimitException : Exception
{
    public OverpassRateLimitException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when the Overpass API server returns an error.
/// </summary>
public class OverpassServerException : Exception
{
    public OverpassServerException(string message) : base(message) { }
}
