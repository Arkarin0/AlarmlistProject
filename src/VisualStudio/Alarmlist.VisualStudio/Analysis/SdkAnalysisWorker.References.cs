// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed partial class SdkAnalysisWorker
    {
        private Type _referenceRequestType, _referenceTextType, _referenceScopeType, _referenceTargetType;
        private MethodInfo _findReferences, _findReferenceTargets, _previewRename, _previewRenames;

        private void InitializeReferences(Assembly assembly, Type compilerType)
        {
            _referenceRequestType = assembly.GetType("Alarmlist.Analysis.ReferenceCompilationSnapshot", true);
            _referenceTextType = assembly.GetType("Alarmlist.Analysis.ReferenceTextSnapshot", true);
            _referenceScopeType = assembly.GetType("Alarmlist.Analysis.ReferenceLibraryScope", true);
            Type target = assembly.GetType("Alarmlist.Analysis.ReferenceSymbolTarget", true);
            _referenceTargetType = target;
            _findReferences = compilerType.GetMethod("FindReferencesSnapshots", new[] { _referenceRequestType, target, typeof(CancellationToken) });
            _findReferenceTargets = compilerType.GetMethod("FindReferencesSnapshots", new[] { _referenceRequestType, typeof(IEnumerable<>).MakeGenericType(target), typeof(CancellationToken) });
            _previewRename = compilerType.GetMethod("PreviewRenameSnapshots", new[] { _referenceRequestType, target, typeof(string), typeof(CancellationToken), typeof(IReadOnlyCollection<string>), _referenceRequestType });
            _previewRenames = compilerType.GetMethod("PreviewRenameSnapshots", new[] { _referenceRequestType, typeof(IEnumerable<>).MakeGenericType(target), typeof(string), typeof(CancellationToken), typeof(IReadOnlyCollection<string>), _referenceRequestType });
            if (_findReferences == null || _findReferences.IsStatic || _findReferences.ReturnType != assembly.GetType("Alarmlist.Analysis.ReferenceSearchResult")
                || _findReferenceTargets == null || _findReferenceTargets.IsStatic || _findReferenceTargets.ReturnType != typeof(IReadOnlyList<>).MakeGenericType(_findReferences.ReturnType)
                || _previewRename == null || _previewRename.IsStatic || _previewRename.ReturnType != assembly.GetType("Alarmlist.Analysis.ReferenceRenamePreview")
                || _previewRenames == null || _previewRenames.IsStatic || _previewRenames.ReturnType != _previewRename.ReturnType
                || _referenceTextType.GetConstructor(new[] { typeof(string), typeof(string), typeof(int) }) == null
                || _propertyType.GetConstructor(new[] { typeof(string), typeof(string), _locationType, typeof(IEnumerable<>).MakeGenericType(_locationType), typeof(bool) }) == null
                || _referenceScopeType.GetConstructor(new[] { assembly.GetType("Alarmlist.Libraries.LibraryIdentity", true), typeof(IEnumerable<KeyValuePair<string, string>>) }) == null
                || _referenceRequestType.GetMethod("TargetAtToken", new[] { typeof(string), typeof(int), typeof(int) })?.ReturnType != target
                || _referenceRequestType.GetMethod("TargetAtLocation", new[] { typeof(string), _locationType, typeof(string) })?.ReturnType != target
                || _referenceRequestType.GetMethod("TargetIdentity", new[] { typeof(string) })?.ReturnType != target
                || _referenceRequestType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(_snapshotType), _contextType,
                    typeof(IEnumerable<>).MakeGenericType(_referenceTextType), _referenceScopeType, typeof(CancellationToken) }) == null)
                throw new NotSupportedException("The SDK has no compatible reference search and rename preview API.");
        }

        public RenameResponse PreviewRenames(AnalysisReferenceRequest request, AnalysisReferenceTarget[] targets, string newName,
            string[] includedGroups, AnalysisReferenceRequest validationInputs, AnalysisCancellationProbe cancellation)
        {
            using (var scope = new WorkerCancellation(cancellation))
            {
                try
                {
                    return WithReferenceRequest(request, scope.Token, captured =>
                    {
                        var selected = Array.CreateInstance(_referenceTargetType, targets.Length);
                        for (int index = 0; index < targets.Length; index++)
                        { scope.Token.ThrowIfCancellationRequested(); selected.SetValue(ReferenceTarget(captured, targets[index]), index); }
                        RenameResponse Invoke(object validation) => new RenameResponse(AnalysisAvailability.Available, null,
                            _reader.ReadRename(_previewRenames.Invoke(_compiler, new object[] { captured, selected, newName, scope.Token, includedGroups, validation })));
                        return validationInputs == null ? Invoke(null) : WithReferenceRequest(validationInputs, scope.Token, Invoke);
                    });
                }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(Unwrap(exception)))
                {
                    var failure = Unwrap(exception);
                    return new RenameResponse(failure is NotSupportedException ? AnalysisAvailability.UnsupportedContract : AnalysisAvailability.InvalidInput,
                        "SDK rename preview is unavailable: " + failure.Message);
                }
            }
        }

        public ReferenceBatchResponse FindReferenceTargets(AnalysisReferenceRequest request, AnalysisReferenceTarget[] targets, AnalysisCancellationProbe cancellation)
        {
            using (var scope = new WorkerCancellation(cancellation))
            {
                try
                {
                    return WithReferenceRequest(request, scope.Token, captured =>
                    {
                        var selected = Array.CreateInstance(_referenceTargetType, targets.Length);
                        for (int index = 0; index < targets.Length; index++)
                        { scope.Token.ThrowIfCancellationRequested(); selected.SetValue(ReferenceTarget(captured, targets[index]), index); }
                        var result = (System.Collections.IEnumerable)_findReferenceTargets.Invoke(_compiler, new object[] { captured, selected, scope.Token });
                        return new ReferenceBatchResponse(AnalysisAvailability.Available, null, _reader.ReadReferenceBatch(result.Cast<object>(), scope.Token));
                    });
                }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(Unwrap(exception)))
                {
                    var failure = Unwrap(exception);
                    return new ReferenceBatchResponse(failure is NotSupportedException ? AnalysisAvailability.UnsupportedContract : AnalysisAvailability.InvalidInput,
                        "SDK reference search is unavailable: " + failure.Message);
                }
            }
        }

        private T WithReferenceRequest<T>(AnalysisReferenceRequest request, CancellationToken token, Func<object, T> operation)
            => WithCapturedInputs(request.Sources.ToArray(), request.Context, (sources, context) =>
            {
                token.ThrowIfCancellationRequested();
                var documents = Array.CreateInstance(_referenceTextType, request.ProjectDocuments.Count);
                for (int i = 0; i < documents.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var input = request.ProjectDocuments[i];
                    documents.SetValue(Activator.CreateInstance(_referenceTextType, input.Text, input.FilePath, input.Version), i);
                }
                object scope = null;
                if (request.DeclaringLibrary != null)
                {
                    var identityType = _contextType.Assembly.GetType("Alarmlist.Libraries.LibraryIdentity", true);
                    scope = Activator.CreateInstance(_referenceScopeType, Activator.CreateInstance(identityType, request.DeclaringLibrary.Id, request.DeclaringLibrary.Version), request.PortableNames);
                }
                var captured = Activator.CreateInstance(_referenceRequestType, sources, context, documents, scope, token);
                return operation(captured);
            });

        private object ReferenceTarget(object request, AnalysisReferenceTarget target)
        {
            string stamp = (string)_referenceRequestType.GetProperty("SnapshotId").GetValue(request);
            if (target.SnapshotId != null && target.SnapshotId != stamp)
                throw new InvalidOperationException("Captured inputs changed. Find references again before previewing or applying a rename.");
            if (target.Kind == "Identity") return _referenceRequestType.GetMethod("TargetIdentity").Invoke(request, new object[] { target.DeclarationId });
            if (target.Kind == "Token")
            {
                if (target.Location?.SourceVersion == null || target.Offset == null) throw new ArgumentException("A token target requires a captured source version and offset.");
                return _referenceRequestType.GetMethod("TargetAtToken").Invoke(request, new object[] { target.Location.FilePath, target.Location.SourceVersion.Value, target.Offset.Value });
            }
            if (target.Location == null) throw new ArgumentException("A declaration target requires its defining source location.");
            object location = Activator.CreateInstance(_locationType, target.Location.FilePath, target.Location.LineNumber, target.Location.ColumnNumber,
                target.Location.EndColumnNumber - target.Location.ColumnNumber, target.Location.SourceVersion);
            return _referenceRequestType.GetMethod("TargetAtLocation").Invoke(request, new[] { (object)target.Kind, location, target.Name });
        }

        public ReferenceResponse FindReferences(AnalysisReferenceRequest request, AnalysisReferenceTarget target, AnalysisCancellationProbe cancellation)
        {
            using (var scope = new WorkerCancellation(cancellation))
            {
                try
                {
                    return WithReferenceRequest(request, scope.Token, captured => new ReferenceResponse(AnalysisAvailability.Available, null,
                        _reader.ReadReferences(_findReferences.Invoke(_compiler, new[] { captured, ReferenceTarget(captured, target), (object)scope.Token }))));
                }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(Unwrap(exception)))
                {
                    var failure = Unwrap(exception);
                    return new ReferenceResponse(failure is NotSupportedException ? AnalysisAvailability.UnsupportedContract : AnalysisAvailability.InvalidInput,
                        "SDK reference search is unavailable: " + failure.Message);
                }
            }
        }

        public RenameResponse PreviewRename(AnalysisReferenceRequest request, AnalysisReferenceTarget target, string newName,
            string[] includedGroups, AnalysisReferenceRequest validationInputs, AnalysisCancellationProbe cancellation)
        {
            using (var scope = new WorkerCancellation(cancellation))
            {
                try
                {
                    return WithReferenceRequest(request, scope.Token, captured =>
                    {
                        var selected = ReferenceTarget(captured, target);
                        RenameResponse Invoke(object validation) => new RenameResponse(AnalysisAvailability.Available, null,
                            _reader.ReadRename(_previewRename.Invoke(_compiler, new object[] { captured, selected, newName, scope.Token, includedGroups, validation })));
                        return validationInputs == null ? Invoke(null) : WithReferenceRequest(validationInputs, scope.Token, Invoke);
                    });
                }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(Unwrap(exception)))
                {
                    var failure = Unwrap(exception);
                    return new RenameResponse(failure is NotSupportedException ? AnalysisAvailability.UnsupportedContract : AnalysisAvailability.InvalidInput,
                        "SDK rename preview is unavailable: " + failure.Message);
                }
            }
        }
    }
}
