using System;

namespace MilMap.Core.Mgrs;

/// <summary>
/// Represents a geographic bounding box.
/// </summary>
public readonly record struct BoundingBox(double MinLat, double MaxLat, double MinLon, double MaxLon)
{
    public double Width => MaxLon - MinLon;
    public double Height => MaxLat - MinLat;
    public double CenterLat => (MinLat + MaxLat) / 2;
    public double CenterLon => (MinLon + MaxLon) / 2;
}

/// <summary>
/// Calculates bounding boxes for MGRS grid references.
/// </summary>
public static class MgrsBoundary
{
    private const string LatBands = "CDEFGHJKLMNPQRSTUVWX";
    private const string ColumnLettersSet1 = "ABCDEFGH";
    private const string ColumnLettersSet2 = "JKLMNPQR";
    private const string ColumnLettersSet3 = "STUVWXYZ";
    
    /// <summary>
    /// Gets the bounding box for a partial or complete MGRS reference.
    /// </summary>
    /// <param name="mgrs">MGRS string (e.g., "18T", "18TXM", "18TXM1234")</param>
    /// <returns>Bounding box covering the MGRS reference area</returns>
    /// <exception cref="ArgumentException">If MGRS format is invalid</exception>
    public static BoundingBox GetBounds(string mgrs)
    {
        if (string.IsNullOrWhiteSpace(mgrs))
            throw new ArgumentException("MGRS string cannot be empty", nameof(mgrs));

        mgrs = mgrs.ToUpperInvariant().Replace(" ", "");

        // Check for UPS (polar) coordinates
        if (mgrs[0] == 'A' || mgrs[0] == 'B' || mgrs[0] == 'Y' || mgrs[0] == 'Z')
        {
            return GetUpsBounds(mgrs);
        }

        return GetUtmBounds(mgrs);
    }

    private static BoundingBox GetUtmBounds(string mgrs)
    {
        // Parse zone number (1-2 digits)
        int zoneEndIndex = 1;
        if (mgrs.Length > 1 && char.IsDigit(mgrs[1]))
            zoneEndIndex = 2;

        if (!int.TryParse(mgrs[..zoneEndIndex], out int zone) || zone < 1 || zone > 60)
            throw new ArgumentException($"Invalid UTM zone: {mgrs[..zoneEndIndex]}", nameof(mgrs));

        // Zone-only bounds
        if (mgrs.Length == zoneEndIndex)
        {
            return GetZoneOnlyBounds(zone);
        }

        // Parse latitude band
        char band = mgrs[zoneEndIndex];
        int bandIndex = LatBands.IndexOf(band);
        if (bandIndex < 0)
            throw new ArgumentException($"Invalid latitude band: {band}", nameof(mgrs));

        // Zone + Band bounds (e.g., "18T")
        if (mgrs.Length == zoneEndIndex + 1)
        {
            return GetZoneBandBounds(zone, band);
        }

        // Parse 100km square letters
        if (mgrs.Length < zoneEndIndex + 3)
            throw new ArgumentException("MGRS must have at least 2 100km square letters after zone/band", nameof(mgrs));

        char colLetter = mgrs[zoneEndIndex + 1];
        char rowLetter = mgrs[zoneEndIndex + 2];

        // Zone + Band + 100km square bounds (e.g., "18TXM")
        if (mgrs.Length == zoneEndIndex + 3)
        {
            return Get100KSquareBounds(zone, band, colLetter, rowLetter);
        }

        // Parse grid digits
        string gridPart = mgrs[(zoneEndIndex + 3)..];
        if (gridPart.Length % 2 != 0)
            throw new ArgumentException("Grid digits must be even count (easting + northing)", nameof(mgrs));

        int digitPairs = gridPart.Length / 2;
        string eastingStr = gridPart[..digitPairs];
        string northingStr = gridPart[digitPairs..];

        if (!int.TryParse(eastingStr, out int eastingDigits) || !int.TryParse(northingStr, out int northingDigits))
            throw new ArgumentException("Invalid grid digits", nameof(mgrs));

        return GetGridSquareBounds(zone, band, colLetter, rowLetter, eastingDigits, northingDigits, digitPairs);
    }

    private static BoundingBox GetZoneOnlyBounds(int zone)
    {
        double minLon = (zone - 1) * 6 - 180;
        double maxLon = minLon + 6;

        // UTM zones cover -80° to 84° latitude
        return new BoundingBox(-80.0, 84.0, minLon, maxLon);
    }

    private static BoundingBox GetZoneBandBounds(int zone, char band)
    {
        double minLon = (zone - 1) * 6 - 180;
        double maxLon = minLon + 6;

        // Handle special zones for Norway and Svalbard
        if (band == 'V' && zone == 31)
        {
            maxLon = 3.0;
        }
        else if (band == 'V' && zone == 32)
        {
            minLon = 3.0;
            maxLon = 12.0;
        }
        else if (band == 'X')
        {
            // Svalbard special cases
            switch (zone)
            {
                case 31: maxLon = 9.0; break;
                case 33: minLon = 9.0; maxLon = 21.0; break;
                case 35: minLon = 21.0; maxLon = 33.0; break;
                case 37: minLon = 33.0; break;
                // Zones 32, 34, 36 don't exist for band X
            }
        }

        int bandIndex = LatBands.IndexOf(band);
        double minLat = -80.0 + bandIndex * 8;
        double maxLat = minLat + 8;

        // Band X extends to 84°
        if (band == 'X')
            maxLat = 84.0;

        return new BoundingBox(minLat, maxLat, minLon, maxLon);
    }

    private static BoundingBox Get100KSquareBounds(int zone, char band, char colLetter, char rowLetter)
    {
        var zoneBandBounds = GetZoneBandBounds(zone, band);

        // Get easting offset from column letter
        int set = zone % 6;
        if (set == 0) set = 6;

        string columnLetters = set switch
        {
            1 or 4 => ColumnLettersSet1,
            2 or 5 => ColumnLettersSet2,
            3 or 6 => ColumnLettersSet3,
            _ => ColumnLettersSet1
        };

        int colIndex = columnLetters.IndexOf(colLetter);
        if (colIndex < 0)
            throw new ArgumentException($"Invalid column letter '{colLetter}' for zone {zone}", nameof(colLetter));

        // Approximate 100km in degrees (varies by latitude)
        double centerLat = zoneBandBounds.CenterLat;
        double metersPerDegreeLon = 111320 * Math.Cos(centerLat * Math.PI / 180);
        double lonPer100K = 100000 / metersPerDegreeLon;
        double latPer100K = 100000 / 111320.0; // ~0.9° latitude per 100km

        // Calculate approximate bounds for the 100k square
        double minLon = zoneBandBounds.MinLon + colIndex * lonPer100K;
        double maxLon = minLon + lonPer100K;

        // Get northing offset from row letter
        const string rowLettersOdd = "ABCDEFGHJKLMNPQRSTUV";
        const string rowLettersEven = "FGHJKLMNPQRSTUVABCDE";
        string rowLetters = (set % 2 == 1) ? rowLettersOdd : rowLettersEven;

        int rowIndex = rowLetters.IndexOf(rowLetter);
        if (rowIndex < 0)
            throw new ArgumentException($"Invalid row letter '{rowLetter}' for zone {zone}", nameof(rowLetter));

        double minLat = zoneBandBounds.MinLat + rowIndex * latPer100K;
        double maxLat = minLat + latPer100K;

        // Clamp to zone band bounds
        minLon = Math.Max(minLon, zoneBandBounds.MinLon);
        maxLon = Math.Min(maxLon, zoneBandBounds.MaxLon);
        minLat = Math.Max(minLat, zoneBandBounds.MinLat);
        maxLat = Math.Min(maxLat, zoneBandBounds.MaxLat);

        return new BoundingBox(minLat, maxLat, minLon, maxLon);
    }

    private static BoundingBox GetGridSquareBounds(int zone, char band, char colLetter, char rowLetter,
        int eastingDigits, int northingDigits, int digitPairs)
    {
        var square100KBounds = Get100KSquareBounds(zone, band, colLetter, rowLetter);

        // Calculate the size of each grid cell based on precision
        double gridSizeMeters = digitPairs switch
        {
            1 => 10000,   // 10km
            2 => 1000,    // 1km
            3 => 100,     // 100m
            4 => 10,      // 10m
            5 => 1,       // 1m
            _ => 1
        };

        // Scale the digit values to full easting/northing within the 100k square
        int scaleFactor = (int)Math.Pow(10, 5 - digitPairs);
        double eastingMeters = eastingDigits * scaleFactor;
        double northingMeters = northingDigits * scaleFactor;

        // Convert meters to degrees at this latitude
        double centerLat = square100KBounds.CenterLat;
        double metersPerDegreeLon = 111320 * Math.Cos(centerLat * Math.PI / 180);
        double metersPerDegreeLat = 111320.0;

        double lonOffset = eastingMeters / metersPerDegreeLon;
        double latOffset = northingMeters / metersPerDegreeLat;
        double lonSize = gridSizeMeters / metersPerDegreeLon;
        double latSize = gridSizeMeters / metersPerDegreeLat;

        double minLon = square100KBounds.MinLon + lonOffset;
        double maxLon = minLon + lonSize;
        double minLat = square100KBounds.MinLat + latOffset;
        double maxLat = minLat + latSize;

        return new BoundingBox(minLat, maxLat, minLon, maxLon);
    }

    private static BoundingBox GetUpsBounds(string mgrs)
    {
        char hemisphere = mgrs[0];
        bool isNorth = hemisphere == 'Y' || hemisphere == 'Z';

        if (mgrs.Length == 1)
        {
            // Just hemisphere letter
            return isNorth
                ? new BoundingBox(84.0, 90.0, -180.0, 180.0)
                : new BoundingBox(-90.0, -80.0, -180.0, 180.0);
        }

        if (mgrs.Length < 3)
            throw new ArgumentException("UPS MGRS must have at least 3 characters for 100km square", nameof(mgrs));

        // For UPS, return approximate polar region bounds
        // Full UPS implementation would require more complex calculations
        return isNorth
            ? new BoundingBox(84.0, 90.0, -180.0, 180.0)
            : new BoundingBox(-90.0, -80.0, -180.0, 180.0);
    }
}
