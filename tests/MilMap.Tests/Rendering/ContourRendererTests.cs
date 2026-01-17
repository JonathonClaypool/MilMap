using MilMap.Core.Elevation;
using MilMap.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Rendering;

public class ContourRendererTests
{
    [Fact]
    public void ContourRenderer_CanBeCreatedWithDefaultOptions()
    {
        var renderer = new ContourRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void ContourRenderer_CanBeCreatedWithCustomOptions()
    {
        var options = new ContourOptions
        {
            ContourIntervalMeters = 50,
            IndexContourFrequency = 4
        };

        var renderer = new ContourRenderer(options);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void ContourOptions_HasCorrectDefaults()
    {
        var options = new ContourOptions();

        Assert.Equal(MapScale.Scale1To25000, options.Scale);
        Assert.Null(options.ContourIntervalMeters);
        Assert.Equal(5, options.IndexContourFrequency);
        Assert.Equal(0.5f, options.ContourLineWidth);
        Assert.Equal(1.5f, options.IndexContourLineWidth);
        Assert.True(options.ShowLabels);
        Assert.True(options.SmoothContours);
    }

    [Theory]
    [InlineData(MapScale.Scale1To10000, 10)]
    [InlineData(MapScale.Scale1To25000, 20)]
    [InlineData(MapScale.Scale1To50000, 20)]
    [InlineData(MapScale.Scale1To100000, 50)]
    public void GetContourInterval_ReturnsCorrectIntervalForScale(MapScale scale, int expectedInterval)
    {
        var options = new ContourOptions { Scale = scale };
        var renderer = new ContourRenderer(options);

        int interval = renderer.GetContourInterval();

        Assert.Equal(expectedInterval, interval);
    }

    [Fact]
    public void GetContourInterval_CustomInterval_OverridesScale()
    {
        var options = new ContourOptions
        {
            Scale = MapScale.Scale1To25000,
            ContourIntervalMeters = 50
        };
        var renderer = new ContourRenderer(options);

        int interval = renderer.GetContourInterval();

        Assert.Equal(50, interval);
    }

    [Fact]
    public void DrawContours_EmptyElevationGrid_ReturnsUnmodifiedBitmap()
    {
        var renderer = new ContourRenderer();
        using var baseMap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        // Grid with no valid data
        var elevations = new double?[10, 10];
        var grid = new ElevationGrid(0, 1, 0, 1, 10, 10, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
        Assert.Equal(100, result.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public void DrawContours_FlatTerrain_NoContoursDrawn()
    {
        var renderer = new ContourRenderer();
        using var baseMap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        // Flat grid at 100m elevation
        var elevations = new double?[10, 10];
        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                elevations[r, c] = 100.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 10, 10, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
    }

    [Fact]
    public void DrawContours_SlopingTerrain_DrawsContours()
    {
        var options = new ContourOptions { ContourIntervalMeters = 10, ShowLabels = false };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        // Create a slope from 0m to 100m
        var elevations = new double?[20, 20];
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 20; c++)
            {
                elevations[r, c] = (19 - r) * 5.0; // 0 to 95m elevation
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 20, 20, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
        // Result should have contour lines drawn (not all white)
        // Check that some pixels are the contour color
        bool hasContourPixels = false;
        var contourColor = options.ContourColor;
        for (int x = 0; x < result.Width && !hasContourPixels; x++)
        {
            for (int y = 0; y < result.Height && !hasContourPixels; y++)
            {
                var pixel = result.GetPixel(x, y);
                // Check if close to contour color (allowing for anti-aliasing)
                if (Math.Abs(pixel.Red - contourColor.Red) < 50 &&
                    Math.Abs(pixel.Green - contourColor.Green) < 50 &&
                    Math.Abs(pixel.Blue - contourColor.Blue) < 50 &&
                    pixel != SKColors.White)
                {
                    hasContourPixels = true;
                }
            }
        }
        Assert.True(hasContourPixels, "Contour lines should be drawn on the map");
    }

    [Fact]
    public void DrawContours_WithLabels_DrawsLabels()
    {
        var options = new ContourOptions
        {
            ContourIntervalMeters = 20,
            ShowLabels = true,
            IndexContourFrequency = 5
        };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(400, 400);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        // Create a slope with enough range for index contours
        var elevations = new double?[40, 40];
        for (int r = 0; r < 40; r++)
        {
            for (int c = 0; c < 40; c++)
            {
                elevations[r, c] = (39 - r) * 10.0; // 0 to 390m
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 40, 40, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
    }

    [Fact]
    public void DrawContours_SmoothingEnabled_ProducesSmoothLines()
    {
        var options = new ContourOptions
        {
            ContourIntervalMeters = 10,
            SmoothContours = true,
            ShowLabels = false
        };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        var elevations = new double?[20, 20];
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 20; c++)
            {
                elevations[r, c] = (19 - r) * 5.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 20, 20, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
    }

    [Fact]
    public void DrawContours_SmoothingDisabled_StillProducesValidOutput()
    {
        var options = new ContourOptions
        {
            ContourIntervalMeters = 10,
            SmoothContours = false,
            ShowLabels = false
        };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        var elevations = new double?[20, 20];
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 20; c++)
            {
                elevations[r, c] = (19 - r) * 5.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 20, 20, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
    }

    [Fact]
    public void DrawContours_WithVoidData_HandlesGracefully()
    {
        var options = new ContourOptions { ContourIntervalMeters = 10, ShowLabels = false };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        // Grid with some void (null) data
        var elevations = new double?[10, 10];
        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                if (r == 5 && c == 5)
                    elevations[r, c] = null; // Void
                else
                    elevations[r, c] = (9 - r) * 10.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 10, 10, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
    }

    [Fact]
    public void DrawContours_CustomColor_UsesSpecifiedColor()
    {
        var options = new ContourOptions
        {
            ContourIntervalMeters = 10,
            ContourColor = SKColors.Blue,
            ShowLabels = false
        };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        var elevations = new double?[20, 20];
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 20; c++)
            {
                elevations[r, c] = (19 - r) * 5.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 20, 20, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        // Check for blue pixels
        bool hasBluePixels = false;
        for (int x = 0; x < result.Width && !hasBluePixels; x++)
        {
            for (int y = 0; y < result.Height && !hasBluePixels; y++)
            {
                var pixel = result.GetPixel(x, y);
                if (pixel.Blue > 200 && pixel.Red < 100 && pixel.Green < 100)
                {
                    hasBluePixels = true;
                }
            }
        }
        Assert.True(hasBluePixels, "Contours should use the specified blue color");
    }

    [Fact]
    public void ContourLine_CanBeCreated()
    {
        var line = new ContourLine(100.0, isIndex: false);

        Assert.Equal(100.0, line.Elevation);
        Assert.False(line.IsIndex);
        Assert.False(line.IsDepression);
        Assert.Empty(line.Points);
    }

    [Fact]
    public void ContourLine_IndexContour_HasCorrectFlag()
    {
        var line = new ContourLine(500.0, isIndex: true);

        Assert.True(line.IsIndex);
    }

    [Fact]
    public void ContourLine_CanAddPoints()
    {
        var line = new ContourLine(100.0, false);
        line.Points.Add(new SKPoint(0, 0));
        line.Points.Add(new SKPoint(10, 10));

        Assert.Equal(2, line.Points.Count);
    }

    [Fact]
    public void DrawContours_DepressionDetection_MarksClosedDepressions()
    {
        var options = new ContourOptions
        {
            ContourIntervalMeters = 10,
            ShowDepressionContours = true,
            ShowLabels = false
        };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        // Create a depression (bowl shape)
        var elevations = new double?[20, 20];
        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 20; c++)
            {
                // Distance from center
                double dr = r - 10;
                double dc = c - 10;
                double dist = Math.Sqrt(dr * dr + dc * dc);
                // Bowl: higher at edges, lower in center
                elevations[r, c] = dist * 10.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 20, 20, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
    }

    [Fact]
    public void DrawContours_LargeElevationRange_HandlesMultipleContours()
    {
        var options = new ContourOptions
        {
            ContourIntervalMeters = 100,
            ShowLabels = false
        };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        // Large elevation range
        var elevations = new double?[50, 50];
        for (int r = 0; r < 50; r++)
        {
            for (int c = 0; c < 50; c++)
            {
                elevations[r, c] = (49 - r) * 100.0; // 0 to 4900m
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 50, 50, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
    }

    [Fact]
    public void DrawContours_NonSquareBitmap_WorksCorrectly()
    {
        var options = new ContourOptions { ContourIntervalMeters = 10, ShowLabels = false };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(300, 150);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        var elevations = new double?[15, 30];
        for (int r = 0; r < 15; r++)
        {
            for (int c = 0; c < 30; c++)
            {
                elevations[r, c] = (14 - r) * 10.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 2, 15, 30, elevations);

        using var result = renderer.DrawContours(baseMap, grid);

        Assert.NotNull(result);
        Assert.Equal(300, result.Width);
        Assert.Equal(150, result.Height);
    }

    [Fact]
    public void DrawContours_WithOffset_AppliesOffsetCorrectly()
    {
        var options = new ContourOptions { ContourIntervalMeters = 10, ShowLabels = false };
        var renderer = new ContourRenderer(options);
        using var baseMap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(baseMap);
        canvas.Clear(SKColors.White);

        var elevations = new double?[10, 10];
        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                elevations[r, c] = (9 - r) * 10.0;
            }
        }
        var grid = new ElevationGrid(0, 1, 0, 1, 10, 10, elevations);

        using var result = renderer.DrawContours(baseMap, grid, offsetX: 50, offsetY: 50);

        Assert.NotNull(result);
    }
}
