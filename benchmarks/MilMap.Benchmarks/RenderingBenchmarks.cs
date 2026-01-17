using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SkiaSharp;
using MilMap.Core.Rendering;

namespace MilMap.Benchmarks;

/// <summary>
/// Benchmarks for map rendering operations at various scales and sizes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class RenderingBenchmarks
{
    private ScaleBarRenderer _scaleBarRenderer = null!;
    private LegendRenderer _legendRenderer = null!;
    private MilitarySymbologyRenderer _symbologyRenderer = null!;
    private SKBitmap _canvas = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scaleBarRenderer = new ScaleBarRenderer(new ScaleBarOptions { ScaleRatio = 25000, Dpi = 300 });
        _legendRenderer = new LegendRenderer();
        _symbologyRenderer = new MilitarySymbologyRenderer();
        _canvas = new SKBitmap(1024, 1024);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _canvas.Dispose();
        _legendRenderer.Dispose();
        _symbologyRenderer.Dispose();
    }

    [Benchmark]
    public void RenderScaleBar_1to25000()
    {
        var result = _scaleBarRenderer.Render();
        result.Bitmap.Dispose();
    }

    [Benchmark]
    [Arguments(10000)]
    [Arguments(25000)]
    [Arguments(50000)]
    [Arguments(100000)]
    public void RenderScaleBar_VariousScales(int scale)
    {
        var renderer = new ScaleBarRenderer(new ScaleBarOptions { ScaleRatio = scale, Dpi = 300 });
        var result = renderer.Render();
        result.Bitmap.Dispose();
    }

    [Benchmark]
    [Arguments(150)]
    [Arguments(300)]
    [Arguments(600)]
    public void RenderScaleBar_VariousDpi(int dpi)
    {
        var renderer = new ScaleBarRenderer(new ScaleBarOptions { ScaleRatio = 25000, Dpi = dpi });
        var result = renderer.Render();
        result.Bitmap.Dispose();
    }

    [Benchmark]
    public void RenderDefaultLegend()
    {
        using var bitmap = _legendRenderer.RenderDefault();
    }

    [Benchmark]
    public void RenderCustomLegend_10Items()
    {
        var items = LegendRenderer.CreateDefaultLegendItems().Take(10).ToList();
        using var bitmap = _legendRenderer.Render(items);
    }

    [Benchmark]
    public void RenderCustomLegend_30Items()
    {
        var items = LegendRenderer.CreateDefaultLegendItems().Take(30).ToList();
        using var bitmap = _legendRenderer.Render(items);
    }

    [Benchmark]
    public void DrawSymbol_AllTypes()
    {
        using var canvas = new SKCanvas(_canvas);
        var symbolTypes = Enum.GetValues<SymbolType>();
        
        float x = 50, y = 50;
        foreach (var symbol in symbolTypes)
        {
            _symbologyRenderer.DrawSymbol(canvas, x, y, symbol);
            x += 50;
            if (x > 950)
            {
                x = 50;
                y += 50;
            }
        }
    }

    [Benchmark]
    public void CreatePatternTiles_AllPatterns()
    {
        var patterns = new[] 
        { 
            SymbolType.Marsh, 
            SymbolType.Swamp, 
            SymbolType.SandDunes, 
            SymbolType.Orchard, 
            SymbolType.Vineyard, 
            SymbolType.Scrub 
        };

        foreach (var pattern in patterns)
        {
            using var tile = _symbologyRenderer.CreatePatternTile(pattern, 24);
        }
    }
}
