using System;

namespace MilMap.CLI;

/// <summary>
/// Provides console-based progress reporting with a progress bar display.
/// </summary>
public sealed class ConsoleProgressReporter<T> : IProgress<T>
{
    private readonly Func<T, (string Message, double Progress)> _formatter;
    private readonly int _barWidth;
    private readonly object _lock = new();
    private bool _lastLineWasProgress;

    /// <summary>
    /// Creates a console progress reporter.
    /// </summary>
    /// <param name="formatter">Function to extract message and progress (0.0-1.0) from progress value.</param>
    /// <param name="barWidth">Width of the progress bar in characters. Default is 30.</param>
    public ConsoleProgressReporter(Func<T, (string Message, double Progress)> formatter, int barWidth = 30)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _barWidth = barWidth;
    }

    /// <summary>
    /// Reports progress to the console.
    /// </summary>
    public void Report(T value)
    {
        var (message, progress) = _formatter(value);
        
        lock (_lock)
        {
            // Clamp progress to valid range
            progress = Math.Clamp(progress, 0.0, 1.0);
            
            // Calculate bar fill
            int filled = (int)(progress * _barWidth);
            int empty = _barWidth - filled;
            
            // Build progress bar
            string bar = new string('█', filled) + new string('░', empty);
            string percentText = $"{progress * 100:F0}%".PadLeft(4);
            
            // Clear line and write progress
            if (_lastLineWasProgress)
            {
                Console.Write('\r');
            }
            
            Console.Write($"  [{bar}] {percentText} {message}");
            
            // Pad with spaces to clear any remaining characters from previous output
            int lineLength = _barWidth + percentText.Length + message.Length + 6;
            int consolWidth = GetConsoleWidth();
            if (lineLength < consolWidth)
            {
                Console.Write(new string(' ', consolWidth - lineLength - 1));
            }
            
            _lastLineWasProgress = true;
            
            // If complete, move to next line
            if (progress >= 1.0)
            {
                Console.WriteLine();
                _lastLineWasProgress = false;
            }
        }
    }

    /// <summary>
    /// Completes the progress display with a final message.
    /// </summary>
    public void Complete(string? message = null)
    {
        lock (_lock)
        {
            if (_lastLineWasProgress)
            {
                Console.WriteLine();
                _lastLineWasProgress = false;
            }
            
            if (!string.IsNullOrEmpty(message))
            {
                Console.WriteLine($"  ✓ {message}");
            }
        }
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            return 80; // Default width if console width unavailable
        }
    }
}

/// <summary>
/// Extension methods for creating console progress reporters.
/// </summary>
public static class ConsoleProgress
{
    /// <summary>
    /// Creates a progress reporter for tile download operations.
    /// </summary>
    public static ConsoleProgressReporter<MilMap.Core.Progress.TileDownloadProgress> ForTileDownload()
    {
        return new ConsoleProgressReporter<MilMap.Core.Progress.TileDownloadProgress>(p =>
        {
            string status;
            if (p.FromCache > 0 && p.Downloaded > 0)
            {
                status = $"Downloading tiles... {p.Downloaded + p.FromCache}/{p.Total} ({p.FromCache} cached)";
            }
            else if (p.FromCache > 0)
            {
                status = $"Loading tiles from cache... {p.FromCache}/{p.Total}";
            }
            else
            {
                status = $"Downloading tiles... {p.Downloaded}/{p.Total}";
            }
            
            if (p.Failed > 0)
            {
                status += $" ({p.Failed} failed)";
            }
            
            return (status, p.Progress);
        });
    }

    /// <summary>
    /// Creates a progress reporter for render operations.
    /// </summary>
    public static ConsoleProgressReporter<MilMap.Core.Progress.RenderProgress> ForRender()
    {
        return new ConsoleProgressReporter<MilMap.Core.Progress.RenderProgress>(p =>
        {
            string phaseName = p.Phase switch
            {
                MilMap.Core.Progress.RenderPhase.BaseMap => "Compositing base map",
                MilMap.Core.Progress.RenderPhase.MgrsGrid => "Drawing MGRS grid",
                MilMap.Core.Progress.RenderPhase.Contours => "Rendering contours",
                MilMap.Core.Progress.RenderPhase.ScaleBar => "Adding scale bar",
                MilMap.Core.Progress.RenderPhase.Symbology => "Applying symbology",
                MilMap.Core.Progress.RenderPhase.Legend => "Generating legend",
                MilMap.Core.Progress.RenderPhase.Encoding => "Encoding output",
                _ => p.Description
            };
            
            return (phaseName, p.PhaseProgress);
        });
    }

    /// <summary>
    /// Creates a progress reporter for general step-based operations.
    /// </summary>
    public static ConsoleProgressReporter<MilMap.Core.Progress.ProgressInfo> ForSteps()
    {
        return new ConsoleProgressReporter<MilMap.Core.Progress.ProgressInfo>(p =>
        {
            string message = $"[{p.CurrentStep}/{p.TotalSteps}] {p.StepDescription}";
            return (message, p.OverallProgress);
        });
    }
}
