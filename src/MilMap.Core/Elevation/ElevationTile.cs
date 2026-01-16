using System;

namespace MilMap.Core.Elevation;

/// <summary>
/// Represents a single elevation data tile.
/// </summary>
public class ElevationTile
{
    /// <summary>
    /// Latitude of the tile's southwest corner.
    /// </summary>
    public int Latitude { get; }

    /// <summary>
    /// Longitude of the tile's southwest corner.
    /// </summary>
    public int Longitude { get; }

    /// <summary>
    /// Resolution of the tile (samples per degree).
    /// SRTM1 = 3601 (1 arc-second), SRTM3 = 1201 (3 arc-second).
    /// </summary>
    public int Resolution { get; }

    /// <summary>
    /// Elevation data in row-major order (north to south, west to east).
    /// Values are in meters. NoData is represented by short.MinValue.
    /// </summary>
    public short[] ElevationData { get; }

    /// <summary>
    /// Creates a new elevation tile.
    /// </summary>
    public ElevationTile(int latitude, int longitude, int resolution, short[] elevationData)
    {
        if (resolution <= 0)
            throw new ArgumentOutOfRangeException(nameof(resolution), "Resolution must be positive");
        if (elevationData.Length != resolution * resolution)
            throw new ArgumentException($"Elevation data must have {resolution * resolution} samples", nameof(elevationData));

        Latitude = latitude;
        Longitude = longitude;
        Resolution = resolution;
        ElevationData = elevationData;
    }

    /// <summary>
    /// Gets the elevation at a specific lat/lon within this tile.
    /// </summary>
    /// <param name="lat">Latitude</param>
    /// <param name="lon">Longitude</param>
    /// <returns>Elevation in meters, or null if no data</returns>
    public double? GetElevation(double lat, double lon)
    {
        if (lat < Latitude || lat > Latitude + 1 || lon < Longitude || lon > Longitude + 1)
            return null;

        // Calculate sample indices
        double fracLat = lat - Latitude;
        double fracLon = lon - Longitude;

        // SRTM data is stored north to south, so we invert the row
        int row = (int)((1.0 - fracLat) * (Resolution - 1));
        int col = (int)(fracLon * (Resolution - 1));

        row = Math.Clamp(row, 0, Resolution - 1);
        col = Math.Clamp(col, 0, Resolution - 1);

        int index = row * Resolution + col;
        short value = ElevationData[index];

        // NoData check
        if (value == short.MinValue || value == -32768)
            return null;

        return value;
    }

    /// <summary>
    /// Gets the elevation at a specific lat/lon using bilinear interpolation.
    /// </summary>
    public double? GetElevationInterpolated(double lat, double lon)
    {
        if (lat < Latitude || lat > Latitude + 1 || lon < Longitude || lon > Longitude + 1)
            return null;

        double fracLat = lat - Latitude;
        double fracLon = lon - Longitude;

        // Calculate fractional indices
        double rowF = (1.0 - fracLat) * (Resolution - 1);
        double colF = fracLon * (Resolution - 1);

        int row0 = (int)rowF;
        int col0 = (int)colF;
        int row1 = Math.Min(row0 + 1, Resolution - 1);
        int col1 = Math.Min(col0 + 1, Resolution - 1);

        double rowFrac = rowF - row0;
        double colFrac = colF - col0;

        // Get four corner values
        short v00 = ElevationData[row0 * Resolution + col0];
        short v01 = ElevationData[row0 * Resolution + col1];
        short v10 = ElevationData[row1 * Resolution + col0];
        short v11 = ElevationData[row1 * Resolution + col1];

        // Check for void data
        if (v00 == short.MinValue || v01 == short.MinValue ||
            v10 == short.MinValue || v11 == short.MinValue)
        {
            // Fall back to nearest neighbor if any void data
            return GetElevation(lat, lon);
        }

        // Bilinear interpolation
        double top = v00 + (v01 - v00) * colFrac;
        double bottom = v10 + (v11 - v10) * colFrac;
        return top + (bottom - top) * rowFrac;
    }
}
