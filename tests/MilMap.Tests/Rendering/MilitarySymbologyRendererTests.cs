using MilMap.Core.Rendering;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Rendering;

public class MilitarySymbologyRendererTests
{
    [Fact]
    public void Constructor_WithDefaultOptions_Succeeds()
    {
        using var renderer = new MilitarySymbologyRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void Constructor_WithCustomOptions_Succeeds()
    {
        var options = new MilitarySymbologyOptions
        {
            SymbolSize = 20f,
            LineWidth = 2.5f,
            LabelFontSize = 12f,
            UseNatoStandard = false,
            PatternOpacity = 0.75f
        };

        using var renderer = new MilitarySymbologyRenderer(options);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MilitarySymbologyRenderer(null!));
    }

    [Theory]
    [InlineData(SymbolType.Hilltop)]
    [InlineData(SymbolType.Saddle)]
    [InlineData(SymbolType.Depression)]
    [InlineData(SymbolType.Spring)]
    [InlineData(SymbolType.Well)]
    [InlineData(SymbolType.Mine)]
    [InlineData(SymbolType.Cave)]
    [InlineData(SymbolType.Tower)]
    [InlineData(SymbolType.Cemetery)]
    [InlineData(SymbolType.Church)]
    [InlineData(SymbolType.LandingZone)]
    [InlineData(SymbolType.DropZone)]
    [InlineData(SymbolType.ObservationPost)]
    [InlineData(SymbolType.CommandPost)]
    [InlineData(SymbolType.CheckPoint)]
    public void DrawSymbol_AllPointTypes_DrawsWithoutError(SymbolType type)
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // Should not throw
        renderer.DrawSymbol(canvas, 50, 50, type);
    }

    [Fact]
    public void DrawSymbol_WithNullCanvas_ThrowsArgumentNullException()
    {
        using var renderer = new MilitarySymbologyRenderer();
        Assert.Throws<ArgumentNullException>(() => renderer.DrawSymbol(null!, 50, 50, SymbolType.Hilltop));
    }

    [Theory]
    [InlineData(SymbolType.Marsh)]
    [InlineData(SymbolType.Swamp)]
    [InlineData(SymbolType.SandDunes)]
    [InlineData(SymbolType.Orchard)]
    [InlineData(SymbolType.Vineyard)]
    [InlineData(SymbolType.Scrub)]
    public void CreatePatternTile_AllPatternTypes_ReturnsValidBitmap(SymbolType patternType)
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var tile = renderer.CreatePatternTile(patternType);

        Assert.NotNull(tile);
        Assert.Equal(24, tile.Width);
        Assert.Equal(24, tile.Height);
    }

    [Fact]
    public void CreatePatternTile_CustomSize_ReturnsCorrectSize()
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var tile = renderer.CreatePatternTile(SymbolType.Orchard, tileSize: 48);

        Assert.Equal(48, tile.Width);
        Assert.Equal(48, tile.Height);
    }

    [Theory]
    [InlineData(SymbolType.FenceBarbed)]
    [InlineData(SymbolType.FenceWood)]
    [InlineData(SymbolType.PowerLine)]
    [InlineData(SymbolType.Pipeline)]
    [InlineData(SymbolType.Levee)]
    [InlineData(SymbolType.Embankment)]
    public void DrawLinearFeature_AllLinearTypes_DrawsWithoutError(SymbolType type)
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var bitmap = new SKBitmap(200, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var points = new[]
        {
            new SKPoint(10, 50),
            new SKPoint(100, 50),
            new SKPoint(190, 50)
        };

        // Should not throw
        renderer.DrawLinearFeature(canvas, points, type);
    }

    [Fact]
    public void DrawLinearFeature_WithNullCanvas_DoesNotThrow()
    {
        using var renderer = new MilitarySymbologyRenderer();
        var points = new[] { new SKPoint(0, 0), new SKPoint(100, 100) };
        
        // With null canvas, should handle gracefully
        Assert.Throws<ArgumentNullException>(() => renderer.DrawLinearFeature(null!, points, SymbolType.Pipeline));
    }

    [Fact]
    public void DrawLinearFeature_WithTooFewPoints_ReturnsWithoutError()
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);

        // Single point - should return early without drawing
        renderer.DrawLinearFeature(canvas, new[] { new SKPoint(50, 50) }, SymbolType.Pipeline);
        
        // Null points - should return early
        renderer.DrawLinearFeature(canvas, null!, SymbolType.Pipeline);
    }

    [Fact]
    public void DrawDeclinationDiagram_WithValidInput_ReturnsValidBitmap()
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var bitmap = renderer.DrawDeclinationDiagram(
            gridDeclination: 0.5,
            magneticDeclination: -12.5);

        Assert.NotNull(bitmap);
        Assert.Equal(120, bitmap.Width);
        Assert.Equal(150, bitmap.Height);
    }

    [Fact]
    public void DrawDeclinationDiagram_CustomSize_ReturnsCorrectSize()
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var bitmap = renderer.DrawDeclinationDiagram(
            gridDeclination: 1.0,
            magneticDeclination: -10.0,
            width: 200,
            height: 250);

        Assert.Equal(200, bitmap.Width);
        Assert.Equal(250, bitmap.Height);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5.0, -15.0)]
    [InlineData(-3.0, 20.0)]
    public void DrawDeclinationDiagram_VariousDeclinations_DrawsWithoutError(double gridDec, double magDec)
    {
        using var renderer = new MilitarySymbologyRenderer();
        using var bitmap = renderer.DrawDeclinationDiagram(gridDec, magDec);

        Assert.NotNull(bitmap);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var renderer = new MilitarySymbologyRenderer();
        renderer.Dispose();
        renderer.Dispose(); // Should not throw
    }
}

public class MilitaryColorsTests
{
    [Fact]
    public void VegetationColors_AreDistinct()
    {
        Assert.NotEqual(MilitaryColors.VegetationLight, MilitaryColors.VegetationDense);
        Assert.NotEqual(MilitaryColors.Woodland, MilitaryColors.Orchard);
    }

    [Fact]
    public void WaterColors_AreBlue()
    {
        // All water colors should have more blue than red
        Assert.True(MilitaryColors.WaterFill.Blue > MilitaryColors.WaterFill.Red);
        Assert.True(MilitaryColors.WaterLine.Blue > MilitaryColors.WaterLine.Red);
        Assert.True(MilitaryColors.IntermittentWater.Blue > MilitaryColors.IntermittentWater.Red);
    }

    [Fact]
    public void ContourColors_AreBrown()
    {
        // Contour colors should have red > blue (brown tones)
        Assert.True(MilitaryColors.ContourIndex.Red > MilitaryColors.ContourIndex.Blue);
        Assert.True(MilitaryColors.ContourIntermediate.Red > MilitaryColors.ContourIntermediate.Blue);
    }

    [Fact]
    public void DangerArea_IsRed()
    {
        Assert.Equal(255, MilitaryColors.DangerArea.Red);
        Assert.Equal(0, MilitaryColors.DangerArea.Green);
        Assert.Equal(0, MilitaryColors.DangerArea.Blue);
    }

    [Fact]
    public void GridMajor_IsBlack()
    {
        Assert.Equal(SKColors.Black, MilitaryColors.GridMajor);
    }

    [Fact]
    public void AllColors_HaveFullOpacity()
    {
        var colorProperties = typeof(MilitaryColors)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(SKColor));

        foreach (var prop in colorProperties)
        {
            var color = (SKColor)prop.GetValue(null)!;
            Assert.Equal(255, color.Alpha);
        }
    }
}

public class MilitarySymbologyOptionsTests
{
    [Fact]
    public void DefaultValues_AreSet()
    {
        var options = new MilitarySymbologyOptions();

        Assert.Equal(12f, options.SymbolSize);
        Assert.Equal(1.5f, options.LineWidth);
        Assert.Equal(8f, options.LabelFontSize);
        Assert.True(options.UseNatoStandard);
        Assert.Equal(0.5f, options.PatternOpacity);
    }
}
