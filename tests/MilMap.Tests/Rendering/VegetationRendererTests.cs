using SkiaSharp;
using Xunit;
using MilMap.Core.Rendering;
using MilMap.Core.Osm;

namespace MilMap.Tests.Rendering;

public class VegetationRendererTests
{
    [Fact]
    public void VegetationRenderer_CanBeCreated()
    {
        using var renderer = new VegetationRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void VegetationRenderer_CanBeCreatedWithClient()
    {
        using var client = new OverpassClient();
        using var renderer = new VegetationRenderer(client);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void VegetationRenderer_ThrowsOnNullClient()
    {
        Assert.Throws<ArgumentNullException>(() => new VegetationRenderer(null!));
    }

    [Fact]
    public void VegetationRenderer_CanBeDisposed()
    {
        var renderer = new VegetationRenderer();
        renderer.Dispose();
        // Should not throw on double dispose
        renderer.Dispose();
    }
}
