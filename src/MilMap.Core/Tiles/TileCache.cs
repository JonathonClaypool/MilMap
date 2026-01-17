using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MilMap.Core.Progress;

namespace MilMap.Core.Tiles;

/// <summary>
/// Configuration options for the tile cache.
/// </summary>
public class TileCacheOptions
{
    /// <summary>
    /// Root directory for cached tiles. Defaults to user's local app data.
    /// </summary>
    public string CacheDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MilMap", "TileCache");

    /// <summary>
    /// Maximum age of cached tiles before they're considered stale. Default is 30 days.
    /// </summary>
    public TimeSpan MaxTileAge { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Maximum cache size in bytes. Default is 500 MB.
    /// </summary>
    public long MaxCacheSizeBytes { get; set; } = 500 * 1024 * 1024;

    /// <summary>
    /// Whether to use stale tiles if fresh fetch fails. Default is true.
    /// </summary>
    public bool UseStaleOnError { get; set; } = true;
}

/// <summary>
/// Provides local file-based caching for map tiles.
/// </summary>
public class TileCache : IDisposable
{
    private readonly OsmTileFetcher _fetcher;
    private readonly TileCacheOptions _options;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Creates a new tile cache with default options.
    /// </summary>
    public TileCache() : this(new OsmTileFetcher(), new TileCacheOptions()) { }

    /// <summary>
    /// Creates a new tile cache with the specified fetcher and options.
    /// </summary>
    public TileCache(OsmTileFetcher fetcher, TileCacheOptions options)
    {
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        EnsureCacheDirectoryExists();
    }

    /// <summary>
    /// Gets a tile from cache or fetches it from the server.
    /// </summary>
    public async Task<TileData?> GetTileAsync(int x, int y, int zoom,
        CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        var cachedTile = await GetFromCacheAsync(x, y, zoom, cancellationToken);
        if (cachedTile != null && !IsTileStale(x, y, zoom))
        {
            return cachedTile;
        }

        // Fetch from server
        try
        {
            var fetchedTile = await _fetcher.FetchTileAsync(x, y, zoom, cancellationToken);
            if (fetchedTile != null)
            {
                await SaveToCacheAsync(fetchedTile, cancellationToken);
                return fetchedTile;
            }
        }
        catch when (_options.UseStaleOnError && cachedTile != null)
        {
            // Return stale tile on error
            return cachedTile;
        }

        return cachedTile;
    }

    /// <summary>
    /// Gets multiple tiles, using cache where available.
    /// </summary>
    public async Task<TileFetchResult> GetTilesAsync(
        double minLat, double maxLat, double minLon, double maxLon, int zoom,
        IProgress<TileDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var coordinates = OsmTileFetcher.CalculateTileCoordinates(minLat, maxLat, minLon, maxLon, zoom);
        return await GetTilesAsync(coordinates, zoom, progress, cancellationToken);
    }

    /// <summary>
    /// Gets multiple tiles by coordinates, using cache where available.
    /// </summary>
    public async Task<TileFetchResult> GetTilesAsync(
        IReadOnlyList<(int X, int Y)> coordinates, int zoom,
        IProgress<TileDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tiles = new List<TileData>();
        var errors = new List<TileFetchError>();
        var tilesToFetch = new List<(int X, int Y)>();
        int fromCacheCount = 0;

        // Check cache for each tile
        foreach (var (x, y) in coordinates)
        {
            var cachedTile = await GetFromCacheAsync(x, y, zoom, cancellationToken);
            if (cachedTile != null && !IsTileStale(x, y, zoom))
            {
                tiles.Add(cachedTile);
                fromCacheCount++;
                progress?.Report(new TileDownloadProgress
                {
                    Downloaded = 0,
                    FromCache = fromCacheCount,
                    Total = coordinates.Count,
                    Failed = 0
                });
            }
            else
            {
                tilesToFetch.Add((x, y));
            }
        }

        // Fetch missing tiles
        if (tilesToFetch.Count > 0)
        {
            // Create a wrapper progress to combine cache and download counts
            var downloadProgress = progress != null
                ? new Progress<TileDownloadProgress>(p =>
                {
                    progress.Report(new TileDownloadProgress
                    {
                        Downloaded = p.Downloaded,
                        FromCache = fromCacheCount,
                        Total = coordinates.Count,
                        Failed = p.Failed
                    });
                })
                : null;

            var fetchResult = await _fetcher.FetchTilesAsync(tilesToFetch, zoom, downloadProgress, cancellationToken);
            
            foreach (var tile in fetchResult.Tiles)
            {
                await SaveToCacheAsync(tile, cancellationToken);
                tiles.Add(tile);
            }

            foreach (var error in fetchResult.Errors)
            {
                // Try to use stale cache on error
                if (_options.UseStaleOnError)
                {
                    var staleTile = await GetFromCacheAsync(error.X, error.Y, zoom, cancellationToken);
                    if (staleTile != null)
                    {
                        tiles.Add(staleTile);
                        continue;
                    }
                }
                errors.Add(error);
            }
        }

        return new TileFetchResult(tiles, errors);
    }

    /// <summary>
    /// Clears all cached tiles.
    /// </summary>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cleanupLock.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(_options.CacheDirectory))
            {
                Directory.Delete(_options.CacheDirectory, recursive: true);
                EnsureCacheDirectoryExists();
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    /// <summary>
    /// Gets the current cache size in bytes.
    /// </summary>
    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(_options.CacheDirectory))
            return 0;

        return new DirectoryInfo(_options.CacheDirectory)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }

    /// <summary>
    /// Removes stale tiles and enforces cache size limits.
    /// </summary>
    public async Task CleanupCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cleanupLock.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(_options.CacheDirectory))
                return;

            var cacheDir = new DirectoryInfo(_options.CacheDirectory);
            var files = cacheDir.EnumerateFiles("*.png", SearchOption.AllDirectories)
                .OrderBy(f => f.LastAccessTime)
                .ToList();

            // Remove stale files
            var staleTime = DateTime.Now - _options.MaxTileAge;
            foreach (var file in files.Where(f => f.LastWriteTime < staleTime))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                    // File may be in use
                }
            }

            // Enforce size limit (remove oldest accessed files first)
            long currentSize = files.Sum(f => f.Exists ? f.Length : 0);
            foreach (var file in files)
            {
                if (currentSize <= _options.MaxCacheSizeBytes)
                    break;

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (file.Exists)
                    {
                        currentSize -= file.Length;
                        file.Delete();
                    }
                }
                catch (IOException)
                {
                    // File may be in use
                }
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    private async Task<TileData?> GetFromCacheAsync(int x, int y, int zoom,
        CancellationToken cancellationToken)
    {
        string filePath = GetTilePath(x, y, zoom);
        if (!File.Exists(filePath))
            return null;

        try
        {
            byte[] imageData = await File.ReadAllBytesAsync(filePath, cancellationToken);
            // Update last access time for LRU tracking
            File.SetLastAccessTime(filePath, DateTime.Now);
            return new TileData(x, y, zoom, imageData);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task SaveToCacheAsync(TileData tile, CancellationToken cancellationToken)
    {
        string filePath = GetTilePath(tile.X, tile.Y, tile.Zoom);
        string? directory = Path.GetDirectoryName(filePath);
        
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(filePath, tile.ImageData, cancellationToken);
    }

    private bool IsTileStale(int x, int y, int zoom)
    {
        string filePath = GetTilePath(x, y, zoom);
        if (!File.Exists(filePath))
            return true;

        var fileInfo = new FileInfo(filePath);
        return DateTime.Now - fileInfo.LastWriteTime > _options.MaxTileAge;
    }

    private string GetTilePath(int x, int y, int zoom)
    {
        // Organize by zoom/x/y.png
        return Path.Combine(_options.CacheDirectory, zoom.ToString(), x.ToString(), $"{y}.png");
    }

    private void EnsureCacheDirectoryExists()
    {
        Directory.CreateDirectory(_options.CacheDirectory);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _fetcher.Dispose();
            _cleanupLock.Dispose();
            _disposed = true;
        }
    }
}
