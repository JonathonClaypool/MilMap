using MilMap.Core.Export;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Export;

public class PdfExporterTests
{
    private static SKBitmap CreateTestBitmap(int width = 800, int height = 600)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightBlue);

        using var paint = new SKPaint
        {
            Color = SKColors.DarkBlue,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };

        // Draw grid
        for (int x = 0; x < width; x += 50)
            canvas.DrawLine(x, 0, x, height, paint);
        for (int y = 0; y < height; y += 50)
            canvas.DrawLine(0, y, width, y, paint);

        return bitmap;
    }

    private static SKBitmap CreateScaleBarBitmap()
    {
        var bitmap = new SKBitmap(200, 30);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill
        };

        // Draw simple scale bar segments
        canvas.DrawRect(0, 10, 40, 10, paint);
        canvas.DrawRect(80, 10, 40, 10, paint);
        canvas.DrawRect(160, 10, 40, 10, paint);

        return bitmap;
    }

    [Fact]
    public void ExportToBytes_ValidInput_ReturnsPdfBytes()
    {
        using var mapBitmap = CreateTestBitmap();
        var exporter = new PdfExporter();

        var pdfBytes = exporter.ExportToBytes(mapBitmap, null);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
        // PDF header check
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }

    [Fact]
    public void ExportToBytes_WithScaleBar_ReturnsPdfBytes()
    {
        using var mapBitmap = CreateTestBitmap();
        using var scaleBar = CreateScaleBarBitmap();
        var exporter = new PdfExporter();

        var pdfBytes = exporter.ExportToBytes(mapBitmap, scaleBar);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
    }

    [Fact]
    public void ExportToBytes_WithOptions_ReturnsPdfBytes()
    {
        using var mapBitmap = CreateTestBitmap();
        var options = new PdfExportOptions
        {
            Title = "Test Map",
            Subtitle = "Test Subtitle",
            ScaleText = "Scale 1:25,000",
            PageSize = PageSize.Letter,
            Orientation = PageOrientation.Landscape
        };
        var exporter = new PdfExporter(options);

        var pdfBytes = exporter.ExportToBytes(mapBitmap, null);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
    }

    [Fact]
    public void ExportToBytes_NullMap_ThrowsArgumentNullException()
    {
        var exporter = new PdfExporter();

        Assert.Throws<ArgumentNullException>(() => exporter.ExportToBytes(null!, null));
    }

    [Theory]
    [InlineData(PageSize.Letter)]
    [InlineData(PageSize.A4)]
    [InlineData(PageSize.Tabloid)]
    [InlineData(PageSize.A3)]
    public void ExportToBytes_VariousPageSizes_ReturnsPdfBytes(PageSize pageSize)
    {
        using var mapBitmap = CreateTestBitmap();
        var options = new PdfExportOptions { PageSize = pageSize };
        var exporter = new PdfExporter(options);

        var pdfBytes = exporter.ExportToBytes(mapBitmap, null);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
    }

    [Theory]
    [InlineData(PageOrientation.Portrait)]
    [InlineData(PageOrientation.Landscape)]
    public void ExportToBytes_BothOrientations_ReturnsPdfBytes(PageOrientation orientation)
    {
        using var mapBitmap = CreateTestBitmap();
        var options = new PdfExportOptions { Orientation = orientation };
        var exporter = new PdfExporter(options);

        var pdfBytes = exporter.ExportToBytes(mapBitmap, null);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
    }

    [Fact]
    public void GetPrintableAreaPixels_ReturnsPositiveDimensions()
    {
        var exporter = new PdfExporter();

        var (width, height) = exporter.GetPrintableAreaPixels();

        Assert.True(width > 0);
        Assert.True(height > 0);
    }

    [Fact]
    public void GetPrintableAreaPixels_HigherDpi_LargerDimensions()
    {
        var lowDpi = new PdfExporter(new PdfExportOptions { Dpi = 72 });
        var highDpi = new PdfExporter(new PdfExportOptions { Dpi = 300 });

        var (lowWidth, lowHeight) = lowDpi.GetPrintableAreaPixels();
        var (highWidth, highHeight) = highDpi.GetPrintableAreaPixels();

        Assert.True(highWidth > lowWidth);
        Assert.True(highHeight > lowHeight);
    }

    [Fact]
    public void GetPrintableAreaPixels_LandscapeWiderThanTall()
    {
        var options = new PdfExportOptions
        {
            PageSize = PageSize.Letter,
            Orientation = PageOrientation.Landscape
        };
        var exporter = new PdfExporter(options);

        var (width, height) = exporter.GetPrintableAreaPixels();

        Assert.True(width > height);
    }

    [Fact]
    public void GetPrintableAreaPixels_PortraitTallerThanWide()
    {
        var options = new PdfExportOptions
        {
            PageSize = PageSize.Letter,
            Orientation = PageOrientation.Portrait
        };
        var exporter = new PdfExporter(options);

        var (width, height) = exporter.GetPrintableAreaPixels();

        Assert.True(height > width);
    }

    [Fact]
    public void PdfExportOptions_HasCorrectDefaults()
    {
        var options = new PdfExportOptions();

        Assert.Equal(300, options.Dpi);
        Assert.Equal(PageSize.Letter, options.PageSize);
        Assert.Equal(PageOrientation.Landscape, options.Orientation);
        Assert.Equal(0.5f, options.MarginInches);
        Assert.True(options.IncludeScaleBar);
        Assert.True(options.IncludeGridLabels);
        Assert.Equal(95, options.ImageQuality);
    }

    [Fact]
    public void Export_CreatesFile()
    {
        using var mapBitmap = CreateTestBitmap();
        var exporter = new PdfExporter();

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_map_{Guid.NewGuid()}.pdf");
        try
        {
            exporter.Export(mapBitmap, null, tempPath);

            Assert.True(File.Exists(tempPath));
            var fileInfo = new FileInfo(tempPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void PdfExporter_CanBeCreatedWithDefaultOptions()
    {
        var exporter = new PdfExporter();
        Assert.NotNull(exporter);
    }

    [Fact]
    public void PdfExporter_CanBeCreatedWithCustomOptions()
    {
        var options = new PdfExportOptions
        {
            Dpi = 150,
            PageSize = PageSize.A4,
            Title = "My Map"
        };

        var exporter = new PdfExporter(options);
        Assert.NotNull(exporter);
    }

    [Theory]
    [InlineData(2000, 100)]   // Very wide image
    [InlineData(100, 2000)]   // Very tall image
    [InlineData(5000, 5000)]  // Large square image
    [InlineData(50, 50)]      // Small square image
    public void ExportToBytes_ExtremeAspectRatios_DoesNotThrow(int width, int height)
    {
        // Regression test for QuestPDF conflicting size constraints error
        using var mapBitmap = CreateTestBitmap(width, height);
        var options = new PdfExportOptions
        {
            Title = "Test Map",
            PageSize = PageSize.Letter,
            Orientation = PageOrientation.Landscape
        };
        var exporter = new PdfExporter(options);

        var pdfBytes = exporter.ExportToBytes(mapBitmap, null);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
    }
}
