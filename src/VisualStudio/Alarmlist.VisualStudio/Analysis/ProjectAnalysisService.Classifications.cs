// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed partial class ProjectAnalysisService
    {
        public Task<ClassificationResponse> ClassifyAsync(ProjectAnalysisState state, AnalysisClassificationTarget target, CancellationToken token)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (target == null) throw new ArgumentNullException(nameof(target));
            token.ThrowIfCancellationRequested();
            return Task.Run(async () =>
            {
                ClassificationResponse ChangedInputs() => new ClassificationResponse(AnalysisAvailability.InvalidInput, "Inputs changed. Classify the current snapshot again.");
                using (var obsolete = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    var cancellationGate = new object(); bool finished = false;
                    EventHandler changed = (sender, args) =>
                    {
                        lock (cancellationGate) if (!finished && !ReferenceEquals(Current, state)) obsolete.Cancel();
                    };
                    Changed += changed;
                    try
                    {
                        if (!ReferenceEquals(Current, state) || state.Status != ProjectAnalysisStatus.Available) return ChangedInputs();
                        var response = ChangedInputs();
                        await _analysisGate.ExecuteAsync(async () =>
                        {
                            var currentToken = obsolete.Token; currentToken.ThrowIfCancellationRequested();
                            if (!ReferenceEquals(Current, state) || !await ProjectAnalysisValidation.InputsMatchAsync(_inputs, state, currentToken).ConfigureAwait(false)) return;
                            if (!(_bridge is ISdkClassificationBridge classification))
                            { response = new ClassificationResponse(AnalysisAvailability.UnsupportedContract, "Restore matching SDK tools with expression classification."); return; }
                            var context = state.Project.Context.WithSavedInputs(state.Resources, state.Lookups, state.Configurations, state.Analyzers);
                            if (state.Libraries != null) context = context.WithLibraries(state.Libraries.References);
                            var captured = new AnalysisReferenceRequest(state.Inputs.Select(input => input.Input), context);
                            response = classification.Classify(new AnalysisClassificationRequest(captured, target), currentToken);
                            if (!ReferenceEquals(Current, state) || !await ProjectAnalysisValidation.InputsMatchAsync(_inputs, state, currentToken).ConfigureAwait(false)) response = ChangedInputs();
                        }, obsolete.Token).ConfigureAwait(false);
                        return response;
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { return ChangedInputs(); }
                    finally
                    {
                        Changed -= changed;
                        lock (cancellationGate) finished = true;
                    }
                }
            }, token);
        }
    }
}
