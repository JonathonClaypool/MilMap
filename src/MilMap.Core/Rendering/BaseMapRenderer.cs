using System;
using SkiaSharp;
using MilMap.Core.Tiles;

namespace MilMap.Core.Rendering;

/// <summary>
/// Options for rendering the base map.
/// </summary>
public class MapRenderOptions
{
    /// <summary>
    /// Output DPI for the rendered image. Default is 300 for print quality.
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// JPEG quality when saving as JPEG (1-100). Default is 95.
    /// </summary>
    public int JpegQuality { get; set; } = 95;

    /// <summary>
    /// PNG compression level (1-9). Default is 6 (balanced).
    /// </summary>
    public int PngCompressionLevel { get; set; } = 6;
}

/// <summary>
/// Renders OSM tiles into a composite map image.
/// </summary>
public class BaseMapRenderer : IDisposable
{
    private const int TileSize = 256; // Standard OSM tile size
    private readonly MapRenderOptions _options;
    private bool _disposed;

    public BaseMapRenderer() : this(new MapRenderOptions()) { }

    public BaseMapRenderer(MapRenderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Renders tiles into a composite image for the specified bounding box.
    /// </summary>
    /// <param name="tiles">The tiles to render</param>
    /// <param name="minLat">Minimum latitude of the output</param>
    /// <param name="maxLat">Maximum latitude of the output</param>
    /// <param name="minLon">Minimum longitude of the output</param>
    /// <param name="maxLon">Maximum longitude of the output</param>
    /// <param name="zoom">Zoom level of the tiles</param>
    /// <returns>Composite map image as byte array (PNG format)</returns>
    public byte[] RenderMap(
        IReadOnlyList<TileData> tiles,
        double minLat, double maxLat,
        double minLon, double maxLon,
        int zoom)
    {
        if (tiles == null || tiles.Count == 0)
            throw new ArgumentException("No tiles to render", nameof(tiles));

        // Calculate the tile range
        int minTileX = tiles.Min(t => t.X);
        int maxTileX = tiles.Max(t => t.X);
        int minTileY = tiles.Min(t => t.Y);
        int maxTileY = tiles.Max(t => t.Y);

        int tilesWide = maxTileX - minTileX + 1;
        int tilesHigh = maxTileY - minTileY + 1;

        // Create full tile canvas
        int fullWidth = tilesWide * TileSize;
        int fullHeight = tilesHigh * TileSize;

        using var fullBitmap = new SKBitmap(fullWidth, fullHeight);
        using var canvas = new SKCanvas(fullBitmap);

        // Clear to white background
        canvas.Clear(SKColors.White);

        // Draw each tile
        foreach (var tile in tiles)
        {
            int offsetX = (tile.X - minTileX) * TileSize;
            int offsetY = (tile.Y - minTileY) * TileSize;

            using var tileImage = SKBitmap.Decode(tile.ImageData);
            if (tileImage != null)
            {
                canvas.DrawBitmap(tileImage, offsetX, offsetY);
            }
        }

        // Calculate crop region to match exact bounding box
        var (cropRect, outputWidth, outputHeight) = CalculateCropRegion(
            minLat, maxLat, minLon, maxLon,
            minTileX, maxTileX, minTileY, maxTileY,
            zoom);

        // Crop to exact bounding box
        using var croppedBitmap = new SKBitmap(outputWidth, outputHeight);
        using var cropCanvas = new SKCanvas(croppedBitmap);

        cropCanvas.DrawBitmap(fullBitmap, cropRect, new SKRect(0, 0, outputWidth, outputHeight));

        // Encode to PNG
        using var image = SKImage.FromBitmap(croppedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <summary>
    /// Renders tiles into a composite image and saves to a file.
    /// </summary>
    public void RenderMapToFile(
        IReadOnlyList<TileData> tiles,
        double minLat, double maxLat,
        double minLon, double maxLon,
        int zoom,
        string outputPath)
    {
        var imageData = RenderMap(tiles, minLat, maxLat, minLon, maxLon, zoom);
        File.WriteAllBytes(outputPath, imageData);
    }

    /// <summary>
    /// Renders tiles as JPEG with configurable quality.
    /// </summary>
    public byte[] RenderMapAsJpeg(
        IReadOnlyList<TileData> tiles,
        double minLat, double maxLat,
        double minLon, double maxLon,
        int zoom)
    {
        if (tiles == null || tiles.Count == 0)
            throw new ArgumentException("No tiles to render", nameof(tiles));

        int minTileX = tiles.Min(t => t.X);
        int maxTileX = tiles.Max(t => t.X);
        int minTileY = tiles.Min(t => t.Y);
        int maxTileY = tiles.Max(t => t.Y);

        int tilesWide = maxTileX - minTileX + 1;
        int tilesHigh = maxTileY - minTileY + 1;

        int fullWidth = tilesWide * TileSize;
        int fullHeight = tilesHigh * TileSize;

        using var fullBitmap = new SKBitmap(fullWidth, fullHeight);
        using var canvas = new SKCanvas(fullBitmap);
        canvas.Clear(SKColors.White);

        foreach (var tile in tiles)
        {
            int offsetX = (tile.X - minTileX) * TileSize;
            int offsetY = (tile.Y - minTileY) * TileSize;

            using var tileImage = SKBitmap.Decode(tile.ImageData);
            if (tileImage != null)
            {
                canvas.DrawBitmap(tileImage, offsetX, offsetY);
            }
        }

        var (cropRect, outputWidth, outputHeight) = CalculateCropRegion(
            minLat, maxLat, minLon, maxLon,
            minTileX, maxTileX, minTileY, maxTileY,
            zoom);

        using var croppedBitmap = new SKBitmap(outputWidth, outputHeight);
        using var cropCanvas = new SKCanvas(croppedBitmap);
        cropCanvas.DrawBitmap(fullBitmap, cropRect, new SKRect(0, 0, outputWidth, outputHeight));

        using var image = SKImage.FromBitmap(croppedBitmap);

        // Try encoding as JPEG, fall back to PNG if JPEG encoding isn't available
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, _options.JpegQuality)
            ?? image.Encode(SKEncodedImageFormat.Png, 100);

        if (data == null)
            throw new InvalidOperationException("Failed to encode image");

        return data.ToArray();
    }

    /// <summary>
    /// Gets the dimensions the rendered map would have.
    /// </summary>
    public (int Width, int Height) CalculateOutputDimensions(
        double minLat, double maxLat,
        double minLon, double maxLon,
        int zoom)
    {
        int minTileX = LonToTileX(minLon, zoom);
        int maxTileX = LonToTileX(maxLon, zoom);
        int minTileY = LatToTileY(maxLat, zoom);
        int maxTileY = LatToTileY(minLat, zoom);

        var (_, width, height) = CalculateCropRegion(
            minLat, maxLat, minLon, maxLon,
            minTileX, maxTileX, minTileY, maxTileY,
            zoom);

        return (width, height);
    }

    /// <summary>
    /// Calculates the estimated memory requirements for rendering a map.
    /// </summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <returns>Memory requirements in bytes and formatted string.</returns>
    public static (long Bytes, long PixelCount, string FormattedSize) CalculateMemoryRequirements(int width, int height)
    {
        // SKBitmap uses 4 bytes per pixel (RGBA)
        const int BytesPerPixel = 4;
        long pixelCount = (long)width * height;
        long bytes = pixelCount * BytesPerPixel;
        string formatted = bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            _ => $"{bytes / 1024.0:F1} KB"
        };
        return (bytes, pixelCount, formatted);
    }

    /// <summary>
    /// Default maximum memory for bitmap allocation (2 GB).
    /// </summary>
    public const long DefaultMaxMemoryBytes = 2L * 1024 * 1024 * 1024;

    private (SKRect cropRect, int width, int height) CalculateCropRegion(
        double minLat, double maxLat,
        double minLon, double maxLon,
        int minTileX, int maxTileX,
        int minTileY, int maxTileY,
        int zoom)
    {
        // Calculate pixel positions within the tile grid
        double n = 1 << zoom;

        // Pixel position of min/max longitude
        double pixelMinX = ((minLon + 180.0) / 360.0 * n - minTileX) * TileSize;
        double pixelMaxX = ((maxLon + 180.0) / 360.0 * n - minTileX) * TileSize;

        // Pixel position of min/max latitude (note: Y is inverted)
        double latRadMax = maxLat * Math.PI / 180.0;
        double latRadMin = minLat * Math.PI / 180.0;

        double pixelMinY = ((1.0 - Math.Log(Math.Tan(latRadMax) + 1.0 / Math.Cos(latRadMax)) / Math.PI) / 2.0 * n - minTileY) * TileSize;
        double pixelMaxY = ((1.0 - Math.Log(Math.Tan(latRadMin) + 1.0 / Math.Cos(latRadMin)) / Math.PI) / 2.0 * n - minTileY) * TileSize;

        int cropX = (int)Math.Floor(pixelMinX);
        int cropY = (int)Math.Floor(pixelMinY);
        int cropWidth = (int)Math.Ceiling(pixelMaxX - pixelMinX);
        int cropHeight = (int)Math.Ceiling(pixelMaxY - pixelMinY);

        // Ensure minimum size
        cropWidth = Math.Max(1, cropWidth);
        cropHeight = Math.Max(1, cropHeight);

        return (new SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight), cropWidth, cropHeight);
    }

    private static int LonToTileX(double lon, int zoom)
    {
        int n = 1 << zoom;
        return Math.Clamp((int)((lon + 180.0) / 360.0 * n), 0, n - 1);
    }

    private static int LatToTileY(double lat, int zoom)
    {
        int n = 1 << zoom;
        double latRad = lat * Math.PI / 180.0;
        double y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return Math.Clamp((int)y, 0, n - 1);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
