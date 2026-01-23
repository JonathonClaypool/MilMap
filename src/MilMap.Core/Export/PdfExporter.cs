using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace MilMap.Core.Export;

/// <summary>
/// Standard page sizes for map export.
/// </summary>
public enum PageSize
{
    Letter,  // 8.5 x 11 inches
    Legal,   // 8.5 x 14 inches
    Tabloid, // 11 x 17 inches
    A4,      // 210 x 297 mm
    A3,      // 297 x 420 mm
    Custom
}

/// <summary>
/// Page orientation for map export.
/// </summary>
public enum PageOrientation
{
    Portrait,
    Landscape
}

/// <summary>
/// Options for PDF export.
/// </summary>
public class PdfExportOptions
{
    /// <summary>
    /// Output DPI for images. Default is 300 for print quality.
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// Page size preset.
    /// </summary>
    public PageSize PageSize { get; set; } = PageSize.Letter;

    /// <summary>
    /// Page orientation.
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;

    /// <summary>
    /// Custom page width in inches (used when PageSize is Custom).
    /// </summary>
    public float CustomWidthInches { get; set; } = 11f;

    /// <summary>
    /// Custom page height in inches (used when PageSize is Custom).
    /// </summary>
    public float CustomHeightInches { get; set; } = 8.5f;

    /// <summary>
    /// Page margins in inches.
    /// </summary>
    public float MarginInches { get; set; } = 0.5f;

    /// <summary>
    /// Map title to display.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Map subtitle or description.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Include scale bar in output.
    /// </summary>
    public bool IncludeScaleBar { get; set; } = true;

    /// <summary>
    /// Include MGRS grid labels in margins.
    /// </summary>
    public bool IncludeGridLabels { get; set; } = true;

    /// <summary>
    /// Scale ratio text (e.g., "1:25,000").
    /// </summary>
    public string? ScaleText { get; set; }

    /// <summary>
    /// PDF metadata: author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// PDF metadata: subject.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Image compression quality for embedded images (1-100).
    /// </summary>
    public int ImageQuality { get; set; } = 95;
}

/// <summary>
/// Exports maps to PDF format for printing.
/// </summary>
public class PdfExporter
{
    private readonly PdfExportOptions _options;

    static PdfExporter()
    {
        // Configure QuestPDF license
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfExporter() : this(new PdfExportOptions()) { }

    public PdfExporter(PdfExportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Exports a map image to PDF.
    /// </summary>
    /// <param name="mapImage">The rendered map image</param>
    /// <param name="scaleBarImage">Optional scale bar image</param>
    /// <param name="outputPath">Path to save the PDF file</param>
    public void Export(SKBitmap mapImage, SKBitmap? scaleBarImage, string outputPath)
    {
        var pdfBytes = ExportToBytes(mapImage, scaleBarImage);
        File.WriteAllBytes(outputPath, pdfBytes);
    }

    /// <summary>
    /// Exports a map image to PDF and returns the bytes.
    /// </summary>
    public byte[] ExportToBytes(SKBitmap mapImage, SKBitmap? scaleBarImage)
    {
        if (mapImage == null)
            throw new ArgumentNullException(nameof(mapImage));

        var (pageWidth, pageHeight) = GetPageDimensions();
        float marginPoints = _options.MarginInches * 72; // 72 points per inch

        // Convert SKBitmap to byte array for embedding
        using var mapStream = new MemoryStream();
        using (var image = SKImage.FromBitmap(mapImage))
        using (var data = image.Encode(SKEncodedImageFormat.Png, _options.ImageQuality))
        {
            data.SaveTo(mapStream);
        }
        byte[] mapBytes = mapStream.ToArray();

        byte[]? scaleBarBytes = null;
        if (scaleBarImage != null)
        {
            using var scaleStream = new MemoryStream();
            using (var image = SKImage.FromBitmap(scaleBarImage))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                data.SaveTo(scaleStream);
            }
            scaleBarBytes = scaleStream.ToArray();
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageWidth, pageHeight, Unit.Point);
                page.Margin(marginPoints, Unit.Point);

                page.Header().Element(header => ComposeHeader(header));

                page.Content().Element(content => ComposeContent(content, mapBytes));

                page.Footer().Element(footer => ComposeFooter(footer, scaleBarBytes));
            });
        });

        // Set document metadata
        var metadata = new DocumentMetadata
        {
            Creator = "MilMap"
        };

        if (!string.IsNullOrEmpty(_options.Title))
            metadata.Title = _options.Title;
        if (!string.IsNullOrEmpty(_options.Author))
            metadata.Author = _options.Author;
        if (!string.IsNullOrEmpty(_options.Subject))
            metadata.Subject = _options.Subject;

        document.WithMetadata(metadata);

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        if (string.IsNullOrEmpty(_options.Title) && string.IsNullOrEmpty(_options.Subtitle))
        {
            container.PaddingBottom(0);
            return;
        }

        container.Column(column =>
        {
            if (!string.IsNullOrEmpty(_options.Title))
            {
                column.Item().Text(_options.Title)
                    .FontSize(16)
                    .Bold()
                    .AlignCenter();
            }

            if (!string.IsNullOrEmpty(_options.Subtitle))
            {
                column.Item().Text(_options.Subtitle)
                    .FontSize(10)
                    .AlignCenter();
            }

            column.Item().PaddingBottom(10);
        });
    }

    private void ComposeContent(IContainer container, byte[] mapImageBytes)
    {
        // Use Shrink() to prevent the image from expanding beyond available space,
        // then FitArea() to scale proportionally within the constrained area.
        // This avoids QuestPDF constraint conflicts that occur when FitArea() 
        // encounters images whose aspect ratio conflicts with the page layout.
        container
            .Shrink()
            .Image(mapImageBytes)
            .FitArea();
    }

    private void ComposeFooter(IContainer container, byte[]? scaleBarBytes)
    {
        container.Row(row =>
        {
            // Scale bar on the left
            if (_options.IncludeScaleBar && scaleBarBytes != null)
            {
                row.RelativeItem().AlignLeft().AlignBottom()
                    .MaxHeight(40)
                    .Shrink()
                    .Image(scaleBarBytes)
                    .FitArea();
            }
            else
            {
                row.RelativeItem();
            }

            // Scale text in center
            if (!string.IsNullOrEmpty(_options.ScaleText))
            {
                row.RelativeItem().AlignCenter().AlignBottom()
                    .Text(_options.ScaleText)
                    .FontSize(10);
            }
            else
            {
                row.RelativeItem();
            }

            // Date/attribution on right
            row.RelativeItem().AlignRight().AlignBottom()
                .Text(text =>
                {
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd"))
                        .FontSize(8);
                });
        });
    }

    private (float width, float height) GetPageDimensions()
    {
        // Get dimensions in points (72 points per inch)
        float width, height;

        switch (_options.PageSize)
        {
            case PageSize.Letter:
                width = 8.5f * 72;
                height = 11f * 72;
                break;
            case PageSize.Legal:
                width = 8.5f * 72;
                height = 14f * 72;
                break;
            case PageSize.Tabloid:
                width = 11f * 72;
                height = 17f * 72;
                break;
            case PageSize.A4:
                width = 210f / 25.4f * 72; // mm to points
                height = 297f / 25.4f * 72;
                break;
            case PageSize.A3:
                width = 297f / 25.4f * 72;
                height = 420f / 25.4f * 72;
                break;
            case PageSize.Custom:
            default:
                width = _options.CustomWidthInches * 72;
                height = _options.CustomHeightInches * 72;
                break;
        }

        // Apply orientation
        if (_options.Orientation == PageOrientation.Landscape && height > width)
        {
            (width, height) = (height, width);
        }
        else if (_options.Orientation == PageOrientation.Portrait && width > height)
        {
            (width, height) = (height, width);
        }

        return (width, height);
    }

    /// <summary>
    /// Gets the printable area dimensions in pixels at the configured DPI.
    /// </summary>
    public (int widthPixels, int heightPixels) GetPrintableAreaPixels()
    {
        var (pageWidth, pageHeight) = GetPageDimensions();
        float marginPoints = _options.MarginInches * 72;

        float printableWidthPoints = pageWidth - 2 * marginPoints;
        float printableHeightPoints = pageHeight - 2 * marginPoints;

        // Account for header and footer
        float headerHeight = string.IsNullOrEmpty(_options.Title) ? 0 : 40;
        float footerHeight = 40;

        printableHeightPoints -= (headerHeight + footerHeight);

        // Convert points to pixels at target DPI
        int widthPixels = (int)(printableWidthPoints / 72 * _options.Dpi);
        int heightPixels = (int)(printableHeightPoints / 72 * _options.Dpi);

        return (widthPixels, heightPixels);
    }
}
