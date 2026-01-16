using System;

namespace MilMap.Core.Tiles;

/// <summary>
/// Result of zoom level calculation.
/// </summary>
public record ZoomCalculationResult(
    int Zoom,
    double MetersPerPixel,
    double ActualScale,
    bool IsApproximate,
    string? Warning);

/// <summary>
/// Calculates the appropriate OSM zoom level for a target map scale and DPI.
/// </summary>
public static class ZoomLevelCalculator
{
    /// <summary>
    /// Standard tile size in pixels for OSM tiles.
    /// </summary>
    public const int TileSize = 256;

    /// <summary>
    /// Earth's equatorial circumference in meters.
    /// </summary>
    public const double EarthCircumference = 40075016.686;

    /// <summary>
    /// Minimum supported OSM zoom level.
    /// </summary>
    public const int MinZoom = 0;

    /// <summary>
    /// Maximum supported OSM zoom level.
    /// </summary>
    public const int MaxZoom = 18;

    /// <summary>
    /// Inches per meter for DPI calculations.
    /// </summary>
    private const double InchesPerMeter = 39.3701;

    /// <summary>
    /// Calculates the required OSM zoom level for a target map scale and output DPI.
    /// </summary>
    /// <param name="scale">Target map scale denominator (e.g., 25000 for 1:25000)</param>
    /// <param name="dpi">Output resolution in dots per inch</param>
    /// <param name="latitude">Latitude for scale calculation (affects tile size)</param>
    /// <returns>Calculation result with zoom level and metadata</returns>
    public static ZoomCalculationResult CalculateZoom(int scale, int dpi, double latitude = 0)
    {
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be positive");
        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be positive");
        if (latitude < -85.0511 || latitude > 85.0511)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -85.0511 and 85.0511");

        // Formula: meters_per_pixel = (scale * 0.0254) / DPI
        // 0.0254 meters = 1 inch, so this converts to ground meters per output pixel
        double targetMetersPerPixel = (scale * 0.0254) / dpi;

        // Find the best zoom level
        int bestZoom = 0;
        double bestMetersPerPixel = GetMetersPerPixel(0, latitude);
        double smallestDiff = Math.Abs(bestMetersPerPixel - targetMetersPerPixel);

        for (int z = 1; z <= MaxZoom; z++)
        {
            double metersPerPixel = GetMetersPerPixel(z, latitude);
            double diff = Math.Abs(metersPerPixel - targetMetersPerPixel);

            if (diff < smallestDiff)
            {
                smallestDiff = diff;
                bestZoom = z;
                bestMetersPerPixel = metersPerPixel;
            }
        }

        // Check if we need higher zoom than available
        string? warning = null;
        double minMetersPerPixel = GetMetersPerPixel(MaxZoom, latitude);
        if (targetMetersPerPixel < minMetersPerPixel)
        {
            warning = $"Requested scale 1:{scale} at {dpi} DPI requires higher resolution than available. " +
                      $"Maximum zoom level {MaxZoom} provides approximately 1:{CalculateActualScale(minMetersPerPixel, dpi):F0} scale.";
            bestZoom = MaxZoom;
            bestMetersPerPixel = minMetersPerPixel;
        }

        // Calculate the actual achieved scale at this zoom level
        double actualScale = CalculateActualScale(bestMetersPerPixel, dpi);
        bool isApproximate = Math.Abs(actualScale - scale) / scale > 0.05; // More than 5% difference

        return new ZoomCalculationResult(
            bestZoom,
            bestMetersPerPixel,
            actualScale,
            isApproximate,
            warning);
    }

    /// <summary>
    /// Gets the ground resolution (meters per pixel) at a given zoom level and latitude.
    /// </summary>
    /// <param name="zoom">OSM zoom level (0-18)</param>
    /// <param name="latitude">Latitude in degrees</param>
    /// <returns>Meters per pixel</returns>
    public static double GetMetersPerPixel(int zoom, double latitude = 0)
    {
        if (zoom < MinZoom || zoom > MaxZoom)
            throw new ArgumentOutOfRangeException(nameof(zoom), $"Zoom must be between {MinZoom} and {MaxZoom}");

        // At the equator, each zoom level halves the meters per pixel
        double metersPerPixelAtEquator = EarthCircumference / (TileSize * Math.Pow(2, zoom));

        // Adjust for latitude (tiles appear to cover less ground near the poles)
        double latitudeRadians = latitude * Math.PI / 180.0;
        return metersPerPixelAtEquator * Math.Cos(latitudeRadians);
    }

    /// <summary>
    /// Calculates the actual map scale for a given ground resolution and DPI.
    /// </summary>
    /// <param name="metersPerPixel">Ground resolution in meters per pixel</param>
    /// <param name="dpi">Output resolution in dots per inch</param>
    /// <returns>Scale denominator (e.g., 25000 for 1:25000)</returns>
    public static double CalculateActualScale(double metersPerPixel, int dpi)
    {
        // metersPerPixel = (scale * 0.0254) / dpi
        // Solving for scale: scale = (metersPerPixel * dpi) / 0.0254
        return (metersPerPixel * dpi) / 0.0254;
    }

    /// <summary>
    /// Gets the ground resolution table for all zoom levels at a given latitude.
    /// </summary>
    /// <param name="latitude">Latitude in degrees</param>
    /// <returns>Array of meters per pixel for each zoom level (0-18)</returns>
    public static double[] GetResolutionTable(double latitude = 0)
    {
        var table = new double[MaxZoom + 1];
        for (int z = 0; z <= MaxZoom; z++)
        {
            table[z] = GetMetersPerPixel(z, latitude);
        }
        return table;
    }

    /// <summary>
    /// Recommends a zoom level for common military map scales.
    /// </summary>
    /// <param name="scale">Target scale denominator</param>
    /// <param name="dpi">Output DPI (default 300)</param>
    /// <param name="latitude">Latitude (default 0 for equator)</param>
    /// <returns>Recommended zoom level</returns>
    public static int RecommendZoom(int scale, int dpi = 300, double latitude = 0)
    {
        return CalculateZoom(scale, dpi, latitude).Zoom;
    }
}
