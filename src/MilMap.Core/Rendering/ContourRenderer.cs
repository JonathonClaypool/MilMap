using System;
using System.Collections.Generic;
using SkiaSharp;
using MilMap.Core.Elevation;

namespace MilMap.Core.Rendering;

/// <summary>
/// Options for contour line rendering.
/// </summary>
public class ContourOptions
{
    /// <summary>
    /// Map scale, determines contour interval if not specified.
    /// </summary>
    public MapScale Scale { get; set; } = MapScale.Scale1To25000;

    /// <summary>
    /// Contour interval in meters. If null, determined by scale.
    /// 1:10,000 = 10m, 1:25,000 = 20m, 1:50,000 = 20m, 1:100,000 = 50m.
    /// </summary>
    public int? ContourIntervalMeters { get; set; }

    /// <summary>
    /// Index contour frequency. Every Nth contour is drawn as an index contour.
    /// Default is 5 (every 5th contour is bolder).
    /// </summary>
    public int IndexContourFrequency { get; set; } = 5;

    /// <summary>
    /// Contour line color. Default is brown/sienna per topographic convention.
    /// </summary>
    public SKColor ContourColor { get; set; } = new SKColor(139, 90, 43); // Sienna brown

    /// <summary>
    /// Regular contour line width in pixels.
    /// </summary>
    public float ContourLineWidth { get; set; } = 0.5f;

    /// <summary>
    /// Index contour line width in pixels.
    /// </summary>
    public float IndexContourLineWidth { get; set; } = 1.5f;

    /// <summary>
    /// Whether to show elevation labels on contours.
    /// </summary>
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// Font size for contour labels.
    /// </summary>
    public float LabelFontSize { get; set; } = 8f;

    /// <summary>
    /// Label color (typically same as contour color).
    /// </summary>
    public SKColor LabelColor { get; set; } = new SKColor(139, 90, 43);

    /// <summary>
    /// Minimum distance in pixels between labels on the same contour.
    /// </summary>
    public int LabelSpacingPixels { get; set; } = 200;

    /// <summary>
    /// Whether to show depression contours (tick marks pointing downhill).
    /// </summary>
    public bool ShowDepressionContours { get; set; } = true;

    /// <summary>
    /// Length of depression tick marks in pixels.
    /// </summary>
    public float DepressionTickLength { get; set; } = 4f;

    /// <summary>
    /// Spacing between depression tick marks in pixels.
    /// </summary>
    public float DepressionTickSpacing { get; set; } = 20f;

    /// <summary>
    /// Enable line smoothing using Bezier curves.
    /// </summary>
    public bool SmoothContours { get; set; } = true;
}

/// <summary>
/// Represents a contour line segment.
/// </summary>
public class ContourLine
{
    /// <summary>
    /// Elevation in meters.
    /// </summary>
    public double Elevation { get; }

    /// <summary>
    /// Whether this is an index contour (every 5th typically).
    /// </summary>
    public bool IsIndex { get; }

    /// <summary>
    /// Whether this is a depression contour.
    /// </summary>
    public bool IsDepression { get; set; }

    /// <summary>
    /// Points in pixel coordinates.
    /// </summary>
    public List<SKPoint> Points { get; } = new();

    public ContourLine(double elevation, bool isIndex)
    {
        Elevation = elevation;
        IsIndex = isIndex;
    }
}

/// <summary>
/// Renders contour lines from elevation data onto map images.
/// Uses marching squares algorithm for contour extraction.
/// </summary>
public class ContourRenderer
{
    private readonly ContourOptions _options;

    public ContourRenderer() : this(new ContourOptions()) { }

    public ContourRenderer(ContourOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the contour interval in meters based on scale.
    /// </summary>
    public int GetContourInterval()
    {
        if (_options.ContourIntervalMeters.HasValue)
            return _options.ContourIntervalMeters.Value;

        return _options.Scale switch
        {
            MapScale.Scale1To10000 => 10,
            MapScale.Scale1To25000 => 20,
            MapScale.Scale1To50000 => 20,
            MapScale.Scale1To100000 => 50,
            _ => 20
        };
    }

    /// <summary>
    /// Draws contour lines on a bitmap from elevation grid data.
    /// </summary>
    /// <param name="baseMap">Base map bitmap to draw on</param>
    /// <param name="elevationGrid">Elevation grid covering the map area</param>
    /// <param name="offsetX">X offset for grid positioning (default 0)</param>
    /// <param name="offsetY">Y offset for grid positioning (default 0)</param>
    /// <returns>New bitmap with contours drawn</returns>
    public SKBitmap DrawContours(
        SKBitmap baseMap,
        ElevationGrid elevationGrid,
        int offsetX = 0,
        int offsetY = 0)
    {
        var result = baseMap.Copy();
        using var canvas = new SKCanvas(result);

        int interval = GetContourInterval();
        int indexInterval = interval * _options.IndexContourFrequency;

        // Find elevation range
        double minElev = double.MaxValue;
        double maxElev = double.MinValue;
        for (int r = 0; r < elevationGrid.Rows; r++)
        {
            for (int c = 0; c < elevationGrid.Cols; c++)
            {
                var elev = elevationGrid.Elevations[r, c];
                if (elev.HasValue)
                {
                    minElev = Math.Min(minElev, elev.Value);
                    maxElev = Math.Max(maxElev, elev.Value);
                }
            }
        }

        if (minElev == double.MaxValue)
            return result; // No elevation data

        // Round to contour intervals
        int startElev = (int)(Math.Floor(minElev / interval) * interval);
        int endElev = (int)(Math.Ceiling(maxElev / interval) * interval);

        // Calculate pixel scaling
        double pixelsPerRow = (double)baseMap.Height / (elevationGrid.Rows - 1);
        double pixelsPerCol = (double)baseMap.Width / (elevationGrid.Cols - 1);

        // Extract and draw contours
        var contours = new List<ContourLine>();

        for (int elev = startElev; elev <= endElev; elev += interval)
        {
            bool isIndex = elev % indexInterval == 0;
            var lines = ExtractContourLines(elevationGrid, elev, isIndex, pixelsPerCol, pixelsPerRow, offsetX, offsetY);
            contours.AddRange(lines);
        }

        // Detect depressions
        DetectDepressions(contours, elevationGrid, pixelsPerCol, pixelsPerRow, offsetX, offsetY);

        // Draw contours (regular first, then index on top)
        DrawContourLines(canvas, contours.Where(c => !c.IsIndex).ToList());
        DrawContourLines(canvas, contours.Where(c => c.IsIndex).ToList());

        // Draw labels on index contours
        if (_options.ShowLabels)
        {
            DrawContourLabels(canvas, contours.Where(c => c.IsIndex).ToList());
        }

        return result;
    }

    /// <summary>
    /// Extracts contour lines at a specific elevation using marching squares.
    /// </summary>
    private List<ContourLine> ExtractContourLines(
        ElevationGrid grid, double elevation, bool isIndex,
        double pixelsPerCol, double pixelsPerRow,
        int offsetX, int offsetY)
    {
        var lines = new List<ContourLine>();
        var segments = new List<(SKPoint start, SKPoint end)>();

        // Marching squares: process each cell (2x2 grid of samples)
        for (int r = 0; r < grid.Rows - 1; r++)
        {
            for (int c = 0; c < grid.Cols - 1; c++)
            {
                var e00 = grid.Elevations[r, c];
                var e01 = grid.Elevations[r, c + 1];
                var e10 = grid.Elevations[r + 1, c];
                var e11 = grid.Elevations[r + 1, c + 1];

                // Skip cells with missing data
                if (!e00.HasValue || !e01.HasValue || !e10.HasValue || !e11.HasValue)
                    continue;

                // Calculate marching squares case
                int caseIndex = 0;
                if (e00.Value >= elevation) caseIndex |= 1;
                if (e01.Value >= elevation) caseIndex |= 2;
                if (e11.Value >= elevation) caseIndex |= 4;
                if (e10.Value >= elevation) caseIndex |= 8;

                // Skip if entirely inside or outside
                if (caseIndex == 0 || caseIndex == 15)
                    continue;

                // Cell corners in pixel coordinates
                float x0 = (float)(c * pixelsPerCol + offsetX);
                float y0 = (float)(r * pixelsPerRow + offsetY);
                float x1 = (float)((c + 1) * pixelsPerCol + offsetX);
                float y1 = (float)((r + 1) * pixelsPerRow + offsetY);

                // Interpolate edge crossings
                var cellSegments = GetMarchingSquaresSegments(
                    caseIndex, elevation,
                    e00.Value, e01.Value, e10.Value, e11.Value,
                    x0, y0, x1, y1);

                segments.AddRange(cellSegments);
            }
        }

        // Connect segments into polylines
        lines = ConnectSegments(segments, elevation, isIndex);

        // Smooth contours if enabled
        if (_options.SmoothContours)
        {
            foreach (var line in lines)
            {
                SmoothContourLine(line);
            }
        }

        return lines;
    }

    /// <summary>
    /// Gets line segments for a marching squares cell case.
    /// </summary>
    private List<(SKPoint start, SKPoint end)> GetMarchingSquaresSegments(
        int caseIndex, double threshold,
        double e00, double e01, double e10, double e11,
        float x0, float y0, float x1, float y1)
    {
        var segments = new List<(SKPoint start, SKPoint end)>();

        // Edge midpoints with interpolation
        SKPoint TopEdge() => new(Lerp(x0, x1, e00, e01, threshold), y0);
        SKPoint BottomEdge() => new(Lerp(x0, x1, e10, e11, threshold), y1);
        SKPoint LeftEdge() => new(x0, Lerp(y0, y1, e00, e10, threshold));
        SKPoint RightEdge() => new(x1, Lerp(y0, y1, e01, e11, threshold));

        switch (caseIndex)
        {
            case 1: case 14:
                segments.Add((TopEdge(), LeftEdge()));
                break;
            case 2: case 13:
                segments.Add((TopEdge(), RightEdge()));
                break;
            case 3: case 12:
                segments.Add((LeftEdge(), RightEdge()));
                break;
            case 4: case 11:
                segments.Add((RightEdge(), BottomEdge()));
                break;
            case 5: // Saddle point
                segments.Add((TopEdge(), RightEdge()));
                segments.Add((LeftEdge(), BottomEdge()));
                break;
            case 6: case 9:
                segments.Add((TopEdge(), BottomEdge()));
                break;
            case 7: case 8:
                segments.Add((LeftEdge(), BottomEdge()));
                break;
            case 10: // Saddle point
                segments.Add((TopEdge(), LeftEdge()));
                segments.Add((RightEdge(), BottomEdge()));
                break;
        }

        return segments;
    }

    /// <summary>
    /// Linear interpolation for edge crossing position.
    /// </summary>
    private static float Lerp(float p0, float p1, double e0, double e1, double threshold)
    {
        if (Math.Abs(e1 - e0) < 0.0001)
            return (p0 + p1) / 2;
        double t = (threshold - e0) / (e1 - e0);
        return (float)(p0 + t * (p1 - p0));
    }

    /// <summary>
    /// Connects individual segments into continuous contour lines.
    /// </summary>
    private List<ContourLine> ConnectSegments(
        List<(SKPoint start, SKPoint end)> segments,
        double elevation, bool isIndex)
    {
        var lines = new List<ContourLine>();
        var remaining = new List<(SKPoint start, SKPoint end)>(segments);
        const float tolerance = 1.5f;

        while (remaining.Count > 0)
        {
            var line = new ContourLine(elevation, isIndex);
            var current = remaining[0];
            remaining.RemoveAt(0);

            line.Points.Add(current.start);
            line.Points.Add(current.end);

            bool extended = true;
            while (extended)
            {
                extended = false;
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    var seg = remaining[i];
                    var last = line.Points[^1];
                    var first = line.Points[0];

                    if (Distance(last, seg.start) < tolerance)
                    {
                        line.Points.Add(seg.end);
                        remaining.RemoveAt(i);
                        extended = true;
                    }
                    else if (Distance(last, seg.end) < tolerance)
                    {
                        line.Points.Add(seg.start);
                        remaining.RemoveAt(i);
                        extended = true;
                    }
                    else if (Distance(first, seg.end) < tolerance)
                    {
                        line.Points.Insert(0, seg.start);
                        remaining.RemoveAt(i);
                        extended = true;
                    }
                    else if (Distance(first, seg.start) < tolerance)
                    {
                        line.Points.Insert(0, seg.end);
                        remaining.RemoveAt(i);
                        extended = true;
                    }
                }
            }

            if (line.Points.Count >= 2)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>
    /// Applies smoothing to a contour line using Chaikin's algorithm.
    /// </summary>
    private void SmoothContourLine(ContourLine line)
    {
        if (line.Points.Count < 3)
            return;

        // Chaikin's corner-cutting algorithm (2 iterations)
        for (int iter = 0; iter < 2; iter++)
        {
            var smoothed = new List<SKPoint>();
            smoothed.Add(line.Points[0]);

            for (int i = 0; i < line.Points.Count - 1; i++)
            {
                var p0 = line.Points[i];
                var p1 = line.Points[i + 1];

                // 25% and 75% points
                var q = new SKPoint(
                    0.75f * p0.X + 0.25f * p1.X,
                    0.75f * p0.Y + 0.25f * p1.Y);
                var r = new SKPoint(
                    0.25f * p0.X + 0.75f * p1.X,
                    0.25f * p0.Y + 0.75f * p1.Y);

                smoothed.Add(q);
                smoothed.Add(r);
            }

            smoothed.Add(line.Points[^1]);

            line.Points.Clear();
            line.Points.AddRange(smoothed);
        }
    }

    /// <summary>
    /// Detects depression contours by checking if inner area is lower.
    /// </summary>
    private void DetectDepressions(
        List<ContourLine> contours,
        ElevationGrid grid,
        double pixelsPerCol, double pixelsPerRow,
        int offsetX, int offsetY)
    {
        if (!_options.ShowDepressionContours)
            return;

        foreach (var contour in contours)
        {
            // Check if this is a closed contour
            if (contour.Points.Count < 4)
                continue;

            var first = contour.Points[0];
            var last = contour.Points[^1];
            if (Distance(first, last) > 10)
                continue; // Not closed

            // Find centroid
            float centroidX = 0, centroidY = 0;
            foreach (var pt in contour.Points)
            {
                centroidX += pt.X;
                centroidY += pt.Y;
            }
            centroidX /= contour.Points.Count;
            centroidY /= contour.Points.Count;

            // Convert centroid back to grid coordinates
            int gridCol = (int)((centroidX - offsetX) / pixelsPerCol);
            int gridRow = (int)((centroidY - offsetY) / pixelsPerRow);

            if (gridRow >= 0 && gridRow < grid.Rows && gridCol >= 0 && gridCol < grid.Cols)
            {
                var centerElev = grid.Elevations[gridRow, gridCol];
                if (centerElev.HasValue && centerElev.Value < contour.Elevation)
                {
                    contour.IsDepression = true;
                }
            }
        }
    }

    /// <summary>
    /// Draws contour lines on the canvas.
    /// </summary>
    private void DrawContourLines(SKCanvas canvas, List<ContourLine> contours)
    {
        using var regularPaint = new SKPaint
        {
            Color = _options.ContourColor,
            StrokeWidth = _options.ContourLineWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        using var indexPaint = new SKPaint
        {
            Color = _options.ContourColor,
            StrokeWidth = _options.IndexContourLineWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        foreach (var contour in contours)
        {
            if (contour.Points.Count < 2)
                continue;

            var paint = contour.IsIndex ? indexPaint : regularPaint;

            using var path = new SKPath();
            path.MoveTo(contour.Points[0]);
            for (int i = 1; i < contour.Points.Count; i++)
            {
                path.LineTo(contour.Points[i]);
            }

            canvas.DrawPath(path, paint);

            // Draw depression ticks if applicable
            if (contour.IsDepression && _options.ShowDepressionContours)
            {
                DrawDepressionTicks(canvas, contour, paint);
            }
        }
    }

    /// <summary>
    /// Draws tick marks on depression contours pointing toward lower elevation.
    /// </summary>
    private void DrawDepressionTicks(SKCanvas canvas, ContourLine contour, SKPaint basePaint)
    {
        using var tickPaint = new SKPaint
        {
            Color = _options.ContourColor,
            StrokeWidth = basePaint.StrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        float accumulated = 0;
        for (int i = 1; i < contour.Points.Count; i++)
        {
            var p0 = contour.Points[i - 1];
            var p1 = contour.Points[i];
            float segmentLen = Distance(p0, p1);
            accumulated += segmentLen;

            if (accumulated >= _options.DepressionTickSpacing)
            {
                accumulated = 0;

                // Calculate perpendicular pointing inward (toward depression)
                float dx = p1.X - p0.X;
                float dy = p1.Y - p0.Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.1f) continue;

                // Perpendicular pointing right (inward for counterclockwise contour)
                float perpX = -dy / len;
                float perpY = dx / len;

                // Midpoint of segment
                float midX = (p0.X + p1.X) / 2;
                float midY = (p0.Y + p1.Y) / 2;

                // Draw tick
                canvas.DrawLine(
                    midX, midY,
                    midX + perpX * _options.DepressionTickLength,
                    midY + perpY * _options.DepressionTickLength,
                    tickPaint);
            }
        }
    }

    /// <summary>
    /// Draws elevation labels on contour lines.
    /// </summary>
    private void DrawContourLabels(SKCanvas canvas, List<ContourLine> contours)
    {
        using var labelPaint = new SKPaint
        {
            Color = _options.LabelColor,
            TextSize = _options.LabelFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.Default
        };

        using var backgroundPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill
        };

        foreach (var contour in contours)
        {
            if (contour.Points.Count < 10)
                continue;

            string label = $"{(int)contour.Elevation}";
            var textBounds = new SKRect();
            labelPaint.MeasureText(label, ref textBounds);

            // Find positions along the contour for labels
            float totalLength = 0;
            var lengths = new List<float> { 0 };

            for (int i = 1; i < contour.Points.Count; i++)
            {
                totalLength += Distance(contour.Points[i - 1], contour.Points[i]);
                lengths.Add(totalLength);
            }

            // Place first label at 25% along the line, then every LabelSpacingPixels
            float labelPos = totalLength * 0.25f;
            while (labelPos < totalLength - textBounds.Width)
            {
                // Find the segment containing this position
                int segIndex = 0;
                for (int i = 1; i < lengths.Count; i++)
                {
                    if (lengths[i] >= labelPos)
                    {
                        segIndex = i - 1;
                        break;
                    }
                }

                if (segIndex >= contour.Points.Count - 1)
                    break;

                var p0 = contour.Points[segIndex];
                var p1 = contour.Points[segIndex + 1];

                // Interpolate position
                float segLen = lengths[segIndex + 1] - lengths[segIndex];
                float t = segLen > 0 ? (labelPos - lengths[segIndex]) / segLen : 0;
                float labelX = p0.X + t * (p1.X - p0.X);
                float labelY = p0.Y + t * (p1.Y - p0.Y);

                // Calculate rotation angle
                float angle = (float)(Math.Atan2(p1.Y - p0.Y, p1.X - p0.X) * 180 / Math.PI);

                // Keep text upright
                if (angle > 90) angle -= 180;
                if (angle < -90) angle += 180;

                // Draw label with background
                canvas.Save();
                canvas.Translate(labelX, labelY);
                canvas.RotateDegrees(angle);

                // Background rectangle
                var bgRect = new SKRect(
                    -2, textBounds.Top - 1,
                    textBounds.Width + 2, textBounds.Bottom + 1);
                canvas.DrawRect(bgRect, backgroundPaint);

                // Text
                canvas.DrawText(label, 0, 0, labelPaint);
                canvas.Restore();

                labelPos += _options.LabelSpacingPixels;
            }
        }
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
