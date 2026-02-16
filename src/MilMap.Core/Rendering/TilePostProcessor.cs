using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Post-processes rendered map tiles to remove unwanted visual artifacts
/// from tile server rendering, such as military installation boundary hatching.
/// </summary>
public static class TilePostProcessor
{
    /// <summary>
    /// Removes the red diagonal hatching and pink fill that tile servers
    /// (OSM, OpenTopoMap) render over landuse=military areas.
    ///
    /// The hatching uses red lines (~#c60000, RGB 198,0,0) over a pale pink
    /// fill (~#f2dcdc, RGB 242,220,220). This method detects those colors
    /// and replaces them with neutral terrain tones.
    /// </summary>
    /// <param name="bitmap">The rendered map bitmap to process (modified in place).</param>
    public static void RemoveMilitaryHatching(SKBitmap bitmap)
    {
        if (bitmap == null) return;

        int width = bitmap.Width;
        int height = bitmap.Height;

        // We need direct pixel access for performance
        var pixels = bitmap.Pixels;

        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];

            if (IsRedHatching(pixel))
            {
                // Replace red hatching lines with a neutral light tan/terrain color
                // Sample surrounding non-red pixels would be ideal, but for
                // performance we use a neutral tone that blends with topo maps
                pixels[i] = new SKColor(245, 240, 228); // Light terrain tan
            }
            else if (IsPinkMilitaryFill(pixel))
            {
                // Replace the pink military fill with neutral terrain
                pixels[i] = new SKColor(245, 240, 228); // Light terrain tan
            }
        }

        bitmap.Pixels = pixels;
    }

    /// <summary>
    /// Detects if a pixel is part of the red military hatching pattern.
    /// Matches the #c60000 range used by OSM and OpenTopoMap.
    /// </summary>
    private static bool IsRedHatching(SKColor pixel)
    {
        // Red hatching: high red, very low green and blue
        return pixel.Red >= 150 &&
               pixel.Green < 60 &&
               pixel.Blue < 60 &&
               pixel.Alpha > 200;
    }

    /// <summary>
    /// Detects if a pixel is part of the pink military fill background.
    /// Matches the #f2dcdc range used by OSM and OpenTopoMap.
    /// </summary>
    private static bool IsPinkMilitaryFill(SKColor pixel)
    {
        // Pink fill: high red, moderately high green/blue, with red notably dominant
        // #f2dcdc = RGB(242, 220, 220) — red is 20+ higher than green/blue,
        // and all channels are in the 200-250 range
        return pixel.Red >= 220 &&
               pixel.Green >= 195 && pixel.Green <= 235 &&
               pixel.Blue >= 195 && pixel.Blue <= 235 &&
               (pixel.Red - pixel.Green) >= 10 &&
               Math.Abs(pixel.Green - pixel.Blue) <= 15 &&
               pixel.Alpha > 200;
    }
}
