using System;

namespace MilMap.Core.Mgrs;

/// <summary>
/// Encodes WGS84 lat/lon coordinates to MGRS grid references.
/// </summary>
public static class MgrsEncoder
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;
    
    // WGS84 ellipsoid parameters
    private const double SemiMajorAxis = 6378137.0;
    private const double Flattening = 1 / 298.257223563;
    private const double SemiMinorAxis = SemiMajorAxis * (1 - Flattening);
    // Eccentricity = sqrt(1 - b²/a²) ≈ 0.0818191908426
    private const double Eccentricity = 0.0818191908426;
    private const double EccentricitySquared = Eccentricity * Eccentricity;
    // EccentricityPrime = sqrt((a²-b²)/b²) ≈ 0.0820944381519
    private const double EccentricityPrime = 0.0820944381519;
    private const double EccentricityPrimeSquared = EccentricityPrime * EccentricityPrime;
    private const double K0 = 0.9996; // UTM scale factor
    
    private const string LatBands = "CDEFGHJKLMNPQRSTUVWX";
    private const string ColumnLettersSet1 = "ABCDEFGH";
    private const string ColumnLettersSet2 = "JKLMNPQR";
    private const string ColumnLettersSet3 = "STUVWXYZ";
    private const string RowLettersOdd = "ABCDEFGHJKLMNPQRSTUV";
    private const string RowLettersEven = "FGHJKLMNPQRSTUVABCDE";

    /// <summary>
    /// MGRS precision levels (1-5, representing 10km to 1m).
    /// </summary>
    public enum Precision
    {
        /// <summary>10km grid square</summary>
        TenKilometers = 1,
        /// <summary>1km grid square</summary>
        OneKilometer = 2,
        /// <summary>100m grid square</summary>
        HundredMeters = 3,
        /// <summary>10m grid square</summary>
        TenMeters = 4,
        /// <summary>1m grid square</summary>
        OneMeter = 5
    }

    /// <summary>
    /// Encodes a lat/lon coordinate to an MGRS string.
    /// </summary>
    /// <param name="latitude">Latitude in degrees (-90 to 90)</param>
    /// <param name="longitude">Longitude in degrees (-180 to 180)</param>
    /// <param name="precision">Output precision (default: 1m)</param>
    /// <returns>MGRS grid reference string</returns>
    /// <exception cref="ArgumentOutOfRangeException">If coordinates are invalid</exception>
    public static string Encode(double latitude, double longitude, Precision precision = Precision.OneMeter)
    {
        ValidateCoordinates(latitude, longitude);

        // Handle polar regions with UPS
        if (latitude >= 84.0 || latitude < -80.0)
        {
            return EncodeUps(latitude, longitude, precision);
        }

        return EncodeUtm(latitude, longitude, precision);
    }

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (latitude < -90.0 || latitude > 90.0)
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees");
        if (longitude < -180.0 || longitude > 180.0)
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees");
    }

    private static string EncodeUtm(double latitude, double longitude, Precision precision)
    {
        var (zone, easting, northing) = LatLonToUtm(latitude, longitude);
        char band = GetLatitudeBand(latitude);
        var (columnLetter, rowLetter) = Get100KIdentifiers(zone, easting, northing);
        
        // Get numeric portion based on precision
        int eastingDigits = (int)(easting % 100000);
        int northingDigits = (int)(northing % 100000);
        
        string eastingStr = FormatGridDigits(eastingDigits, precision);
        string northingStr = FormatGridDigits(northingDigits, precision);
        
        return $"{zone}{band}{columnLetter}{rowLetter}{eastingStr}{northingStr}";
    }

    private static string EncodeUps(double latitude, double longitude, Precision precision)
    {
        bool isNorth = latitude >= 0;
        var (easting, northing) = LatLonToUps(latitude, longitude);
        
        char hemisphere = isNorth ? (longitude < 0 ? 'Y' : 'Z') : (longitude < 0 ? 'A' : 'B');
        var (columnLetter, rowLetter) = GetUps100KIdentifiers(hemisphere, easting, northing);
        
        int eastingDigits = (int)(easting % 100000);
        int northingDigits = (int)(northing % 100000);
        
        string eastingStr = FormatGridDigits(eastingDigits, precision);
        string northingStr = FormatGridDigits(northingDigits, precision);
        
        return $"{hemisphere}{columnLetter}{rowLetter}{eastingStr}{northingStr}";
    }

    private static (int zone, double easting, double northing) LatLonToUtm(double lat, double lon)
    {
        int zone = GetUtmZone(lat, lon);
        double lonOrigin = (zone - 1) * 6 - 180 + 3;
        
        double latRad = lat * DegToRad;
        double lonRad = lon * DegToRad;
        double lonOriginRad = lonOrigin * DegToRad;
        
        double N = SemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * Math.Sin(latRad) * Math.Sin(latRad));
        double T = Math.Tan(latRad) * Math.Tan(latRad);
        double C = EccentricityPrimeSquared * Math.Cos(latRad) * Math.Cos(latRad);
        double A = Math.Cos(latRad) * (lonRad - lonOriginRad);
        
        double M = SemiMajorAxis * (
            (1 - EccentricitySquared / 4 - 3 * Math.Pow(EccentricitySquared, 2) / 64 - 5 * Math.Pow(EccentricitySquared, 3) / 256) * latRad
            - (3 * EccentricitySquared / 8 + 3 * Math.Pow(EccentricitySquared, 2) / 32 + 45 * Math.Pow(EccentricitySquared, 3) / 1024) * Math.Sin(2 * latRad)
            + (15 * Math.Pow(EccentricitySquared, 2) / 256 + 45 * Math.Pow(EccentricitySquared, 3) / 1024) * Math.Sin(4 * latRad)
            - (35 * Math.Pow(EccentricitySquared, 3) / 3072) * Math.Sin(6 * latRad)
        );
        
        double easting = K0 * N * (
            A + (1 - T + C) * Math.Pow(A, 3) / 6
            + (5 - 18 * T + T * T + 72 * C - 58 * EccentricityPrimeSquared) * Math.Pow(A, 5) / 120
        ) + 500000.0;
        
        double northing = K0 * (
            M + N * Math.Tan(latRad) * (
                A * A / 2
                + (5 - T + 9 * C + 4 * C * C) * Math.Pow(A, 4) / 24
                + (61 - 58 * T + T * T + 600 * C - 330 * EccentricityPrimeSquared) * Math.Pow(A, 6) / 720
            )
        );
        
        if (lat < 0)
            northing += 10000000.0;
        
        return (zone, easting, northing);
    }

    private static (double easting, double northing) LatLonToUps(double lat, double lon)
    {
        bool isNorth = lat >= 0;
        double absLat = Math.Abs(lat);
        
        double latRad = absLat * DegToRad;
        double lonRad = lon * DegToRad;
        
        double e = Eccentricity;
        double k0 = 0.994;
        
        double t = Math.Tan(Math.PI / 4 - latRad / 2) / 
                   Math.Pow((1 - e * Math.Sin(latRad)) / (1 + e * Math.Sin(latRad)), e / 2);
        
        double rho = 2 * SemiMajorAxis * k0 * t / 
                     Math.Sqrt(Math.Pow(1 + e, 1 + e) * Math.Pow(1 - e, 1 - e));
        
        double easting = 2000000 + rho * Math.Sin(lonRad);
        double northing = isNorth 
            ? 2000000 - rho * Math.Cos(lonRad) 
            : 2000000 + rho * Math.Cos(lonRad);
        
        return (easting, northing);
    }

    private static int GetUtmZone(double lat, double lon)
    {
        // Handle special zones for Norway and Svalbard
        if (lat >= 56.0 && lat < 64.0 && lon >= 3.0 && lon < 12.0)
            return 32;
        
        if (lat >= 72.0 && lat < 84.0)
        {
            if (lon >= 0.0 && lon < 9.0) return 31;
            if (lon >= 9.0 && lon < 21.0) return 33;
            if (lon >= 21.0 && lon < 33.0) return 35;
            if (lon >= 33.0 && lon < 42.0) return 37;
        }
        
        return (int)((lon + 180) / 6) + 1;
    }

    private static char GetLatitudeBand(double lat)
    {
        if (lat >= 84.0) return 'X';
        if (lat < -80.0) return 'C';
        
        int bandIndex = (int)((lat + 80) / 8);
        if (bandIndex >= LatBands.Length) bandIndex = LatBands.Length - 1;
        return LatBands[bandIndex];
    }

    private static (char column, char row) Get100KIdentifiers(int zone, double easting, double northing)
    {
        int set = zone % 6;
        if (set == 0) set = 6;
        
        int col100k = (int)(easting / 100000);
        int row100k = (int)(northing / 100000) % 20;
        
        string columnLetters = set switch
        {
            1 or 4 => ColumnLettersSet1,
            2 or 5 => ColumnLettersSet2,
            3 or 6 => ColumnLettersSet3,
            _ => ColumnLettersSet1
        };
        
        int colIndex = (col100k - 1) % 8;
        if (colIndex < 0) colIndex += 8;
        char columnLetter = columnLetters[colIndex];
        
        string rowLetters = (set % 2 == 1) ? RowLettersOdd : RowLettersEven;
        char rowLetter = rowLetters[row100k % 20];
        
        return (columnLetter, rowLetter);
    }

    private static (char column, char row) GetUps100KIdentifiers(char hemisphere, double easting, double northing)
    {
        const string upsColumnLettersNorth = "RSTUXYZABCFGHJ";
        const string upsColumnLettersSouth = "JKLPQRSTUXYZABC";
        const string upsRowLettersNorth = "ABCDEFGHJKLMNP";
        const string upsRowLettersSouth = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        
        bool isNorth = hemisphere == 'Y' || hemisphere == 'Z';
        
        int col100k = (int)(easting / 100000);
        int row100k = (int)(northing / 100000);
        
        string colLetters = isNorth ? upsColumnLettersNorth : upsColumnLettersSouth;
        string rowLetters = isNorth ? upsRowLettersNorth : upsRowLettersSouth;
        
        int colIndex = col100k % colLetters.Length;
        int rowIndex = row100k % rowLetters.Length;
        
        return (colLetters[colIndex], rowLetters[rowIndex]);
    }

    private static string FormatGridDigits(int value, Precision precision)
    {
        int divisor = precision switch
        {
            Precision.TenKilometers => 10000,
            Precision.OneKilometer => 1000,
            Precision.HundredMeters => 100,
            Precision.TenMeters => 10,
            Precision.OneMeter => 1,
            _ => 1
        };
        
        int digits = (int)precision;
        int scaledValue = value / divisor;
        
        return scaledValue.ToString().PadLeft(digits, '0');
    }
}
