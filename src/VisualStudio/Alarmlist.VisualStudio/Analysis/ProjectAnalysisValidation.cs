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
            var configurations = await Task.Run(() => source.CaptureConfigurationsAsync(state.Project.Inputs, token), token).ConfigureAwait(false);
            var analyzers = await Task.Run(() => source.CaptureAnalyzersAsync(state.Project.Analyzers, token), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return captured.Count == state.Inputs.Count && captured.Select((input, index) => input.Matches(state.Inputs[index])).All(matches => matches)
                && resources.Count == state.Resources.Count && resources.Select((input, index) => input.Matches(state.Resources[index])).All(matches => matches)
                && lookups.Count == state.Lookups.Count && lookups.Select((input, index) => input.Matches(state.Lookups[index])).All(matches => matches)
                && configurations.Count == state.Configurations.Count && configurations.Select((input, index) => input.Matches(state.Configurations[index])).All(matches => matches)
                && analyzers.Count == state.Analyzers.Count && analyzers.Select((input, index) => input.Matches(state.Analyzers[index])).All(matches => matches);
        }
    }
}
