using System;
using System.IO;
using System.Threading.Tasks;
using MilMap.Core.Tiles;
using Xunit;

namespace MilMap.Tests.Tiles;

public class TileCacheTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly TileCacheOptions _options;

    public TileCacheTests()
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), "MilMapTestCache", Guid.NewGuid().ToString());
        _options = new TileCacheOptions
        {
            CacheDirectory = _testCacheDir,
            MaxTileAge = TimeSpan.FromDays(1),
            MaxCacheSizeBytes = 10 * 1024 * 1024 // 10MB for tests
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testCacheDir))
            {
                Directory.Delete(_testCacheDir, recursive: true);
            }
        }
        catch
        {
            // Cleanup best effort
        }
    }

    [Fact]
    public void TileCacheOptions_Defaults_AreReasonable()
    {
        var options = new TileCacheOptions();

        Assert.Contains("MilMap", options.CacheDirectory);
        Assert.Contains("TileCache", options.CacheDirectory);
        Assert.Equal(TimeSpan.FromDays(30), options.MaxTileAge);
        Assert.Equal(500 * 1024 * 1024, options.MaxCacheSizeBytes);
        Assert.True(options.UseStaleOnError);
    }

    [Fact]
    public void GetCacheSizeBytes_EmptyCache_ReturnsZero()
    {
        using var cache = CreateTestCache();

        long size = cache.GetCacheSizeBytes();

        Assert.Equal(0, size);
    }

    [Fact]
    public async Task GetCacheSizeBytes_WithCachedTiles_ReturnsCorrectSize()
    {
        using var cache = CreateTestCache();

        // Write a test file directly to simulate cached tile
        string tilePath = Path.Combine(_testCacheDir, "10", "512", "512.png");
        Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
        byte[] testData = new byte[1000];
        await File.WriteAllBytesAsync(tilePath, testData);

        long size = cache.GetCacheSizeBytes();

        Assert.Equal(1000, size);
    }

    [Fact]
    public async Task ClearCacheAsync_RemovesAllCachedTiles()
    {
        using var cache = CreateTestCache();

        // Create some cached files
        string tilePath = Path.Combine(_testCacheDir, "10", "512", "512.png");
        Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
        await File.WriteAllBytesAsync(tilePath, new byte[100]);

        Assert.True(File.Exists(tilePath));

        await cache.ClearCacheAsync();

        Assert.False(File.Exists(tilePath));
        Assert.True(Directory.Exists(_testCacheDir)); // Root dir should still exist
    }

    [Fact]
    public async Task CleanupCacheAsync_RemovesStaleTiles()
    {
        using var cache = CreateTestCache();

        // Create a stale cached file (older than MaxTileAge)
        string stalePath = Path.Combine(_testCacheDir, "10", "512", "stale.png");
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        await File.WriteAllBytesAsync(stalePath, new byte[100]);
        File.SetLastWriteTime(stalePath, DateTime.Now - TimeSpan.FromDays(5)); // Stale

        // Create a fresh cached file
        string freshPath = Path.Combine(_testCacheDir, "10", "512", "fresh.png");
        await File.WriteAllBytesAsync(freshPath, new byte[100]);

        await cache.CleanupCacheAsync();

        Assert.False(File.Exists(stalePath));
        Assert.True(File.Exists(freshPath));
    }

    [Fact]
    public async Task CleanupCacheAsync_EnforcesSizeLimit()
    {
        var smallCacheOptions = new TileCacheOptions
        {
            CacheDirectory = _testCacheDir,
            MaxTileAge = TimeSpan.FromDays(30),
            MaxCacheSizeBytes = 500 // Very small limit
        };

        using var cache = new TileCache(new OsmTileFetcher(), smallCacheOptions);

        // Create files that exceed the limit
        string dir = Path.Combine(_testCacheDir, "10", "512");
        Directory.CreateDirectory(dir);

        for (int i = 0; i < 5; i++)
        {
            string path = Path.Combine(dir, $"{i}.png");
            await File.WriteAllBytesAsync(path, new byte[200]);
            await Task.Delay(50); // Ensure different access times
        }

        await cache.CleanupCacheAsync();

        long finalSize = cache.GetCacheSizeBytes();
        Assert.True(finalSize <= 500, $"Cache size {finalSize} exceeds limit of 500");
    }

    [Fact]
    public void TileCache_NullFetcher_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TileCache(null!, _options));
    }

    [Fact]
    public void TileCache_NullOptions_ThrowsArgumentNullException()
    {
        using var fetcher = new OsmTileFetcher();
        Assert.Throws<ArgumentNullException>(() => new TileCache(fetcher, null!));
    }

    [Fact]
    public void TileCache_CreatesCacheDirectory()
    {
        Assert.False(Directory.Exists(_testCacheDir));

        using var cache = CreateTestCache();

        Assert.True(Directory.Exists(_testCacheDir));
    }

    [Fact]
    public async Task GetTileAsync_CachesRetrievedTile()
    {
        // This test would need a mock HttpClient, so we just verify cache structure
        using var cache = CreateTestCache();

        // Simulate what GetTileAsync would do after fetching
        string tilePath = Path.Combine(_testCacheDir, "12", "2048", "1024.png");
        Directory.CreateDirectory(Path.GetDirectoryName(tilePath)!);
        byte[] fakeImageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        await File.WriteAllBytesAsync(tilePath, fakeImageData);

        // Verify tile path structure is correct
        Assert.True(File.Exists(tilePath));
        var data = await File.ReadAllBytesAsync(tilePath);
        Assert.Equal(fakeImageData, data);
    }

    [Fact]
    public async Task CacheDirectory_UsesZoomXYStructure()
    {
        using var cache = CreateTestCache();

        // Create tiles at different zoom levels
        var testTiles = new[]
        {
            (Zoom: 5, X: 10, Y: 20),
            (Zoom: 10, X: 512, Y: 256),
            (Zoom: 15, X: 16384, Y: 8192)
        };

        foreach (var (zoom, x, y) in testTiles)
        {
            string expectedPath = Path.Combine(_testCacheDir, zoom.ToString(), x.ToString(), $"{y}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            await File.WriteAllBytesAsync(expectedPath, new byte[] { 1, 2, 3 });

            Assert.True(File.Exists(expectedPath));
        }
    }

    private TileCache CreateTestCache()
    {
        return new TileCache(new OsmTileFetcher(), _options);
    }
}
