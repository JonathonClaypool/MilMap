using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MilMap.Core.Elevation;

/// <summary>
/// Result of an elevation grid query.
/// </summary>
public record ElevationGrid(
    double MinLat,
    double MaxLat,
    double MinLon,
    double MaxLon,
    int Rows,
    int Cols,
    double?[,] Elevations)
{
    /// <summary>
    /// Gets the latitude step between rows.
    /// </summary>
    public double LatStep => (MaxLat - MinLat) / (Rows - 1);

    /// <summary>
    /// Gets the longitude step between columns.
    /// </summary>
    public double LonStep => (MaxLon - MinLon) / (Cols - 1);
}

/// <summary>
/// Configuration for the elevation data source.
/// </summary>
public class ElevationDataSourceOptions
{
    /// <summary>
    /// Directory for caching downloaded elevation tiles.
    /// </summary>
    public string CacheDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MilMap", "elevation-cache");

    /// <summary>
    /// Base URL for SRTM tile downloads.
    /// Uses the USGS EarthExplorer or OpenTopography mirror format.
    /// </summary>
    public string TileServerUrl { get; set; } = "https://elevation-tiles-prod.s3.amazonaws.com/skadi/{ns}{lat:D2}/{ns}{lat:D2}{ew}{lon:D3}.hgt.gz";

    /// <summary>
    /// User-Agent header for requests.
    /// </summary>
    public string UserAgent { get; set; } = "MilMap/1.0 (https://github.com/milmap/milmap)";

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to use SRTM1 (1 arc-second, ~30m) or SRTM3 (3 arc-second, ~90m).
    /// </summary>
    public bool UseSrtm1 { get; set; } = true;
}

/// <summary>
/// Provides elevation data from SRTM (Shuttle Radar Topography Mission) tiles.
/// </summary>
public class SrtmElevationSource : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ElevationDataSourceOptions _options;
    private readonly ConcurrentDictionary<string, ElevationTile?> _tileCache = new();
    private readonly SemaphoreSlim _downloadSemaphore = new(2);
    private bool _disposed;

    /// <summary>
    /// Resolution for SRTM1 tiles (1 arc-second).
    /// </summary>
    public const int Srtm1Resolution = 3601;

    /// <summary>
    /// Resolution for SRTM3 tiles (3 arc-second).
    /// </summary>
    public const int Srtm3Resolution = 1201;

    public SrtmElevationSource() : this(new ElevationDataSourceOptions()) { }

    public SrtmElevationSource(ElevationDataSourceOptions options)
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

        // Ensure cache directory exists
        if (!string.IsNullOrEmpty(options.CacheDirectory))
        {
            Directory.CreateDirectory(options.CacheDirectory);
        }
    }

    /// <summary>
    /// Gets the elevation at a specific coordinate.
    /// </summary>
    /// <param name="lat">Latitude</param>
    /// <param name="lon">Longitude</param>
    /// <param name="interpolate">Whether to use bilinear interpolation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Elevation in meters, or null if no data available</returns>
    public async Task<double?> GetElevationAsync(double lat, double lon,
        bool interpolate = true, CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(lat, lon);

        var tile = await GetTileAsync(lat, lon, cancellationToken);
        if (tile == null)
            return null;

        return interpolate
            ? tile.GetElevationInterpolated(lat, lon)
            : tile.GetElevation(lat, lon);
    }

    /// <summary>
    /// Gets an elevation grid for a bounding box.
    /// </summary>
    /// <param name="minLat">Minimum latitude</param>
    /// <param name="maxLat">Maximum latitude</param>
    /// <param name="minLon">Minimum longitude</param>
    /// <param name="maxLon">Maximum longitude</param>
    /// <param name="rows">Number of rows in the output grid</param>
    /// <param name="cols">Number of columns in the output grid</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Grid of elevation values</returns>
    public async Task<ElevationGrid> GetElevationGridAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        int rows, int cols, CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(minLat, minLon);
        ValidateCoordinates(maxLat, maxLon);
        if (minLat >= maxLat)
            throw new ArgumentException("minLat must be less than maxLat");
        if (minLon >= maxLon)
            throw new ArgumentException("minLon must be less than maxLon");
        if (rows < 2 || cols < 2)
            throw new ArgumentException("Grid must have at least 2 rows and 2 columns");

        // Pre-load all required tiles
        await PreloadTilesAsync(minLat, maxLat, minLon, maxLon, cancellationToken);

        // Build the elevation grid
        var elevations = new double?[rows, cols];
        double latStep = (maxLat - minLat) / (rows - 1);
        double lonStep = (maxLon - minLon) / (cols - 1);

        for (int r = 0; r < rows; r++)
        {
            double lat = maxLat - r * latStep; // North to south
            for (int c = 0; c < cols; c++)
            {
                double lon = minLon + c * lonStep;
                var tile = await GetTileAsync(lat, lon, cancellationToken);
                elevations[r, c] = tile?.GetElevationInterpolated(lat, lon);
            }
        }

        return new ElevationGrid(minLat, maxLat, minLon, maxLon, rows, cols, elevations);
    }

    /// <summary>
    /// Pre-downloads tiles for a bounding box.
    /// </summary>
    private async Task PreloadTilesAsync(double minLat, double maxLat,
        double minLon, double maxLon, CancellationToken cancellationToken)
    {
        int latMin = (int)Math.Floor(minLat);
        int latMax = (int)Math.Floor(maxLat);
        int lonMin = (int)Math.Floor(minLon);
        int lonMax = (int)Math.Floor(maxLon);

        var tasks = new List<Task>();
        for (int lat = latMin; lat <= latMax; lat++)
        {
            for (int lon = lonMin; lon <= lonMax; lon++)
            {
                int tileLat = lat;
                int tileLon = lon;
                tasks.Add(GetTileAsync(tileLat + 0.5, tileLon + 0.5, cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets a tile for the given coordinate.
    /// </summary>
    private async Task<ElevationTile?> GetTileAsync(double lat, double lon,
        CancellationToken cancellationToken)
    {
        int tileLat = (int)Math.Floor(lat);
        int tileLon = (int)Math.Floor(lon);
        string key = GetTileKey(tileLat, tileLon);

        // Check memory cache
        if (_tileCache.TryGetValue(key, out var cachedTile))
            return cachedTile;

        // Check disk cache
        string cachePath = GetTileCachePath(tileLat, tileLon);
        if (File.Exists(cachePath))
        {
            var tile = await LoadTileFromDiskAsync(cachePath, tileLat, tileLon);
            _tileCache[key] = tile;
            return tile;
        }

        // Download tile
        await _downloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring semaphore
            if (_tileCache.TryGetValue(key, out cachedTile))
                return cachedTile;

            var tile = await DownloadTileAsync(tileLat, tileLon, cancellationToken);
            _tileCache[key] = tile;
            return tile;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Downloads a tile from the server.
    /// </summary>
    private async Task<ElevationTile?> DownloadTileAsync(int lat, int lon,
        CancellationToken cancellationToken)
    {
        string url = BuildTileUrl(lat, lon);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Tile might not exist (ocean, polar regions, etc.)
                return null;
            }

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            // Decompress if gzipped
            if (url.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) || 
                response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                data = DecompressGzip(data);
            }

            // Save to disk cache
            string cachePath = GetTileCachePath(lat, lon);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllBytesAsync(cachePath, data, cancellationToken);

            return ParseHgtData(data, lat, lon);
        }
        catch (HttpRequestException)
        {
            // Tile doesn't exist or network error
            return null;
        }
    }

    /// <summary>
    /// Loads a tile from the disk cache.
    /// </summary>
    private async Task<ElevationTile?> LoadTileFromDiskAsync(string path, int lat, int lon)
    {
        var data = await File.ReadAllBytesAsync(path);
        return ParseHgtData(data, lat, lon);
    }

    /// <summary>
    /// Parses raw HGT data into an ElevationTile.
    /// </summary>
    private ElevationTile? ParseHgtData(byte[] data, int lat, int lon)
    {
        // Determine resolution from file size
        int resolution;
        if (data.Length == Srtm1Resolution * Srtm1Resolution * 2)
        {
            resolution = Srtm1Resolution;
        }
        else if (data.Length == Srtm3Resolution * Srtm3Resolution * 2)
        {
            resolution = Srtm3Resolution;
        }
        else
        {
            // Invalid or corrupt file
            return null;
        }

        // Parse big-endian shorts
        var elevations = new short[resolution * resolution];
        for (int i = 0; i < elevations.Length; i++)
        {
            // HGT files are big-endian signed 16-bit integers
            elevations[i] = (short)((data[i * 2] << 8) | data[i * 2 + 1]);
        }

        return new ElevationTile(lat, lon, resolution, elevations);
    }

    private byte[] DecompressGzip(byte[] data)
    {
        using var compressedStream = new MemoryStream(data);
        using var gzipStream = new System.IO.Compression.GZipStream(
            compressedStream, System.IO.Compression.CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        gzipStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    private string BuildTileUrl(int lat, int lon)
    {
        string ns = lat >= 0 ? "N" : "S";
        string ew = lon >= 0 ? "E" : "W";
        int absLat = Math.Abs(lat);
        int absLon = Math.Abs(lon);

        return _options.TileServerUrl
            .Replace("{ns}", ns)
            .Replace("{ew}", ew)
            .Replace("{lat:D2}", absLat.ToString("D2"))
            .Replace("{lon:D3}", absLon.ToString("D3"))
            .Replace("{lat}", absLat.ToString())
            .Replace("{lon}", absLon.ToString());
    }

    private string GetTileKey(int lat, int lon)
    {
        string ns = lat >= 0 ? "N" : "S";
        string ew = lon >= 0 ? "E" : "W";
        return $"{ns}{Math.Abs(lat):D2}{ew}{Math.Abs(lon):D3}";
    }

    private string GetTileCachePath(int lat, int lon)
    {
        string key = GetTileKey(lat, lon);
        return Path.Combine(_options.CacheDirectory, $"{key}.hgt");
    }

    private static void ValidateCoordinates(double lat, double lon)
    {
        if (lat < -90 || lat > 90)
            throw new ArgumentOutOfRangeException(nameof(lat), "Latitude must be between -90 and 90");
        if (lon < -180 || lon > 180)
            throw new ArgumentOutOfRangeException(nameof(lon), "Longitude must be between -180 and 180");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _downloadSemaphore.Dispose();
            _disposed = true;
        }
    }
}
