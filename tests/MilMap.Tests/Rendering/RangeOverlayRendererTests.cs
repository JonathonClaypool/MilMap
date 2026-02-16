using Xunit;
using MilMap.Core.Rendering;
using MilMap.Core.Osm;

namespace MilMap.Tests.Rendering;

public class RangeOverlayRendererTests
{
    [Fact]
    public void RangeOverlayRenderer_CanBeCreated()
    {
        using var renderer = new RangeOverlayRenderer();
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RangeOverlayRenderer_CanBeCreatedWithClient()
    {
        using var client = new OverpassClient();
        using var renderer = new RangeOverlayRenderer(client);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RangeOverlayRenderer_ThrowsOnNullClient()
    {
        Assert.Throws<ArgumentNullException>(() => new RangeOverlayRenderer(null!));
    }

    [Fact]
    public void RangeOverlayRenderer_CanBeDisposedMultipleTimes()
    {
        var renderer = new RangeOverlayRenderer();
        renderer.Dispose();
        renderer.Dispose(); // Should not throw
    }
}
