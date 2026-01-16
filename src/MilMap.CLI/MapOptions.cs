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
public record MapOptions
{
    /// <summary>
    /// Required: Output file path for the generated map.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// MGRS coordinate or grid reference (mutually exclusive with Bounds and Installation).
    /// </summary>
    public string? Mgrs { get; init; }

    /// <summary>
    /// Lat/lon bounding box as "minLat,minLon,maxLat,maxLon" (mutually exclusive with Mgrs and Installation).
    /// </summary>
    public string? Bounds { get; init; }

    /// <summary>
    /// Military installation name to look up (mutually exclusive with Mgrs and Bounds).
    /// </summary>
    public string? Installation { get; init; }

    /// <summary>
    /// Map scale denominator (e.g., 25000 for 1:25000). Default: 25000.
    /// </summary>
    public int Scale { get; init; } = 25000;

    /// <summary>
    /// Output resolution in dots per inch. Default: 300.
    /// </summary>
    public int Dpi { get; init; } = 300;

    /// <summary>
    /// Output format (pdf, png, geotiff). Default: Pdf.
    /// </summary>
    public OutputFormat Format { get; init; } = OutputFormat.Pdf;

    /// <summary>
    /// Directory for caching downloaded tiles. Default: system temp directory.
    /// </summary>
    public string? CacheDir { get; init; }

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
