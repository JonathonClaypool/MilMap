using System;

namespace MilMap.Core.Mgrs;

/// <summary>
/// Parses MGRS grid references to WGS84 lat/lon coordinates.
/// </summary>
public static class MgrsParser
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    // WGS84 ellipsoid parameters
    private const double SemiMajorAxis = 6378137.0;
    private const double Eccentricity = 0.0818191908426;
    private const double EccentricitySquared = Eccentricity * Eccentricity;
    private const double EccentricityPrime = 0.0820944381519;
    private const double EccentricityPrimeSquared = EccentricityPrime * EccentricityPrime;
    private const double K0 = 0.9996; // UTM scale factor

    private const string LatBands = "CDEFGHJKLMNPQRSTUVWX";
    private const string ColumnLettersSet1 = "ABCDEFGH";
    private const string ColumnLettersSet2 = "JKLMNPQR";
    private const string ColumnLettersSet3 = "STUVWXYZ";
    private const string RowLettersOdd = "ABCDEFGHJKLMNPQRSTUV";
    private const string RowLettersEven = "FGHJKLMNPQRSTUVABCDE";

    // Northing at the base of each latitude band (for odd zones at central meridian)
    private static readonly double[] BandNorthings = new double[20];

    static MgrsParser()
    {
        // Pre-calculate northing at the start of each latitude band
        for (int i = 0; i < 20; i++)
        {
            double lat = -80.0 + i * 8;
            BandNorthings[i] = LatitudeToNorthing(lat);
        }
    }

    private static double LatitudeToNorthing(double lat)
    {
        double latRad = lat * DegToRad;
        double M = SemiMajorAxis * (
            (1 - EccentricitySquared / 4 - 3 * Math.Pow(EccentricitySquared, 2) / 64 - 5 * Math.Pow(EccentricitySquared, 3) / 256) * latRad
            - (3 * EccentricitySquared / 8 + 3 * Math.Pow(EccentricitySquared, 2) / 32 + 45 * Math.Pow(EccentricitySquared, 3) / 1024) * Math.Sin(2 * latRad)
            + (15 * Math.Pow(EccentricitySquared, 2) / 256 + 45 * Math.Pow(EccentricitySquared, 3) / 1024) * Math.Sin(4 * latRad)
            - (35 * Math.Pow(EccentricitySquared, 3) / 3072) * Math.Sin(6 * latRad)
        );
        return K0 * M;
    }

    /// <summary>
    /// Parses an MGRS string to WGS84 latitude and longitude.
    /// Returns the center point of the grid square.
    /// </summary>
    public static (double Latitude, double Longitude) Parse(string mgrs)
    {
        if (string.IsNullOrWhiteSpace(mgrs))
            throw new ArgumentException("MGRS string cannot be empty", nameof(mgrs));

        mgrs = mgrs.ToUpperInvariant().Replace(" ", "");

        // Check for UPS (polar) coordinates
        if (mgrs[0] == 'A' || mgrs[0] == 'B' || mgrs[0] == 'Y' || mgrs[0] == 'Z')
        {
            return ParseUps(mgrs);
        }

        return ParseUtm(mgrs);
    }

    private static (double Latitude, double Longitude) ParseUtm(string mgrs)
    {
        // Parse zone number (1-2 digits)
        int zoneEndIndex = 1;
        if (mgrs.Length > 1 && char.IsDigit(mgrs[1]))
            zoneEndIndex = 2;

        if (!int.TryParse(mgrs[..zoneEndIndex], out int zone) || zone < 1 || zone > 60)
            throw new ArgumentException($"Invalid UTM zone: {mgrs[..zoneEndIndex]}", nameof(mgrs));

        if (mgrs.Length <= zoneEndIndex)
            throw new ArgumentException("MGRS must include latitude band", nameof(mgrs));

        // Parse latitude band
        char band = mgrs[zoneEndIndex];
        int bandIndex = LatBands.IndexOf(band);
        if (bandIndex < 0)
            throw new ArgumentException($"Invalid latitude band: {band}", nameof(mgrs));

        if (mgrs.Length < zoneEndIndex + 3)
            throw new ArgumentException("MGRS must include 100km square identifier", nameof(mgrs));

        // Parse 100km square letters
        char colLetter = mgrs[zoneEndIndex + 1];
        char rowLetter = mgrs[zoneEndIndex + 2];

        // Parse grid digits
        string gridPart = mgrs.Length > zoneEndIndex + 3 ? mgrs[(zoneEndIndex + 3)..] : "";

        if (gridPart.Length % 2 != 0)
            throw new ArgumentException("Grid digits must be even count (easting + northing)", nameof(mgrs));

        int digitPairs = gridPart.Length / 2;
        double eastingWithin100k, northingWithin100k;

        if (digitPairs > 0)
        {
            string eastingStr = gridPart[..digitPairs];
            string northingStr = gridPart[digitPairs..];

            if (!long.TryParse(eastingStr, out long eastingDigits) || !long.TryParse(northingStr, out long northingDigits))
                throw new ArgumentException("Invalid grid digits", nameof(mgrs));

            // Scale digits to meters within 100km square
            double scaleFactor = Math.Pow(10, 5 - digitPairs);
            double gridSize = scaleFactor;

            // Center of grid square
            eastingWithin100k = eastingDigits * scaleFactor + gridSize / 2;
            northingWithin100k = northingDigits * scaleFactor + gridSize / 2;
        }
        else
        {
            // Center of 100km square
            eastingWithin100k = 50000;
            northingWithin100k = 50000;
        }

        // Get easting from column letter
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
            throw new ArgumentException($"Invalid column letter '{colLetter}' for zone {zone}", nameof(mgrs));

        // Full easting: column index gives 100km block (1-indexed for UTM easting)
        double easting = (colIndex + 1) * 100000 + eastingWithin100k;

        // Get row letter northing offset
        string rowLetters = (set % 2 == 1) ? RowLettersOdd : RowLettersEven;
        int rowIndex = rowLetters.IndexOf(rowLetter);
        if (rowIndex < 0)
            throw new ArgumentException($"Invalid row letter '{rowLetter}' for zone {zone}", nameof(mgrs));

        // Row northing within 2,000,000m cycle
        double rowNorthing = rowIndex * 100000 + northingWithin100k;

        // Determine full northing based on latitude band
        double fullNorthing = ComputeFullNorthing(bandIndex, rowNorthing, band);

        // Determine if southern hemisphere
        bool isSouthernHemisphere = band < 'N';

        // Convert UTM to lat/lon
        return UtmToLatLon(zone, fullNorthing, easting, isSouthernHemisphere);
    }

    private static double ComputeFullNorthing(int bandIndex, double rowNorthing, char band)
    {
        // Get the minimum northing for this band
        double bandMinNorthing = BandNorthings[bandIndex];
        
        // For southern hemisphere, adjust to false northing
        bool isSouthern = band < 'N';
        if (isSouthern)
        {
            bandMinNorthing += 10000000;
        }

        // Row letters cycle every 2,000,000 meters
        const double cycleSize = 2000000;

        // Find which 2M cycle contains the band minimum
        double baseCycleNorthing = Math.Floor(bandMinNorthing / cycleSize) * cycleSize;

        // Try to find the correct cycle that places us within the band
        // Usually rowNorthing % cycleSize gives us position within a cycle
        double offsetInCycle = rowNorthing % cycleSize;
        
        // Start with the base cycle
        double fullNorthing = baseCycleNorthing + offsetInCycle;

        // If that's below the band minimum, try the next cycle up
        if (fullNorthing < bandMinNorthing - 100000) // Allow some tolerance
        {
            fullNorthing += cycleSize;
        }
        // If above band maximum (next band), try cycle below
        else if (bandIndex < 19)
        {
            double bandMaxNorthing = BandNorthings[bandIndex + 1];
            if (isSouthern && bandIndex < 9) // band < 'N' but we need next band's northing
            {
                bandMaxNorthing += 10000000;
            }
            else if (bandIndex >= 9 && bandIndex + 1 >= 10) // Crossing into 'N' or above
            {
                // No adjustment needed for northern bands
            }
            
            if (fullNorthing > bandMaxNorthing + 100000)
            {
                fullNorthing -= cycleSize;
            }
        }

        return fullNorthing;
    }

    private static (double Latitude, double Longitude) UtmToLatLon(int zone, double northing, double easting, bool isSouthernHemisphere)
    {
        // Adjust northing for southern hemisphere
        double adjustedNorthing = isSouthernHemisphere ? northing - 10000000 : northing;

        double lonOrigin = (zone - 1) * 6 - 180 + 3;

        double e1 = (1 - Math.Sqrt(1 - EccentricitySquared)) / (1 + Math.Sqrt(1 - EccentricitySquared));
        double M = adjustedNorthing / K0;
        double mu = M / (SemiMajorAxis * (1 - EccentricitySquared / 4 - 3 * Math.Pow(EccentricitySquared, 2) / 64 - 5 * Math.Pow(EccentricitySquared, 3) / 256));

        double phi1Rad = mu
            + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu)
            + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu)
            + (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu)
            + (1097 * Math.Pow(e1, 4) / 512) * Math.Sin(8 * mu);

        double N1 = SemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * Math.Sin(phi1Rad) * Math.Sin(phi1Rad));
        double T1 = Math.Tan(phi1Rad) * Math.Tan(phi1Rad);
        double C1 = EccentricityPrimeSquared * Math.Cos(phi1Rad) * Math.Cos(phi1Rad);
        double R1 = SemiMajorAxis * (1 - EccentricitySquared) / Math.Pow(1 - EccentricitySquared * Math.Sin(phi1Rad) * Math.Sin(phi1Rad), 1.5);
        double D = (easting - 500000) / (N1 * K0);

        double lat = phi1Rad
            - (N1 * Math.Tan(phi1Rad) / R1) * (
                D * D / 2
                - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * EccentricityPrimeSquared) * Math.Pow(D, 4) / 24
                + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * EccentricityPrimeSquared - 3 * C1 * C1) * Math.Pow(D, 6) / 720
            );

        double lon = lonOrigin + RadToDeg * (
            D
            - (1 + 2 * T1 + C1) * Math.Pow(D, 3) / 6
            + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * EccentricityPrimeSquared + 24 * T1 * T1) * Math.Pow(D, 5) / 120
        ) / Math.Cos(phi1Rad);

        return (lat * RadToDeg, lon);
    }

    private static (double Latitude, double Longitude) ParseUps(string mgrs)
    {
        char hemisphere = mgrs[0];
        bool isNorth = hemisphere == 'Y' || hemisphere == 'Z';

        if (mgrs.Length < 3)
            throw new ArgumentException("UPS MGRS must have at least 3 characters", nameof(mgrs));

        char colLetter = mgrs[1];
        char rowLetter = mgrs[2];

        // Parse grid digits
        string gridPart = mgrs.Length > 3 ? mgrs[3..] : "";

        if (gridPart.Length % 2 != 0)
            throw new ArgumentException("Grid digits must be even count", nameof(mgrs));

        int digitPairs = gridPart.Length / 2;
        double easting, northing;

        if (digitPairs > 0)
        {
            string eastingStr = gridPart[..digitPairs];
            string northingStr = gridPart[digitPairs..];

            if (!long.TryParse(eastingStr, out long eastingDigits) || !long.TryParse(northingStr, out long northingDigits))
                throw new ArgumentException("Invalid grid digits", nameof(mgrs));

            double scaleFactor = Math.Pow(10, 5 - digitPairs);
            double gridSize = scaleFactor;

            easting = eastingDigits * scaleFactor + gridSize / 2;
            northing = northingDigits * scaleFactor + gridSize / 2;
        }
        else
        {
            easting = 50000;
            northing = 50000;
        }

        // Get full easting/northing from 100k letters
        var (colOffset, rowOffset) = GetUps100KOffsets(hemisphere, colLetter, rowLetter);
        easting += colOffset;
        northing += rowOffset;

        return UpsToLatLon(easting, northing, isNorth);
    }

    private static (double colOffset, double rowOffset) GetUps100KOffsets(char hemisphere, char colLetter, char rowLetter)
    {
        const string upsColumnLettersNorth = "RSTUXYZABCFGHJ";
        const string upsColumnLettersSouth = "JKLPQRSTUXYZABC";
        const string upsRowLettersNorth = "ABCDEFGHJKLMNP";
        const string upsRowLettersSouth = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        bool isNorth = hemisphere == 'Y' || hemisphere == 'Z';

        string colLetters = isNorth ? upsColumnLettersNorth : upsColumnLettersSouth;
        string rowLetters = isNorth ? upsRowLettersNorth : upsRowLettersSouth;

        int colIndex = colLetters.IndexOf(colLetter);
        int rowIndex = rowLetters.IndexOf(rowLetter);

        if (colIndex < 0)
            throw new ArgumentException($"Invalid UPS column letter: {colLetter}", "mgrs");
        if (rowIndex < 0)
            throw new ArgumentException($"Invalid UPS row letter: {rowLetter}", "mgrs");

        return (colIndex * 100000, rowIndex * 100000);
    }

    private static (double Latitude, double Longitude) UpsToLatLon(double easting, double northing, bool isNorth)
    {
        double e = Eccentricity;
        double k0 = 0.994;

        double x = easting - 2000000;
        double y = isNorth ? 2000000 - northing : northing - 2000000;

        double rho = Math.Sqrt(x * x + y * y);
        double t = rho * Math.Sqrt(Math.Pow(1 + e, 1 + e) * Math.Pow(1 - e, 1 - e)) / (2 * SemiMajorAxis * k0);

        double phi = Math.PI / 2 - 2 * Math.Atan(t);
        for (int i = 0; i < 10; i++)
        {
            double newPhi = Math.PI / 2 - 2 * Math.Atan(t * Math.Pow((1 - e * Math.Sin(phi)) / (1 + e * Math.Sin(phi)), e / 2));
            if (Math.Abs(newPhi - phi) < 1e-12)
                break;
            phi = newPhi;
        }

        double lon = Math.Atan2(x, y);

        double lat = phi * RadToDeg;
        double longitude = lon * RadToDeg;

        if (!isNorth)
            lat = -lat;

        return (lat, longitude);
    }
}
