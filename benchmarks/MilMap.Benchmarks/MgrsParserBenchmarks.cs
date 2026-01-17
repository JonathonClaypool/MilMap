using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MilMap.Core.Mgrs;

namespace MilMap.Benchmarks;

/// <summary>
/// Benchmarks for MGRS parsing and encoding operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class MgrsParserBenchmarks
{
    private readonly string[] _gridSquares =
    {
        "18TXM",
        "32UPU",
        "54SUD",
        "4QFJ",
        "33UUP"
    };

    private readonly string[] _preciseCoordinates =
    {
        "18TXM8546",
        "32UPU12345678",
        "54SUD9999",
        "4QFJ00000000",
        "33UUP85461234"
    };

    [Benchmark(Baseline = true)]
    public void ParseGridSquare()
    {
        foreach (var mgrs in _gridSquares)
        {
            _ = MgrsParser.Parse(mgrs);
        }
    }

    [Benchmark]
    public void ParsePreciseCoordinate()
    {
        foreach (var mgrs in _preciseCoordinates)
        {
            _ = MgrsParser.Parse(mgrs);
        }
    }

    [Benchmark]
    public void ParseAndGetBoundary()
    {
        foreach (var mgrs in _gridSquares)
        {
            _ = MgrsBoundary.GetBounds(mgrs);
        }
    }
}
