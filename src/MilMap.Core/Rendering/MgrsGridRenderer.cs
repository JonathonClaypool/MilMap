using System;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Map scale presets with corresponding grid intervals.
/// </summary>
public enum MapScale
{
    /// <summary>1:10,000 scale - 100m or 1km grid</summary>
    Scale1To10000,
    /// <summary>1:25,000 scale - 1km grid</summary>
    Scale1To25000,
    /// <summary>1:50,000 scale - 1km or 10km grid</summary>
    Scale1To50000,
    /// <summary>1:100,000 scale - 10km grid</summary>
    Scale1To100000
}

/// <summary>
/// Options for MGRS grid overlay rendering.
/// </summary>
public class MgrsGridOptions
{
    /// <summary>
    /// Map scale, determines grid interval.
    /// </summary>
    public MapScale Scale { get; set; } = MapScale.Scale1To25000;

    /// <summary>
    /// Grid interval in meters. If null, determined by scale.
    /// </summary>
    public int? GridIntervalMeters { get; set; }

    /// <summary>
    /// Grid line color. Default is black per military convention.
    /// </summary>
    public SKColor GridLineColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Grid line width in pixels.
    /// </summary>
    public float GridLineWidth { get; set; } = 1.0f;

    /// <summary>
    /// Label font size in points.
    /// </summary>
    public float LabelFontSize { get; set; } = 10f;

    /// <summary>
    /// Emphasized digit font size in points (principal digits).
    /// </summary>
    public float PrincipalDigitFontSize { get; set; } = 14f;

    /// <summary>
    /// Label color.
    /// </summary>
    public SKColor LabelColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Draw labels on margins.
    /// </summary>
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// Margin width in pixels for labels.
    /// </summary>
    public int MarginPixels { get; set; } = 30;
}

/// <summary>
/// Renders MGRS grid lines and labels on map images.
/// </summary>
public class MgrsGridRenderer
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;
    private const double SemiMajorAxis = 6378137.0;
    private const double EccentricitySquared = 0.00669438;
    private const double K0 = 0.9996;

    private readonly MgrsGridOptions _options;

    public MgrsGridRenderer() : this(new MgrsGridOptions()) { }

    public MgrsGridRenderer(MgrsGridOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the grid interval in meters based on scale.
    /// </summary>
    public int GetGridInterval()
    {
        if (_options.GridIntervalMeters.HasValue)
            return _options.GridIntervalMeters.Value;

        return _options.Scale switch
        {
            MapScale.Scale1To10000 => 1000,   // 1km
            MapScale.Scale1To25000 => 1000,   // 1km
            MapScale.Scale1To50000 => 1000,   // 1km
            MapScale.Scale1To100000 => 10000, // 10km
            _ => 1000
        };
    }

    /// <summary>
    /// Draws MGRS grid overlay on a bitmap.
    /// </summary>
    public SKBitmap DrawGrid(
        SKBitmap baseMap,
        double minLat, double maxLat,
        double minLon, double maxLon)
    {
        int gridInterval = GetGridInterval();

        // Create copy with margins
        int marginWidth = _options.ShowLabels ? _options.MarginPixels : 0;
        int outputWidth = baseMap.Width + 2 * marginWidth;
        int outputHeight = baseMap.Height + 2 * marginWidth;

        var result = new SKBitmap(outputWidth, outputHeight);
        using var canvas = new SKCanvas(result);

        // Fill margins with white
        canvas.Clear(SKColors.White);

        // Draw base map in center
        canvas.DrawBitmap(baseMap, marginWidth, marginWidth);

        // Calculate UTM zone for the center
        double centerLon = (minLon + maxLon) / 2;
        double centerLat = (minLat + maxLat) / 2;
        int zone = GetUtmZone(centerLat, centerLon);
        double lonOrigin = (zone - 1) * 6 - 180 + 3;

        // Convert corners to UTM
        var (minE, minN) = LatLonToUtm(minLat, minLon, zone);
        var (maxE, maxN) = LatLonToUtm(maxLat, maxLon, zone);

        // Round to grid interval
        double startEasting = Math.Floor(minE / gridInterval) * gridInterval;
        double endEasting = Math.Ceiling(maxE / gridInterval) * gridInterval;
        double startNorthing = Math.Floor(minN / gridInterval) * gridInterval;
        double endNorthing = Math.Ceiling(maxN / gridInterval) * gridInterval;

        // Pixels per meter
        double pixelsPerMeterX = baseMap.Width / (maxE - minE);
        double pixelsPerMeterY = baseMap.Height / (maxN - minN);

        using var linePaint = new SKPaint
        {
            Color = _options.GridLineColor,
            StrokeWidth = _options.GridLineWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        using var labelPaint = new SKPaint
        {
            Color = _options.LabelColor,
            TextSize = _options.LabelFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.Default
        };

        using var principalPaint = new SKPaint
        {
            Color = _options.LabelColor,
            TextSize = _options.PrincipalDigitFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        // Draw vertical grid lines (constant easting)
        for (double easting = startEasting; easting <= endEasting; easting += gridInterval)
        {
            double pixelX = (easting - minE) * pixelsPerMeterX + marginWidth;

            if (pixelX >= marginWidth && pixelX <= marginWidth + baseMap.Width)
            {
                canvas.DrawLine(
                    (float)pixelX, marginWidth,
                    (float)pixelX, marginWidth + baseMap.Height,
                    linePaint);

                if (_options.ShowLabels)
                {
                    DrawEastingLabel(canvas, easting, (float)pixelX, marginWidth, baseMap.Height, labelPaint, principalPaint);
                }
            }
        }

        // Draw horizontal grid lines (constant northing)
        for (double northing = startNorthing; northing <= endNorthing; northing += gridInterval)
        {
            // Northing increases upward, but pixel Y increases downward
            double pixelY = marginWidth + baseMap.Height - (northing - minN) * pixelsPerMeterY;

            if (pixelY >= marginWidth && pixelY <= marginWidth + baseMap.Height)
            {
                canvas.DrawLine(
                    marginWidth, (float)pixelY,
                    marginWidth + baseMap.Width, (float)pixelY,
                    linePaint);

                if (_options.ShowLabels)
                {
                    DrawNorthingLabel(canvas, northing, (float)pixelY, marginWidth, baseMap.Width, labelPaint, principalPaint);
                }
            }
        }

        return result;
    }

    private void DrawEastingLabel(
        SKCanvas canvas, double easting, float pixelX,
        int marginWidth, int mapHeight,
        SKPaint labelPaint, SKPaint principalPaint)
    {
        // Format: full value is like 385000
        // Principal digits are the kilometers (85 from 385)
        int kmValue = (int)(easting / 1000) % 100;
        string label = kmValue.ToString("00");

        var bounds = new SKRect();
        principalPaint.MeasureText(label, ref bounds);

        // Draw at bottom margin
        float textY = marginWidth + mapHeight + bounds.Height + 5;
        float textX = pixelX - bounds.Width / 2;

        canvas.DrawText(label, textX, textY, principalPaint);

        // Draw at top margin
        textY = marginWidth - 5;
        canvas.DrawText(label, textX, textY, principalPaint);
    }

    private void DrawNorthingLabel(
        SKCanvas canvas, double northing, float pixelY,
        int marginWidth, int mapWidth,
        SKPaint labelPaint, SKPaint principalPaint)
    {
        // Principal digits are the kilometers (last 2 digits of km value)
        int kmValue = (int)(northing / 1000) % 100;
        string label = kmValue.ToString("00");

        var bounds = new SKRect();
        principalPaint.MeasureText(label, ref bounds);

        // Draw at left margin
        float textX = marginWidth - bounds.Width - 5;
        float textY = pixelY + bounds.Height / 2;

        canvas.DrawText(label, textX, textY, principalPaint);

        // Draw at right margin
        textX = marginWidth + mapWidth + 5;
        canvas.DrawText(label, textX, textY, principalPaint);
    }

    /// <summary>
    /// Calculates the grid convergence angle at a given location.
    /// This is the angle between grid north and true north.
    /// </summary>
    public double GetGridConvergence(double lat, double lon)
    {
        int zone = GetUtmZone(lat, lon);
        double lonOrigin = (zone - 1) * 6 - 180 + 3;
        double deltaLon = lon - lonOrigin;

        // Simplified convergence calculation
        double convergence = deltaLon * Math.Sin(lat * DegToRad);
        return convergence;
    }

    private static int GetUtmZone(double lat, double lon)
    {
        // Handle special zones for Norway and Svalbard
        if (lat >= 56.0 && lat < 64.0 && lon >= 3.0 && lon < 12.0)
            return 32;

        if (lat >= 72.0 && lat < 84.0)
        {
            if (lon >= 0.0 && lon < 9.0) return 31;
            if (lon >= 9.0 && lon < 21.0) return 33;
            if (lon >= 21.0 && lon < 33.0) return 35;
            if (lon >= 33.0 && lon < 42.0) return 37;
        }

        return (int)((lon + 180) / 6) + 1;
    }

    private static (double easting, double northing) LatLonToUtm(double lat, double lon, int zone)
    {
        double lonOrigin = (zone - 1) * 6 - 180 + 3;

        double latRad = lat * DegToRad;
        double lonRad = lon * DegToRad;
        double lonOriginRad = lonOrigin * DegToRad;

        double N = SemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * Math.Sin(latRad) * Math.Sin(latRad));
        double T = Math.Tan(latRad) * Math.Tan(latRad);
        double C = EccentricitySquared / (1 - EccentricitySquared) * Math.Cos(latRad) * Math.Cos(latRad);
        double A = Math.Cos(latRad) * (lonRad - lonOriginRad);

        double M = SemiMajorAxis * (
            (1 - EccentricitySquared / 4 - 3 * Math.Pow(EccentricitySquared, 2) / 64) * latRad
            - (3 * EccentricitySquared / 8 + 3 * Math.Pow(EccentricitySquared, 2) / 32) * Math.Sin(2 * latRad)
            + (15 * Math.Pow(EccentricitySquared, 2) / 256) * Math.Sin(4 * latRad)
        );

        double easting = K0 * N * (
            A + (1 - T + C) * Math.Pow(A, 3) / 6
            + (5 - 18 * T + T * T) * Math.Pow(A, 5) / 120
        ) + 500000.0;

        double northing = K0 * (
            M + N * Math.Tan(latRad) * (
                A * A / 2
                + (5 - T + 9 * C + 4 * C * C) * Math.Pow(A, 4) / 24
                + (61 - 58 * T + T * T) * Math.Pow(A, 6) / 720
            )
        );

        if (lat < 0)
            northing += 10000000.0;

        return (easting, northing);
    }
}
