using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MilMap.Core.Osm;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Renders vegetation, wetland, and other terrain overlays from OSM data
/// onto the map using military-standard colors.
/// </summary>
public class VegetationRenderer : IDisposable
{
    private readonly OverpassClient _overpassClient;
    private bool _disposed;

    public VegetationRenderer()
    {
        _overpassClient = new OverpassClient();
    }

    public VegetationRenderer(OverpassClient client)
    {
        _overpassClient = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Fetches vegetation and terrain features from OSM and renders them
    /// as semi-transparent overlays on the bitmap.
    /// </summary>
    public async Task RenderVegetationAsync(
        SKBitmap bitmap,
        double minLat, double maxLat,
        double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        var features = await FetchVegetationFeaturesAsync(
            minLat, maxLat, minLon, maxLon, cancellationToken);

        if (features.Count == 0) return;

        using var canvas = new SKCanvas(bitmap);

        foreach (var feature in features)
        {
            DrawFeature(canvas, feature, bitmap.Width, bitmap.Height,
                minLat, maxLat, minLon, maxLon);
        }
    }

    private async Task<List<VegetationFeature>> FetchVegetationFeaturesAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken)
    {
        var features = new List<VegetationFeature>();

        string bbox = $"{minLat},{minLon},{maxLat},{maxLon}";

        // Query for forests, wetlands, scrub, and other vegetation
        string query = $@"[out:json][timeout:120];
(
  way[""natural""=""wood""]({bbox});
  way[""landuse""=""forest""]({bbox});
  way[""natural""=""wetland""]({bbox});
  way[""natural""=""scrub""]({bbox});
  way[""natural""=""heath""]({bbox});
  way[""landuse""=""meadow""]({bbox});
  way[""landuse""=""orchard""]({bbox});
  relation[""natural""=""wood""]({bbox});
  relation[""landuse""=""forest""]({bbox});
  relation[""natural""=""wetland""]({bbox});
);
out geom;";

        var result = await _overpassClient.ExecuteQueryAsync(query, cancellationToken);
        if (!result.Success || result.Response == null) return features;

        foreach (var element in result.Response.Elements)
        {
            if (element.Geometry == null || element.Geometry.Length < 3) continue;

            var featureType = ClassifyFeature(element);
            if (featureType == VegetationType.Unknown) continue;

            features.Add(new VegetationFeature
            {
                Type = featureType,
                Points = element.Geometry
                    .Select(p => new GeoPoint(p.Lat, p.Lon))
                    .ToList()
            });
        }

        return features;
    }

    private static VegetationType ClassifyFeature(OsmElement element)
    {
        if (element.Tags == null) return VegetationType.Unknown;

        if (element.Tags.TryGetValue("natural", out var natural))
        {
            return natural switch
            {
                "wood" => VegetationType.Forest,
                "wetland" => VegetationType.Wetland,
                "scrub" => VegetationType.Scrub,
                "heath" => VegetationType.Scrub,
                _ => VegetationType.Unknown
            };
        }

        if (element.Tags.TryGetValue("landuse", out var landuse))
        {
            return landuse switch
            {
                "forest" => VegetationType.Forest,
                "meadow" => VegetationType.Meadow,
                "orchard" => VegetationType.Orchard,
                _ => VegetationType.Unknown
            };
        }

        return VegetationType.Unknown;
    }

    private static void DrawFeature(
        SKCanvas canvas, VegetationFeature feature,
        int bitmapWidth, int bitmapHeight,
        double minLat, double maxLat, double minLon, double maxLon)
    {
        // Project lat/lon to Web Mercator and then to pixel coordinates
        double minMercY = LatToMercatorY(maxLat); // Note: maxLat = top of image = smaller Mercator Y
        double maxMercY = LatToMercatorY(minLat);

        var path = new SKPath();
        bool first = true;

        foreach (var point in feature.Points)
        {
            float px = (float)((point.Lon - minLon) / (maxLon - minLon) * bitmapWidth);
            double mercY = LatToMercatorY(point.Lat);
            float py = (float)((mercY - minMercY) / (maxMercY - minMercY) * bitmapHeight);

            if (first)
            {
                path.MoveTo(px, py);
                first = false;
            }
            else
            {
                path.LineTo(px, py);
            }
        }

        path.Close();

        var (fillColor, strokeColor) = GetFeatureColors(feature.Type);

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = fillColor,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = strokeColor,
            StrokeWidth = 1.0f,
            IsAntialias = true
        };

        canvas.DrawPath(path, fillPaint);
        canvas.DrawPath(path, strokePaint);
        path.Dispose();
    }

    private static double LatToMercatorY(double lat)
    {
        double latRad = lat * Math.PI / 180.0;
        return Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad));
    }

    private static (SKColor fill, SKColor stroke) GetFeatureColors(VegetationType type)
    {
        return type switch
        {
            VegetationType.Forest => (
                MilitaryColors.Woodland.WithAlpha(70),
                MilitaryColors.VegetationDense.WithAlpha(120)),
            VegetationType.Scrub => (
                MilitaryColors.VegetationLight.WithAlpha(50),
                MilitaryColors.VegetationLight.WithAlpha(100)),
            VegetationType.Wetland => (
                MilitaryColors.Marsh.WithAlpha(60),
                MilitaryColors.IntermittentWater.WithAlpha(100)),
            VegetationType.Meadow => (
                MilitaryColors.VegetationLight.WithAlpha(35),
                MilitaryColors.VegetationLight.WithAlpha(80)),
            VegetationType.Orchard => (
                MilitaryColors.Orchard.WithAlpha(50),
                MilitaryColors.Orchard.WithAlpha(100)),
            _ => (SKColors.Transparent, SKColors.Transparent)
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _overpassClient.Dispose();
            _disposed = true;
        }
    }
}

internal enum VegetationType
{
    Unknown,
    Forest,
    Scrub,
    Wetland,
    Meadow,
    Orchard
}

internal record GeoPoint(double Lat, double Lon);

internal class VegetationFeature
{
    public VegetationType Type { get; init; }
    public List<GeoPoint> Points { get; init; } = new();
}
