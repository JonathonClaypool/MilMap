using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MilMap.Core.Tiles;

namespace MilMap.Benchmarks;

/// <summary>
/// Benchmarks for tile calculations and caching operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class TileBenchmarks
{
    private readonly (double minLat, double minLon, double maxLat, double maxLon)[] _boundingBoxes =
    {
        (38.8, -77.1, 38.9, -77.0),   // Small area (~10km)
        (38.5, -77.5, 39.5, -76.5),   // Medium area (~100km)
        (35.0, -80.0, 40.0, -75.0),   // Large area (~500km)
    };

    [Benchmark]
    [Arguments(10000, 300)]
    [Arguments(25000, 300)]
    [Arguments(50000, 300)]
    [Arguments(100000, 300)]
    public void CalculateZoom_VariousScales(int scale, int dpi)
    {
        foreach (var bb in _boundingBoxes)
        {
            double centerLat = (bb.minLat + bb.maxLat) / 2;
            _ = ZoomLevelCalculator.CalculateZoom(scale, dpi, centerLat);
        }
    }

    [Benchmark]
    [Arguments(300)]
    [Arguments(150)]
    [Arguments(600)]
    public void CalculateZoom_VariousDpi(int dpi)
    {
        foreach (var bb in _boundingBoxes)
        {
            double centerLat = (bb.minLat + bb.maxLat) / 2;
            _ = ZoomLevelCalculator.CalculateZoom(25000, dpi, centerLat);
        }
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(12)]
    [Arguments(15)]
    public void GetMetersPerPixel_VariousZoom(int zoom)
    {
        foreach (var bb in _boundingBoxes)
        {
            double centerLat = (bb.minLat + bb.maxLat) / 2;
            _ = ZoomLevelCalculator.GetMetersPerPixel(zoom, centerLat);
        }
    }

    [Benchmark]
    public void RecommendZoom_SmallArea()
    {
        var (minLat, _, maxLat, _) = _boundingBoxes[0];
        double centerLat = (minLat + maxLat) / 2;
        _ = ZoomLevelCalculator.RecommendZoom(25000, 300, centerLat);
    }

    [Benchmark]
    public void RecommendZoom_LargeArea()
    {
        var (minLat, _, maxLat, _) = _boundingBoxes[2];
        double centerLat = (minLat + maxLat) / 2;
        _ = ZoomLevelCalculator.RecommendZoom(100000, 300, centerLat);
    }
}
