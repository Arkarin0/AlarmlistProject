// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed partial class SdkAnalysisBridge
    {
        public ReferenceBatchResponse FindReferences(AnalysisReferenceRequest request, IReadOnlyList<AnalysisReferenceTarget> targets, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            cancellationToken.ThrowIfCancellationRequested();
            var captured = targets.ToArray();
            if (captured.Any(target => target == null)) throw new ArgumentException("A reference target cannot be null.", nameof(targets));
            lock (_gate)
            {
                if (_domain == null) throw new ObjectDisposedException(nameof(SdkAnalysisBridge));
                cancellationToken.ThrowIfCancellationRequested();
                ReferenceBatchResponse response;
                try { response = WithHostAssemblyResolution(() => _worker.FindReferenceTargets(request, captured, new AnalysisCancellationProbe(cancellationToken))); }
                catch (Exception exception) when (IsRecoverable(exception)) { response = new ReferenceBatchResponse(AnalysisAvailability.AnalysisFailed, "SDK reference search failed: " + exception.Message); }
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
        }

        public ReferenceResponse FindReferences(AnalysisReferenceRequest request, AnalysisReferenceTarget target, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (target == null) throw new ArgumentNullException(nameof(target));
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_domain == null) throw new ObjectDisposedException(nameof(SdkAnalysisBridge));
                cancellationToken.ThrowIfCancellationRequested();
                ReferenceResponse response;
                try { response = WithHostAssemblyResolution(() => _worker.FindReferences(request, target, new AnalysisCancellationProbe(cancellationToken))); }
                catch (Exception exception) when (IsRecoverable(exception)) { response = new ReferenceResponse(AnalysisAvailability.AnalysisFailed, "SDK reference search failed: " + exception.Message); }
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
        }

        public RenameResponse PreviewRename(AnalysisReferenceRequest request, AnalysisReferenceTarget target, string newName,
            CancellationToken cancellationToken = default, IReadOnlyCollection<string> includedGroups = null, AnalysisReferenceRequest validationInputs = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (target == null) throw new ArgumentNullException(nameof(target));
            cancellationToken.ThrowIfCancellationRequested();
            var selection = includedGroups?.ToArray();
            lock (_gate)
            {
                if (_domain == null) throw new ObjectDisposedException(nameof(SdkAnalysisBridge));
                cancellationToken.ThrowIfCancellationRequested();
                RenameResponse response;
                try { response = WithHostAssemblyResolution(() => _worker.PreviewRename(request, target, newName, selection, validationInputs, new AnalysisCancellationProbe(cancellationToken))); }
                catch (Exception exception) when (IsRecoverable(exception)) { response = new RenameResponse(AnalysisAvailability.AnalysisFailed, "SDK rename preview failed: " + exception.Message); }
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
        }

        public RenameResponse PreviewRename(AnalysisReferenceRequest request, IReadOnlyList<AnalysisReferenceTarget> targets, string newName,
            CancellationToken cancellationToken = default, IReadOnlyCollection<string> includedGroups = null, AnalysisReferenceRequest validationInputs = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            cancellationToken.ThrowIfCancellationRequested();
            var selected = targets.ToArray(); var selection = includedGroups?.ToArray();
            if (selected.Length == 0 || selected.Any(target => target == null)) throw new ArgumentException("At least one non-null symbol target is required.", nameof(targets));
            lock (_gate)
            {
                if (_domain == null) throw new ObjectDisposedException(nameof(SdkAnalysisBridge));
                cancellationToken.ThrowIfCancellationRequested();
                RenameResponse response;
                try { response = WithHostAssemblyResolution(() => _worker.PreviewRenames(request, selected, newName, selection, validationInputs, new AnalysisCancellationProbe(cancellationToken))); }
                catch (Exception exception) when (IsRecoverable(exception)) { response = new RenameResponse(AnalysisAvailability.AnalysisFailed, "SDK rename preview failed: " + exception.Message); }
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
        }
    }
}
