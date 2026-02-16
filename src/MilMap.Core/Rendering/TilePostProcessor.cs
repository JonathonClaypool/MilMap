using SkiaSharp;

namespace MilMap.Core.Rendering;

/// <summary>
/// Post-processes rendered map tiles to remove unwanted visual artifacts
/// from tile server rendering, such as military installation boundary hatching.
/// </summary>
public static class TilePostProcessor
{
    // The military overlay color used by OpenTopoMap: a semi-transparent red/pink
    // composited over the base terrain. Empirically the overlay is approximately
    // RGBA(198, 0, 0, ~40) for hatching and a fill of RGBA(242, 210, 210, ~50).
    // We reverse the alpha blend to recover the underlying terrain.

    /// <summary>
    /// Removes the red diagonal hatching and pink fill that tile servers
    /// (OSM, OpenTopoMap) render over landuse=military areas.
    ///
    /// Instead of replacing pixels with a flat color (which destroys terrain
    /// detail like forests), this method reverses the alpha blend to recover
    /// the approximate original terrain color underneath the overlay.
    /// </summary>
    /// <param name="bitmap">The rendered map bitmap to process (modified in place).</param>
    public static void RemoveMilitaryHatching(SKBitmap bitmap)
    {
        if (bitmap == null) return;

        int width = bitmap.Width;
        int height = bitmap.Height;

        var pixels = bitmap.Pixels;

        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];

            if (IsRedHatching(pixel))
            {
                // Red hatching lines: the original terrain is completely obscured.
                // Replace with average of nearby non-red pixels using a simple
                // horizontal scan (check left and right neighbors).
                pixels[i] = InterpolateFromNeighbors(pixels, i, width, height);
            }
            else if (HasPinkMilitaryTint(pixel))
            {
                // Pink tinted pixel: reverse the semi-transparent overlay blend
                // to recover the underlying terrain color.
                pixels[i] = RemovePinkTint(pixel);
            }
        }

        bitmap.Pixels = pixels;
    }

    /// <summary>
    /// Detects if a pixel is part of the red military hatching pattern.
    /// Matches the #c60000 range used by OSM and OpenTopoMap.
    /// </summary>
    public static bool IsRedHatching(SKColor pixel)
    {
        // Red hatching: high red, very low green and blue
        return pixel.Red >= 150 &&
               pixel.Green < 60 &&
               pixel.Blue < 60 &&
               pixel.Alpha > 200;
    }

    /// <summary>
    /// Detects if a pixel has been tinted by the pink military fill overlay.
    /// Rather than matching a specific exact color, this detects the characteristic
    /// red-shifted tint that the overlay adds to ANY underlying terrain color.
    /// The key signature: red channel is disproportionately elevated relative to
    /// green and blue, and green/blue are relatively close to each other.
    /// </summary>
    public static bool HasPinkMilitaryTint(SKColor pixel)
    {
        // The overlay shifts red up relative to green/blue.
        // On white terrain: results in pinkish (R~242, G~220, B~220)
        // On green forest:  results in muted olive-pink (R↑, G stays, B slightly↑)
        // On blue water:    results in purple-ish tint
        //
        // We detect pixels where red is notably higher than the average of green/blue,
        // which indicates the red-shift from the military overlay.
        int redExcess = pixel.Red - (pixel.Green + pixel.Blue) / 2;

        // The overlay adds approximately 10-25 units of red excess depending
        // on the underlying terrain color. We use a conservative threshold
        // to avoid touching legitimately red features (roads, buildings, labels).
        // Also require that the pixel isn't already very dark (shadow/text).
        return redExcess >= 12 &&
               pixel.Red >= 100 &&
               pixel.Alpha > 200 &&
               // Don't touch actual red features like roads — those have very low G and B
               pixel.Green >= 60 &&
               // Don't touch brown contour lines or dark features
               (pixel.Green + pixel.Blue) >= 140;
    }

    /// <summary>
    /// Reverses the pink military tint by reducing the red channel excess.
    /// This approximates un-doing the alpha blend of the semi-transparent
    /// red/pink overlay that the tile server composited over the terrain.
    /// </summary>
    public static SKColor RemovePinkTint(SKColor pixel)
    {
        // Estimate how much red was added by the overlay.
        // The overlay adds a red shift; we subtract it to recover the terrain color.
        int avgGB = (pixel.Green + pixel.Blue) / 2;
        int redExcess = pixel.Red - avgGB;

        // Remove the estimated overlay contribution from red,
        // and slightly boost green (forests often lose some green saturation
        // from the pink overlay).
        int newRed = Math.Clamp(pixel.Red - redExcess, 0, 255);

        // The overlay also slightly reduces green saturation; restore a small amount
        int greenBoost = Math.Min(redExcess / 3, 15);
        int newGreen = Math.Clamp(pixel.Green + greenBoost, 0, 255);

        return new SKColor((byte)newRed, (byte)newGreen, pixel.Blue, pixel.Alpha);
    }

    /// <summary>
    /// Interpolates a replacement color from neighboring non-red pixels.
    /// Used for red hatching lines where the original terrain is fully obscured.
    /// </summary>
    private static SKColor InterpolateFromNeighbors(SKColor[] pixels, int index, int width, int height)
    {
        int y = index / width;
        int x = index % width;

        int totalR = 0, totalG = 0, totalB = 0, count = 0;

        // Sample in a small cross pattern around the pixel
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                var neighbor = pixels[ny * width + nx];
                if (!IsRedHatching(neighbor))
                {
                    // If the neighbor has pink tint, remove it first
                    if (HasPinkMilitaryTint(neighbor))
                        neighbor = RemovePinkTint(neighbor);

                    totalR += neighbor.Red;
                    totalG += neighbor.Green;
                    totalB += neighbor.Blue;
                    count++;
                }
            }
        }

        if (count == 0)
        {
            // Fallback: no usable neighbors, use neutral terrain
            return new SKColor(245, 240, 228);
        }

        return new SKColor(
            (byte)(totalR / count),
            (byte)(totalG / count),
            (byte)(totalB / count));
    }
}

