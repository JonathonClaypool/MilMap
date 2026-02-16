using System;
using System.Threading;
using System.Threading.Tasks;
using MilMap.Core.Elevation;
using MilMap.Core.Export;
using MilMap.Core.Input;
using MilMap.Core.Mgrs;
using MilMap.Core.Progress;
using MilMap.Core.Rendering;
using MilMap.Core.Tiles;
using SkiaSharp;
using System.IO;

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
    /// When null (default), auto-detects based on map size vs page dimensions.
    /// </summary>
    public bool? MultiPage { get; set; }

    /// <summary>
    /// Page size for PDF output.
    /// </summary>
    public PageSize PageSize { get; set; } = PageSize.Letter;

    /// <summary>
    /// Page orientation for PDF output.
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;

    /// <summary>
    /// When true, overlay military range and impact area boundaries with
    /// semi-transparent red fills and labels from OSM data.
    /// </summary>
    public bool ShowRangeOverlay { get; set; } = false;
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
    private readonly TileFetcherOptions _fetcherOptions;
    private bool _disposed;

    public MapGenerator() : this(null) { }

    public MapGenerator(string? cacheDirectory)
    {
        _fetcherOptions = new TileFetcherOptions();
        var cacheOptions = new TileCacheOptions();
        if (!string.IsNullOrEmpty(cacheDirectory))
        {
            cacheOptions.CacheDirectory = cacheDirectory;
        }

        _tileCache = new TileCache(new OsmTileFetcher(_fetcherOptions), cacheOptions);
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

            // Cap zoom level to tile server's maximum supported zoom
            int effectiveZoom = Math.Min(zoomResult.Zoom, _fetcherOptions.MaxZoom);
            if (effectiveZoom < zoomResult.Zoom)
            {
                Console.WriteLine($"  Note: Capped zoom from {zoomResult.Zoom} to {effectiveZoom} (tile server max)");
                zoomResult = ZoomLevelCalculator.CalculateZoom(
                    (int)ZoomLevelCalculator.CalculateActualScale(
                        ZoomLevelCalculator.GetMetersPerPixel(effectiveZoom, options.Bounds.CenterLat),
                        options.Dpi),
                    options.Dpi,
                    options.Bounds.CenterLat);
            }

            if (zoomResult.Warning != null)
            {
                Console.WriteLine($"  Warning: {zoomResult.Warning}");
            }

            // Determine if multi-page mode should be used
            bool useMultiPage = options.MultiPage ?? false;

            // Auto-detect multi-page for PDF format when not explicitly set
            if (options.Format == MapOutputFormat.Pdf && options.MultiPage == null)
            {
                useMultiPage = ShouldUseMultiPage(options);
                if (useMultiPage)
                {
                    Console.WriteLine("  Auto-enabled multi-page output (map exceeds page dimensions)");
                }
            }

            // Pre-flight validation: check memory requirements before proceeding
            // Skip for multi-page mode which renders sheets individually
            if (!useMultiPage)
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

            if (useMultiPage && options.Format == MapOutputFormat.Pdf)
            {
                // Multi-page mode: render each sheet separately to avoid memory issues
                await ExportMultiPagePdfAsync(tileResult.Tiles, zoomResult.Zoom, options, cancellationToken);
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

                // Remove military hatching/fill from tile rendering
                TilePostProcessor.RemoveMilitaryHatching(mapBitmap);

                // Apply vegetation overlay from OSM data
                await ApplyVegetationOverlayAsync(mapBitmap, options, cancellationToken);

                // Apply contour lines from elevation data
                using var contourBitmap = await ApplyContoursAsync(mapBitmap, options, cancellationToken);

                // Apply range/impact area overlay if enabled
                if (options.ShowRangeOverlay)
                {
                    await ApplyRangeOverlayAsync(contourBitmap, options, cancellationToken);
                }

                // Apply MGRS grid overlay
                using var gridBitmap = ApplyMgrsGrid(contourBitmap, options);

                // Render scale bar
                var scaleBarResult = RenderScaleBar(options);

                ExportMap(gridBitmap, scaleBarResult, options);

                scaleBarResult.Bitmap.Dispose();
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

    private void ExportMap(SKBitmap mapBitmap, ScaleBarResult? scaleBar, MapGeneratorOptions options)
    {
        switch (options.Format)
        {
            case MapOutputFormat.Pdf:
                ExportPdf(mapBitmap, scaleBar, options);
                break;
            case MapOutputFormat.Png:
                ExportPng(mapBitmap, options);
                break;
            case MapOutputFormat.GeoTiff:
                ExportGeoTiff(mapBitmap, options);
                break;
        }
    }

    private void ExportPdf(SKBitmap mapBitmap, ScaleBarResult? scaleBar, MapGeneratorOptions options)
    {
        var pdfOptions = new PdfExportOptions
        {
            Dpi = options.Dpi,
            Title = options.Title,
            ScaleText = $"1:{options.Scale:N0}"
        };

        var exporter = new PdfExporter(pdfOptions);
        exporter.Export(mapBitmap, scaleBar?.Bitmap, options.OutputPath);
    }

    private void ExportPng(SKBitmap mapBitmap, MapGeneratorOptions options)
    {
        // Add title, scale bar, and metadata margins directly into the image
        var marginOptions = new MapMarginOptions
        {
            Title = options.Title,
            ScaleRatio = options.Scale,
            Dpi = options.Dpi,
            CenterLat = options.Bounds.CenterLat,
            CenterLon = options.Bounds.CenterLon,
            ShowScaleBar = true,
            ShowDeclination = true,
            ShowDatum = true,
            ShowDate = true
        };

        var marginRenderer = new MapMarginRenderer(marginOptions);
        using var finalBitmap = marginRenderer.AddMargins(mapBitmap);

        var imageOptions = new ImageExportOptions
        {
            Format = ImageFormat.Png,
            Dpi = options.Dpi,
            Quality = 95
        };

        var exporter = new ImageExporter(imageOptions);
        exporter.Export(finalBitmap, options.OutputPath);
    }

    private void ExportGeoTiff(SKBitmap mapBitmap, MapGeneratorOptions options)
    {
        var geoTiffOptions = new GeoTiffExportOptions
        {
            BoundingBox = options.Bounds,
            Dpi = options.Dpi
        };

        var exporter = new GeoTiffExporter(geoTiffOptions);
        exporter.Export(mapBitmap, options.OutputPath);
    }

    /// <summary>
    /// Applies MGRS grid overlay to a rendered map bitmap.
    /// </summary>
    private static SKBitmap ApplyMgrsGrid(SKBitmap baseBitmap, MapGeneratorOptions options)
    {
        var gridOptions = new MgrsGridOptions
        {
            Scale = ScaleToMapScale(options.Scale),
            ShowLabels = true
        };

        var gridRenderer = new MgrsGridRenderer(gridOptions);
        return gridRenderer.DrawGrid(
            baseBitmap,
            options.Bounds.MinLat,
            options.Bounds.MaxLat,
            options.Bounds.MinLon,
            options.Bounds.MaxLon);
    }

    /// <summary>
    /// Renders a scale bar for the map.
    /// </summary>
    private static ScaleBarResult RenderScaleBar(MapGeneratorOptions options)
    {
        var scaleBarOptions = new ScaleBarOptions
        {
            ScaleRatio = options.Scale,
            Dpi = options.Dpi
        };

        var renderer = new ScaleBarRenderer(scaleBarOptions);
        return renderer.Render();
    }

    /// <summary>
    /// Maps a numeric scale denominator to the closest MapScale enum value.
    /// </summary>
    private static MapScale ScaleToMapScale(int scale)
    {
        if (scale <= 15000) return MapScale.Scale1To10000;
        if (scale <= 37500) return MapScale.Scale1To25000;
        if (scale <= 75000) return MapScale.Scale1To50000;
        return MapScale.Scale1To100000;
    }

    /// <summary>
    /// Fetches vegetation/terrain features from OSM and renders them as overlays.
    /// Falls back gracefully if OSM data is unavailable.
    /// </summary>
    private static async Task ApplyVegetationOverlayAsync(
        SKBitmap bitmap, MapGeneratorOptions options, CancellationToken cancellationToken)
    {
        try
        {
            using var renderer = new VegetationRenderer();
            await renderer.RenderVegetationAsync(
                bitmap,
                options.Bounds.MinLat, options.Bounds.MaxLat,
                options.Bounds.MinLon, options.Bounds.MaxLon,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Could not load vegetation data: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches military range/impact area data from OSM and renders labeled overlays.
    /// Falls back gracefully if OSM data is unavailable.
    /// </summary>
    private static async Task ApplyRangeOverlayAsync(
        SKBitmap bitmap, MapGeneratorOptions options, CancellationToken cancellationToken)
    {
        try
        {
            using var renderer = new RangeOverlayRenderer();
            await renderer.RenderRangesAsync(
                bitmap,
                options.Bounds.MinLat, options.Bounds.MaxLat,
                options.Bounds.MinLon, options.Bounds.MaxLon,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Could not load range overlay data: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches elevation data and draws contour lines on the map.
    /// Falls back gracefully to the unmodified bitmap if elevation data is unavailable.
    /// </summary>
    private static async Task<SKBitmap> ApplyContoursAsync(
        SKBitmap baseBitmap, MapGeneratorOptions options, CancellationToken cancellationToken)
    {
        try
        {
            // Determine grid resolution based on image size (one sample per ~4 pixels for smooth contours)
            int gridCols = Math.Max(2, baseBitmap.Width / 4);
            int gridRows = Math.Max(2, baseBitmap.Height / 4);

            // Cap to reasonable limits to avoid excessive download time
            gridCols = Math.Min(gridCols, 1200);
            gridRows = Math.Min(gridRows, 1200);

            using var elevationSource = new SrtmElevationSource();
            var elevationGrid = await elevationSource.GetElevationGridAsync(
                options.Bounds.MinLat, options.Bounds.MaxLat,
                options.Bounds.MinLon, options.Bounds.MaxLon,
                gridRows, gridCols, cancellationToken);

            var contourOptions = new ContourOptions
            {
                Scale = ScaleToMapScale(options.Scale),
                ShowLabels = true,
                SmoothContours = true
            };

            var contourRenderer = new ContourRenderer(contourOptions);
            return contourRenderer.DrawContours(baseBitmap, elevationGrid);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Could not load elevation data for contours: {ex.Message}");
            return baseBitmap.Copy();
        }
    }

    private async Task ExportMultiPagePdfAsync(IReadOnlyList<TileData> tiles, int zoom, MapGeneratorOptions options, CancellationToken cancellationToken)
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

        // Pre-render scale bar for all sheets
        var scaleBarResult = RenderScaleBar(options);

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

                using var rawBitmap = SKBitmap.Decode(sheetImageBytes);

                // Remove military hatching/fill from tile rendering
                TilePostProcessor.RemoveMilitaryHatching(rawBitmap);

                // Apply vegetation overlay from OSM data
                var sheetOptions = new MapGeneratorOptions
                {
                    Bounds = new BoundingBox(sheet.MinLat, sheet.MaxLat, sheet.MinLon, sheet.MaxLon),
                    Scale = options.Scale,
                    Dpi = options.Dpi,
                    ShowRangeOverlay = options.ShowRangeOverlay
                };
                await ApplyVegetationOverlayAsync(rawBitmap, sheetOptions, cancellationToken);

                // Apply contour lines to each sheet
                using var contourBitmap = await ApplyContoursAsync(rawBitmap, sheetOptions, cancellationToken);

                // Apply range/impact area overlay if enabled
                if (options.ShowRangeOverlay)
                {
                    await ApplyRangeOverlayAsync(contourBitmap, sheetOptions, cancellationToken);
                }

                // Apply MGRS grid overlay to each sheet
                sheet.MapImage = ApplyMgrsGrid(contourBitmap, sheetOptions);
                sheet.ScaleBarImage = scaleBarResult.Bitmap;
            }
        }

        // Export the multi-page PDF
        multiExporter.Export(layout, options.OutputPath);

        // Clean up sheet images (don't dispose ScaleBarImage since it's shared)
        foreach (var sheet in layout.Sheets)
        {
            sheet.MapImage?.Dispose();
            sheet.ScaleBarImage = null;
        }

        scaleBarResult.Bitmap.Dispose();
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

    /// <summary>
    /// Determines if multi-page mode should be automatically enabled based on map size vs page dimensions.
    /// </summary>
    private static bool ShouldUseMultiPage(MapGeneratorOptions options)
    {
        const double MetersPerDegreeLatitude = 111320;

        // Calculate area dimensions in meters
        double centerLat = (options.Bounds.MinLat + options.Bounds.MaxLat) / 2;
        double metersPerDegreeLon = MetersPerDegreeLatitude * Math.Cos(centerLat * Math.PI / 180);

        double areaWidthMeters = (options.Bounds.MaxLon - options.Bounds.MinLon) * metersPerDegreeLon;
        double areaHeightMeters = (options.Bounds.MaxLat - options.Bounds.MinLat) * MetersPerDegreeLatitude;

        // Calculate print dimensions at scale (in inches)
        const double MetersPerInch = 0.0254;
        double printWidthInches = areaWidthMeters / options.Scale / MetersPerInch;
        double printHeightInches = areaHeightMeters / options.Scale / MetersPerInch;

        // Get page dimensions (in inches)
        var (pageWidth, pageHeight) = GetPageDimensionsInches(options.PageSize, options.Orientation);

        // Account for margins (0.5 inch on each side)
        const float marginInches = 0.5f;
        float printableWidth = pageWidth - 2 * marginInches;
        float printableHeight = pageHeight - 2 * marginInches;

        // If print dimensions exceed page dimensions, use multi-page
        return printWidthInches > printableWidth || printHeightInches > printableHeight;
    }

    private static (float width, float height) GetPageDimensionsInches(PageSize pageSize, PageOrientation orientation)
    {
        float width, height;

        switch (pageSize)
        {
            case PageSize.Letter:
                width = 8.5f;
                height = 11f;
                break;
            case PageSize.Legal:
                width = 8.5f;
                height = 14f;
                break;
            case PageSize.Tabloid:
                width = 11f;
                height = 17f;
                break;
            case PageSize.A4:
                width = 8.27f;
                height = 11.69f;
                break;
            case PageSize.A3:
                width = 11.69f;
                height = 16.54f;
                break;
            case PageSize.Custom:
            default:
                width = 11f;
                height = 8.5f;
                break;
        }

        if (orientation == PageOrientation.Landscape && height > width)
        {
            (width, height) = (height, width);
        }
        else if (orientation == PageOrientation.Portrait && width > height)
        {
            (width, height) = (height, width);
        }

        return (width, height);
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
