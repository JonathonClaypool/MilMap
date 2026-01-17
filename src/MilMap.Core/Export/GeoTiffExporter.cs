using System;
using System.IO;
using BitMiracle.LibTiff.Classic;
using MilMap.Core.Mgrs;
using SkiaSharp;

namespace MilMap.Core.Export;

/// <summary>
/// Options for GeoTIFF export.
/// </summary>
public class GeoTiffExportOptions
{
    /// <summary>
    /// The bounding box of the map in WGS84 coordinates.
    /// </summary>
    public required BoundingBox BoundingBox { get; init; }

    /// <summary>
    /// Output DPI. Default is 300 for print quality.
    /// </summary>
    public int Dpi { get; init; } = 300;

    /// <summary>
    /// TIFF compression type.
    /// </summary>
    public TiffCompression Compression { get; init; } = TiffCompression.Lzw;

    /// <summary>
    /// Whether to write as tiled TIFF for better random access performance.
    /// </summary>
    public bool UseTiles { get; init; } = true;

    /// <summary>
    /// Tile width when UseTiles is true. Must be a multiple of 16.
    /// </summary>
    public int TileWidth { get; init; } = 256;

    /// <summary>
    /// Tile height when UseTiles is true. Must be a multiple of 16.
    /// </summary>
    public int TileHeight { get; init; } = 256;
}

/// <summary>
/// TIFF compression options.
/// </summary>
public enum TiffCompression
{
    None,
    Lzw,
    Deflate,
    Jpeg
}

/// <summary>
/// Exports maps to GeoTIFF format with embedded georeferencing for GIS applications.
/// </summary>
public class GeoTiffExporter
{
    // GeoTIFF tag IDs
    private const int TIFFTAG_GEOPIXELSCALE = 33550;
    private const int TIFFTAG_GEOTIEPOINTS = 33922;
    private const int TIFFTAG_GEOKEYDIRECTORY = 34735;
    private const int TIFFTAG_GEODOUBLEPARAMS = 34736;
    private const int TIFFTAG_GEOASCIIPARAMS = 34737;

    // GeoKey values
    private const int GTModelTypeGeoKey = 1024;
    private const int GTRasterTypeGeoKey = 1025;
    private const int GeographicTypeGeoKey = 2048;
    private const int GeogAngularUnitsGeoKey = 2054;

    // Model types
    private const int ModelTypeGeographic = 2;
    
    // Raster types
    private const int RasterPixelIsArea = 1;
    
    // Geographic types
    private const int GCS_WGS_84 = 4326;
    
    // Angular units
    private const int Angular_Degree = 9102;

    private readonly GeoTiffExportOptions _options;

    public GeoTiffExporter(GeoTiffExportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Exports a bitmap to a GeoTIFF file.
    /// </summary>
    /// <param name="bitmap">The bitmap to export</param>
    /// <param name="outputPath">Path to save the GeoTIFF file</param>
    public void Export(SKBitmap bitmap, string outputPath)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentNullException(nameof(outputPath));

        using var tiff = Tiff.Open(outputPath, "w");
        if (tiff == null)
            throw new IOException($"Failed to create TIFF file: {outputPath}");

        WriteGeoTiff(tiff, bitmap);
    }

    /// <summary>
    /// Exports a bitmap to a GeoTIFF byte array.
    /// </summary>
    /// <param name="bitmap">The bitmap to export</param>
    /// <returns>GeoTIFF data as byte array</returns>
    public byte[] ExportToBytes(SKBitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        using var stream = new MemoryStream();
        using var tiff = Tiff.ClientOpen("memory", "w", stream, new TiffStream());
        if (tiff == null)
            throw new InvalidOperationException("Failed to create in-memory TIFF");

        WriteGeoTiff(tiff, bitmap);
        tiff.Flush();

        return stream.ToArray();
    }

    private void WriteGeoTiff(Tiff tiff, SKBitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        // Set basic TIFF tags
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, height);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 4); // RGBA
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.EXTRASAMPLES, 1, new short[] { (short)ExtraSample.UNASSALPHA });
        tiff.SetField(TiffTag.COMPRESSION, GetCompression(_options.Compression));
        tiff.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);

        // Set DPI
        tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
        tiff.SetField(TiffTag.XRESOLUTION, (double)_options.Dpi);
        tiff.SetField(TiffTag.YRESOLUTION, (double)_options.Dpi);

        // Write GeoTIFF tags
        WriteGeoTags(tiff, width, height);

        // Write image data
        if (_options.UseTiles)
        {
            WriteTiledImage(tiff, bitmap);
        }
        else
        {
            WriteStrippedImage(tiff, bitmap);
        }
    }

    private void WriteGeoTags(Tiff tiff, int width, int height)
    {
        var bbox = _options.BoundingBox;

        // Calculate pixel scale (degrees per pixel)
        double scaleX = (bbox.MaxLon - bbox.MinLon) / width;
        double scaleY = (bbox.MaxLat - bbox.MinLat) / height;

        // GeoPixelScale tag: [ScaleX, ScaleY, ScaleZ]
        double[] pixelScale = { scaleX, scaleY, 0.0 };
        tiff.SetField((TiffTag)TIFFTAG_GEOPIXELSCALE, 3, pixelScale);

        // GeoTiePoints tag: [I, J, K, X, Y, Z] - ties pixel (0,0) to upper-left corner
        // Note: TIFF origin is top-left, so Y is MaxLat
        double[] tiePoints = { 0.0, 0.0, 0.0, bbox.MinLon, bbox.MaxLat, 0.0 };
        tiff.SetField((TiffTag)TIFFTAG_GEOTIEPOINTS, 6, tiePoints);

        // GeoKeyDirectory tag - defines the coordinate system
        // Format: [KeyDirectoryVersion, KeyRevision, MinorRevision, NumberOfKeys, KeyEntry1, ...]
        // Each KeyEntry: [KeyID, TIFFTagLocation, Count, Value_Offset]
        ushort[] geoKeys = {
            1, 1, 0, 4,  // Version 1.1.0, 4 keys
            (ushort)GTModelTypeGeoKey, 0, 1, (ushort)ModelTypeGeographic,
            (ushort)GTRasterTypeGeoKey, 0, 1, (ushort)RasterPixelIsArea,
            (ushort)GeographicTypeGeoKey, 0, 1, (ushort)GCS_WGS_84,
            (ushort)GeogAngularUnitsGeoKey, 0, 1, (ushort)Angular_Degree
        };
        tiff.SetField((TiffTag)TIFFTAG_GEOKEYDIRECTORY, geoKeys.Length, geoKeys);
    }

    private void WriteTiledImage(Tiff tiff, SKBitmap bitmap)
    {
        int tileWidth = _options.TileWidth;
        int tileHeight = _options.TileHeight;

        tiff.SetField(TiffTag.TILEWIDTH, tileWidth);
        tiff.SetField(TiffTag.TILELENGTH, tileHeight);

        int tilesAcross = (bitmap.Width + tileWidth - 1) / tileWidth;
        int tilesDown = (bitmap.Height + tileHeight - 1) / tileHeight;

        byte[] tileBuffer = new byte[tileWidth * tileHeight * 4];

        for (int ty = 0; ty < tilesDown; ty++)
        {
            for (int tx = 0; tx < tilesAcross; tx++)
            {
                int tileIndex = ty * tilesAcross + tx;
                int startX = tx * tileWidth;
                int startY = ty * tileHeight;

                // Clear buffer
                Array.Clear(tileBuffer, 0, tileBuffer.Length);

                // Copy pixel data from bitmap to tile buffer
                for (int y = 0; y < tileHeight && (startY + y) < bitmap.Height; y++)
                {
                    for (int x = 0; x < tileWidth && (startX + x) < bitmap.Width; x++)
                    {
                        var pixel = bitmap.GetPixel(startX + x, startY + y);
                        int offset = (y * tileWidth + x) * 4;
                        tileBuffer[offset] = pixel.Red;
                        tileBuffer[offset + 1] = pixel.Green;
                        tileBuffer[offset + 2] = pixel.Blue;
                        tileBuffer[offset + 3] = pixel.Alpha;
                    }
                }

                tiff.WriteEncodedTile(tileIndex, tileBuffer, tileBuffer.Length);
            }
        }
    }

    private void WriteStrippedImage(Tiff tiff, SKBitmap bitmap)
    {
        int rowsPerStrip = 16; // Standard strip height
        tiff.SetField(TiffTag.ROWSPERSTRIP, rowsPerStrip);

        int stripSize = bitmap.Width * rowsPerStrip * 4;
        byte[] stripBuffer = new byte[stripSize];

        int strips = (bitmap.Height + rowsPerStrip - 1) / rowsPerStrip;

        for (int strip = 0; strip < strips; strip++)
        {
            int startRow = strip * rowsPerStrip;
            int rowsThisStrip = Math.Min(rowsPerStrip, bitmap.Height - startRow);

            // Copy pixel data from bitmap to strip buffer
            int bufferOffset = 0;
            for (int y = 0; y < rowsThisStrip; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, startRow + y);
                    stripBuffer[bufferOffset++] = pixel.Red;
                    stripBuffer[bufferOffset++] = pixel.Green;
                    stripBuffer[bufferOffset++] = pixel.Blue;
                    stripBuffer[bufferOffset++] = pixel.Alpha;
                }
            }

            tiff.WriteEncodedStrip(strip, stripBuffer, rowsThisStrip * bitmap.Width * 4);
        }
    }

    private static Compression GetCompression(TiffCompression compression)
    {
        return compression switch
        {
            TiffCompression.None => Compression.NONE,
            TiffCompression.Lzw => Compression.LZW,
            TiffCompression.Deflate => Compression.DEFLATE,
            TiffCompression.Jpeg => Compression.JPEG,
            _ => Compression.LZW
        };
    }

    /// <summary>
    /// Gets the file extension for GeoTIFF files.
    /// </summary>
    public static string GetFileExtension() => ".tif";

    /// <summary>
    /// Gets the MIME type for GeoTIFF files.
    /// </summary>
    public static string GetMimeType() => "image/tiff";
}
