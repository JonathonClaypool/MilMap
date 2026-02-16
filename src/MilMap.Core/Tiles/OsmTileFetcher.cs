using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MilMap.Core.Progress;
using SkiaSharp;

namespace MilMap.Core.Tiles;

/// <summary>
/// Represents a single map tile with its coordinates and image data.
/// </summary>
public record TileData(int X, int Y, int Zoom, byte[] ImageData);

/// <summary>
/// Result of a tile fetch operation.
/// </summary>
public record TileFetchResult(
    IReadOnlyList<TileData> Tiles,
    IReadOnlyList<TileFetchError> Errors);

/// <summary>
/// Error information for a failed tile fetch.
/// </summary>
public record TileFetchError(int X, int Y, int Zoom, string ErrorMessage);

/// <summary>
/// Configuration options for the tile fetcher.
/// </summary>
public class TileFetcherOptions
{
    /// <summary>
    /// Base URL for the tile server. Default is USGS National Map Topo which provides
    /// detailed topographic maps with vegetation cover, contour lines, water features,
    /// and no military installation hatching/overlay.
    /// Use {z}, {x}, {y} placeholders for tile coordinates.
    /// Note: USGS tile URL uses {z}/{y}/{x} order (ArcGIS REST convention).
    /// </summary>
    public string TileServerUrl { get; set; } = "https://basemap.nationalmap.gov/arcgis/rest/services/USGSTopo/MapServer/tile/{z}/{y}/{x}";

    /// <summary>
    /// User-Agent header for requests. Required by tile server usage policies.
    /// </summary>
    public string UserAgent { get; set; } = "MilMap/1.0 (military-map-generator; https://github.com/milmap/milmap)";

    /// <summary>
    /// Maximum concurrent downloads. Default is 4 for USGS servers.
    /// Reduce to 2 if using OSM tile servers per their usage policy.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Number of retry attempts for failed downloads.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Delay between retry attempts in milliseconds.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1500;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum zoom level supported by the tile server.
    /// USGS National Map supports up to zoom 16. OSM supports up to 18.
    /// </summary>
    public int MaxZoom { get; set; } = 16;
}

/// <summary>
/// Downloads OSM map tiles for a given bounding box.
/// </summary>
public class OsmTileFetcher : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TileFetcherOptions _options;
    private bool _disposed;

    /// <summary>
    /// The configured tile server URL template.
    /// </summary>
    public string TileServerUrl => _options.TileServerUrl;

    public OsmTileFetcher() : this(new TileFetcherOptions()) { }

    public OsmTileFetcher(TileFetcherOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
    }

    /// <summary>
    /// Calculates the tile coordinates needed to cover a bounding box at a given zoom level.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> CalculateTileCoordinates(
        double minLat, double maxLat, double minLon, double maxLon, int zoom)
    {
        ValidateBoundingBox(minLat, maxLat, minLon, maxLon);
        ValidateZoomLevel(zoom);

        int minTileX = LonToTileX(minLon, zoom);
        int maxTileX = LonToTileX(maxLon, zoom);
        int minTileY = LatToTileY(maxLat, zoom); // Note: Y is inverted
        int maxTileY = LatToTileY(minLat, zoom);

        var tiles = new List<(int X, int Y)>();
        for (int x = minTileX; x <= maxTileX; x++)
        {
            for (int y = minTileY; y <= maxTileY; y++)
            {
                tiles.Add((x, y));
            }
        }

        return tiles;
    }

    /// <summary>
    /// Downloads all tiles for a bounding box at the specified zoom level.
    /// </summary>
    public async Task<TileFetchResult> FetchTilesAsync(
        double minLat, double maxLat, double minLon, double maxLon, int zoom,
        IProgress<TileDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var coordinates = CalculateTileCoordinates(minLat, maxLat, minLon, maxLon, zoom);
        return await FetchTilesAsync(coordinates, zoom, progress, cancellationToken);
    }

    /// <summary>
    /// Downloads tiles for the specified coordinates.
    /// </summary>
    public async Task<TileFetchResult> FetchTilesAsync(
        IReadOnlyList<(int X, int Y)> coordinates, int zoom,
        IProgress<TileDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateZoomLevel(zoom);

        var tiles = new ConcurrentBag<TileData>();
        var errors = new ConcurrentBag<TileFetchError>();
        int downloadedCount = 0;
        int failedCount = 0;

        using var semaphore = new SemaphoreSlim(_options.MaxConcurrency);

        // Report initial progress
        progress?.Report(new TileDownloadProgress
        {
            Downloaded = 0,
            FromCache = 0,
            Total = coordinates.Count,
            Failed = 0
        });

        var tasks = coordinates.Select(async coord =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await FetchSingleTileWithRetryAsync(coord.X, coord.Y, zoom, cancellationToken);
                if (result != null)
                {
                    tiles.Add(result);
                }
                else
                {
                    // Tile doesn't exist (404) — generate a placeholder to avoid white gaps
                    tiles.Add(CreatePlaceholderTile(coord.X, coord.Y, zoom));
                }
                int current = Interlocked.Increment(ref downloadedCount);
                progress?.Report(new TileDownloadProgress
                {
                    Downloaded = current,
                    FromCache = 0,
                    Total = coordinates.Count,
                    Failed = Volatile.Read(ref failedCount)
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Generate placeholder tile for failed downloads too
                tiles.Add(CreatePlaceholderTile(coord.X, coord.Y, zoom));
                errors.Add(new TileFetchError(coord.X, coord.Y, zoom, ex.Message));
                int failed = Interlocked.Increment(ref failedCount);
                progress?.Report(new TileDownloadProgress
                {
                    Downloaded = Volatile.Read(ref downloadedCount),
                    FromCache = 0,
                    Total = coordinates.Count,
                    Failed = failed
                });
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return new TileFetchResult(tiles.ToList(), errors.ToList());
    }

    /// <summary>
    /// Downloads a single tile.
    /// </summary>
    public async Task<TileData?> FetchTileAsync(int x, int y, int zoom,
        CancellationToken cancellationToken = default)
    {
        ValidateZoomLevel(zoom);
        ValidateTileCoordinates(x, y, zoom);

        string url = BuildTileUrl(x, y, zoom);

        var response = await _httpClient.GetAsync(url, cancellationToken);

        // Return null for 404s (tile doesn't exist at this zoom level)
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        byte[] imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new TileData(x, y, zoom, imageData);
    }

    private async Task<TileData?> FetchSingleTileWithRetryAsync(int x, int y, int zoom,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                return await FetchTileAsync(x, y, zoom, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                if (attempt < _options.MaxRetries)
                {
                    // Exponential backoff: 1.5s, 3s, 6s, 12s, 24s
                    int delay = _options.RetryDelayMs * (1 << attempt);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Failed to fetch tile");
    }

    private string BuildTileUrl(int x, int y, int zoom)
    {
        return _options.TileServerUrl
            .Replace("{z}", zoom.ToString())
            .Replace("{x}", x.ToString())
            .Replace("{y}", y.ToString());
    }

    private static int LonToTileX(double lon, int zoom)
    {
        int n = 1 << zoom;
        int x = (int)((lon + 180.0) / 360.0 * n);
        return Math.Clamp(x, 0, n - 1);
    }

    private static int LatToTileY(double lat, int zoom)
    {
        int n = 1 << zoom;
        double latRad = lat * Math.PI / 180.0;
        double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return Math.Clamp((int)y, 0, n - 1);
    }

    private static void ValidateBoundingBox(double minLat, double maxLat, double minLon, double maxLon)
    {
        if (minLat < -85.0511 || maxLat > 85.0511)
            throw new ArgumentOutOfRangeException(nameof(minLat), "Latitude must be between -85.0511 and 85.0511 for Web Mercator");
        if (minLat >= maxLat)
            throw new ArgumentException("minLat must be less than maxLat");
        if (minLon >= maxLon)
            throw new ArgumentException("minLon must be less than maxLon");
    }

    private static void ValidateZoomLevel(int zoom)
    {
        if (zoom < 0 || zoom > 19)
            throw new ArgumentOutOfRangeException(nameof(zoom), "Zoom level must be between 0 and 19");
    }

    private static void ValidateTileCoordinates(int x, int y, int zoom)
    {
        int maxTile = (1 << zoom) - 1;
        if (x < 0 || x > maxTile)
            throw new ArgumentOutOfRangeException(nameof(x), $"X coordinate must be between 0 and {maxTile} for zoom {zoom}");
        if (y < 0 || y > maxTile)
            throw new ArgumentOutOfRangeException(nameof(y), $"Y coordinate must be between 0 and {maxTile} for zoom {zoom}");
    }

    /// <summary>
    /// Creates a neutral-colored placeholder tile for missing/failed tiles
    /// to prevent white gaps in the rendered map.
    /// </summary>
    private static TileData CreatePlaceholderTile(int x, int y, int zoom)
    {
        using var bitmap = new SKBitmap(TileSize, TileSize);
        using var canvas = new SKCanvas(bitmap);
        // Use a light neutral gray that blends with topo map backgrounds
        canvas.Clear(new SKColor(246, 246, 246));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        return new TileData(x, y, zoom, data.ToArray());
    }

    private const int TileSize = 256;

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
