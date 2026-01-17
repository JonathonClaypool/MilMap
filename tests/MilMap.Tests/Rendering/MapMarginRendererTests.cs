using MilMap.Core.Mgrs;
using MilMap.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Rendering;

public class MapMarginRendererTests
{
    private static SKBitmap CreateTestMap(int width = 512, int height = 512)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightGray);
        
        // Draw a simple pattern to verify the map is centered
        using var paint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke };
        canvas.DrawRect(10, 10, width - 20, height - 20, paint);
        
        return bitmap;
    }

    [Fact]
    public void AddMargins_IncreasesImageSize()
    {
        using var map = CreateTestMap(512, 512);
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            TopMarginHeight = 80,
            BottomMarginHeight = 120,
            LeftMarginWidth = 40,
            RightMarginWidth = 40
        });

        using var result = renderer.AddMargins(map);

        Assert.Equal(512 + 40 + 40, result.Width);
        Assert.Equal(512 + 80 + 120, result.Height);
    }

    [Fact]
    public void AddMargins_WithTitle_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            Title = "Test Map Title",
            Subtitle = "Test Subtitle",
            ScaleRatio = 25000
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
        Assert.True(result.Width > 0);
        Assert.True(result.Height > 0);
    }

    [Fact]
    public void AddMargins_WithMgrsZone_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            Title = "Washington DC Area",
            MgrsZone = "18T",
            CenterLat = 38.9,
            CenterLon = -77.0,
            ScaleRatio = 25000
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddMargins_WithDeclination_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            ShowDeclination = true,
            CenterLat = 40.0,
            CenterLon = -75.0
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddMargins_WithScaleBar_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            ShowScaleBar = true,
            ScaleRatio = 50000,
            Dpi = 300
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddMargins_WithDate_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            ShowDate = true,
            Date = new DateTime(2025, 1, 15)
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddMargins_WithDatum_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            ShowDatum = true
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddMargins_WithNotes_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            Notes = "For training use only"
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddMargins_WithAllOptions_RendersSuccessfully()
    {
        using var map = CreateTestMap(800, 600);
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            Title = "Fort Liberty Training Area",
            Subtitle = "North Carolina, USA",
            MgrsZone = "17SQV",
            ScaleRatio = 25000,
            Dpi = 300,
            CenterLat = 35.14,
            CenterLon = -79.01,
            ShowDeclination = true,
            ShowScaleBar = true,
            ShowDatum = true,
            ShowDate = true,
            Date = new DateTime(2025, 6, 15),
            Notes = "Contour interval 10 meters"
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
        Assert.True(result.Width > map.Width);
        Assert.True(result.Height > map.Height);
    }

    [Fact]
    public void AddMargins_WithoutOptionalElements_RendersSuccessfully()
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            ShowDeclination = false,
            ShowScaleBar = false,
            ShowDatum = false,
            ShowDate = false
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void CreateMapSheet_SetsZoneFromBounds()
    {
        using var map = CreateTestMap();
        var options = new MapMarginOptions
        {
            Title = "Test Map"
        };
        var renderer = new MapMarginRenderer(options);

        var bounds = new BoundingBox(38.8, 38.9, -77.1, -77.0);
        using var result = renderer.CreateMapSheet(map, bounds);

        Assert.NotNull(result);
        // Zone should be set based on center of bounds (-77.05 lon, 38.85 lat)
        Assert.Equal("18S", options.MgrsZone);
    }

    [Fact]
    public void MapMarginOptions_HasCorrectDefaults()
    {
        var options = new MapMarginOptions();

        Assert.Equal(25000, options.ScaleRatio);
        Assert.Equal(300, options.Dpi);
        Assert.Equal(80, options.TopMarginHeight);
        Assert.Equal(120, options.BottomMarginHeight);
        Assert.Equal(40, options.LeftMarginWidth);
        Assert.Equal(40, options.RightMarginWidth);
        Assert.True(options.ShowDeclination);
        Assert.True(options.ShowScaleBar);
        Assert.True(options.ShowDatum);
        Assert.True(options.ShowDate);
        Assert.Null(options.Date);
        Assert.Null(options.Title);
    }

    [Theory]
    [InlineData(512, 512)]
    [InlineData(1024, 768)]
    [InlineData(256, 256)]
    public void AddMargins_VariousSizes_MaintainsCorrectDimensions(int width, int height)
    {
        using var map = CreateTestMap(width, height);
        var options = new MapMarginOptions
        {
            TopMarginHeight = 80,
            BottomMarginHeight = 120,
            LeftMarginWidth = 40,
            RightMarginWidth = 40
        };
        var renderer = new MapMarginRenderer(options);

        using var result = renderer.AddMargins(map);

        int expectedWidth = width + options.LeftMarginWidth + options.RightMarginWidth;
        int expectedHeight = height + options.TopMarginHeight + options.BottomMarginHeight;
        
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(25000)]
    [InlineData(50000)]
    [InlineData(100000)]
    public void AddMargins_VariousScales_RendersSuccessfully(int scale)
    {
        using var map = CreateTestMap();
        var renderer = new MapMarginRenderer(new MapMarginOptions
        {
            ScaleRatio = scale,
            ShowScaleBar = true
        });

        using var result = renderer.AddMargins(map);

        Assert.NotNull(result);
    }

    [Fact]
    public void AddMargins_NullMap_ThrowsArgumentNullException()
    {
        var renderer = new MapMarginRenderer();

        Assert.Throws<ArgumentNullException>(() => renderer.AddMargins(null!));
    }
}
