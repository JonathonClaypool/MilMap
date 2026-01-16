using System;
using System.IO;
using SkiaSharp;

namespace MilMap.Core.Export;

/// <summary>
/// Supported image export formats.
/// </summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    Webp
}

/// <summary>
/// Options for image export.
/// </summary>
public class ImageExportOptions
{
    /// <summary>
    /// Output image format.
    /// </summary>
    public ImageFormat Format { get; set; } = ImageFormat.Png;

    /// <summary>
    /// JPEG/WebP quality (1-100). Higher values = better quality, larger files.
    /// </summary>
    public int Quality { get; set; } = 95;

    /// <summary>
    /// Output DPI metadata. Default is 300 for print quality.
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// Whether to include alpha channel (transparency) for PNG.
    /// </summary>
    public bool IncludeAlpha { get; set; } = false;

    /// <summary>
    /// Background color when alpha is not included. Default is white.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.White;
}

/// <summary>
/// Exports maps to high-resolution raster image formats (PNG, JPEG, WebP).
/// </summary>
public class ImageExporter
{
    private readonly ImageExportOptions _options;

    public ImageExporter() : this(new ImageExportOptions()) { }

    public ImageExporter(ImageExportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Exports a bitmap to a file.
    /// </summary>
    /// <param name="bitmap">The bitmap to export</param>
    /// <param name="outputPath">Path to save the image file</param>
    public void Export(SKBitmap bitmap, string outputPath)
    {
        var bytes = ExportToBytes(bitmap);
        File.WriteAllBytes(outputPath, bytes);
    }

    /// <summary>
    /// Exports a bitmap to a byte array.
    /// </summary>
    /// <param name="bitmap">The bitmap to export</param>
    /// <returns>Image data as byte array</returns>
    public byte[] ExportToBytes(SKBitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        SKBitmap exportBitmap = bitmap;
        bool needsDispose = false;

        // Handle alpha channel for non-PNG formats
        if (!_options.IncludeAlpha && _options.Format != ImageFormat.Png)
        {
            exportBitmap = FlattenAlpha(bitmap);
            needsDispose = true;
        }
        else if (!_options.IncludeAlpha && _options.Format == ImageFormat.Png && bitmap.AlphaType != SKAlphaType.Opaque)
        {
            exportBitmap = FlattenAlpha(bitmap);
            needsDispose = true;
        }

        try
        {
            using var image = SKImage.FromBitmap(exportBitmap);
            var format = GetSkiaFormat(_options.Format);
            using var data = image.Encode(format, _options.Quality);

            if (data == null)
            {
                // Fallback to PNG if encoding fails
                using var fallbackData = image.Encode(SKEncodedImageFormat.Png, 100);
                if (fallbackData == null)
                    throw new InvalidOperationException("Failed to encode image");
                return fallbackData.ToArray();
            }

            return data.ToArray();
        }
        finally
        {
            if (needsDispose)
                exportBitmap.Dispose();
        }
    }

    /// <summary>
    /// Exports a bitmap to a stream.
    /// </summary>
    /// <param name="bitmap">The bitmap to export</param>
    /// <param name="stream">Stream to write to</param>
    public void ExportToStream(SKBitmap bitmap, Stream stream)
    {
        var bytes = ExportToBytes(bitmap);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Gets the appropriate file extension for the current format.
    /// </summary>
    public string GetFileExtension()
    {
        return _options.Format switch
        {
            ImageFormat.Png => ".png",
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Webp => ".webp",
            _ => ".png"
        };
    }

    /// <summary>
    /// Gets the MIME type for the current format.
    /// </summary>
    public string GetMimeType()
    {
        return _options.Format switch
        {
            ImageFormat.Png => "image/png",
            ImageFormat.Jpeg => "image/jpeg",
            ImageFormat.Webp => "image/webp",
            _ => "image/png"
        };
    }

    /// <summary>
    /// Resizes a bitmap to the specified dimensions.
    /// </summary>
    /// <param name="bitmap">Source bitmap</param>
    /// <param name="width">Target width</param>
    /// <param name="height">Target height</param>
    /// <returns>Resized bitmap (caller must dispose)</returns>
    public static SKBitmap Resize(SKBitmap bitmap, int width, int height)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        var resized = new SKBitmap(width, height);
        using var canvas = new SKCanvas(resized);
        canvas.DrawBitmap(bitmap, new SKRect(0, 0, width, height));
        return resized;
    }

    /// <summary>
    /// Scales a bitmap by a factor.
    /// </summary>
    /// <param name="bitmap">Source bitmap</param>
    /// <param name="scale">Scale factor (e.g., 2.0 for 2x size)</param>
    /// <returns>Scaled bitmap (caller must dispose)</returns>
    public static SKBitmap Scale(SKBitmap bitmap, float scale)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale));

        int newWidth = (int)(bitmap.Width * scale);
        int newHeight = (int)(bitmap.Height * scale);

        return Resize(bitmap, newWidth, newHeight);
    }

    private SKBitmap FlattenAlpha(SKBitmap source)
    {
        var flattened = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(flattened);

        canvas.Clear(_options.BackgroundColor);
        canvas.DrawBitmap(source, 0, 0);

        return flattened;
    }

    private static SKEncodedImageFormat GetSkiaFormat(ImageFormat format)
    {
        return format switch
        {
            ImageFormat.Png => SKEncodedImageFormat.Png,
            ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Webp => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        };
    }
}
