using System;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Military color palette following common topographic map conventions.
/// Based on US military and NATO standard map colors.
/// </summary>
public static class MilitaryColors
{
    // Vegetation colors
    public static SKColor VegetationLight { get; } = new SKColor(200, 230, 180);     // Light green for scattered vegetation
    public static SKColor VegetationDense { get; } = new SKColor(140, 195, 110);     // Medium green for dense vegetation
    public static SKColor Woodland { get; } = new SKColor(180, 220, 160);            // Standard woodland green
    public static SKColor Orchard { get; } = new SKColor(170, 210, 150);             // Orchard/plantation green

    // Water colors
    public static SKColor WaterFill { get; } = new SKColor(166, 206, 227);           // Light blue for water bodies
    public static SKColor WaterLine { get; } = new SKColor(100, 149, 237);           // Cornflower blue for streams
    public static SKColor IntermittentWater { get; } = new SKColor(173, 216, 230);   // Lighter blue for intermittent

    // Terrain colors
    public static SKColor SandDunes { get; } = new SKColor(255, 235, 205);           // Blanched almond for sand
    public static SKColor Rock { get; } = new SKColor(169, 169, 169);                // Dark gray for rock/cliff
    public static SKColor Marsh { get; } = new SKColor(176, 224, 230);               // Powder blue tint for marsh

    // Infrastructure colors
    public static SKColor RoadPrimary { get; } = new SKColor(178, 34, 34);           // Firebrick red for major roads
    public static SKColor RoadSecondary { get; } = new SKColor(210, 105, 30);        // Chocolate for secondary roads
    public static SKColor RoadMinor { get; } = new SKColor(139, 69, 19);             // Saddle brown for minor roads
    public static SKColor Trail { get; } = new SKColor(101, 67, 33);                 // Dark brown for trails
    public static SKColor Railroad { get; } = new SKColor(0, 0, 0);                  // Black for railroads
    public static SKColor Building { get; } = new SKColor(64, 64, 64);               // Dark gray for buildings

    // Boundary colors
    public static SKColor InternationalBoundary { get; } = new SKColor(128, 0, 128); // Purple for international
    public static SKColor StateBoundary { get; } = new SKColor(128, 0, 128);         // Purple for state/province
    public static SKColor CountyBoundary { get; } = new SKColor(128, 0, 128);        // Purple for county

    // Contour colors
    public static SKColor ContourIndex { get; } = new SKColor(139, 90, 43);          // Sienna brown for index contours
    public static SKColor ContourIntermediate { get; } = new SKColor(205, 133, 63);  // Peru brown for intermediate
    public static SKColor ContourSupplementary { get; } = new SKColor(222, 184, 135);// Burlywood for supplementary

    // Grid and reference colors
    public static SKColor GridMajor { get; } = new SKColor(0, 0, 0);                 // Black for major grid
    public static SKColor GridMinor { get; } = new SKColor(128, 128, 128);           // Gray for minor grid
    public static SKColor MagneticDeclination { get; } = new SKColor(255, 0, 0);     // Red for magnetic north

    // Special features
    public static SKColor DangerArea { get; } = new SKColor(255, 0, 0);              // Red for danger zones
    public static SKColor RestrictedArea { get; } = new SKColor(255, 165, 0);        // Orange for restricted
    public static SKColor MilitaryInstallation { get; } = new SKColor(128, 0, 0);    // Maroon for military areas
}

/// <summary>
/// Types of military map symbols.
/// </summary>
public enum SymbolType
{
    // Point features
    Hilltop,
    Saddle,
    Depression,
    Spring,
    Well,
    Mine,
    Cave,
    Tower,
    Windmill,
    Tank,
    Cemetery,
    Church,
    School,
    Hospital,
    
    // Linear features
    FenceBarbed,
    FenceWood,
    PowerLine,
    Pipeline,
    Levee,
    Embankment,
    CutFill,
    
    // Area patterns
    Marsh,
    Swamp,
    MangroveSwamp,
    SandDunes,
    Orchard,
    Vineyard,
    Scrub,
    
    // Military specific
    LandingZone,
    DropZone,
    ObservationPost,
    CommandPost,
    CheckPoint
}

/// <summary>
/// Options for military symbology rendering.
/// </summary>
public class MilitarySymbologyOptions
{
    /// <summary>
    /// Base symbol size in pixels.
    /// </summary>
    public float SymbolSize { get; set; } = 12f;

    /// <summary>
    /// Line width for symbol outlines.
    /// </summary>
    public float LineWidth { get; set; } = 1.5f;

    /// <summary>
    /// Font size for symbol labels.
    /// </summary>
    public float LabelFontSize { get; set; } = 8f;

    /// <summary>
    /// Whether to use NATO standard symbology where applicable.
    /// </summary>
    public bool UseNatoStandard { get; set; } = true;

    /// <summary>
    /// Opacity for area fill patterns (0.0-1.0).
    /// </summary>
    public float PatternOpacity { get; set; } = 0.5f;
}

/// <summary>
/// Renders military-style symbology and map features.
/// </summary>
public class MilitarySymbologyRenderer : IDisposable
{
    private readonly MilitarySymbologyOptions _options;
    private bool _disposed;

    public MilitarySymbologyRenderer() : this(new MilitarySymbologyOptions()) { }

    public MilitarySymbologyRenderer(MilitarySymbologyOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Draws a point symbol at the specified location.
    /// </summary>
    public void DrawSymbol(SKCanvas canvas, float x, float y, SymbolType type)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            StrokeWidth = _options.LineWidth,
            Color = SKColors.Black
        };

        switch (type)
        {
            case SymbolType.Hilltop:
                DrawHilltopSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Saddle:
                DrawSaddleSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Depression:
                DrawDepressionSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Spring:
                DrawSpringSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Well:
                DrawWellSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Mine:
                DrawMineSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Cave:
                DrawCaveSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Tower:
                DrawTowerSymbol(canvas, x, y, paint);
                break;
            case SymbolType.Cemetery:
                DrawCemeterySymbol(canvas, x, y, paint);
                break;
            case SymbolType.Church:
                DrawChurchSymbol(canvas, x, y, paint);
                break;
            case SymbolType.LandingZone:
                DrawLandingZoneSymbol(canvas, x, y, paint);
                break;
            case SymbolType.DropZone:
                DrawDropZoneSymbol(canvas, x, y, paint);
                break;
            case SymbolType.ObservationPost:
                DrawObservationPostSymbol(canvas, x, y, paint);
                break;
            case SymbolType.CommandPost:
                DrawCommandPostSymbol(canvas, x, y, paint);
                break;
            case SymbolType.CheckPoint:
                DrawCheckPointSymbol(canvas, x, y, paint);
                break;
            default:
                DrawDefaultSymbol(canvas, x, y, paint);
                break;
        }
    }

    /// <summary>
    /// Creates a pattern bitmap for area fills.
    /// </summary>
    public SKBitmap CreatePatternTile(SymbolType patternType, int tileSize = 24)
    {
        var bitmap = new SKBitmap(tileSize, tileSize);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            StrokeWidth = _options.LineWidth,
            Color = SKColors.Black.WithAlpha((byte)(_options.PatternOpacity * 255))
        };

        switch (patternType)
        {
            case SymbolType.Marsh:
                DrawMarshPattern(canvas, tileSize, paint);
                break;
            case SymbolType.Swamp:
                DrawSwampPattern(canvas, tileSize, paint);
                break;
            case SymbolType.SandDunes:
                DrawSandDunePattern(canvas, tileSize, paint);
                break;
            case SymbolType.Orchard:
                DrawOrchardPattern(canvas, tileSize, paint);
                break;
            case SymbolType.Vineyard:
                DrawVineyardPattern(canvas, tileSize, paint);
                break;
            case SymbolType.Scrub:
                DrawScrubPattern(canvas, tileSize, paint);
                break;
            default:
                // Return empty tile for unknown patterns
                break;
        }

        return bitmap;
    }

    /// <summary>
    /// Draws a linear feature with appropriate styling.
    /// </summary>
    public void DrawLinearFeature(SKCanvas canvas, SKPoint[] points, SymbolType type)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (points == null || points.Length < 2)
            return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _options.LineWidth
        };

        using var path = new SKPath();
        path.MoveTo(points[0]);
        for (int i = 1; i < points.Length; i++)
        {
            path.LineTo(points[i]);
        }

        switch (type)
        {
            case SymbolType.FenceBarbed:
                DrawBarbedFence(canvas, path, paint);
                break;
            case SymbolType.FenceWood:
                DrawWoodFence(canvas, path, paint);
                break;
            case SymbolType.PowerLine:
                DrawPowerLine(canvas, path, paint);
                break;
            case SymbolType.Pipeline:
                DrawPipeline(canvas, path, paint);
                break;
            case SymbolType.Levee:
                DrawLevee(canvas, path, paint);
                break;
            case SymbolType.Embankment:
                DrawEmbankment(canvas, path, paint);
                break;
            default:
                paint.Color = SKColors.Black;
                canvas.DrawPath(path, paint);
                break;
        }
    }

    /// <summary>
    /// Draws a declination diagram showing true north, magnetic north, and grid north.
    /// </summary>
    public SKBitmap DrawDeclinationDiagram(double gridDeclination, double magneticDeclination, int width = 120, int height = 150)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        float centerX = width / 2f;
        float baseY = height - 20;
        float arrowLength = height - 50;

        using var blackPaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 1.5f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        using var redPaint = new SKPaint
        {
            Color = MilitaryColors.MagneticDeclination,
            StrokeWidth = 1.5f,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        // Draw True North (straight up, with star)
        DrawArrowWithStar(canvas, centerX, baseY, 0, arrowLength, blackPaint, "TN");

        // Draw Grid North (offset by grid declination)
        float gnAngle = (float)(gridDeclination * Math.PI / 180);
        DrawArrowWithGN(canvas, centerX, baseY, gnAngle, arrowLength * 0.9f, blackPaint, "GN");

        // Draw Magnetic North (offset by magnetic declination)
        float mnAngle = (float)(magneticDeclination * Math.PI / 180);
        DrawArrowWithMN(canvas, centerX, baseY, mnAngle, arrowLength * 0.85f, redPaint, "MN");

        // Draw declination values
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 8,
            IsAntialias = true
        };

        string gdText = $"GD: {gridDeclination:F1}°";
        string mdText = $"MD: {magneticDeclination:F1}°";
        
        using var font = new SKFont(SKTypeface.Default, 8);
        canvas.DrawText(gdText, 5, height - 5, SKTextAlign.Left, font, textPaint);
        canvas.DrawText(mdText, width - 5 - MeasureTextWidth(mdText, font), height - 5, SKTextAlign.Left, font, textPaint);

        return bitmap;
    }

    #region Point Symbol Implementations

    private void DrawHilltopSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Style = SKPaintStyle.Fill;
        paint.Color = MilitaryColors.ContourIndex;
        
        // Small filled circle for hilltop
        canvas.DrawCircle(x, y, size / 4, paint);
    }

    private void DrawSaddleSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = MilitaryColors.ContourIndex;
        
        // Hourglass shape for saddle
        using var path = new SKPath();
        path.MoveTo(x - size / 2, y - size / 2);
        path.LineTo(x, y);
        path.LineTo(x + size / 2, y - size / 2);
        path.MoveTo(x - size / 2, y + size / 2);
        path.LineTo(x, y);
        path.LineTo(x + size / 2, y + size / 2);
        
        canvas.DrawPath(path, paint);
    }

    private void DrawDepressionSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = MilitaryColors.ContourIndex;
        
        // Circle with tick marks pointing inward
        canvas.DrawCircle(x, y, size / 2, paint);
        
        // Draw inward ticks
        for (int i = 0; i < 4; i++)
        {
            float angle = (float)(i * Math.PI / 2);
            float outerX = x + (float)Math.Cos(angle) * size / 2;
            float outerY = y + (float)Math.Sin(angle) * size / 2;
            float innerX = x + (float)Math.Cos(angle) * size / 3;
            float innerY = y + (float)Math.Sin(angle) * size / 3;
            canvas.DrawLine(outerX, outerY, innerX, innerY, paint);
        }
    }

    private void DrawSpringSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = MilitaryColors.WaterLine;
        paint.Style = SKPaintStyle.Fill;
        
        // Filled circle for spring
        canvas.DrawCircle(x, y, size / 3, paint);
        
        // Short wavy line flowing from it
        paint.Style = SKPaintStyle.Stroke;
        using var path = new SKPath();
        path.MoveTo(x + size / 3, y);
        path.CubicTo(x + size / 2, y - size / 4, x + size * 0.7f, y + size / 4, x + size, y);
        canvas.DrawPath(path, paint);
    }

    private void DrawWellSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = MilitaryColors.WaterLine;
        paint.Style = SKPaintStyle.Stroke;
        
        // Circle with dot
        canvas.DrawCircle(x, y, size / 2, paint);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(x, y, size / 6, paint);
    }

    private void DrawMineSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Stroke;
        
        // Crossed pickaxes symbol
        canvas.DrawLine(x - size / 2, y - size / 2, x + size / 2, y + size / 2, paint);
        canvas.DrawLine(x - size / 2, y + size / 2, x + size / 2, y - size / 2, paint);
    }

    private void DrawCaveSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Stroke;
        
        // Inverted V with opening
        using var path = new SKPath();
        path.MoveTo(x - size / 2, y + size / 3);
        path.LineTo(x, y - size / 2);
        path.LineTo(x + size / 2, y + size / 3);
        canvas.DrawPath(path, paint);
    }

    private void DrawTowerSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Stroke;
        
        // Triangle tower symbol
        using var path = new SKPath();
        path.MoveTo(x, y - size / 2);
        path.LineTo(x - size / 3, y + size / 2);
        path.LineTo(x + size / 3, y + size / 2);
        path.Close();
        canvas.DrawPath(path, paint);
        
        // Dot at top
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(x, y - size / 2, size / 8, paint);
    }

    private void DrawCemeterySymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Stroke;
        
        // Cross symbol
        canvas.DrawLine(x, y - size / 2, x, y + size / 2, paint);
        canvas.DrawLine(x - size / 3, y - size / 4, x + size / 3, y - size / 4, paint);
    }

    private void DrawChurchSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Stroke;
        
        // Cross with base
        canvas.DrawLine(x, y - size / 2, x, y + size / 3, paint);
        canvas.DrawLine(x - size / 3, y - size / 4, x + size / 3, y - size / 4, paint);
        
        // Base rectangle
        canvas.DrawRect(x - size / 4, y + size / 3, size / 2, size / 4, paint);
    }

    private void DrawLandingZoneSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize * 1.5f;
        paint.Color = MilitaryColors.MilitaryInstallation;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2f;
        
        // Circle with H
        canvas.DrawCircle(x, y, size / 2, paint);
        
        // H for helicopter
        float hSize = size / 3;
        canvas.DrawLine(x - hSize, y - hSize, x - hSize, y + hSize, paint);
        canvas.DrawLine(x + hSize, y - hSize, x + hSize, y + hSize, paint);
        canvas.DrawLine(x - hSize, y, x + hSize, y, paint);
    }

    private void DrawDropZoneSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize * 1.5f;
        paint.Color = MilitaryColors.MilitaryInstallation;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2f;
        
        // Circle with parachute shape
        canvas.DrawCircle(x, y, size / 2, paint);
        
        // Parachute arc
        using var path = new SKPath();
        path.AddArc(new SKRect(x - size / 3, y - size / 2, x + size / 3, y), 180, 180);
        canvas.DrawPath(path, paint);
        
        // Lines to payload
        canvas.DrawLine(x - size / 3, y, x, y + size / 3, paint);
        canvas.DrawLine(x + size / 3, y, x, y + size / 3, paint);
    }

    private void DrawObservationPostSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = MilitaryColors.MilitaryInstallation;
        paint.Style = SKPaintStyle.Stroke;
        
        // Triangle (NATO observation post)
        using var path = new SKPath();
        path.MoveTo(x, y - size / 2);
        path.LineTo(x - size / 2, y + size / 2);
        path.LineTo(x + size / 2, y + size / 2);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private void DrawCommandPostSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = MilitaryColors.MilitaryInstallation;
        paint.Style = SKPaintStyle.Stroke;
        
        // Rectangle (NATO command post base)
        canvas.DrawRect(x - size / 2, y - size / 3, size, size * 2 / 3, paint);
        
        // Flag
        using var path = new SKPath();
        path.MoveTo(x - size / 2, y - size / 3);
        path.LineTo(x - size / 2, y - size);
        path.LineTo(x, y - size * 2 / 3);
        path.LineTo(x - size / 2, y - size / 3);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawPath(path, paint);
    }

    private void DrawCheckPointSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Color = MilitaryColors.RoadPrimary;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2f;
        
        // Diamond shape
        using var path = new SKPath();
        path.MoveTo(x, y - size / 2);
        path.LineTo(x + size / 2, y);
        path.LineTo(x, y + size / 2);
        path.LineTo(x - size / 2, y);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private void DrawDefaultSymbol(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        float size = _options.SymbolSize;
        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.Black;
        canvas.DrawCircle(x, y, size / 4, paint);
    }

    #endregion

    #region Pattern Implementations

    private void DrawMarshPattern(SKCanvas canvas, int size, SKPaint paint)
    {
        paint.Color = MilitaryColors.Marsh.WithAlpha((byte)(_options.PatternOpacity * 255));
        paint.Style = SKPaintStyle.Stroke;
        
        // Horizontal lines with small vertical tufts (grass symbols)
        for (int y = size / 4; y < size; y += size / 2)
        {
            canvas.DrawLine(0, y, size, y, paint);
            
            // Tufts
            for (int x = size / 4; x < size; x += size / 2)
            {
                canvas.DrawLine(x, y, x - 2, y - 4, paint);
                canvas.DrawLine(x, y, x, y - 5, paint);
                canvas.DrawLine(x, y, x + 2, y - 4, paint);
            }
        }
    }

    private void DrawSwampPattern(SKCanvas canvas, int size, SKPaint paint)
    {
        paint.Color = MilitaryColors.Marsh.WithAlpha((byte)(_options.PatternOpacity * 255));
        paint.Style = SKPaintStyle.Stroke;
        
        // Marsh pattern with tree symbols
        DrawMarshPattern(canvas, size, paint);
        
        // Add small tree symbols
        paint.Color = MilitaryColors.VegetationDense.WithAlpha((byte)(_options.PatternOpacity * 255));
        float cx = size / 2f;
        float cy = size / 2f;
        
        // Simple tree (triangle on stick)
        using var path = new SKPath();
        path.MoveTo(cx, cy - 4);
        path.LineTo(cx - 3, cy + 2);
        path.LineTo(cx + 3, cy + 2);
        path.Close();
        canvas.DrawPath(path, paint);
        canvas.DrawLine(cx, cy + 2, cx, cy + 5, paint);
    }

    private void DrawSandDunePattern(SKCanvas canvas, int size, SKPaint paint)
    {
        paint.Color = MilitaryColors.SandDunes.WithAlpha((byte)(0.7 * 255));
        paint.Style = SKPaintStyle.Fill;
        
        // Fill background
        canvas.DrawRect(0, 0, size, size, paint);
        
        // Draw stipple dots
        paint.Color = new SKColor(200, 180, 140).WithAlpha((byte)(_options.PatternOpacity * 255));
        var random = new Random(42); // Fixed seed for consistent pattern
        for (int i = 0; i < 8; i++)
        {
            float x = (float)(random.NextDouble() * size);
            float y = (float)(random.NextDouble() * size);
            canvas.DrawCircle(x, y, 1, paint);
        }
    }

    private void DrawOrchardPattern(SKCanvas canvas, int size, SKPaint paint)
    {
        paint.Color = MilitaryColors.Orchard.WithAlpha((byte)(_options.PatternOpacity * 255));
        paint.Style = SKPaintStyle.Fill;
        
        // Regular grid of circles (tree symbols)
        float spacing = size / 2f;
        for (float x = spacing / 2; x < size; x += spacing)
        {
            for (float y = spacing / 2; y < size; y += spacing)
            {
                canvas.DrawCircle(x, y, 3, paint);
            }
        }
    }

    private void DrawVineyardPattern(SKCanvas canvas, int size, SKPaint paint)
    {
        paint.Color = MilitaryColors.VegetationLight.WithAlpha((byte)(_options.PatternOpacity * 255));
        paint.Style = SKPaintStyle.Stroke;
        
        // Parallel lines with small perpendicular marks
        float spacing = size / 3f;
        for (float y = spacing / 2; y < size; y += spacing)
        {
            canvas.DrawLine(0, y, size, y, paint);
            
            // Vine marks
            for (float x = spacing / 2; x < size; x += spacing / 2)
            {
                canvas.DrawLine(x, y - 2, x, y + 2, paint);
            }
        }
    }

    private void DrawScrubPattern(SKCanvas canvas, int size, SKPaint paint)
    {
        paint.Color = MilitaryColors.VegetationLight.WithAlpha((byte)(_options.PatternOpacity * 255));
        paint.Style = SKPaintStyle.Stroke;
        
        // Irregular scattered dots and short curved lines
        var random = new Random(42);
        for (int i = 0; i < 5; i++)
        {
            float x = (float)(random.NextDouble() * size);
            float y = (float)(random.NextDouble() * size);
            float angle = (float)(random.NextDouble() * Math.PI);
            
            using var path = new SKPath();
            path.MoveTo(x, y);
            path.QuadTo(x + 3, y - 2, x + 5, y);
            canvas.DrawPath(path, paint);
        }
    }

    #endregion

    #region Linear Feature Implementations

    private void DrawBarbedFence(SKCanvas canvas, SKPath basePath, SKPaint paint)
    {
        paint.Color = SKColors.Black;
        canvas.DrawPath(basePath, paint);
        
        // Add barb marks along the path
        using var measure = new SKPathMeasure(basePath);
        float length = measure.Length;
        float interval = _options.SymbolSize;
        
        for (float d = interval; d < length; d += interval)
        {
            if (measure.GetPositionAndTangent(d, out var pos, out var tangent))
            {
                // Draw X mark perpendicular to path
                float perpX = -tangent.Y * 3;
                float perpY = tangent.X * 3;
                
                canvas.DrawLine(pos.X - perpX - 2, pos.Y - perpY - 2, 
                               pos.X + perpX + 2, pos.Y + perpY + 2, paint);
                canvas.DrawLine(pos.X - perpX + 2, pos.Y - perpY - 2,
                               pos.X + perpX - 2, pos.Y + perpY + 2, paint);
            }
        }
    }

    private void DrawWoodFence(SKCanvas canvas, SKPath basePath, SKPaint paint)
    {
        paint.Color = MilitaryColors.Trail;
        canvas.DrawPath(basePath, paint);
        
        // Add post marks
        using var measure = new SKPathMeasure(basePath);
        float length = measure.Length;
        float interval = _options.SymbolSize * 1.5f;
        
        for (float d = 0; d < length; d += interval)
        {
            if (measure.GetPositionAndTangent(d, out var pos, out var tangent))
            {
                float perpX = -tangent.Y * 4;
                float perpY = tangent.X * 4;
                canvas.DrawLine(pos.X - perpX, pos.Y - perpY, pos.X + perpX, pos.Y + perpY, paint);
            }
        }
    }

    private void DrawPowerLine(SKCanvas canvas, SKPath basePath, SKPaint paint)
    {
        paint.Color = SKColors.Black;
        canvas.DrawPath(basePath, paint);
        
        // Add tower symbols
        using var measure = new SKPathMeasure(basePath);
        float length = measure.Length;
        float interval = _options.SymbolSize * 3;
        
        for (float d = interval / 2; d < length; d += interval)
        {
            if (measure.GetPosition(d, out var pos))
            {
                // Small circle for tower
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawCircle(pos.X, pos.Y, 3, paint);
                paint.Style = SKPaintStyle.Stroke;
            }
        }
    }

    private void DrawPipeline(SKCanvas canvas, SKPath basePath, SKPaint paint)
    {
        paint.Color = SKColors.Black;
        paint.PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0);
        canvas.DrawPath(basePath, paint);
        paint.PathEffect = null;
    }

    private void DrawLevee(SKCanvas canvas, SKPath basePath, SKPaint paint)
    {
        paint.Color = MilitaryColors.Trail;
        paint.StrokeWidth = _options.LineWidth * 2;
        canvas.DrawPath(basePath, paint);
        
        // Add tick marks on one side
        paint.StrokeWidth = _options.LineWidth;
        using var measure = new SKPathMeasure(basePath);
        float length = measure.Length;
        float interval = _options.SymbolSize;
        
        for (float d = 0; d < length; d += interval)
        {
            if (measure.GetPositionAndTangent(d, out var pos, out var tangent))
            {
                float perpX = -tangent.Y * 5;
                float perpY = tangent.X * 5;
                canvas.DrawLine(pos.X, pos.Y, pos.X + perpX, pos.Y + perpY, paint);
            }
        }
    }

    private void DrawEmbankment(SKCanvas canvas, SKPath basePath, SKPaint paint)
    {
        paint.Color = MilitaryColors.Trail;
        canvas.DrawPath(basePath, paint);
        
        // Add hachure marks
        using var measure = new SKPathMeasure(basePath);
        float length = measure.Length;
        float interval = _options.SymbolSize / 2;
        
        for (float d = 0; d < length; d += interval)
        {
            if (measure.GetPositionAndTangent(d, out var pos, out var tangent))
            {
                float perpX = -tangent.Y * 6;
                float perpY = tangent.X * 6;
                canvas.DrawLine(pos.X, pos.Y, pos.X + perpX, pos.Y + perpY, paint);
            }
        }
    }

    #endregion

    #region Declination Diagram Helpers

    private void DrawArrowWithStar(SKCanvas canvas, float x, float y, float angle, float length, SKPaint paint, string label)
    {
        float tipX = x + (float)Math.Sin(angle) * length;
        float tipY = y - (float)Math.Cos(angle) * length;
        
        // Draw arrow line
        canvas.DrawLine(x, y, tipX, tipY, paint);
        
        // Draw star at tip
        DrawStar(canvas, tipX, tipY - 8, 6, paint);
        
        // Draw label
        using var font = new SKFont(SKTypeface.Default, 10);
        using var textPaint = new SKPaint { Color = paint.Color };
        canvas.DrawText(label, tipX + 5, tipY - 5, SKTextAlign.Left, font, textPaint);
    }

    private void DrawArrowWithGN(SKCanvas canvas, float x, float y, float angle, float length, SKPaint paint, string label)
    {
        float tipX = x + (float)Math.Sin(angle) * length;
        float tipY = y - (float)Math.Cos(angle) * length;
        
        canvas.DrawLine(x, y, tipX, tipY, paint);
        
        // Arrow head
        DrawArrowHead(canvas, tipX, tipY, angle, paint);
        
        using var font = new SKFont(SKTypeface.Default, 10);
        using var textPaint = new SKPaint { Color = paint.Color };
        canvas.DrawText(label, tipX + 5, tipY, SKTextAlign.Left, font, textPaint);
    }

    private void DrawArrowWithMN(SKCanvas canvas, float x, float y, float angle, float length, SKPaint paint, string label)
    {
        float tipX = x + (float)Math.Sin(angle) * length;
        float tipY = y - (float)Math.Cos(angle) * length;
        
        // Draw half-arrow (magnetic north style)
        paint.PathEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0);
        canvas.DrawLine(x, y, tipX, tipY, paint);
        paint.PathEffect = null;
        
        // Arrow head
        DrawArrowHead(canvas, tipX, tipY, angle, paint);
        
        using var font = new SKFont(SKTypeface.Default, 10);
        using var textPaint = new SKPaint { Color = paint.Color };
        canvas.DrawText(label, tipX + 5, tipY, SKTextAlign.Left, font, textPaint);
    }

    private void DrawStar(SKCanvas canvas, float x, float y, float size, SKPaint paint)
    {
        paint.Style = SKPaintStyle.Fill;
        using var path = new SKPath();
        
        for (int i = 0; i < 5; i++)
        {
            float outerAngle = (float)(i * 2 * Math.PI / 5 - Math.PI / 2);
            float innerAngle = (float)((i + 0.5) * 2 * Math.PI / 5 - Math.PI / 2);
            
            float outerX = x + (float)Math.Cos(outerAngle) * size;
            float outerY = y + (float)Math.Sin(outerAngle) * size;
            float innerX = x + (float)Math.Cos(innerAngle) * size / 2;
            float innerY = y + (float)Math.Sin(innerAngle) * size / 2;
            
            if (i == 0)
                path.MoveTo(outerX, outerY);
            else
                path.LineTo(outerX, outerY);
            
            path.LineTo(innerX, innerY);
        }
        path.Close();
        canvas.DrawPath(path, paint);
        paint.Style = SKPaintStyle.Stroke;
    }

    private void DrawArrowHead(SKCanvas canvas, float x, float y, float angle, SKPaint paint)
    {
        float headSize = 8;
        float leftAngle = angle - (float)Math.PI * 0.85f;
        float rightAngle = angle + (float)Math.PI * 0.85f;
        
        using var path = new SKPath();
        path.MoveTo(x, y);
        path.LineTo(x + (float)Math.Sin(leftAngle) * headSize, y - (float)Math.Cos(leftAngle) * headSize);
        path.MoveTo(x, y);
        path.LineTo(x + (float)Math.Sin(rightAngle) * headSize, y - (float)Math.Cos(rightAngle) * headSize);
        
        canvas.DrawPath(path, paint);
    }

    private float MeasureTextWidth(string text, SKFont font)
    {
        using var paint = new SKPaint();
        return paint.MeasureText(text);
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
