using SkiaSharp;
using Xunit;
using MilMap.Core.Rendering;

namespace MilMap.Tests.Rendering;

public class TilePostProcessorTests
{
    [Fact]
    public void RemoveMilitaryHatching_ReplacesRedPixels()
    {
        using var bitmap = new SKBitmap(10, 10);
        var redHatch = new SKColor(198, 0, 0);
        bitmap.SetPixel(3, 3, redHatch);
        bitmap.SetPixel(5, 5, redHatch);

        TilePostProcessor.RemoveMilitaryHatching(bitmap);

        var p1 = bitmap.GetPixel(3, 3);
        Assert.NotEqual(redHatch, p1);
    }

    [Fact]
    public void RemoveMilitaryHatching_RemovesPinkTintPreservingTerrain()
    {
        // Simulate a green forest pixel with pink military tint overlaid:
        // Original green ~(140, 195, 110), tinted by red overlay → ~(165, 185, 110)
        using var bitmap = new SKBitmap(10, 10);
        var tintedGreen = new SKColor(165, 130, 110);
        bitmap.SetPixel(4, 4, tintedGreen);

        TilePostProcessor.RemoveMilitaryHatching(bitmap);

        var result = bitmap.GetPixel(4, 4);
        // After removing pink tint, red should decrease and green should increase
        // relative to the tinted input — terrain color should be more neutral/green
        Assert.True(result.Red <= tintedGreen.Red,
            $"Red should decrease after tint removal: was {tintedGreen.Red}, now {result.Red}");
    }

    [Fact]
    public void RemoveMilitaryHatching_PreservesNonMilitaryPixels()
    {
        using var bitmap = new SKBitmap(10, 10);
        var green = new SKColor(100, 180, 100);
        var blue = new SKColor(100, 100, 200);
        bitmap.SetPixel(2, 2, green);
        bitmap.SetPixel(7, 7, blue);

        TilePostProcessor.RemoveMilitaryHatching(bitmap);

        // Green pixel has no red excess, should be untouched
        Assert.Equal(green, bitmap.GetPixel(2, 2));
        // Blue pixel has no red excess, should be untouched
        Assert.Equal(blue, bitmap.GetPixel(7, 7));
    }

    [Fact]
    public void RemoveMilitaryHatching_HandlesNullBitmap()
    {
        TilePostProcessor.RemoveMilitaryHatching(null!);
    }

    [Fact]
    public void RemoveMilitaryHatching_HandlesSmallBitmap()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.White);

        TilePostProcessor.RemoveMilitaryHatching(bitmap);

        Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
    }

    [Theory]
    [InlineData(200, 10, 10)]   // Strong red
    [InlineData(180, 30, 20)]   // Moderate red
    [InlineData(150, 50, 50)]   // Edge case lower bound
    public void IsRedHatching_DetectsVariousRedShades(byte r, byte g, byte b)
    {
        Assert.True(TilePostProcessor.IsRedHatching(new SKColor(r, g, b)));
    }

    [Theory]
    [InlineData(100, 0, 0)]     // Too dark
    [InlineData(200, 80, 50)]   // Green too high
    [InlineData(140, 30, 30)]   // Red below threshold
    public void IsRedHatching_RejectsNonHatching(byte r, byte g, byte b)
    {
        Assert.False(TilePostProcessor.IsRedHatching(new SKColor(r, g, b)));
    }

    [Fact]
    public void HasPinkMilitaryTint_DetectsHighRedExcess()
    {
        // Pixel with significant red excess over green/blue average
        var tinted = new SKColor(200, 160, 150);
        Assert.True(TilePostProcessor.HasPinkMilitaryTint(tinted));
    }

    [Fact]
    public void HasPinkMilitaryTint_RejectsBalancedColors()
    {
        // Balanced green — no red excess
        var green = new SKColor(100, 180, 100);
        Assert.False(TilePostProcessor.HasPinkMilitaryTint(green));

        // White — minimal red excess
        Assert.False(TilePostProcessor.HasPinkMilitaryTint(SKColors.White));
    }

    [Fact]
    public void HasPinkMilitaryTint_RejectsDarkPixels()
    {
        // Very dark pixel with slight red dominance — should NOT match
        var dark = new SKColor(50, 30, 30);
        Assert.False(TilePostProcessor.HasPinkMilitaryTint(dark));
    }

    [Fact]
    public void RemovePinkTint_ReducesRedAndBoostsGreen()
    {
        var tinted = new SKColor(200, 160, 150);
        var result = TilePostProcessor.RemovePinkTint(tinted);

        Assert.True(result.Red < tinted.Red, "Red should decrease");
        Assert.True(result.Green >= tinted.Green, "Green should increase or stay");
        Assert.Equal(tinted.Blue, result.Blue);
    }

    [Fact]
    public void RemoveMilitaryHatching_InterpolatesRedHatchFromNeighbors()
    {
        // Create bitmap with green terrain and a red hatching pixel in center
        using var bitmap = new SKBitmap(5, 5);
        var green = new SKColor(140, 195, 110);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                bitmap.SetPixel(x, y, green);

        bitmap.SetPixel(2, 2, new SKColor(198, 0, 0)); // Red hatching

        TilePostProcessor.RemoveMilitaryHatching(bitmap);

        var result = bitmap.GetPixel(2, 2);
        // Should be interpolated from green neighbors, not flat tan
        Assert.True(result.Green > 100, $"Should interpolate green from neighbors, got G={result.Green}");
    }
}
