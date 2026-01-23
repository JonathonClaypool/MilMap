using System;
using System.Threading;
using System.Threading.Tasks;
using MilMap.Core.Export;
using MilMap.Core.Input;
using MilMap.Core.Mgrs;
using MilMap.Core.Progress;
using MilMap.Core.Rendering;
using MilMap.Core.Tiles;
using SkiaSharp;

namespace MilMap.Core;

/// <summary>
/// Options for map generation.
/// </summary>
public class MapGeneratorOptions
{
    /// <summary>
    /// Bounding box for the map area.
    /// </summary>
    public BoundingBox Bounds { get; set; }

    /// <summary>
    /// Map scale denominator (e.g., 25000 for 1:25000).
    /// </summary>
    public int Scale { get; set; } = 25000;

    /// <summary>
    /// Output resolution in dots per inch.
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// Output file path.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Output format.
    /// </summary>
    public MapOutputFormat Format { get; set; } = MapOutputFormat.Pdf;

    /// <summary>
    /// Optional title for the map.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Cache directory for tiles. If null, uses system default.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Enable multi-page PDF output for large maps.
    /// When true, automatically splits the map into multiple pages.
    /// </summary>
    public bool MultiPage { get; set; }

    /// <summary>
    /// Page size for PDF output.
    /// </summary>
    public PageSize PageSize { get; set; } = PageSize.Letter;

    /// <summary>
    /// Page orientation for PDF output.
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;
}

/// <summary>
/// Output format for generated maps.
/// </summary>
public enum MapOutputFormat
{
    Pdf,
    Png,
    GeoTiff
}

/// <summary>
/// Result of map generation.
/// </summary>
public record MapGeneratorResult(
    bool Success,
    string OutputPath,
    string? ErrorMessage = null,
    int TilesDownloaded = 0,
    int TilesFromCache = 0,
    TimeSpan Duration = default);

/// <summary>
/// Orchestrates the map generation pipeline.
/// </summary>
public class MapGenerator : IDisposable
{
    private readonly TileCache _tileCache;
    private bool _disposed;

    public MapGenerator() : this(null) { }

    public MapGenerator(string? cacheDirectory)
    {
        var cacheOptions = new TileCacheOptions();
        if (!string.IsNullOrEmpty(cacheDirectory))
        {
            cacheOptions.CacheDirectory = cacheDirectory;
        }

        _tileCache = new TileCache(new OsmTileFetcher(), cacheOptions);
    }

    /// <summary>
    /// Generates a map with the specified options.
    /// </summary>
    public async Task<MapGeneratorResult> GenerateAsync(
        MapGeneratorOptions options,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        int tilesDownloaded = 0;
        int tilesFromCache = 0;

        try
        {
            // Step 1: Calculate zoom level
            progress?.Report(new ProgressInfo
            {
                CurrentStep = 1,
                TotalSteps = 4,
                StepDescription = "Calculating map parameters"
            });

            var zoomResult = ZoomLevelCalculator.CalculateZoom(
                options.Scale,
                options.Dpi,
                options.Bounds.CenterLat);

            if (zoomResult.Warning != null)
            {
                Console.WriteLine($"  Warning: {zoomResult.Warning}");
            }

            // Pre-flight validation: check memory requirements before proceeding
            // Skip for multi-page mode which renders sheets individually
            if (!options.MultiPage)
            {
                using var tempRenderer = new BaseMapRenderer(new MapRenderOptions { Dpi = options.Dpi });
                var (outputWidth, outputHeight) = tempRenderer.CalculateOutputDimensions(
                    options.Bounds.MinLat,
                    options.Bounds.MaxLat,
                    options.Bounds.MinLon,
                    options.Bounds.MaxLon,
                    zoomResult.Zoom);

                var (memoryBytes, pixelCount, memoryFormatted) = BaseMapRenderer.CalculateMemoryRequirements(outputWidth, outputHeight);

                if (memoryBytes > BaseMapRenderer.DefaultMaxMemoryBytes)
                {
                    return new MapGeneratorResult(
                        false,
                        options.OutputPath,
                        $"Map requires {memoryFormatted} of memory ({outputWidth:N0}x{outputHeight:N0} pixels, {pixelCount:N0} total), which exceeds the 2 GB limit. " +
                        $"Try: (1) Use --multi-page for automatic sheet splitting, (2) Use a smaller scale (e.g., 1:{options.Scale * 2:N0}), (3) Reduce DPI (current: {options.Dpi}), or (4) Select a smaller area.");
                }
            }

            // Step 2: Fetch tiles
            progress?.Report(new ProgressInfo
            {
                CurrentStep = 2,
                TotalSteps = 4,
                StepDescription = "Downloading map tiles",
                SubProgress = 0
            });

            var tileProgress = new Progress<TileDownloadProgress>(p =>
            {
                tilesDownloaded = p.Downloaded;
                tilesFromCache = p.FromCache;
                progress?.Report(new ProgressInfo
                {
                    CurrentStep = 2,
                    TotalSteps = 4,
                    StepDescription = $"Downloading tiles ({p.Downloaded + p.FromCache}/{p.Total})",
                    SubProgress = p.Progress
                });
            });

            var tileResult = await _tileCache.GetTilesAsync(
                options.Bounds.MinLat,
                options.Bounds.MaxLat,
                options.Bounds.MinLon,
                options.Bounds.MaxLon,
                zoomResult.Zoom,
                tileProgress,
                cancellationToken);

            if (tileResult.Tiles.Count == 0)
            {
                return new MapGeneratorResult(
                    false,
                    options.OutputPath,
                    "No tiles could be fetched for the specified area");
            }

            // Step 3: Render map
            progress?.Report(new ProgressInfo
            {
                CurrentStep = 3,
                TotalSteps = 4,
                StepDescription = "Rendering map"
            });

            // Step 4: Export
            progress?.Report(new ProgressInfo
            {
                CurrentStep = 4,
                TotalSteps = 4,
                StepDescription = "Exporting to file"
            });

            if (options.MultiPage && options.Format == MapOutputFormat.Pdf)
            {
                // Multi-page mode: render each sheet separately to avoid memory issues
                ExportMultiPagePdf(tileResult.Tiles, zoomResult.Zoom, options);
            }
            else
            {
                // Single-page mode: render entire map
                using var renderer = new BaseMapRenderer(new MapRenderOptions { Dpi = options.Dpi });
                var mapImageBytes = renderer.RenderMap(
                    tileResult.Tiles,
                    options.Bounds.MinLat,
                    options.Bounds.MaxLat,
                    options.Bounds.MinLon,
                    options.Bounds.MaxLon,
                    zoomResult.Zoom);

                using var mapBitmap = SKBitmap.Decode(mapImageBytes);
                ExportMap(mapBitmap, options);
            }

            var duration = DateTime.UtcNow - startTime;

            progress?.Report(new ProgressInfo
            {
                CurrentStep = 4,
                TotalSteps = 4,
                StepDescription = "Complete",
                SubProgress = 1.0
            });

            return new MapGeneratorResult(
                true,
                options.OutputPath,
                null,
                tilesDownloaded,
                tilesFromCache,
                duration);
        }
        catch (OperationCanceledException)
        {
            return new MapGeneratorResult(
                false,
                options.OutputPath,
                "Operation was cancelled");
        }
        catch (Exception ex)
        {
            return new MapGeneratorResult(
                false,
                options.OutputPath,
                ex.Message);
        }
    }

    private void ExportMap(SKBitmap mapBitmap, MapGeneratorOptions options)
    {
        switch (options.Format)
        {
            case MapOutputFormat.Pdf:
                ExportPdf(mapBitmap, options);
                break;
            case MapOutputFormat.Png:
                ExportPng(mapBitmap, options);
                break;
            case MapOutputFormat.GeoTiff:
                // GeoTiff export would require additional metadata
                // For now, export as regular TIFF/PNG
                ExportPng(mapBitmap, options);
                break;
        }
    }

    private void ExportPdf(SKBitmap mapBitmap, MapGeneratorOptions options)
    {
        var pdfOptions = new PdfExportOptions
        {
            Dpi = options.Dpi,
            Title = options.Title,
            ScaleText = $"1:{options.Scale:N0}"
        };

        var exporter = new PdfExporter(pdfOptions);
        exporter.Export(mapBitmap, null, options.OutputPath);
    }

    private void ExportPng(SKBitmap mapBitmap, MapGeneratorOptions options)
    {
        var imageOptions = new ImageExportOptions
        {
            Format = ImageFormat.Png,
            Dpi = options.Dpi,
            Quality = 95
        };

        var exporter = new ImageExporter(imageOptions);
        exporter.Export(mapBitmap, options.OutputPath);
    }

    private void ExportMultiPagePdf(IReadOnlyList<TileData> tiles, int zoom, MapGeneratorOptions options)
    {
        var multiPageOptions = new MultiPagePdfOptions
        {
            Dpi = options.Dpi,
            Title = options.Title,
            ScaleText = $"1:{options.Scale:N0}",
            PageSize = options.PageSize,
            Orientation = options.Orientation,
            IncludeIndexPage = true,
            IncludeSheetLabels = true,
            IncludeAdjacentReferences = true
        };

        var multiExporter = new MultiPagePdfExporter(multiPageOptions);

        // Calculate the layout
        var layout = multiExporter.CalculateLayout(
            options.Bounds.MinLat,
            options.Bounds.MaxLat,
            options.Bounds.MinLon,
            options.Bounds.MaxLon,
            options.Scale);

        // Render each sheet individually to avoid memory issues
        using var renderer = new BaseMapRenderer(new MapRenderOptions { Dpi = options.Dpi });

        foreach (var sheet in layout.Sheets)
        {
            // Filter tiles that overlap with this sheet's bounds
            var sheetTiles = tiles.Where(t => TileOverlapsSheet(t, sheet, zoom)).ToList();

            if (sheetTiles.Count > 0)
            {
                var sheetImageBytes = renderer.RenderMap(
                    sheetTiles,
                    sheet.MinLat,
                    sheet.MaxLat,
                    sheet.MinLon,
                    sheet.MaxLon,
                    zoom);

                sheet.MapImage = SKBitmap.Decode(sheetImageBytes);
            }
        }

        // Export the multi-page PDF
        multiExporter.Export(layout, options.OutputPath);

        // Clean up sheet images
        foreach (var sheet in layout.Sheets)
        {
            sheet.MapImage?.Dispose();
        }
    }

    private static bool TileOverlapsSheet(TileData tile, MapSheet sheet, int zoom)
    {
        // Calculate tile bounds
        int n = 1 << zoom;
        double tileMinLon = tile.X * 360.0 / n - 180.0;
        double tileMaxLon = (tile.X + 1) * 360.0 / n - 180.0;

        double tileMaxLat = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * tile.Y / n))) * 180.0 / Math.PI;
        double tileMinLat = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * (tile.Y + 1) / n))) * 180.0 / Math.PI;

        // Check for overlap
        return !(tileMaxLon < sheet.MinLon || tileMinLon > sheet.MaxLon ||
                 tileMaxLat < sheet.MinLat || tileMinLat > sheet.MaxLat);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _tileCache.Dispose();
            _disposed = true;
        }
    }
}
