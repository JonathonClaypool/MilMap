using System;
using System.Collections.Generic;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Configuration for a single legend item.
/// </summary>
public record LegendItem(
    string Label,
    LegendItemType Type,
    SKColor? Color = null,
    SymbolType? Symbol = null,
    float[]? DashPattern = null);

/// <summary>
/// Types of legend items.
/// </summary>
public enum LegendItemType
{
    /// <summary>Point symbol from military symbology.</summary>
    PointSymbol,
    /// <summary>Solid line.</summary>
    SolidLine,
    /// <summary>Dashed line.</summary>
    DashedLine,
    /// <summary>Filled area.</summary>
    FilledArea,
    /// <summary>Pattern-filled area.</summary>
    PatternArea,
    /// <summary>Contour line.</summary>
    ContourLine,
    /// <summary>Section header (no symbol, just text).</summary>
    Header
}

/// <summary>
/// Options for legend rendering.
/// </summary>
public class LegendOptions
{
    /// <summary>
    /// Title displayed at the top of the legend.
    /// </summary>
    public string Title { get; set; } = "LEGEND";

    /// <summary>
    /// Width of the legend in pixels.
    /// </summary>
    public int Width { get; set; } = 200;

    /// <summary>
    /// Height of each legend item row in pixels.
    /// </summary>
    public int RowHeight { get; set; } = 24;

    /// <summary>
    /// Width of the symbol sample area in pixels.
    /// </summary>
    public int SymbolWidth { get; set; } = 40;

    /// <summary>
    /// Padding around the legend content in pixels.
    /// </summary>
    public int Padding { get; set; } = 12;

    /// <summary>
    /// Font size for legend labels.
    /// </summary>
    public float LabelFontSize { get; set; } = 10f;

    /// <summary>
    /// Font size for the legend title.
    /// </summary>
    public float TitleFontSize { get; set; } = 12f;

    /// <summary>
    /// Font size for section headers.
    /// </summary>
    public float HeaderFontSize { get; set; } = 11f;

    /// <summary>
    /// Background color of the legend.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.White;

    /// <summary>
    /// Border color of the legend.
    /// </summary>
    public SKColor BorderColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Text color for labels.
    /// </summary>
    public SKColor TextColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Line width for symbol samples.
    /// </summary>
    public float LineWidth { get; set; } = 1.5f;

    /// <summary>
    /// Whether to draw a border around the legend.
    /// </summary>
    public bool DrawBorder { get; set; } = true;

    /// <summary>
    /// Number of columns for legend layout.
    /// </summary>
    public int Columns { get; set; } = 1;
}

/// <summary>
/// Renders map legends with symbols and scale information.
/// </summary>
public class LegendRenderer : IDisposable
{
    private readonly LegendOptions _options;
    private readonly MilitarySymbologyRenderer _symbologyRenderer;
    private bool _disposed;

    public LegendRenderer() : this(new LegendOptions()) { }

    public LegendRenderer(LegendOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _symbologyRenderer = new MilitarySymbologyRenderer();
    }

    /// <summary>
    /// Creates a default set of legend items for military topographic maps.
    /// </summary>
    public static IReadOnlyList<LegendItem> CreateDefaultLegendItems()
    {
        return new List<LegendItem>
        {
            // Roads section
            new("ROADS", LegendItemType.Header),
            new("Primary Road", LegendItemType.SolidLine, MilitaryColors.RoadPrimary),
            new("Secondary Road", LegendItemType.SolidLine, MilitaryColors.RoadSecondary),
            new("Minor Road", LegendItemType.SolidLine, MilitaryColors.RoadMinor),
            new("Trail", LegendItemType.DashedLine, MilitaryColors.Trail, DashPattern: new[] { 6f, 4f }),
            new("Railroad", LegendItemType.SolidLine, MilitaryColors.Railroad),

            // Water section
            new("WATER FEATURES", LegendItemType.Header),
            new("Water Body", LegendItemType.FilledArea, MilitaryColors.WaterFill),
            new("Stream/River", LegendItemType.SolidLine, MilitaryColors.WaterLine),
            new("Intermittent Stream", LegendItemType.DashedLine, MilitaryColors.IntermittentWater, DashPattern: new[] { 8f, 4f }),
            new("Marsh/Swamp", LegendItemType.PatternArea, MilitaryColors.Marsh, SymbolType.Marsh),

            // Vegetation section
            new("VEGETATION", LegendItemType.Header),
            new("Woodland", LegendItemType.FilledArea, MilitaryColors.Woodland),
            new("Orchard", LegendItemType.PatternArea, MilitaryColors.Orchard, SymbolType.Orchard),
            new("Scrub", LegendItemType.PatternArea, MilitaryColors.VegetationLight, SymbolType.Scrub),

            // Terrain section
            new("TERRAIN", LegendItemType.Header),
            new("Index Contour", LegendItemType.ContourLine, MilitaryColors.ContourIndex),
            new("Intermediate Contour", LegendItemType.ContourLine, MilitaryColors.ContourIntermediate),
            new("Sand/Dunes", LegendItemType.PatternArea, MilitaryColors.SandDunes, SymbolType.SandDunes),

            // Structures section
            new("STRUCTURES", LegendItemType.Header),
            new("Building", LegendItemType.FilledArea, MilitaryColors.Building),
            new("Church", LegendItemType.PointSymbol, Symbol: SymbolType.Church),
            new("Cemetery", LegendItemType.PointSymbol, Symbol: SymbolType.Cemetery),
            new("Tower", LegendItemType.PointSymbol, Symbol: SymbolType.Tower),

            // Military section
            new("MILITARY", LegendItemType.Header),
            new("Landing Zone", LegendItemType.PointSymbol, Symbol: SymbolType.LandingZone),
            new("Drop Zone", LegendItemType.PointSymbol, Symbol: SymbolType.DropZone),
            new("Observation Post", LegendItemType.PointSymbol, Symbol: SymbolType.ObservationPost),
            new("Checkpoint", LegendItemType.PointSymbol, Symbol: SymbolType.CheckPoint),

            // Boundaries section
            new("BOUNDARIES", LegendItemType.Header),
            new("International", LegendItemType.DashedLine, MilitaryColors.InternationalBoundary, DashPattern: new[] { 12f, 4f, 4f, 4f }),
            new("State/Province", LegendItemType.DashedLine, MilitaryColors.StateBoundary, DashPattern: new[] { 8f, 4f }),

            // Other features section
            new("OTHER FEATURES", LegendItemType.Header),
            new("Power Line", LegendItemType.SolidLine, SKColors.Black),
            new("Fence", LegendItemType.SolidLine, SKColors.Black),
            new("Spring", LegendItemType.PointSymbol, Symbol: SymbolType.Spring),
            new("Well", LegendItemType.PointSymbol, Symbol: SymbolType.Well),
            new("Mine", LegendItemType.PointSymbol, Symbol: SymbolType.Mine)
        };
    }

    /// <summary>
    /// Renders the legend with the specified items.
    /// </summary>
    /// <param name="items">Legend items to render.</param>
    /// <returns>Bitmap containing the rendered legend.</returns>
    public SKBitmap Render(IReadOnlyList<LegendItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Calculate height based on number of items
        int contentHeight = CalculateContentHeight(items);
        int totalHeight = contentHeight + _options.Padding * 2;

        var bitmap = new SKBitmap(_options.Width, totalHeight);
        using var canvas = new SKCanvas(bitmap);

        // Draw background
        canvas.Clear(_options.BackgroundColor);

        // Draw border
        if (_options.DrawBorder)
        {
            using var borderPaint = new SKPaint
            {
                Color = _options.BorderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            canvas.DrawRect(0.5f, 0.5f, _options.Width - 1, totalHeight - 1, borderPaint);
        }

        float y = _options.Padding;

        // Draw title
        using var titlePaint = new SKPaint
        {
            Color = _options.TextColor,
            IsAntialias = true
        };
        using var titleFont = new SKFont(
            SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            _options.TitleFontSize);

        float titleWidth = titleFont.MeasureText(_options.Title, titlePaint);
        canvas.DrawText(_options.Title, (_options.Width - titleWidth) / 2, y + _options.TitleFontSize, titleFont, titlePaint);
        y += _options.TitleFontSize + _options.Padding;

        // Draw separator line under title
        using var linePaint = new SKPaint
        {
            Color = _options.BorderColor,
            StrokeWidth = 0.5f
        };
        canvas.DrawLine(_options.Padding, y, _options.Width - _options.Padding, y, linePaint);
        y += 8;

        // Draw each item
        foreach (var item in items)
        {
            y = DrawLegendItem(canvas, item, y);
        }

        return bitmap;
    }

    /// <summary>
    /// Renders the legend with default military map items.
    /// </summary>
    public SKBitmap RenderDefault()
    {
        return Render(CreateDefaultLegendItems());
    }

    /// <summary>
    /// Renders the legend and saves to a file.
    /// </summary>
    public void RenderToFile(IReadOnlyList<LegendItem> items, string outputPath)
    {
        using var bitmap = Render(items);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }

    private int CalculateContentHeight(IReadOnlyList<LegendItem> items)
    {
        int height = (int)_options.TitleFontSize + _options.Padding + 8; // Title + separator

        foreach (var item in items)
        {
            if (item.Type == LegendItemType.Header)
            {
                height += (int)_options.HeaderFontSize + 12; // Header with spacing
            }
            else
            {
                height += _options.RowHeight;
            }
        }

        return height;
    }

    private float DrawLegendItem(SKCanvas canvas, LegendItem item, float y)
    {
        float x = _options.Padding;
        float symbolCenterX = x + _options.SymbolWidth / 2f;
        float symbolCenterY = y + _options.RowHeight / 2f;
        float labelX = x + _options.SymbolWidth + 8;

        using var textPaint = new SKPaint
        {
            Color = _options.TextColor,
            IsAntialias = true
        };

        if (item.Type == LegendItemType.Header)
        {
            // Draw section header
            using var headerFont = new SKFont(
                SKTypeface.FromFamilyName(null, SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                _options.HeaderFontSize);

            y += 6; // Extra space before header
            canvas.DrawText(item.Label, x, y + _options.HeaderFontSize, headerFont, textPaint);
            return y + _options.HeaderFontSize + 6;
        }

        using var labelFont = new SKFont(SKTypeface.Default, _options.LabelFontSize);

        // Draw symbol based on type
        switch (item.Type)
        {
            case LegendItemType.PointSymbol:
                if (item.Symbol.HasValue)
                {
                    _symbologyRenderer.DrawSymbol(canvas, symbolCenterX, symbolCenterY, item.Symbol.Value);
                }
                break;

            case LegendItemType.SolidLine:
                DrawLineSample(canvas, x, symbolCenterY, _options.SymbolWidth, item.Color ?? SKColors.Black, null);
                break;

            case LegendItemType.DashedLine:
                DrawLineSample(canvas, x, symbolCenterY, _options.SymbolWidth, item.Color ?? SKColors.Black, item.DashPattern);
                break;

            case LegendItemType.ContourLine:
                DrawContourSample(canvas, x, symbolCenterY, _options.SymbolWidth, item.Color ?? MilitaryColors.ContourIndex);
                break;

            case LegendItemType.FilledArea:
                DrawAreaSample(canvas, x, symbolCenterY - 8, _options.SymbolWidth, 16, item.Color ?? SKColors.Gray, null);
                break;

            case LegendItemType.PatternArea:
                if (item.Symbol.HasValue)
                {
                    DrawPatternAreaSample(canvas, x, symbolCenterY - 8, _options.SymbolWidth, 16, item.Color ?? SKColors.Gray, item.Symbol.Value);
                }
                else
                {
                    DrawAreaSample(canvas, x, symbolCenterY - 8, _options.SymbolWidth, 16, item.Color ?? SKColors.Gray, null);
                }
                break;
        }

        // Draw label
        canvas.DrawText(item.Label, labelX, symbolCenterY + _options.LabelFontSize / 3, labelFont, textPaint);

        return y + _options.RowHeight;
    }

    private void DrawLineSample(SKCanvas canvas, float x, float y, float width, SKColor color, float[]? dashPattern)
    {
        using var paint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _options.LineWidth,
            IsAntialias = true
        };

        if (dashPattern != null)
        {
            paint.PathEffect = SKPathEffect.CreateDash(dashPattern, 0);
        }

        canvas.DrawLine(x + 2, y, x + width - 2, y, paint);
    }

    private void DrawContourSample(SKCanvas canvas, float x, float y, float width, SKColor color)
    {
        using var paint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = _options.LineWidth,
            IsAntialias = true
        };

        // Draw a wavy line to represent contour
        using var path = new SKPath();
        path.MoveTo(x + 2, y);
        float segmentWidth = (width - 4) / 3;
        path.CubicTo(
            x + 2 + segmentWidth * 0.5f, y - 4,
            x + 2 + segmentWidth * 1.5f, y + 4,
            x + 2 + segmentWidth * 2, y);
        path.CubicTo(
            x + 2 + segmentWidth * 2.5f, y - 3,
            x + width - 4, y + 2,
            x + width - 2, y);

        canvas.DrawPath(path, paint);
    }

    private void DrawAreaSample(SKCanvas canvas, float x, float y, float width, float height, SKColor color, SKBitmap? pattern)
    {
        using var paint = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var rect = new SKRect(x + 2, y, x + width - 2, y + height);
        canvas.DrawRect(rect, paint);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f
        };
        canvas.DrawRect(rect, borderPaint);
    }

    private void DrawPatternAreaSample(SKCanvas canvas, float x, float y, float width, float height, SKColor baseColor, SymbolType patternType)
    {
        // Draw base color
        DrawAreaSample(canvas, x, y, width, height, baseColor, null);

        // Overlay pattern
        using var patternTile = _symbologyRenderer.CreatePatternTile(patternType, 16);
        if (patternTile != null)
        {
            using var shader = SKShader.CreateBitmap(patternTile, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
            using var patternPaint = new SKPaint
            {
                Shader = shader,
                IsAntialias = true
            };

            var rect = new SKRect(x + 2, y, x + width - 2, y + height);
            canvas.DrawRect(rect, patternPaint);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _symbologyRenderer.Dispose();
            _disposed = true;
        }
    }
}
