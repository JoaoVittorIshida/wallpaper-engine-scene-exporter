using WallpaperSceneExporter.Helpers;
using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Tests;

public sealed class PipelineStateMachineTests
{
    [Fact]
    public void HappyPathReachesCompleted()
    {
        var machine = new PipelineStateMachine();
        machine.MoveTo(PipelineState.Preparing);
        machine.MoveTo(PipelineState.Rendering);
        machine.MoveTo(PipelineState.Recording);
        machine.MoveTo(PipelineState.Converting);
        machine.MoveTo(PipelineState.Completed);
        Assert.Equal(PipelineState.Completed, machine.Current);
    }

    [Fact]
    public void InvalidTransitionIsRejected()
    {
        var machine = new PipelineStateMachine();
        Assert.Throws<InvalidOperationException>(() => machine.MoveTo(PipelineState.Completed));
    }
}
