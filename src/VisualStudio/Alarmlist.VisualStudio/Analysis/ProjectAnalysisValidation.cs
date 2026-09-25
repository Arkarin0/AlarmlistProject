// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alarmlist.VisualStudio.Analysis
{
    internal static class ProjectAnalysisValidation
    {
        public static async Task<bool> InputsMatchAsync(IProjectAnalysisInputSource source, ProjectAnalysisState state, CancellationToken token)
        {
            var captured = await Task.Run(() => source.CaptureAsync(state.Project.Inputs, token), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return captured.Count == state.Inputs.Count && captured.Select((input, index) => input.Matches(state.Inputs[index])).All(matches => matches);
        }
    }
}
