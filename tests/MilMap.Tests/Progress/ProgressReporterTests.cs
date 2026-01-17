using MilMap.Core.Progress;
using Xunit;

namespace MilMap.Tests.Progress;

public class ProgressReporterTests
{
    [Fact]
    public void ProgressInfo_OverallProgress_CalculatesCorrectly()
    {
        var progress = new ProgressInfo
        {
            CurrentStep = 2,
            TotalSteps = 4,
            StepDescription = "Step 2",
            SubProgress = 0.5
        };

        // Step 2 at 50% = (2-1 + 0.5) / 4 = 1.5 / 4 = 0.375
        Assert.Equal(0.375, progress.OverallProgress);
    }

    [Fact]
    public void ProgressInfo_OverallProgress_DefaultsToCompleteStepWhenNoSubProgress()
    {
        var progress = new ProgressInfo
        {
            CurrentStep = 3,
            TotalSteps = 4,
            StepDescription = "Step 3"
        };

        // Step 3 complete = (3-1 + 1.0) / 4 = 3 / 4 = 0.75
        Assert.Equal(0.75, progress.OverallProgress);
    }

    [Fact]
    public void ProgressInfo_OverallProgress_ReturnsZeroWhenNoSteps()
    {
        var progress = new ProgressInfo
        {
            CurrentStep = 0,
            TotalSteps = 0
        };

        Assert.Equal(0.0, progress.OverallProgress);
    }

    [Fact]
    public void TileDownloadProgress_Progress_CalculatesCorrectly()
    {
        var progress = new TileDownloadProgress
        {
            Downloaded = 5,
            FromCache = 3,
            Total = 10,
            Failed = 1
        };

        // (5 + 3) / 10 = 0.8
        Assert.Equal(0.8, progress.Progress);
    }

    [Fact]
    public void TileDownloadProgress_Progress_ReturnsZeroWhenNoTotal()
    {
        var progress = new TileDownloadProgress
        {
            Downloaded = 0,
            FromCache = 0,
            Total = 0
        };

        Assert.Equal(0.0, progress.Progress);
    }

    [Fact]
    public void RenderProgress_StoresPhaseAndProgress()
    {
        var progress = new RenderProgress
        {
            Phase = RenderPhase.MgrsGrid,
            Description = "Drawing grid lines",
            PhaseProgress = 0.5
        };

        Assert.Equal(RenderPhase.MgrsGrid, progress.Phase);
        Assert.Equal("Drawing grid lines", progress.Description);
        Assert.Equal(0.5, progress.PhaseProgress);
    }

    [Fact]
    public void ProgressReporter_None_ReturnsNonNullReporter()
    {
        var reporter = ProgressReporter.None<TileDownloadProgress>();
        Assert.NotNull(reporter);
    }

    [Fact]
    public void ProgressReporter_None_AcceptsReportsWithoutError()
    {
        var reporter = ProgressReporter.None<TileDownloadProgress>();
        
        // Should not throw
        reporter.Report(new TileDownloadProgress
        {
            Downloaded = 1,
            Total = 10
        });
    }

    [Theory]
    [InlineData(RenderPhase.BaseMap)]
    [InlineData(RenderPhase.MgrsGrid)]
    [InlineData(RenderPhase.Contours)]
    [InlineData(RenderPhase.ScaleBar)]
    [InlineData(RenderPhase.Symbology)]
    [InlineData(RenderPhase.Legend)]
    [InlineData(RenderPhase.Encoding)]
    public void RenderPhase_HasAllExpectedValues(RenderPhase phase)
    {
        // Verify all enum values are defined
        Assert.True(Enum.IsDefined(typeof(RenderPhase), phase));
    }
}
