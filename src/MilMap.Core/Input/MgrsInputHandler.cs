using System;
using System.Text.RegularExpressions;
using MilMap.Core.Mgrs;

namespace MilMap.Core.Input;

/// <summary>
/// Distance unit for radius specifications.
/// </summary>
public enum DistanceUnit
{
    Meters,
    Kilometers,
    Miles,
    Feet
}

/// <summary>
/// Result of parsing MGRS input into a bounding box.
/// </summary>
public record MgrsInputResult(
    BoundingBox BoundingBox,
    double CenterLat,
    double CenterLon,
    string OriginalInput);

/// <summary>
/// Handles MGRS coordinate input and converts to bounding boxes.
/// </summary>
public static class MgrsInputHandler
{
    private static readonly Regex MgrsPattern = new(
        @"^([1-9]|[1-5][0-9]|60)?([A-HJ-NP-Z])([A-HJ-NP-Z]{2})?(\d{2,10})?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Creates a bounding box from a single MGRS coordinate with a radius.
    /// </summary>
    /// <param name="mgrs">MGRS coordinate string</param>
    /// <param name="radius">Radius around the point</param>
    /// <param name="unit">Distance unit for radius</param>
    /// <returns>Bounding box centered on the MGRS coordinate</returns>
    public static MgrsInputResult FromMgrsWithRadius(string mgrs, double radius, DistanceUnit unit = DistanceUnit.Kilometers)
    {
        ValidateMgrs(mgrs);

        var (lat, lon) = MgrsParser.Parse(mgrs);
        double radiusMeters = ConvertToMeters(radius, unit);

        // Convert radius to approximate degrees
        double latRadiusDeg = radiusMeters / 111320.0;
        double lonRadiusDeg = radiusMeters / (111320.0 * Math.Cos(lat * Math.PI / 180));

        var box = new BoundingBox(
            lat - latRadiusDeg,
            lat + latRadiusDeg,
            lon - lonRadiusDeg,
            lon + lonRadiusDeg);

        return new MgrsInputResult(box, lat, lon, mgrs);
    }

    /// <summary>
    /// Creates a bounding box from two MGRS coordinates (SW and NE corners).
    /// </summary>
    /// <param name="mgrsSw">Southwest corner MGRS coordinate</param>
    /// <param name="mgrsNe">Northeast corner MGRS coordinate</param>
    /// <returns>Bounding box defined by the two corners</returns>
    public static MgrsInputResult FromMgrsCorners(string mgrsSw, string mgrsNe)
    {
        ValidateMgrs(mgrsSw);
        ValidateMgrs(mgrsNe);

        var (latSw, lonSw) = MgrsParser.Parse(mgrsSw);
        var (latNe, lonNe) = MgrsParser.Parse(mgrsNe);

        if (latSw >= latNe)
            throw new ArgumentException("Southwest latitude must be less than northeast latitude");
        if (lonSw >= lonNe)
            throw new ArgumentException("Southwest longitude must be less than northeast longitude");

        var box = new BoundingBox(latSw, latNe, lonSw, lonNe);
        double centerLat = (latSw + latNe) / 2;
        double centerLon = (lonSw + lonNe) / 2;

        return new MgrsInputResult(box, centerLat, centerLon, $"{mgrsSw} to {mgrsNe}");
    }

    /// <summary>
    /// Creates a bounding box from an MGRS grid square identifier (e.g., "18TXM").
    /// </summary>
    /// <param name="mgrs">MGRS grid square (zone + band + optional 100km square)</param>
    /// <returns>Bounding box covering the grid square</returns>
    public static MgrsInputResult FromMgrsGridSquare(string mgrs)
    {
        ValidateMgrs(mgrs);

        var box = MgrsBoundary.GetBounds(mgrs);
        double centerLat = box.CenterLat;
        double centerLon = box.CenterLon;

        return new MgrsInputResult(box, centerLat, centerLon, mgrs);
    }

    /// <summary>
    /// Parses an MGRS input string and returns a bounding box.
    /// Automatically detects the input format.
    /// </summary>
    /// <param name="input">MGRS input (single coordinate, grid square, or with radius)</param>
    /// <param name="defaultRadiusKm">Default radius in km if a single point is given</param>
    /// <returns>Bounding box for the input</returns>
    public static MgrsInputResult Parse(string input, double defaultRadiusKm = 5.0)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be empty", nameof(input));

        input = input.Trim().ToUpperInvariant();

        // Check if input is a partial MGRS (zone + band or zone + band + 100km square)
        // These should use the boundary calculator, not center point + radius
        if (IsPartialMgrs(input))
        {
            return FromMgrsGridSquare(input);
        }

        // Full MGRS coordinate - use center point with default radius
        ValidateMgrs(input);
        return FromMgrsWithRadius(input, defaultRadiusKm, DistanceUnit.Kilometers);
    }

    /// <summary>
    /// Validates an MGRS string format.
    /// </summary>
    /// <param name="mgrs">MGRS string to validate</param>
    /// <exception cref="ArgumentException">If the MGRS format is invalid</exception>
    public static void ValidateMgrs(string mgrs)
    {
        if (string.IsNullOrWhiteSpace(mgrs))
            throw new ArgumentException("MGRS string cannot be empty", nameof(mgrs));

        string cleaned = mgrs.ToUpperInvariant().Replace(" ", "");

        // Basic format validation
        if (cleaned.Length < 2)
            throw new ArgumentException("MGRS must have at least zone and band", nameof(mgrs));

        // Check for valid characters
        foreach (char c in cleaned)
        {
            if (!char.IsLetterOrDigit(c))
                throw new ArgumentException($"Invalid character in MGRS: {c}", nameof(mgrs));
        }

        // More detailed validation is done by the parser
        try
        {
            MgrsBoundary.GetBounds(cleaned);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid MGRS format: {ex.Message}", nameof(mgrs));
        }
    }

    /// <summary>
    /// Checks if the input is a valid MGRS format without throwing.
    /// </summary>
    public static bool IsValidMgrs(string mgrs)
    {
        try
        {
            ValidateMgrs(mgrs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a radius string like "5km", "3mi", "1000m".
    /// </summary>
    public static (double value, DistanceUnit unit) ParseRadius(string radiusStr)
    {
        if (string.IsNullOrWhiteSpace(radiusStr))
            throw new ArgumentException("Radius string cannot be empty", nameof(radiusStr));

        radiusStr = radiusStr.Trim().ToLowerInvariant();

        var match = Regex.Match(radiusStr, @"^([\d.]+)\s*(km|m|mi|ft|miles?|meters?|feet|kilometers?)?$");
        if (!match.Success)
            throw new ArgumentException($"Invalid radius format: {radiusStr}", nameof(radiusStr));

        double value = double.Parse(match.Groups[1].Value);
        string unitStr = match.Groups[2].Value;

        DistanceUnit unit = unitStr switch
        {
            "km" or "kilometer" or "kilometers" => DistanceUnit.Kilometers,
            "m" or "meter" or "meters" => DistanceUnit.Meters,
            "mi" or "mile" or "miles" => DistanceUnit.Miles,
            "ft" or "feet" => DistanceUnit.Feet,
            "" => DistanceUnit.Kilometers, // Default to km
            _ => DistanceUnit.Kilometers
        };

        return (value, unit);
    }

    private static bool IsPartialMgrs(string mgrs)
    {
        mgrs = mgrs.Replace(" ", "");

        // Partial MGRS: just zone+band (e.g., "18T") or zone+band+100km square (e.g., "18TXM")
        // Full MGRS includes numeric grid coordinates (e.g., "18TXM8546")

        // Check if it ends with letters only (no grid digits)
        if (mgrs.Length < 2)
            return false;

        // Find where the zone number ends
        int zoneEnd = 0;
        if (char.IsDigit(mgrs[0]))
        {
            zoneEnd = 1;
            if (mgrs.Length > 1 && char.IsDigit(mgrs[1]))
                zoneEnd = 2;
        }

        // Check the rest - if it's all letters, it's partial
        for (int i = zoneEnd; i < mgrs.Length; i++)
        {
            if (char.IsDigit(mgrs[i]))
                return false; // Has grid digits, not partial
        }

        return true;
    }

    private static double ConvertToMeters(double value, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Meters => value,
            DistanceUnit.Kilometers => value * 1000,
            DistanceUnit.Miles => value * 1609.344,
            DistanceUnit.Feet => value * 0.3048,
            _ => value * 1000 // Default to km
        };
    }
}
