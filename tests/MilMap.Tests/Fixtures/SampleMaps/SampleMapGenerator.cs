using MilMap.Core.Export;
using MilMap.Core.Rendering;
using SkiaSharp;

namespace MilMap.Tests.Fixtures.SampleMaps;

/// <summary>
/// Generates sample reference maps for visual regression testing.
/// Each sample represents a specific rendering scenario that should be validated.
/// </summary>
public static class SampleMapGenerator
{
    /// <summary>
    /// Gets the directory where sample maps are stored.
    /// </summary>
    public static string SampleMapsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleMaps");

    /// <summary>
    /// Sample map definitions for visual regression testing.
    /// </summary>
    public static readonly SampleMapDefinition[] Samples =
    [
        new SampleMapDefinition(
            "mgrs_grid_dc",
            "MGRS grid overlay for Washington DC area",
            38.88, 38.92, -77.05, -77.0,
            MapScale.Scale1To25000,
            512, 512),

        new SampleMapDefinition(
            "mgrs_grid_london",
            "MGRS grid overlay for London area (near zone 30/31 boundary)",
            51.48, 51.52, -0.15, -0.05,
            MapScale.Scale1To25000,
            512, 512),

        new SampleMapDefinition(
            "mgrs_grid_sydney",
            "MGRS grid overlay for Sydney (southern hemisphere)",
            -33.88, -33.84, 151.18, 151.24,
            MapScale.Scale1To25000,
            512, 512),

        new SampleMapDefinition(
            "mgrs_grid_cross_zone",
            "MGRS grid spanning UTM zone boundary (zones 17/18)",
            40.0, 40.5, -78.5, -77.5,
            MapScale.Scale1To50000,
            768, 512),

        new SampleMapDefinition(
            "mgrs_grid_scale_100k",
            "MGRS grid at 1:100000 scale",
            45.0, 45.5, -93.5, -93.0,
            MapScale.Scale1To100000,
            512, 512),

        new SampleMapDefinition(
            "scale_bar_25k",
            "Scale bar at 1:25000",
            ScaleBarOnly: true,
            ScaleRatio: 25000),

        new SampleMapDefinition(
            "scale_bar_50k",
            "Scale bar at 1:50000",
            ScaleBarOnly: true,
            ScaleRatio: 50000)
    ];

    /// <summary>
    /// Generates all sample maps and saves them to the fixtures directory.
    /// Used to regenerate baselines when rendering logic changes intentionally.
    /// </summary>
    public static void GenerateAll(string? outputDirectory = null)
    {
        outputDirectory ??= SampleMapsDirectory;
        Directory.CreateDirectory(outputDirectory);

        foreach (var sample in Samples)
        {
            GenerateSample(sample, outputDirectory);
        }
    }

    /// <summary>
    /// Generates a single sample map.
    /// </summary>
    public static void GenerateSample(SampleMapDefinition sample, string outputDirectory)
    {
        var exporter = new ImageExporter(new ImageExportOptions
        {
            Format = ImageFormat.Png,
            Quality = 100
        });

        string outputPath = Path.Combine(outputDirectory, $"{sample.Name}.png");

        if (sample.ScaleBarOnly)
        {
            var renderer = new ScaleBarRenderer(new ScaleBarOptions
            {
                ScaleRatio = sample.ScaleRatio,
                Dpi = 300,
                ShowMetric = true,
                ShowImperial = true
            });

            var result = renderer.Render();
            exporter.Export(result.Bitmap, outputPath);
            result.Bitmap.Dispose();
        }
        else
        {
            using var baseMap = CreateBaseMap(sample.Width, sample.Height);
            var gridRenderer = new MgrsGridRenderer(new MgrsGridOptions
            {
                Scale = sample.Scale,
                ShowLabels = true
            });

            using var mapWithGrid = gridRenderer.DrawGrid(
                baseMap,
                sample.MinLat, sample.MaxLat,
                sample.MinLon, sample.MaxLon);

            exporter.Export(mapWithGrid, outputPath);
        }
    }

    /// <summary>
    /// Renders a sample map and returns the bitmap without saving.
    /// Used for comparison in tests.
    /// </summary>
    public static SKBitmap RenderSample(SampleMapDefinition sample)
    {
        if (sample.ScaleBarOnly)
        {
            var renderer = new ScaleBarRenderer(new ScaleBarOptions
            {
                ScaleRatio = sample.ScaleRatio,
                Dpi = 300,
                ShowMetric = true,
                ShowImperial = true
            });

            return renderer.Render().Bitmap;
        }

        using var baseMap = CreateBaseMap(sample.Width, sample.Height);
        var gridRenderer = new MgrsGridRenderer(new MgrsGridOptions
        {
            Scale = sample.Scale,
            ShowLabels = true
        });

        return gridRenderer.DrawGrid(
            baseMap,
            sample.MinLat, sample.MaxLat,
            sample.MinLon, sample.MaxLon);
    }

    /// <summary>
    /// Loads a reference sample map from the fixtures directory.
    /// </summary>
    public static SKBitmap? LoadReference(string sampleName, string? directory = null)
    {
        directory ??= SampleMapsDirectory;
        string path = Path.Combine(directory, $"{sampleName}.png");

        if (!File.Exists(path))
            return null;

        return SKBitmap.Decode(path);
    }

    private static SKBitmap CreateBaseMap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        // Light tan/beige background typical of topographic maps
        canvas.Clear(new SKColor(245, 240, 230));

        // Add subtle terrain-like pattern for realism
        using var paint = new SKPaint
        {
            Color = new SKColor(235, 230, 215),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            IsAntialias = true
        };

        // Draw contour-like lines
        for (int y = 20; y < height; y += 40)
        {
            var path = new SKPath();
            path.MoveTo(0, y);
            for (int x = 0; x < width; x += 20)
            {
                float offset = (float)(Math.Sin((x + y) * 0.05) * 5);
                path.LineTo(x, y + offset);
            }
            canvas.DrawPath(path, paint);
        }

        return bitmap;
    }
}

/// <summary>
/// Definition of a sample map for visual regression testing.
/// </summary>
public record SampleMapDefinition
{
    public string Name { get; }
    public string Description { get; }
    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLon { get; }
    public double MaxLon { get; }
    public MapScale Scale { get; }
    public int Width { get; }
    public int Height { get; }
    public bool ScaleBarOnly { get; }
    public int ScaleRatio { get; }

    public SampleMapDefinition(
        string name,
        string description,
        double minLat = 0,
        double maxLat = 0,
        double minLon = 0,
        double maxLon = 0,
        MapScale scale = MapScale.Scale1To25000,
        int width = 512,
        int height = 512,
        bool ScaleBarOnly = false,
        int ScaleRatio = 25000)
    {
        Name = name;
        Description = description;
        MinLat = minLat;
        MaxLat = maxLat;
        MinLon = minLon;
        MaxLon = maxLon;
        Scale = scale;
        Width = width;
        Height = height;
        this.ScaleBarOnly = ScaleBarOnly;
        this.ScaleRatio = ScaleRatio;
    }
}
