using System;
using System.Collections.Generic;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace MilMap.Core.Export;

/// <summary>
/// Represents a single page in a multi-page map sheet.
/// </summary>
public class MapSheet
{
    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Row index in the grid (0-based, top to bottom).
    /// </summary>
    public int Row { get; init; }

    /// <summary>
    /// Column index in the grid (0-based, left to right).
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// Minimum latitude of this sheet.
    /// </summary>
    public double MinLat { get; init; }

    /// <summary>
    /// Maximum latitude of this sheet.
    /// </summary>
    public double MaxLat { get; init; }

    /// <summary>
    /// Minimum longitude of this sheet.
    /// </summary>
    public double MinLon { get; init; }

    /// <summary>
    /// Maximum longitude of this sheet.
    /// </summary>
    public double MaxLon { get; init; }

    /// <summary>
    /// The rendered map image for this sheet.
    /// </summary>
    public SKBitmap? MapImage { get; set; }

    /// <summary>
    /// Optional scale bar image for this sheet.
    /// </summary>
    public SKBitmap? ScaleBarImage { get; set; }

    /// <summary>
    /// Sheet identifier (e.g., "A1", "B2").
    /// </summary>
    public string SheetId => $"{(char)('A' + Column)}{Row + 1}";
}

/// <summary>
/// Options for multi-page PDF export.
/// </summary>
public class MultiPagePdfOptions : PdfExportOptions
{
    /// <summary>
    /// Overlap between adjacent sheets in meters.
    /// This ensures features at page boundaries are visible on both pages.
    /// </summary>
    public double OverlapMeters { get; set; } = 200;

    /// <summary>
    /// Include an index page showing all sheets.
    /// </summary>
    public bool IncludeIndexPage { get; set; } = true;

    /// <summary>
    /// Include sheet identifiers on each page (e.g., "Sheet A1 of 6").
    /// </summary>
    public bool IncludeSheetLabels { get; set; } = true;

    /// <summary>
    /// Include adjacent sheet references in margins.
    /// </summary>
    public bool IncludeAdjacentReferences { get; set; } = true;

    /// <summary>
    /// Maximum columns in the sheet grid. If null, calculated automatically.
    /// </summary>
    public int? MaxColumns { get; set; }

    /// <summary>
    /// Maximum rows in the sheet grid. If null, calculated automatically.
    /// </summary>
    public int? MaxRows { get; set; }
}

/// <summary>
/// Result of multi-page PDF layout calculation.
/// </summary>
public class MultiPageLayout
{
    /// <summary>
    /// Number of columns in the sheet grid.
    /// </summary>
    public int Columns { get; init; }

    /// <summary>
    /// Number of rows in the sheet grid.
    /// </summary>
    public int Rows { get; init; }

    /// <summary>
    /// Total number of sheets.
    /// </summary>
    public int TotalSheets => Columns * Rows;

    /// <summary>
    /// Width of each sheet in meters (before overlap).
    /// </summary>
    public double SheetWidthMeters { get; init; }

    /// <summary>
    /// Height of each sheet in meters (before overlap).
    /// </summary>
    public double SheetHeightMeters { get; init; }

    /// <summary>
    /// Individual sheet definitions.
    /// </summary>
    public IReadOnlyList<MapSheet> Sheets { get; init; } = Array.Empty<MapSheet>();
}

/// <summary>
/// Exports maps to multi-page PDF format for large areas.
/// </summary>
public class MultiPagePdfExporter
{
    private const double MetersPerDegreeLatitude = 111320;
    private readonly MultiPagePdfOptions _options;

    static MultiPagePdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public MultiPagePdfExporter() : this(new MultiPagePdfOptions()) { }

    public MultiPagePdfExporter(MultiPagePdfOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Calculates the optimal page layout for a given bounding box.
    /// </summary>
    /// <param name="minLat">Minimum latitude of the area.</param>
    /// <param name="maxLat">Maximum latitude of the area.</param>
    /// <param name="minLon">Minimum longitude of the area.</param>
    /// <param name="maxLon">Maximum longitude of the area.</param>
    /// <param name="scaleRatio">Map scale ratio (e.g., 25000 for 1:25,000).</param>
    /// <returns>The calculated layout with sheet definitions.</returns>
    public MultiPageLayout CalculateLayout(
        double minLat, double maxLat,
        double minLon, double maxLon,
        int scaleRatio)
    {
        // Calculate area dimensions in meters
        double centerLat = (minLat + maxLat) / 2;
        double metersPerDegreeLon = MetersPerDegreeLatitude * Math.Cos(centerLat * Math.PI / 180);

        double areaWidthMeters = (maxLon - minLon) * metersPerDegreeLon;
        double areaHeightMeters = (maxLat - minLat) * MetersPerDegreeLatitude;

        // Calculate printable area dimensions in meters at scale
        var (printableWidthPixels, printableHeightPixels) = GetPrintableAreaPixels();
        double printableWidthInches = (double)printableWidthPixels / _options.Dpi;
        double printableHeightInches = (double)printableHeightPixels / _options.Dpi;

        // Ground distance per sheet (inches * scale ratio / inches per meter)
        double sheetWidthMeters = printableWidthInches * scaleRatio * 0.0254;
        double sheetHeightMeters = printableHeightInches * scaleRatio * 0.0254;

        // Account for overlap
        double effectiveWidthMeters = sheetWidthMeters - _options.OverlapMeters;
        double effectiveHeightMeters = sheetHeightMeters - _options.OverlapMeters;

        // Calculate number of sheets needed
        int columns = Math.Max(1, (int)Math.Ceiling(areaWidthMeters / effectiveWidthMeters));
        int rows = Math.Max(1, (int)Math.Ceiling(areaHeightMeters / effectiveHeightMeters));

        // Apply limits if specified
        if (_options.MaxColumns.HasValue)
            columns = Math.Min(columns, _options.MaxColumns.Value);
        if (_options.MaxRows.HasValue)
            rows = Math.Min(rows, _options.MaxRows.Value);

        // Generate sheet definitions
        var sheets = new List<MapSheet>();
        int pageNumber = 1;

        // Recalculate effective dimensions to cover the area exactly
        double actualSheetWidthDeg = (maxLon - minLon) / columns + _options.OverlapMeters / metersPerDegreeLon;
        double actualSheetHeightDeg = (maxLat - minLat) / rows + _options.OverlapMeters / MetersPerDegreeLatitude;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                double sheetMinLon = minLon + col * (maxLon - minLon) / columns;
                double sheetMaxLon = Math.Min(maxLon, sheetMinLon + actualSheetWidthDeg);

                // Rows go from top (north) to bottom (south)
                double sheetMaxLat = maxLat - row * (maxLat - minLat) / rows;
                double sheetMinLat = Math.Max(minLat, sheetMaxLat - actualSheetHeightDeg);

                sheets.Add(new MapSheet
                {
                    PageNumber = pageNumber++,
                    Row = row,
                    Column = col,
                    MinLat = sheetMinLat,
                    MaxLat = sheetMaxLat,
                    MinLon = sheetMinLon,
                    MaxLon = sheetMaxLon
                });
            }
        }

        return new MultiPageLayout
        {
            Columns = columns,
            Rows = rows,
            SheetWidthMeters = sheetWidthMeters,
            SheetHeightMeters = sheetHeightMeters,
            Sheets = sheets
        };
    }

    /// <summary>
    /// Exports multiple map sheets to a single PDF file.
    /// </summary>
    /// <param name="layout">The layout containing sheet definitions and images.</param>
    /// <param name="outputPath">Path to save the PDF file.</param>
    public void Export(MultiPageLayout layout, string outputPath)
    {
        var pdfBytes = ExportToBytes(layout);
        File.WriteAllBytes(outputPath, pdfBytes);
    }

    /// <summary>
    /// Exports multiple map sheets to a PDF and returns the bytes.
    /// </summary>
    public byte[] ExportToBytes(MultiPageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Sheets.Count == 0)
            throw new ArgumentException("No sheets to export", nameof(layout));

        var (pageWidth, pageHeight) = GetPageDimensions();
        float marginPoints = _options.MarginInches * 72;

        var document = Document.Create(container =>
        {
            // Index page if enabled
            if (_options.IncludeIndexPage && layout.TotalSheets > 1)
            {
                container.Page(page =>
                {
                    page.Size(pageWidth, pageHeight, Unit.Point);
                    page.Margin(marginPoints, Unit.Point);
                    page.Header().Element(h => ComposeIndexHeader(h, layout));
                    page.Content().Element(c => ComposeIndexContent(c, layout));
                    page.Footer().AlignCenter().Text("Index Page").FontSize(10);
                });
            }

            // Individual map sheets
            foreach (var sheet in layout.Sheets)
            {
                container.Page(page =>
                {
                    page.Size(pageWidth, pageHeight, Unit.Point);
                    page.Margin(marginPoints, Unit.Point);
                    page.Header().Element(h => ComposeSheetHeader(h, sheet, layout));
                    page.Content().Element(c => ComposeSheetContent(c, sheet));
                    page.Footer().Element(f => ComposeSheetFooter(f, sheet, layout));
                });
            }
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

    /// <summary>
    /// Exports each sheet to a separate PDF file.
    /// </summary>
    /// <param name="layout">The layout containing sheet definitions and images.</param>
    /// <param name="outputDirectory">Directory to save the PDF files.</param>
    /// <param name="filenamePrefix">Prefix for each filename (e.g., "map" produces "map_A1.pdf").</param>
    public void ExportSeparateFiles(MultiPageLayout layout, string outputDirectory, string filenamePrefix)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Directory.CreateDirectory(outputDirectory);

        foreach (var sheet in layout.Sheets)
        {
            var singleLayout = new MultiPageLayout
            {
                Columns = 1,
                Rows = 1,
                SheetWidthMeters = layout.SheetWidthMeters,
                SheetHeightMeters = layout.SheetHeightMeters,
                Sheets = new[] { sheet }
            };

            var includeIndex = _options.IncludeIndexPage;
            _options.IncludeIndexPage = false;

            var pdfBytes = ExportToBytes(singleLayout);
            var filename = $"{filenamePrefix}_{sheet.SheetId}.pdf";
            File.WriteAllBytes(Path.Combine(outputDirectory, filename), pdfBytes);

            _options.IncludeIndexPage = includeIndex;
        }
    }

    private void ComposeIndexHeader(IContainer container, MultiPageLayout layout)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrEmpty(_options.Title))
            {
                column.Item().Text(_options.Title)
                    .FontSize(18)
                    .Bold()
                    .AlignCenter();
            }

            column.Item().Text("Sheet Index")
                .FontSize(14)
                .AlignCenter();

            column.Item().PaddingBottom(10);
        });
    }

    private void ComposeIndexContent(IContainer container, MultiPageLayout layout)
    {
        container.Column(column =>
        {
            // Draw grid overview
            column.Item().AlignCenter().Row(row =>
            {
                row.AutoItem().Border(1).Padding(5).Column(grid =>
                {
                    for (int r = 0; r < layout.Rows; r++)
                    {
                        grid.Item().Row(gridRow =>
                        {
                            for (int c = 0; c < layout.Columns; c++)
                            {
                                var sheet = layout.Sheets[r * layout.Columns + c];
                                gridRow.AutoItem()
                                    .Border(1)
                                    .Width(60)
                                    .Height(40)
                                    .AlignCenter()
                                    .AlignMiddle()
                                    .Text(sheet.SheetId)
                                    .FontSize(12)
                                    .Bold();
                            }
                        });
                    }
                });
            });

            column.Item().PaddingTop(20);

            // Sheet listing with coordinates
            column.Item().Text("Sheet Details:").FontSize(12).Bold();
            column.Item().PaddingTop(5);

            foreach (var sheet in layout.Sheets)
            {
                column.Item().Text(text =>
                {
                    text.Span($"Sheet {sheet.SheetId}: ").Bold().FontSize(9);
                    text.Span($"{sheet.MinLat:F4}°N to {sheet.MaxLat:F4}°N, ").FontSize(9);
                    text.Span($"{sheet.MinLon:F4}°E to {sheet.MaxLon:F4}°E").FontSize(9);
                });
            }

            if (!string.IsNullOrEmpty(_options.ScaleText))
            {
                column.Item().PaddingTop(15);
                column.Item().Text($"Scale: {_options.ScaleText}").FontSize(10);
            }

            column.Item().PaddingTop(10);
            column.Item().Text($"Total sheets: {layout.TotalSheets} ({layout.Columns} × {layout.Rows})").FontSize(10);
        });
    }

    private void ComposeSheetHeader(IContainer container, MapSheet sheet, MultiPageLayout layout)
    {
        container.Row(row =>
        {
            // Title on left
            row.RelativeItem().Column(col =>
            {
                if (!string.IsNullOrEmpty(_options.Title))
                {
                    col.Item().Text(_options.Title)
                        .FontSize(14)
                        .Bold();
                }

                if (!string.IsNullOrEmpty(_options.Subtitle))
                {
                    col.Item().Text(_options.Subtitle)
                        .FontSize(9);
                }
            });

            // Sheet identifier on right
            if (_options.IncludeSheetLabels && layout.TotalSheets > 1)
            {
                row.AutoItem().AlignRight().Column(col =>
                {
                    col.Item().Text($"Sheet {sheet.SheetId}")
                        .FontSize(12)
                        .Bold();
                    col.Item().Text($"Page {sheet.PageNumber} of {layout.TotalSheets}")
                        .FontSize(9);
                });
            }
        });
    }

    private void ComposeSheetContent(IContainer container, MapSheet sheet)
    {
        if (sheet.MapImage == null)
        {
            container.AlignCenter().AlignMiddle()
                .Text($"[Map image for sheet {sheet.SheetId}]")
                .FontSize(14);
            return;
        }

        // Convert SKBitmap to byte array
        using var stream = new MemoryStream();
        using (var image = SKImage.FromBitmap(sheet.MapImage))
        using (var data = image.Encode(SKEncodedImageFormat.Png, _options.ImageQuality))
        {
            data.SaveTo(stream);
        }

        container.Image(stream.ToArray()).FitArea();
    }

    private void ComposeSheetFooter(IContainer container, MapSheet sheet, MultiPageLayout layout)
    {
        container.Column(col =>
        {
            // Adjacent sheet references
            if (_options.IncludeAdjacentReferences && layout.TotalSheets > 1)
            {
                col.Item().Row(row =>
                {
                    // Left reference
                    if (sheet.Column > 0)
                    {
                        var leftSheet = layout.Sheets[sheet.Row * layout.Columns + sheet.Column - 1];
                        row.RelativeItem().AlignLeft()
                            .Text($"← Sheet {leftSheet.SheetId}")
                            .FontSize(8);
                    }
                    else
                    {
                        row.RelativeItem();
                    }

                    // Up/Down references in center
                    row.RelativeItem().AlignCenter().Column(centerCol =>
                    {
                        if (sheet.Row > 0)
                        {
                            var upSheet = layout.Sheets[(sheet.Row - 1) * layout.Columns + sheet.Column];
                            centerCol.Item().Text($"↑ Sheet {upSheet.SheetId}").FontSize(8);
                        }
                        if (sheet.Row < layout.Rows - 1)
                        {
                            var downSheet = layout.Sheets[(sheet.Row + 1) * layout.Columns + sheet.Column];
                            centerCol.Item().Text($"↓ Sheet {downSheet.SheetId}").FontSize(8);
                        }
                    });

                    // Right reference
                    if (sheet.Column < layout.Columns - 1)
                    {
                        var rightSheet = layout.Sheets[sheet.Row * layout.Columns + sheet.Column + 1];
                        row.RelativeItem().AlignRight()
                            .Text($"Sheet {rightSheet.SheetId} →")
                            .FontSize(8);
                    }
                    else
                    {
                        row.RelativeItem();
                    }
                });
            }

            // Bottom row with scale bar and info
            col.Item().Row(row =>
            {
                // Scale bar
                if (_options.IncludeScaleBar && sheet.ScaleBarImage != null)
                {
                    using var stream = new MemoryStream();
                    using (var image = SKImage.FromBitmap(sheet.ScaleBarImage))
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        data.SaveTo(stream);
                    }

                    row.RelativeItem().AlignLeft().MaxHeight(35)
                        .Image(stream.ToArray()).FitArea();
                }
                else
                {
                    row.RelativeItem();
                }

                // Scale text
                if (!string.IsNullOrEmpty(_options.ScaleText))
                {
                    row.RelativeItem().AlignCenter().AlignBottom()
                        .Text(_options.ScaleText).FontSize(9);
                }
                else
                {
                    row.RelativeItem();
                }

                // Coordinates and date
                row.RelativeItem().AlignRight().AlignBottom().Column(infoCol =>
                {
                    infoCol.Item().Text($"{sheet.MinLat:F3}°N - {sheet.MaxLat:F3}°N")
                        .FontSize(7);
                    infoCol.Item().Text($"{sheet.MinLon:F3}°E - {sheet.MaxLon:F3}°E")
                        .FontSize(7);
                    infoCol.Item().Text(DateTime.Now.ToString("yyyy-MM-dd"))
                        .FontSize(7);
                });
            });
        });
    }

    private (float width, float height) GetPageDimensions()
    {
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
                width = 210f / 25.4f * 72;
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

    private (int widthPixels, int heightPixels) GetPrintableAreaPixels()
    {
        var (pageWidth, pageHeight) = GetPageDimensions();
        float marginPoints = _options.MarginInches * 72;

        float printableWidthPoints = pageWidth - 2 * marginPoints;
        float printableHeightPoints = pageHeight - 2 * marginPoints;

        // Account for header and footer
        float headerHeight = string.IsNullOrEmpty(_options.Title) ? 0 : 50;
        float footerHeight = 50;

        printableHeightPoints -= (headerHeight + footerHeight);

        int widthPixels = (int)(printableWidthPoints / 72 * _options.Dpi);
        int heightPixels = (int)(printableHeightPoints / 72 * _options.Dpi);

        return (widthPixels, heightPixels);
    }
}
