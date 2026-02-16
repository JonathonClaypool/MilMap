using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MilMap.Core.Osm;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Renders military range and impact area overlays from OSM data
/// with semi-transparent red fills and labels.
/// </summary>
public class RangeOverlayRenderer : IDisposable
{
    private readonly OverpassClient _overpassClient;
    private bool _disposed;

    // Semi-transparent red for range fills (matching the user's request for
    // "loosely highlighted red the way we had it before")
    private static readonly SKColor RangeFillColor = new(200, 50, 50, 45);
    private static readonly SKColor RangeStrokeColor = new(180, 30, 30, 140);
    private static readonly SKColor DangerFillColor = new(220, 40, 40, 55);
    private static readonly SKColor DangerStrokeColor = new(200, 20, 20, 160);
    private static readonly SKColor LabelColor = new(140, 20, 20, 220);
    private static readonly SKColor LabelHaloColor = new(255, 255, 255, 180);

    public RangeOverlayRenderer()
    {
        _overpassClient = new OverpassClient();
    }

    public RangeOverlayRenderer(OverpassClient client)
    {
        _overpassClient = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Fetches military ranges and impact areas from OSM and renders them
    /// as labeled semi-transparent red overlays on the bitmap.
    /// </summary>
    public async Task RenderRangesAsync(
        SKBitmap bitmap,
        double minLat, double maxLat,
        double minLon, double maxLon,
        CancellationToken cancellationToken = default)
    {
        var ranges = await FetchRangeFeaturesAsync(
            minLat, maxLat, minLon, maxLon, cancellationToken);

        if (ranges.Count == 0) return;

        using var canvas = new SKCanvas(bitmap);

        // Draw fills first, then labels on top
        foreach (var range in ranges)
        {
            DrawRangeFill(canvas, range, bitmap.Width, bitmap.Height,
                minLat, maxLat, minLon, maxLon);
        }

        foreach (var range in ranges)
        {
            if (!string.IsNullOrWhiteSpace(range.Name))
            {
                DrawRangeLabel(canvas, range, bitmap.Width, bitmap.Height,
                    minLat, maxLat, minLon, maxLon);
            }
        }
    }

    private async Task<List<RangeFeature>> FetchRangeFeaturesAsync(
        double minLat, double maxLat, double minLon, double maxLon,
        CancellationToken cancellationToken)
    {
        var features = new List<RangeFeature>();

        string bbox = $"{minLat},{minLon},{maxLat},{maxLon}";

        string query = $@"[out:json][timeout:60];
(
  way[""military""=""range""]({bbox});
  way[""military""=""danger_area""]({bbox});
  way[""military""=""training_area""]({bbox});
  relation[""military""=""range""]({bbox});
  relation[""military""=""danger_area""]({bbox});
);
out geom;";

        var result = await _overpassClient.ExecuteQueryAsync(query, cancellationToken);
        if (!result.Success || result.Response == null) return features;

        foreach (var element in result.Response.Elements)
        {
            var rangeType = ClassifyRange(element);
            if (rangeType == RangeType.Unknown) continue;

            string name = element.Tags?.GetValueOrDefault("name") ?? "";

            if (element.Type == "way" && element.Geometry != null && element.Geometry.Length >= 3)
            {
                features.Add(new RangeFeature
                {
                    Type = rangeType,
                    Name = name,
                    Points = element.Geometry
                        .Select(p => (p.Lat, p.Lon))
                        .ToList()
                });
            }
            else if (element.Type == "relation" && element.Members != null)
            {
                // Collect all outer member geometries into one feature
                var allPoints = new List<(double Lat, double Lon)>();
                foreach (var member in element.Members)
                {
                    if (member.Role == "outer" && member.Geometry != null && member.Geometry.Length >= 3)
                    {
                        if (allPoints.Count > 0)
                        {
                            // Multiple outer rings — add as separate features
                            features.Add(new RangeFeature
                            {
                                Type = rangeType,
                                Name = name,
                                Points = allPoints
                            });
                            allPoints = new List<(double, double)>();
                        }
                        allPoints.AddRange(member.Geometry.Select(p => (p.Lat, p.Lon)));
                    }
                }
                if (allPoints.Count >= 3)
                {
                    features.Add(new RangeFeature
                    {
                        Type = rangeType,
                        Name = name,
                        Points = allPoints
                    });
                }
            }
        }

        return features;
    }

    private static RangeType ClassifyRange(OsmElement element)
    {
        if (element.Tags == null) return RangeType.Unknown;

        if (element.Tags.TryGetValue("military", out var military))
        {
            return military switch
            {
                "range" => RangeType.Range,
                "danger_area" => RangeType.DangerArea,
                "training_area" => RangeType.TrainingArea,
                _ => RangeType.Unknown
            };
        }

        return RangeType.Unknown;
    }

    private static void DrawRangeFill(
        SKCanvas canvas, RangeFeature range,
        int bitmapWidth, int bitmapHeight,
        double minLat, double maxLat, double minLon, double maxLon)
    {
        double minMercY = LatToMercatorY(maxLat);
        double maxMercY = LatToMercatorY(minLat);

        using var path = new SKPath();
        bool first = true;

        foreach (var (lat, lon) in range.Points)
        {
            float px = (float)((lon - minLon) / (maxLon - minLon) * bitmapWidth);
            double mercY = LatToMercatorY(lat);
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

        var (fillColor, strokeColor) = range.Type switch
        {
            RangeType.DangerArea => (DangerFillColor, DangerStrokeColor),
            _ => (RangeFillColor, RangeStrokeColor)
        };

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
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        canvas.DrawPath(path, fillPaint);
        canvas.DrawPath(path, strokePaint);
    }

    private static void DrawRangeLabel(
        SKCanvas canvas, RangeFeature range,
        int bitmapWidth, int bitmapHeight,
        double minLat, double maxLat, double minLon, double maxLon)
    {
        // Calculate centroid for label placement
        double centroidLat = range.Points.Average(p => p.Lat);
        double centroidLon = range.Points.Average(p => p.Lon);

        double minMercY = LatToMercatorY(maxLat);
        double maxMercY = LatToMercatorY(minLat);

        float cx = (float)((centroidLon - minLon) / (maxLon - minLon) * bitmapWidth);
        double mercY = LatToMercatorY(centroidLat);
        float cy = (float)((mercY - minMercY) / (maxMercY - minMercY) * bitmapHeight);

        // Calculate label font size based on feature area (larger features get larger labels)
        float fontSize = CalculateLabelFontSize(range, bitmapWidth, bitmapHeight,
            minLat, maxLat, minLon, maxLon);

        using var font = new SKFont(
            SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            fontSize);

        string label = range.Name;

        // Measure text for halo
        float textWidth = font.MeasureText(label);

        // Draw halo (white outline behind text for readability)
        using var haloPaint = new SKPaint
        {
            Color = LabelHaloColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            IsAntialias = true
        };
        canvas.DrawText(label, cx - textWidth / 2, cy + fontSize / 3,
            SKTextAlign.Left, font, haloPaint);

        // Draw label text
        using var labelPaint = new SKPaint
        {
            Color = LabelColor,
            IsAntialias = true
        };
        canvas.DrawText(label, cx - textWidth / 2, cy + fontSize / 3,
            SKTextAlign.Left, font, labelPaint);
    }

    /// <summary>
    /// Calculates an appropriate font size based on the feature's pixel extent.
    /// Larger features get larger labels, clamped to a reasonable range.
    /// </summary>
    private static float CalculateLabelFontSize(
        RangeFeature range, int bitmapWidth, int bitmapHeight,
        double minLat, double maxLat, double minLon, double maxLon)
    {
        double featureMinLon = range.Points.Min(p => p.Lon);
        double featureMaxLon = range.Points.Max(p => p.Lon);
        double featureMinLat = range.Points.Min(p => p.Lat);
        double featureMaxLat = range.Points.Max(p => p.Lat);

        float pixelWidth = (float)((featureMaxLon - featureMinLon) / (maxLon - minLon) * bitmapWidth);
        float pixelHeight = (float)((featureMaxLat - featureMinLat) / (maxLat - minLat) * bitmapHeight);

        // Size label to roughly 1/8 of the smaller dimension, clamped
        float featureSize = Math.Min(pixelWidth, pixelHeight);
        float fontSize = Math.Clamp(featureSize / 8f, 10f, 48f);

        return fontSize;
    }

    private static double LatToMercatorY(double lat)
    {
        double latRad = lat * Math.PI / 180.0;
        return Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad));
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

internal enum RangeType
{
    Unknown,
    Range,
    DangerArea,
    TrainingArea
}

internal class RangeFeature
{
    public RangeType Type { get; init; }
    public string Name { get; init; } = "";
    public List<(double Lat, double Lon)> Points { get; init; } = new();
}
