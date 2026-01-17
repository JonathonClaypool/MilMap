using System;
using MilMap.Core.Mgrs;
using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Options for map margin and metadata rendering.
/// </summary>
public class MapMarginOptions
{
    /// <summary>
    /// Map title (e.g., geographic area name).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Subtitle or secondary title.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Map scale ratio (e.g., 25000 for 1:25,000).
    /// </summary>
    public int ScaleRatio { get; set; } = 25000;

    /// <summary>
    /// MGRS grid zone designator (e.g., "18T").
    /// </summary>
    public string? MgrsZone { get; set; }

    /// <summary>
    /// Center latitude for declination calculations.
    /// </summary>
    public double CenterLat { get; set; }

    /// <summary>
    /// Center longitude for declination calculations.
    /// </summary>
    public double CenterLon { get; set; }

    /// <summary>
    /// Output DPI for scale bar.
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// Top margin height in pixels.
    /// </summary>
    public int TopMarginHeight { get; set; } = 80;

    /// <summary>
    /// Bottom margin height in pixels.
    /// </summary>
    public int BottomMarginHeight { get; set; } = 120;

    /// <summary>
    /// Left margin width in pixels.
    /// </summary>
    public int LeftMarginWidth { get; set; } = 40;

    /// <summary>
    /// Right margin width in pixels.
    /// </summary>
    public int RightMarginWidth { get; set; } = 40;

    /// <summary>
    /// Background color for margins.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.White;

    /// <summary>
    /// Text color.
    /// </summary>
    public SKColor TextColor { get; set; } = SKColors.Black;

    /// <summary>
    /// Title font size.
    /// </summary>
    public float TitleFontSize { get; set; } = 24f;

    /// <summary>
    /// Subtitle font size.
    /// </summary>
    public float SubtitleFontSize { get; set; } = 14f;

    /// <summary>
    /// Metadata font size.
    /// </summary>
    public float MetadataFontSize { get; set; } = 10f;

    /// <summary>
    /// Whether to include declination diagram.
    /// </summary>
    public bool ShowDeclination { get; set; } = true;

    /// <summary>
    /// Whether to include scale bar.
    /// </summary>
    public bool ShowScaleBar { get; set; } = true;

    /// <summary>
    /// Whether to show datum information.
    /// </summary>
    public bool ShowDatum { get; set; } = true;

    /// <summary>
    /// Whether to show generation date.
    /// </summary>
    public bool ShowDate { get; set; } = true;

    /// <summary>
    /// Custom date to display. If null, uses current date.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Custom notes or additional text to display.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Renders map margins with title, metadata, scale bar, and declination diagram.
/// </summary>
public class MapMarginRenderer
{
    private readonly MapMarginOptions _options;

    public MapMarginRenderer() : this(new MapMarginOptions()) { }

    public MapMarginRenderer(MapMarginOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Adds margins and metadata to a map bitmap.
    /// </summary>
    /// <param name="mapImage">The map image without margins</param>
    /// <returns>Map image with margins and metadata</returns>
    public SKBitmap AddMargins(SKBitmap mapImage)
    {
        if (mapImage == null)
            throw new ArgumentNullException(nameof(mapImage));

        int totalWidth = mapImage.Width + _options.LeftMarginWidth + _options.RightMarginWidth;
        int totalHeight = mapImage.Height + _options.TopMarginHeight + _options.BottomMarginHeight;

        var result = new SKBitmap(totalWidth, totalHeight);
        using var canvas = new SKCanvas(result);

        // Fill background
        canvas.Clear(_options.BackgroundColor);

        // Draw the map image in the center
        canvas.DrawBitmap(mapImage, _options.LeftMarginWidth, _options.TopMarginHeight);

        // Draw border around map
        using var borderPaint = new SKPaint
        {
            Color = _options.TextColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRect(
            _options.LeftMarginWidth - 1,
            _options.TopMarginHeight - 1,
            mapImage.Width + 2,
            mapImage.Height + 2,
            borderPaint);

        // Render top margin (title area)
        RenderTopMargin(canvas, totalWidth);

        // Render bottom margin (metadata area)
        RenderBottomMargin(canvas, totalWidth, totalHeight, mapImage.Width);

        return result;
    }

    private void RenderTopMargin(SKCanvas canvas, int totalWidth)
    {
        using var titlePaint = new SKPaint
        {
            Color = _options.TextColor,
            TextSize = _options.TitleFontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        using var subtitlePaint = new SKPaint
        {
            Color = _options.TextColor,
            TextSize = _options.SubtitleFontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        float centerX = totalWidth / 2f;
        float y = 30;

        // Draw title
        if (!string.IsNullOrEmpty(_options.Title))
        {
            canvas.DrawText(_options.Title, centerX, y, titlePaint);
            y += _options.TitleFontSize + 5;
        }

        // Draw subtitle
        if (!string.IsNullOrEmpty(_options.Subtitle))
        {
            canvas.DrawText(_options.Subtitle, centerX, y, subtitlePaint);
            y += _options.SubtitleFontSize + 5;
        }

        // Draw MGRS zone designator if provided
        if (!string.IsNullOrEmpty(_options.MgrsZone))
        {
            canvas.DrawText($"MGRS Zone: {_options.MgrsZone}", centerX, y, subtitlePaint);
        }
    }

    private void RenderBottomMargin(SKCanvas canvas, int totalWidth, int totalHeight, int mapWidth)
    {
        using var metadataPaint = new SKPaint
        {
            Color = _options.TextColor,
            TextSize = _options.MetadataFontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Left
        };

        float bottomAreaTop = totalHeight - _options.BottomMarginHeight + 10;
        float leftX = _options.LeftMarginWidth + 10;
        float rightSectionX = totalWidth / 2f;

        // Left section: Scale bar
        if (_options.ShowScaleBar)
        {
            var scaleBarRenderer = new ScaleBarRenderer(new ScaleBarOptions
            {
                ScaleRatio = _options.ScaleRatio,
                Dpi = _options.Dpi,
                ShowMetric = true,
                ShowImperial = true
            });
            
            var scaleBarResult = scaleBarRenderer.Render();
            canvas.DrawBitmap(scaleBarResult.Bitmap, leftX, bottomAreaTop);
            
            // Scale text below the bar
            float scaleY = bottomAreaTop + scaleBarResult.Bitmap.Height + 15;
            canvas.DrawText($"Scale 1:{_options.ScaleRatio:N0}", leftX, scaleY, metadataPaint);
            
            scaleBarResult.Bitmap.Dispose();
        }

        // Right section: Declination diagram
        if (_options.ShowDeclination)
        {
            var declinationRenderer = new MagneticDeclinationRenderer(new DeclinationOptions
            {
                Width = 80,
                Height = 100,
                ShowGridNorth = true,
                ShowAnnualChange = true
            });
            
            var decResult = declinationRenderer.Render(_options.CenterLat, _options.CenterLon);
            float decX = totalWidth - _options.RightMarginWidth - decResult.Diagram.Width - 10;
            canvas.DrawBitmap(decResult.Diagram, decX, bottomAreaTop);
            
            decResult.Diagram.Dispose();
        }

        // Center section: Metadata text
        float metadataY = bottomAreaTop + 60;
        using var centerPaint = new SKPaint
        {
            Color = _options.TextColor,
            TextSize = _options.MetadataFontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        float metadataCenterX = totalWidth / 2f;

        if (_options.ShowDatum)
        {
            canvas.DrawText("Datum: WGS84", metadataCenterX, metadataY, centerPaint);
            metadataY += _options.MetadataFontSize + 4;
        }

        // Grid zone designation
        if (!string.IsNullOrEmpty(_options.MgrsZone))
        {
            string utmZone = GetUtmZoneFromMgrs(_options.MgrsZone);
            if (!string.IsNullOrEmpty(utmZone))
            {
                canvas.DrawText($"UTM Zone {utmZone}", metadataCenterX, metadataY, centerPaint);
                metadataY += _options.MetadataFontSize + 4;
            }
        }

        if (_options.ShowDate)
        {
            var date = _options.Date ?? DateTime.UtcNow;
            canvas.DrawText($"Generated: {date:yyyy-MM-dd}", metadataCenterX, metadataY, centerPaint);
            metadataY += _options.MetadataFontSize + 4;
        }

        if (!string.IsNullOrEmpty(_options.Notes))
        {
            canvas.DrawText(_options.Notes, metadataCenterX, metadataY, centerPaint);
        }
    }

    private static string GetUtmZoneFromMgrs(string mgrs)
    {
        if (string.IsNullOrEmpty(mgrs))
            return string.Empty;

        mgrs = mgrs.ToUpperInvariant().Replace(" ", "");

        // Extract zone number (1-2 digits at start)
        int zoneEndIndex = 1;
        if (mgrs.Length > 1 && char.IsDigit(mgrs[1]))
            zoneEndIndex = 2;

        if (int.TryParse(mgrs[..zoneEndIndex], out int zone))
        {
            // Include latitude band if present
            if (mgrs.Length > zoneEndIndex && char.IsLetter(mgrs[zoneEndIndex]))
            {
                return $"{zone}{mgrs[zoneEndIndex]}";
            }
            return zone.ToString();
        }

        return string.Empty;
    }

    /// <summary>
    /// Creates a complete map sheet with margins.
    /// </summary>
    /// <param name="mapImage">The rendered map content</param>
    /// <param name="bounds">The geographic bounds of the map</param>
    /// <returns>Complete map sheet with margins and metadata</returns>
    public SKBitmap CreateMapSheet(SKBitmap mapImage, BoundingBox bounds)
    {
        // Determine MGRS zone from bounds center
        double centerLat = bounds.CenterLat;
        double centerLon = bounds.CenterLon;
        
        int zone = (int)((centerLon + 180) / 6) + 1;
        char band = GetLatitudeBand(centerLat);
        
        _options.MgrsZone = $"{zone}{band}";
        _options.CenterLat = centerLat;
        _options.CenterLon = centerLon;

        return AddMargins(mapImage);
    }

    private static char GetLatitudeBand(double lat)
    {
        const string latBands = "CDEFGHJKLMNPQRSTUVWX";
        
        if (lat >= 84.0) return 'X';
        if (lat < -80.0) return 'C';
        
        int bandIndex = (int)((lat + 80) / 8);
        if (bandIndex >= latBands.Length) bandIndex = latBands.Length - 1;
        if (bandIndex < 0) bandIndex = 0;
        return latBands[bandIndex];
    }
}
