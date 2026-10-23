using WallpaperSceneExporter.Models;

namespace WallpaperSceneExporter.Helpers;

public sealed class PipelineStateMachine
{
    private static readonly IReadOnlyDictionary<PipelineState, PipelineState[]> Allowed =
        new Dictionary<PipelineState, PipelineState[]>
        {
            [PipelineState.Waiting] = [PipelineState.Preparing, PipelineState.Error],
            [PipelineState.Preparing] = [PipelineState.Rendering, PipelineState.Error],
            [PipelineState.Rendering] = [PipelineState.Recording, PipelineState.Error],
            [PipelineState.Recording] = [PipelineState.Converting, PipelineState.Error],
            [PipelineState.Converting] = [PipelineState.Completed, PipelineState.Error],
            [PipelineState.Completed] = [PipelineState.Waiting, PipelineState.Preparing],
            [PipelineState.Error] = [PipelineState.Waiting, PipelineState.Preparing]
        };

    public PipelineState Current { get; private set; } = PipelineState.Waiting;

    public void MoveTo(PipelineState next)
    {
        if (next == Current) return;
        if (!Allowed[Current].Contains(next))
            throw new InvalidOperationException($"Transição de estado inválida: {Current} -> {next}.");
        Current = next;
    }
}
