using MilMap.Tests.Fixtures.SampleMaps;
using SkiaSharp;
using Xunit;

namespace MilMap.Tests.Rendering;

/// <summary>
/// Visual regression tests that compare rendered output against reference sample maps.
/// These tests ensure rendering consistency across code changes.
/// 
/// To regenerate baselines after intentional rendering changes:
/// 1. Run <see cref="SampleMapGenerator.GenerateAll"/> 
/// 2. Commit the updated sample images
/// </summary>
public class VisualRegressionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _fixturesDir;

    /// <summary>
    /// Maximum allowed pixel difference percentage for a test to pass.
    /// Small differences (less than 1%) are acceptable due to anti-aliasing variations.
    /// </summary>
    private const double MaxAllowedDifferencePercent = 1.0;

    public VisualRegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MilMap_VisualTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _fixturesDir = SampleMapGenerator.SampleMapsDirectory;

        // Generate reference images if they don't exist
        EnsureReferenceImagesExist();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private void EnsureReferenceImagesExist()
    {
        if (!Directory.Exists(_fixturesDir))
        {
            Directory.CreateDirectory(_fixturesDir);
        }

        // Check if any reference images are missing
        bool anyMissing = SampleMapGenerator.Samples.Any(s =>
            !File.Exists(Path.Combine(_fixturesDir, $"{s.Name}.png")));

        if (anyMissing)
        {
            SampleMapGenerator.GenerateAll(_fixturesDir);
        }
    }

    [Theory]
    [MemberData(nameof(GetSampleMapNames))]
    public void RenderOutput_MatchesReference(string sampleName)
    {
        var sample = SampleMapGenerator.Samples.First(s => s.Name == sampleName);

        using var reference = SampleMapGenerator.LoadReference(sampleName, _fixturesDir);
        Assert.NotNull(reference);

        using var rendered = SampleMapGenerator.RenderSample(sample);

        var comparison = CompareBitmaps(reference, rendered);

        Assert.True(
            comparison.DifferencePercent <= MaxAllowedDifferencePercent,
            $"Visual regression detected for '{sampleName}': " +
            $"{comparison.DifferencePercent:F2}% pixels differ (max allowed: {MaxAllowedDifferencePercent}%). " +
            $"Dimensions: reference={reference.Width}x{reference.Height}, rendered={rendered.Width}x{rendered.Height}. " +
            $"Different pixels: {comparison.DifferentPixels} of {comparison.TotalPixels}");
    }

    [Fact]
    public void AllSamples_HaveValidDimensions()
    {
        foreach (var sample in SampleMapGenerator.Samples)
        {
            using var rendered = SampleMapGenerator.RenderSample(sample);

            Assert.True(rendered.Width > 0, $"Sample '{sample.Name}' has invalid width");
            Assert.True(rendered.Height > 0, $"Sample '{sample.Name}' has invalid height");
        }
    }

    [Fact]
    public void SampleMaps_CanBeExportedToPng()
    {
        var sample = SampleMapGenerator.Samples[0];
        using var rendered = SampleMapGenerator.RenderSample(sample);

        string outputPath = Path.Combine(_tempDir, "test_export.png");
        var exporter = new MilMap.Core.Export.ImageExporter();
        exporter.Export(rendered, outputPath);

        Assert.True(File.Exists(outputPath));
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 100);
    }

    [Fact]
    public void ReferenceImages_Exist()
    {
        foreach (var sample in SampleMapGenerator.Samples)
        {
            string path = Path.Combine(_fixturesDir, $"{sample.Name}.png");
            Assert.True(File.Exists(path), $"Reference image missing: {sample.Name}.png");
        }
    }

    public static IEnumerable<object[]> GetSampleMapNames()
    {
        return SampleMapGenerator.Samples.Select(s => new object[] { s.Name });
    }

    private static BitmapComparison CompareBitmaps(SKBitmap reference, SKBitmap rendered)
    {
        // If dimensions differ, calculate max possible comparison area
        int compareWidth = Math.Min(reference.Width, rendered.Width);
        int compareHeight = Math.Min(reference.Height, rendered.Height);

        // Dimension differences count as differences
        int dimensionDiffPixels = 0;
        if (reference.Width != rendered.Width || reference.Height != rendered.Height)
        {
            int maxWidth = Math.Max(reference.Width, rendered.Width);
            int maxHeight = Math.Max(reference.Height, rendered.Height);
            dimensionDiffPixels = (maxWidth * maxHeight) - (compareWidth * compareHeight);
        }

        int differentPixels = dimensionDiffPixels;
        const int tolerance = 5; // Allow small color variations

        for (int y = 0; y < compareHeight; y++)
        {
            for (int x = 0; x < compareWidth; x++)
            {
                var refPixel = reference.GetPixel(x, y);
                var rendPixel = rendered.GetPixel(x, y);

                if (!PixelsMatch(refPixel, rendPixel, tolerance))
                {
                    differentPixels++;
                }
            }
        }

        int totalPixels = Math.Max(reference.Width, rendered.Width) *
                         Math.Max(reference.Height, rendered.Height);

        return new BitmapComparison(differentPixels, totalPixels);
    }

    private static bool PixelsMatch(SKColor a, SKColor b, int tolerance)
    {
        return Math.Abs(a.Red - b.Red) <= tolerance &&
               Math.Abs(a.Green - b.Green) <= tolerance &&
               Math.Abs(a.Blue - b.Blue) <= tolerance &&
               Math.Abs(a.Alpha - b.Alpha) <= tolerance;
    }

    private record BitmapComparison(int DifferentPixels, int TotalPixels)
    {
        public double DifferencePercent => TotalPixels > 0
            ? (DifferentPixels * 100.0) / TotalPixels
            : 0;
    }
}
