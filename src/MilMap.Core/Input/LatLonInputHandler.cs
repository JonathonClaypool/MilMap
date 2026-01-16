using System;
using System.Text.RegularExpressions;
using MilMap.Core.Mgrs;

namespace MilMap.Core.Input;

/// <summary>
/// Result of parsing lat/lon input into a bounding box.
/// </summary>
public record LatLonInputResult(
    BoundingBox BoundingBox,
    double CenterLat,
    double CenterLon,
    string OriginalInput);

/// <summary>
/// Handles decimal degree lat/lon coordinate input and converts to bounding boxes.
/// </summary>
public static class LatLonInputHandler
{
    /// <summary>
    /// Creates a bounding box from explicit corner coordinates.
    /// </summary>
    /// <param name="minLat">Minimum latitude (south)</param>
    /// <param name="maxLat">Maximum latitude (north)</param>
    /// <param name="minLon">Minimum longitude (west)</param>
    /// <param name="maxLon">Maximum longitude (east)</param>
    /// <returns>Bounding box and center point</returns>
    public static LatLonInputResult FromBounds(double minLat, double maxLat, double minLon, double maxLon)
    {
        ValidateLatitude(minLat);
        ValidateLatitude(maxLat);
        ValidateLongitude(minLon);
        ValidateLongitude(maxLon);

        if (minLat >= maxLat)
            throw new ArgumentException("minLat must be less than maxLat");
        if (minLon >= maxLon)
            throw new ArgumentException("minLon must be less than maxLon");

        var box = new BoundingBox(minLat, maxLat, minLon, maxLon);
        double centerLat = box.CenterLat;
        double centerLon = box.CenterLon;

        string input = $"{minLat:F6},{minLon:F6} to {maxLat:F6},{maxLon:F6}";
        return new LatLonInputResult(box, centerLat, centerLon, input);
    }

    /// <summary>
    /// Creates a bounding box centered on a point with a radius.
    /// </summary>
    /// <param name="lat">Center latitude</param>
    /// <param name="lon">Center longitude</param>
    /// <param name="radius">Radius around the point</param>
    /// <param name="unit">Distance unit for radius</param>
    /// <returns>Bounding box centered on the point</returns>
    public static LatLonInputResult FromCenterRadius(double lat, double lon, double radius, DistanceUnit unit = DistanceUnit.Kilometers)
    {
        ValidateLatitude(lat);
        ValidateLongitude(lon);

        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive");

        double radiusMeters = ConvertToMeters(radius, unit);

        // Convert radius to approximate degrees
        double latRadiusDeg = radiusMeters / 111320.0;
        double lonRadiusDeg = radiusMeters / (111320.0 * Math.Cos(lat * Math.PI / 180));

        var box = new BoundingBox(
            lat - latRadiusDeg,
            lat + latRadiusDeg,
            lon - lonRadiusDeg,
            lon + lonRadiusDeg);

        string input = $"{lat:F6},{lon:F6} radius {radius}{GetUnitSuffix(unit)}";
        return new LatLonInputResult(box, lat, lon, input);
    }

    /// <summary>
    /// Parses a bounding box string in various formats.
    /// </summary>
    /// <param name="input">
    /// Supported formats:
    /// - "minLat,minLon,maxLat,maxLon" (4 numbers comma-separated)
    /// - "minLat minLon maxLat maxLon" (4 numbers space-separated)
    /// - "sw:lat,lon ne:lat,lon"
    /// </param>
    /// <returns>Bounding box for the input</returns>
    public static LatLonInputResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be empty", nameof(input));

        input = input.Trim();

        // Try comma-separated: minLat,minLon,maxLat,maxLon
        var commaMatch = Regex.Match(input, @"^(-?\d+\.?\d*)\s*,\s*(-?\d+\.?\d*)\s*,\s*(-?\d+\.?\d*)\s*,\s*(-?\d+\.?\d*)$");
        if (commaMatch.Success)
        {
            double minLat = double.Parse(commaMatch.Groups[1].Value);
            double minLon = double.Parse(commaMatch.Groups[2].Value);
            double maxLat = double.Parse(commaMatch.Groups[3].Value);
            double maxLon = double.Parse(commaMatch.Groups[4].Value);
            return FromBounds(minLat, maxLat, minLon, maxLon);
        }

        // Try space-separated: minLat minLon maxLat maxLon
        var spaceMatch = Regex.Match(input, @"^(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)\s+(-?\d+\.?\d*)$");
        if (spaceMatch.Success)
        {
            double minLat = double.Parse(spaceMatch.Groups[1].Value);
            double minLon = double.Parse(spaceMatch.Groups[2].Value);
            double maxLat = double.Parse(spaceMatch.Groups[3].Value);
            double maxLon = double.Parse(spaceMatch.Groups[4].Value);
            return FromBounds(minLat, maxLat, minLon, maxLon);
        }

        throw new ArgumentException($"Unable to parse bounding box: {input}. Expected format: minLat,minLon,maxLat,maxLon", nameof(input));
    }

    /// <summary>
    /// Parses a coordinate string (lat,lon or lat lon).
    /// </summary>
    public static (double lat, double lon) ParseCoordinate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be empty", nameof(input));

        input = input.Trim();

        var match = Regex.Match(input, @"^(-?\d+\.?\d*)\s*[,\s]\s*(-?\d+\.?\d*)$");
        if (!match.Success)
            throw new ArgumentException($"Unable to parse coordinate: {input}. Expected format: lat,lon", nameof(input));

        double lat = double.Parse(match.Groups[1].Value);
        double lon = double.Parse(match.Groups[2].Value);

        ValidateLatitude(lat);
        ValidateLongitude(lon);

        return (lat, lon);
    }

    /// <summary>
    /// Validates a latitude value.
    /// </summary>
    public static void ValidateLatitude(double lat)
    {
        if (lat < -90 || lat > 90)
            throw new ArgumentOutOfRangeException(nameof(lat), lat, "Latitude must be between -90 and 90");
    }

    /// <summary>
    /// Validates a longitude value.
    /// </summary>
    public static void ValidateLongitude(double lon)
    {
        if (lon < -180 || lon > 180)
            throw new ArgumentOutOfRangeException(nameof(lon), lon, "Longitude must be between -180 and 180");
    }

    private static double ConvertToMeters(double value, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Meters => value,
            DistanceUnit.Kilometers => value * 1000,
            DistanceUnit.Miles => value * 1609.344,
            DistanceUnit.Feet => value * 0.3048,
            _ => value * 1000
        };
    }

    private static string GetUnitSuffix(DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Meters => "m",
            DistanceUnit.Kilometers => "km",
            DistanceUnit.Miles => "mi",
            DistanceUnit.Feet => "ft",
            _ => "km"
        };
    }
}
