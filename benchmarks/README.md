# MilMap Benchmarks

Performance benchmarks for MilMap using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Benchmark Categories

### MGRS Parser Benchmarks (`MgrsParserBenchmarks`)
- Grid square parsing (e.g., "18TXM")
- Precise coordinate parsing (e.g., "18TXM12345678")
- Parsing with boundary calculation

### Rendering Benchmarks (`RenderingBenchmarks`)
- Scale bar rendering at various scales (1:10,000 to 1:100,000)
- Scale bar rendering at various DPI (150, 300, 600)
- Legend rendering with different item counts
- Military symbol drawing
- Pattern tile generation

### Tile Benchmarks (`TileBenchmarks`)
- Zoom level calculation at various scales
- Zoom level calculation at various DPI
- Meters per pixel calculation

### Installation Benchmarks (`InstallationBenchmarks`)
- Exact name lookup
- Fuzzy search with partial names
- Fuzzy search with typos
- Filtering by branch and state

## Running Benchmarks

### Run all benchmarks:

```bash
cd benchmarks/MilMap.Benchmarks
dotnet run -c Release
```

### Run specific benchmark class:

```bash
dotnet run -c Release -- --filter "MilMap.Benchmarks.RenderingBenchmarks*"
```

### Run with specific job:

```bash
dotnet run -c Release -- --job short
```

### Export results:

```bash
dotnet run -c Release -- --exporters json html
```

## Results

Benchmark results are saved to `BenchmarkDotNet.Artifacts/` in the project directory.

## Adding New Benchmarks

1. Create a new benchmark class in the `MilMap.Benchmarks` namespace
2. Add `[MemoryDiagnoser]` and `[SimpleJob(RuntimeMoniker.Net80)]` attributes
3. Mark benchmark methods with `[Benchmark]` attribute
4. Use `[GlobalSetup]` and `[GlobalCleanup]` for test fixtures
5. Use `[Arguments]` for parameterized benchmarks
