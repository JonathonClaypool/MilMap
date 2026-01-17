using System;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Options for magnetic declination diagram rendering.
/// </summary>
public class DeclinationOptions
{
    /// <summary>
    /// Width of the declination diagram in pixels.
    /// </summary>
    public int Width { get; set; } = 100;

    /// <summary>
    /// Height of the declination diagram in pixels.
    /// </summary>
    public int Height { get; set; } = 120;

    /// <summary>
    /// Background color for the diagram.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.White;

    /// <summary>
    /// Color for the arrows and labels.
    /// </summary>
    public SKColor ForegroundColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Font size for labels.
    /// </summary>
    public float LabelFontSize { get; set; } = 10f;

    /// <summary>
    /// Arrow line width.
    /// </summary>
    public float ArrowLineWidth { get; set; } = 1.5f;

    /// <summary>
    /// Length of the true north arrow.
    /// </summary>
    public float TrueNorthArrowLength { get; set; } = 60f;

    /// <summary>
    /// Length of the magnetic north arrow.
    /// </summary>
    public float MagneticNorthArrowLength { get; set; } = 50f;

    /// <summary>
    /// Length of the grid north arrow.
    /// </summary>
    public float GridNorthArrowLength { get; set; } = 45f;

    /// <summary>
    /// Show grid north (GN) in addition to true north and magnetic north.
    /// </summary>
    public bool ShowGridNorth { get; set; } = true;

    /// <summary>
    /// Year for the magnetic declination calculation.
    /// If null, uses the current year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Show annual change rate label.
    /// </summary>
    public bool ShowAnnualChange { get; set; } = true;
}

/// <summary>
/// Result of magnetic declination calculation.
/// </summary>
public record MagneticDeclinationResult(
    double Declination,
    double AnnualChange,
    double GridConvergence,
    int Year,
    SKBitmap Diagram);

/// <summary>
/// Calculates and renders magnetic declination diagrams for topographic maps.
/// </summary>
public class MagneticDeclinationRenderer
{
    // WMM2020 coefficients (simplified - actual implementation would use full model)
    // These are approximations suitable for general mapping purposes
    private const double WmmBaseYear = 2025.0;
    
    private readonly DeclinationOptions _options;

    public MagneticDeclinationRenderer() : this(new DeclinationOptions()) { }

    public MagneticDeclinationRenderer(DeclinationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Calculates magnetic declination for a given location.
    /// Uses a simplified World Magnetic Model approximation.
    /// </summary>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="longitude">Longitude in degrees</param>
    /// <param name="year">Year for calculation (defaults to current year)</param>
    /// <returns>Magnetic declination in degrees (positive = east, negative = west)</returns>
    public static double CalculateDeclination(double latitude, double longitude, int? year = null)
    {
        int calcYear = year ?? DateTime.UtcNow.Year;
        
        // Simplified magnetic declination model based on WMM
        // This is an approximation - production use should employ full WMM or IGRF
        double latRad = latitude * Math.PI / 180.0;
        double lonRad = longitude * Math.PI / 180.0;

        // Base declination calculation (simplified dipole model with secular variation)
        // The magnetic pole is approximately at 80.65°N, -72.68°W (2025)
        double magPoleLat = 80.65 * Math.PI / 180.0;
        double magPoleLon = -72.68 * Math.PI / 180.0;

        // Calculate approximate declination using spherical geometry
        double sinMagLat = Math.Sin(magPoleLat);
        double cosMagLat = Math.Cos(magPoleLat);
        double sinLat = Math.Sin(latRad);
        double cosLat = Math.Cos(latRad);
        double lonDiff = lonRad - magPoleLon;

        // Approximate declination formula
        double numerator = cosMagLat * Math.Sin(lonDiff);
        double denominator = sinMagLat * cosLat - cosMagLat * sinLat * Math.Cos(lonDiff);
        
        double declination = Math.Atan2(numerator, denominator) * 180.0 / Math.PI;

        // Apply time-dependent correction (simplified linear secular variation)
        double yearsSinceBase = calcYear - WmmBaseYear;
        double annualChange = CalculateAnnualChange(latitude, longitude);
        declination += annualChange * yearsSinceBase;

        // Normalize to -180 to 180
        while (declination > 180) declination -= 360;
        while (declination < -180) declination += 360;

        return declination;
    }

    /// <summary>
    /// Calculates the annual change in magnetic declination (degrees per year).
    /// </summary>
    public static double CalculateAnnualChange(double latitude, double longitude)
    {
        // Simplified annual change model
        // Actual values vary from about -0.2°/year to +0.2°/year globally
        double latRad = latitude * Math.PI / 180.0;
        double lonRad = longitude * Math.PI / 180.0;

        // Approximate annual change based on location
        // Higher latitudes near the magnetic poles have greater change
        double baseChange = 0.05; // Base change in degrees/year
        double latFactor = Math.Abs(Math.Sin(latRad)) * 0.1;
        
        // Longitude-dependent variation
        double lonFactor = Math.Cos(lonRad * 2) * 0.02;

        return baseChange + latFactor + lonFactor;
    }

    /// <summary>
    /// Calculates grid convergence (angle between true north and grid north).
    /// </summary>
    /// <param name="latitude">Latitude in degrees</param>
    /// <param name="longitude">Longitude in degrees</param>
    /// <returns>Grid convergence in degrees (positive = grid north east of true north)</returns>
    public static double CalculateGridConvergence(double latitude, double longitude)
    {
        // Grid convergence depends on the UTM zone
        int zone = (int)((longitude + 180) / 6) + 1;
        double centralMeridian = (zone - 1) * 6 - 180 + 3;
        double lonDiff = longitude - centralMeridian;

        // Grid convergence approximation
        double latRad = latitude * Math.PI / 180.0;
        double convergence = lonDiff * Math.Sin(latRad);

        return convergence;
    }

    /// <summary>
    /// Renders a declination diagram for a given location.
    /// </summary>
    /// <param name="latitude">Center latitude of the map</param>
    /// <param name="longitude">Center longitude of the map</param>
    /// <returns>Declination calculation result with rendered diagram</returns>
    public MagneticDeclinationResult Render(double latitude, double longitude)
    {
        int year = _options.Year ?? DateTime.UtcNow.Year;
        double declination = CalculateDeclination(latitude, longitude, year);
        double annualChange = CalculateAnnualChange(latitude, longitude);
        double gridConvergence = CalculateGridConvergence(latitude, longitude);

        var bitmap = new SKBitmap(_options.Width, _options.Height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(_options.BackgroundColor);

        float centerX = _options.Width / 2f;
        float centerY = _options.Height * 0.6f; // Arrows start from lower center

        using var arrowPaint = new SKPaint
        {
            Color = _options.ForegroundColor,
            StrokeWidth = _options.ArrowLineWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        using var fillPaint = new SKPaint
        {
            Color = _options.ForegroundColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        using var labelPaint = new SKPaint
        {
            Color = _options.ForegroundColor,
            TextSize = _options.LabelFontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        // Draw True North arrow (straight up, with star)
        DrawArrow(canvas, centerX, centerY, 0, _options.TrueNorthArrowLength, arrowPaint, fillPaint);
        DrawStar(canvas, centerX, centerY - _options.TrueNorthArrowLength - 8, 5, fillPaint);
        canvas.DrawText("TN", centerX, centerY - _options.TrueNorthArrowLength - 15, labelPaint);

        // Draw Magnetic North arrow (rotated by declination)
        float magAngle = (float)declination;
        float magX = centerX + (float)(_options.MagneticNorthArrowLength * Math.Sin(magAngle * Math.PI / 180));
        float magY = centerY - (float)(_options.MagneticNorthArrowLength * Math.Cos(magAngle * Math.PI / 180));
        DrawArrowWithHead(canvas, centerX, centerY, magX, magY, arrowPaint, fillPaint);
        
        // MN label
        float mnLabelX = centerX + (float)((_options.MagneticNorthArrowLength + 12) * Math.Sin(magAngle * Math.PI / 180));
        float mnLabelY = centerY - (float)((_options.MagneticNorthArrowLength + 12) * Math.Cos(magAngle * Math.PI / 180));
        canvas.DrawText("MN", mnLabelX, mnLabelY, labelPaint);

        // Draw Grid North arrow if enabled
        if (_options.ShowGridNorth)
        {
            float gnAngle = (float)gridConvergence;
            float gnX = centerX + (float)(_options.GridNorthArrowLength * Math.Sin(gnAngle * Math.PI / 180));
            float gnY = centerY - (float)(_options.GridNorthArrowLength * Math.Cos(gnAngle * Math.PI / 180));
            
            using var gnPaint = new SKPaint
            {
                Color = _options.ForegroundColor,
                StrokeWidth = _options.ArrowLineWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                PathEffect = SKPathEffect.CreateDash(new float[] { 4, 2 }, 0)
            };
            DrawArrowWithHead(canvas, centerX, centerY, gnX, gnY, gnPaint, fillPaint);
            
            float gnLabelX = centerX + (float)((_options.GridNorthArrowLength + 12) * Math.Sin(gnAngle * Math.PI / 180));
            float gnLabelY = centerY - (float)((_options.GridNorthArrowLength + 12) * Math.Cos(gnAngle * Math.PI / 180));
            canvas.DrawText("GN", gnLabelX, gnLabelY, labelPaint);
        }

        // Draw declination angle arc
        if (Math.Abs(declination) > 0.5)
        {
            float arcRadius = 20;
            using var arcPaint = new SKPaint
            {
                Color = _options.ForegroundColor,
                StrokeWidth = 0.5f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            
            float startAngle = -90; // True north is at -90 degrees in Skia coordinates
            float sweepAngle = (float)declination;
            
            var rect = new SKRect(centerX - arcRadius, centerY - arcRadius, centerX + arcRadius, centerY + arcRadius);
            canvas.DrawArc(rect, startAngle, sweepAngle, false, arcPaint);
        }

        // Draw declination value label
        string declinationStr = FormatDeclination(declination);
        float labelY = _options.Height - 20;
        canvas.DrawText(declinationStr, centerX, labelY, labelPaint);

        // Draw annual change if enabled
        if (_options.ShowAnnualChange)
        {
            string changeStr = $"({annualChange:+0.0;-0.0}°/year)";
            canvas.DrawText(changeStr, centerX, labelY + 12, labelPaint);
        }

        return new MagneticDeclinationResult(declination, annualChange, gridConvergence, year, bitmap);
    }

    private static void DrawArrow(SKCanvas canvas, float x, float y, float angle, float length, SKPaint strokePaint, SKPaint fillPaint)
    {
        float angleRad = angle * (float)Math.PI / 180f;
        float endX = x + length * (float)Math.Sin(angleRad);
        float endY = y - length * (float)Math.Cos(angleRad);
        
        canvas.DrawLine(x, y, endX, endY, strokePaint);
    }

    private static void DrawArrowWithHead(SKCanvas canvas, float x1, float y1, float x2, float y2, SKPaint strokePaint, SKPaint fillPaint)
    {
        canvas.DrawLine(x1, y1, x2, y2, strokePaint);
        
        // Draw arrowhead
        float angle = (float)Math.Atan2(y2 - y1, x2 - x1);
        float headLength = 6;
        float headAngle = 25 * (float)Math.PI / 180f;
        
        using var path = new SKPath();
        path.MoveTo(x2, y2);
        path.LineTo(
            x2 - headLength * (float)Math.Cos(angle - headAngle),
            y2 - headLength * (float)Math.Sin(angle - headAngle));
        path.LineTo(
            x2 - headLength * (float)Math.Cos(angle + headAngle),
            y2 - headLength * (float)Math.Sin(angle + headAngle));
        path.Close();
        
        canvas.DrawPath(path, fillPaint);
    }

    private static void DrawStar(SKCanvas canvas, float cx, float cy, float radius, SKPaint fillPaint)
    {
        // Draw a 5-pointed star for True North
        using var path = new SKPath();
        
        for (int i = 0; i < 5; i++)
        {
            double angle = i * 2 * Math.PI / 5 - Math.PI / 2;
            float x = cx + radius * (float)Math.Cos(angle);
            float y = cy + radius * (float)Math.Sin(angle);
            
            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
            
            // Inner point
            double innerAngle = angle + Math.PI / 5;
            float innerRadius = radius * 0.4f;
            float ix = cx + innerRadius * (float)Math.Cos(innerAngle);
            float iy = cy + innerRadius * (float)Math.Sin(innerAngle);
            path.LineTo(ix, iy);
        }
        path.Close();
        
        canvas.DrawPath(path, fillPaint);
    }

    private static string FormatDeclination(double declination)
    {
        string direction = declination >= 0 ? "E" : "W";
        double absDec = Math.Abs(declination);
        int degrees = (int)absDec;
        int minutes = (int)((absDec - degrees) * 60);
        
        return $"{degrees}° {minutes}' {direction}";
    }
}
