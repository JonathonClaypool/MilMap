using System;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Options for scale bar rendering.
/// </summary>
public class ScaleBarOptions
{
    /// <summary>
    /// Map scale ratio (e.g., 25000 for 1:25,000).
    /// </summary>
    public int ScaleRatio { get; set; } = 25000;

    /// <summary>
    /// Output DPI for accurate printed scale.
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// Show metric units (kilometers/meters).
    /// </summary>
    public bool ShowMetric { get; set; } = true;

    /// <summary>
    /// Show imperial units (miles).
    /// </summary>
    public bool ShowImperial { get; set; } = true;

    /// <summary>
    /// Height of scale bar segments in pixels.
    /// </summary>
    public int BarHeight { get; set; } = 8;

    /// <summary>
    /// Font size for labels.
    /// </summary>
    public float LabelFontSize { get; set; } = 10f;

    /// <summary>
    /// Scale bar background color.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.White;

    /// <summary>
    /// Scale bar segment primary color.
    /// </summary>
    public SKColor PrimaryColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Scale bar segment secondary color.
    /// </summary>
    public SKColor SecondaryColor { get; set; } = SKColors.White;

    /// <summary>
    /// Text/label color.
    /// </summary>
    public SKColor TextColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Border color for segments.
    /// </summary>
    public SKColor BorderColor { get; set; } = SKColors.Black;
}

/// <summary>
/// Result of scale bar rendering.
/// </summary>
public record ScaleBarResult(SKBitmap Bitmap, int MetricLengthMeters, double ImperialLengthMiles);

/// <summary>
/// Renders scale bars for military topographic maps.
/// </summary>
public class ScaleBarRenderer
{
    private const double MetersPerMile = 1609.344;
    private const double InchesPerMeter = 39.3701;

    private readonly ScaleBarOptions _options;

    public ScaleBarRenderer() : this(new ScaleBarOptions()) { }

    public ScaleBarRenderer(ScaleBarOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Renders a scale bar bitmap.
    /// </summary>
    public ScaleBarResult Render()
    {
        // Calculate scale bar dimensions
        // At scale 1:25000, 1 inch on map = 25000 inches ground = 635 meters
        double inchesOnMap = 2.0; // Standard 2-inch scale bar
        double groundDistanceInches = inchesOnMap * _options.ScaleRatio;
        double groundDistanceMeters = groundDistanceInches / InchesPerMeter;

        // Round to nice metric value
        int roundedMeters = GetNiceMetricValue((int)groundDistanceMeters);
        double actualInchesOnMap = roundedMeters * InchesPerMeter / _options.ScaleRatio;
        int metricPixelWidth = (int)(actualInchesOnMap * _options.Dpi);

        // Calculate imperial equivalent
        double imperialMiles = roundedMeters / MetersPerMile;
        double roundedMiles = GetNiceImperialValue(imperialMiles);
        double imperialMeters = roundedMiles * MetersPerMile;
        double imperialInchesOnMap = imperialMeters * InchesPerMeter / _options.ScaleRatio;
        int imperialPixelWidth = (int)(imperialInchesOnMap * _options.Dpi);

        // Determine bitmap size
        int maxWidth = Math.Max(metricPixelWidth, imperialPixelWidth);
        int padding = 20;
        int bitmapWidth = maxWidth + padding * 2;

        int rowHeight = _options.BarHeight + 20; // Bar + labels
        int titleHeight = 30;
        int rows = (_options.ShowMetric ? 1 : 0) + (_options.ShowImperial ? 1 : 0);
        int bitmapHeight = titleHeight + rows * rowHeight + padding;

        var bitmap = new SKBitmap(bitmapWidth, bitmapHeight);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(_options.BackgroundColor);

        using var textPaint = new SKPaint
        {
            Color = _options.TextColor,
            TextSize = _options.LabelFontSize,
            IsAntialias = true
        };

        using var titlePaint = new SKPaint
        {
            Color = _options.TextColor,
            TextSize = _options.LabelFontSize + 2,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        using var fillPaintPrimary = new SKPaint
        {
            Color = _options.PrimaryColor,
            Style = SKPaintStyle.Fill
        };

        using var fillPaintSecondary = new SKPaint
        {
            Color = _options.SecondaryColor,
            Style = SKPaintStyle.Fill
        };

        using var strokePaint = new SKPaint
        {
            Color = _options.BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        // Draw scale ratio title
        string title = $"SCALE 1:{_options.ScaleRatio:N0}";
        var titleBounds = new SKRect();
        titlePaint.MeasureText(title, ref titleBounds);
        float titleX = (bitmapWidth - titleBounds.Width) / 2;
        canvas.DrawText(title, titleX, 20, titlePaint);

        float currentY = titleHeight;

        // Draw metric scale bar
        if (_options.ShowMetric)
        {
            DrawScaleBar(canvas, padding, currentY, metricPixelWidth, roundedMeters,
                "m", "km", 1000, fillPaintPrimary, fillPaintSecondary, strokePaint, textPaint);
            currentY += rowHeight;
        }

        // Draw imperial scale bar
        if (_options.ShowImperial)
        {
            int imperialFeet = (int)(roundedMiles * 5280);
            DrawScaleBar(canvas, padding, currentY, imperialPixelWidth, imperialFeet,
                "ft", "mi", 5280, fillPaintPrimary, fillPaintSecondary, strokePaint, textPaint);
        }

        return new ScaleBarResult(bitmap, roundedMeters, roundedMiles);
    }

    private void DrawScaleBar(
        SKCanvas canvas, float x, float y, int pixelWidth, int totalUnits,
        string smallUnit, string largeUnit, int unitsPerLarge,
        SKPaint primary, SKPaint secondary, SKPaint stroke, SKPaint text)
    {
        // Determine number of segments (typically 4-5)
        int numSegments = 5;
        int unitsPerSegment = totalUnits / numSegments;
        float pixelsPerSegment = (float)pixelWidth / numSegments;

        float barY = y;

        // Draw alternating segments
        for (int i = 0; i < numSegments; i++)
        {
            var rect = new SKRect(
                x + i * pixelsPerSegment,
                barY,
                x + (i + 1) * pixelsPerSegment,
                barY + _options.BarHeight);

            var paint = (i % 2 == 0) ? primary : secondary;
            canvas.DrawRect(rect, paint);
            canvas.DrawRect(rect, stroke);
        }

        // Draw labels
        float labelY = barY + _options.BarHeight + 15;

        // Start label (0)
        canvas.DrawText("0", x, labelY, text);

        // End label
        string endLabel;
        if (totalUnits >= unitsPerLarge)
        {
            double largeValue = (double)totalUnits / unitsPerLarge;
            endLabel = $"{largeValue:G3} {largeUnit}";
        }
        else
        {
            endLabel = $"{totalUnits} {smallUnit}";
        }

        var endBounds = new SKRect();
        text.MeasureText(endLabel, ref endBounds);
        canvas.DrawText(endLabel, x + pixelWidth - endBounds.Width, labelY, text);

        // Middle labels for major segments
        for (int i = 1; i < numSegments; i++)
        {
            int segmentUnits = i * unitsPerSegment;
            string segmentLabel;
            if (segmentUnits >= unitsPerLarge)
            {
                double largeValue = (double)segmentUnits / unitsPerLarge;
                segmentLabel = largeValue.ToString("G2");
            }
            else
            {
                segmentLabel = segmentUnits.ToString();
            }

            var segBounds = new SKRect();
            text.MeasureText(segmentLabel, ref segBounds);
            float segX = x + i * pixelsPerSegment - segBounds.Width / 2;
            canvas.DrawText(segmentLabel, segX, labelY, text);
        }
    }

    private static int GetNiceMetricValue(int meters)
    {
        // Round to nice values: 100, 200, 250, 500, 1000, 2000, 5000, etc.
        int[] niceValues = { 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000, 20000, 50000 };

        foreach (int nice in niceValues)
        {
            if (meters <= nice * 1.2)
                return nice;
        }

        return (int)Math.Ceiling(meters / 10000.0) * 10000;
    }

    private static double GetNiceImperialValue(double miles)
    {
        // Round to nice values: 0.1, 0.25, 0.5, 1, 2, 5, etc.
        double[] niceValues = { 0.1, 0.25, 0.5, 1.0, 2.0, 2.5, 5.0, 10.0 };

        foreach (double nice in niceValues)
        {
            if (miles <= nice * 1.2)
                return nice;
        }

        return Math.Ceiling(miles);
    }

    /// <summary>
    /// Calculates the pixel width needed for a given ground distance.
    /// </summary>
    public int CalculatePixelWidth(double groundDistanceMeters)
    {
        double inchesOnMap = groundDistanceMeters * InchesPerMeter / _options.ScaleRatio;
        return (int)(inchesOnMap * _options.Dpi);
    }

    /// <summary>
    /// Calculates the ground distance represented by a given pixel width.
    /// </summary>
    public double CalculateGroundDistance(int pixelWidth)
    {
        double inchesOnMap = (double)pixelWidth / _options.Dpi;
        double groundDistanceInches = inchesOnMap * _options.ScaleRatio;
        return groundDistanceInches / InchesPerMeter;
    }
}
