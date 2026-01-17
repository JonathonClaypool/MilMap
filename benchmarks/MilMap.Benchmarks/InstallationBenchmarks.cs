using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MilMap.Core.Input;

namespace MilMap.Benchmarks;

/// <summary>
/// Benchmarks for installation search and lookup operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class InstallationBenchmarks
{
    private readonly string[] _exactNames =
    {
        "Fort Liberty",
        "Fort Hood",
        "Camp Pendleton",
        "Naval Station Norfolk",
        "Joint Base Lewis-McChord"
    };

    private readonly string[] _partialNames =
    {
        "Fort",
        "Camp",
        "Base",
        "Naval",
        "Joint"
    };

    private readonly string[] _typos =
    {
        "Fort Libery",
        "Fort Hod",
        "Camp Pendelton",
        "Navl Station",
        "Lewis McChord"
    };

    [Benchmark(Baseline = true)]
    public void ExactLookup()
    {
        foreach (var name in _exactNames)
        {
            _ = InstallationInputHandler.TryLookup(name, out _);
        }
    }

    [Benchmark]
    public void FuzzySearch_Partial()
    {
        foreach (var name in _partialNames)
        {
            _ = InstallationInputHandler.Search(name, 10).ToList();
        }
    }

    [Benchmark]
    public void FuzzySearch_WithTypos()
    {
        foreach (var name in _typos)
        {
            _ = InstallationInputHandler.Search(name, 5).ToList();
        }
    }

    [Benchmark]
    [Arguments(5)]
    [Arguments(10)]
    [Arguments(25)]
    [Arguments(50)]
    public void Search_VariousMaxResults(int maxResults)
    {
        _ = InstallationInputHandler.Search("Fort", maxResults).ToList();
    }

    [Benchmark]
    public void GetAllInstallations()
    {
        _ = InstallationInputHandler.GetAllInstallations().Count();
    }

    [Benchmark]
    public void FilterByBranch()
    {
        var installations = InstallationInputHandler.GetAllInstallations();
        _ = installations.Where(i => i.Branch == "Army").ToList();
    }

    [Benchmark]
    public void FilterByState()
    {
        var installations = InstallationInputHandler.GetAllInstallations();
        _ = installations.Where(i => i.State == "NC").ToList();
    }
}
