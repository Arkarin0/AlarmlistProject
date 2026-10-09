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
            var resources = await Task.Run(() => source.CaptureResourcesAsync(state.Project.Resources, token), token).ConfigureAwait(false);
            var lookups = await Task.Run(() => source.CaptureLookupsAsync(state.Project.Lookups, token), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return captured.Count == state.Inputs.Count && captured.Select((input, index) => input.Matches(state.Inputs[index])).All(matches => matches)
                && resources.Count == state.Resources.Count && resources.Select((input, index) => input.Matches(state.Resources[index])).All(matches => matches)
                && lookups.Count == state.Lookups.Count && lookups.Select((input, index) => input.Matches(state.Lookups[index])).All(matches => matches);
        }
    }
}
