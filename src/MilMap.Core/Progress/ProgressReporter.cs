using System;

namespace MilMap.Core.Progress;

/// <summary>
/// Represents progress information for a multi-step operation.
/// </summary>
public record ProgressInfo
{
    /// <summary>
    /// Current step in the operation (1-based).
    /// </summary>
    public int CurrentStep { get; init; }

    /// <summary>
    /// Total number of steps in the operation.
    /// </summary>
    public int TotalSteps { get; init; }

    /// <summary>
    /// Description of the current step.
    /// </summary>
    public string StepDescription { get; init; } = string.Empty;

    /// <summary>
    /// Optional sub-progress within the current step (0.0 to 1.0).
    /// </summary>
    public double? SubProgress { get; init; }

    /// <summary>
    /// Overall progress as a percentage (0.0 to 1.0).
    /// </summary>
    public double OverallProgress => TotalSteps > 0
        ? (CurrentStep - 1 + (SubProgress ?? 1.0)) / TotalSteps
        : 0.0;
}

/// <summary>
/// Progress information specific to tile download operations.
/// </summary>
public record TileDownloadProgress
{
    /// <summary>
    /// Number of tiles downloaded so far.
    /// </summary>
    public int Downloaded { get; init; }

    /// <summary>
    /// Number of tiles loaded from cache.
    /// </summary>
    public int FromCache { get; init; }

    /// <summary>
    /// Total number of tiles to process.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Number of tiles that failed to download.
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// Progress as a percentage (0.0 to 1.0).
    /// </summary>
    public double Progress => Total > 0 ? (double)(Downloaded + FromCache) / Total : 0.0;
}

/// <summary>
/// Progress information specific to render operations.
/// </summary>
public record RenderProgress
{
    /// <summary>
    /// Current render phase.
    /// </summary>
    public RenderPhase Phase { get; init; }

    /// <summary>
    /// Description of current rendering activity.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Progress within the current phase (0.0 to 1.0).
    /// </summary>
    public double PhaseProgress { get; init; }
}

/// <summary>
/// Phases of the rendering process.
/// </summary>
public enum RenderPhase
{
    /// <summary>Base map tile compositing.</summary>
    BaseMap,
    /// <summary>MGRS grid overlay rendering.</summary>
    MgrsGrid,
    /// <summary>Contour line rendering.</summary>
    Contours,
    /// <summary>Scale bar rendering.</summary>
    ScaleBar,
    /// <summary>Military symbology rendering.</summary>
    Symbology,
    /// <summary>Legend generation.</summary>
    Legend,
    /// <summary>Final output encoding.</summary>
    Encoding
}

/// <summary>
/// Factory for creating progress reporters.
/// </summary>
public static class ProgressReporter
{
    /// <summary>
    /// Creates a no-op progress reporter that discards all progress updates.
    /// </summary>
    public static IProgress<T> None<T>() => new NullProgress<T>();

    private sealed class NullProgress<T> : IProgress<T>
    {
        public void Report(T value) { }
    }
}
