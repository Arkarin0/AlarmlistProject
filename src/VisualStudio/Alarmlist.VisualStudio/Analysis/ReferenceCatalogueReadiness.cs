// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alarmlist.VisualStudio.Analysis
{
    internal static class ReferenceCatalogueReadiness
    {
        // Opening a source starts ordinary analysis. Wait for that known active
        // context, not an invented delay. Changes to membership/configuration
        // return immediately so whole-capture comparison can reject the rebase.
        internal static async Task WaitAsync(IReferenceSolutionCatalogue catalogue, CapturedReferenceSolution original, CancellationToken token)
        {
            var keys = new HashSet<string>(original.Selections.Where(selection => selection.Unavailable == null).Select(ContextKey), StringComparer.Ordinal);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler handler = (sender, args) => changed.TrySetResult(true);
                catalogue.Changed += handler;
                try
                {
                    using (token.Register(() => changed.TrySetCanceled()))
                    {
                        var snapshot = await catalogue.CaptureAsync(token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        if (!snapshot.Projects.Any(project => project.IsRefreshing && keys.Contains(ContextKey(project)))) return;
                        await changed.Task.ConfigureAwait(false);
                    }
                }
                finally { catalogue.Changed -= handler; }
            }
        }
        private static string ContextKey(ReferenceProjectSelection project)
        {
            string Value(string name) => project.Globals.TryGetValue(name, out string value) ? value ?? "" : "";
            // Pending CPS snapshots may carry the previous solution mapping.
            // This key identifies what to await, never whether to accept it:
            // MatchesAfterOpening still compares every global after readiness.
            return project.ProjectPath.ToUpperInvariant() + "\n" + project.UseOpenBuffers + "\n" + Value("Configuration").ToUpperInvariant()
                + "\n" + ReferenceProjectSelection.NormalizePlatform(Value("Platform")).ToUpperInvariant() + "\n" + Value("AlarmlistCulture").ToUpperInvariant();
        }
    }
}
