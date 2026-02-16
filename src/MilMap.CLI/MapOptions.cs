namespace MilMap.CLI;

/// <summary>
/// Output format for the generated map.
/// </summary>
public enum OutputFormat
{
    Pdf,
    Png,
    GeoTiff
}

/// <summary>
/// Options for map generation parsed from command-line arguments.
/// </summary>
public class MapOptions
{
    /// <summary>
    /// Required: Output file path for the generated map.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// MGRS coordinate or grid reference (mutually exclusive with Bounds and Installation).
    /// </summary>
    public string? Mgrs { get; set; }

    /// <summary>
    /// Lat/lon bounding box as "minLat,minLon,maxLat,maxLon" (mutually exclusive with Mgrs and Installation).
    /// </summary>
    public string? Bounds { get; set; }

    /// <summary>
    /// Military installation name to look up (mutually exclusive with Mgrs and Bounds).
    /// </summary>
    public string? Installation { get; set; }

    /// <summary>
    /// Map scale denominator (e.g., 25000 for 1:25000). Default: 25000.
    /// </summary>
    public int Scale { get; set; } = 25000;

    /// <summary>
    /// Output resolution in dots per inch. Default: 300.
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// Output format (pdf, png, geotiff). Default: Pdf.
    /// </summary>
    public OutputFormat Format { get; set; } = OutputFormat.Pdf;

    /// <summary>
    /// Directory for caching downloaded tiles. Default: system temp directory.
    /// </summary>
    public string? CacheDir { get; set; }

    /// <summary>
    /// Force multi-page PDF output. If null, auto-detects based on map size.
    /// </summary>
    public bool? MultiPage { get; set; }

    /// <summary>
    /// Page size for PDF output (letter, legal, tabloid, a4, a3).
    /// </summary>
    public string? PageSize { get; set; }

    /// <summary>
    /// Page orientation for PDF output (portrait, landscape).
    /// </summary>
    public string? Orientation { get; set; }

    /// <summary>
    /// Show military range and impact area overlays with labels.
    /// </summary>
    public bool ShowRangeOverlay { get; set; }

    /// <summary>
    /// Gets the region input type that was specified.
    /// </summary>
    public RegionInputType GetRegionInputType()
    {
        if (!string.IsNullOrEmpty(Mgrs)) return RegionInputType.Mgrs;
        if (!string.IsNullOrEmpty(Bounds)) return RegionInputType.Bounds;
        if (!string.IsNullOrEmpty(Installation)) return RegionInputType.Installation;
        return RegionInputType.None;
    }
}

/// <summary>
/// Type of region input specified.
/// </summary>
public enum RegionInputType
{
    None,
    Mgrs,
    Bounds,
    Installation
}
