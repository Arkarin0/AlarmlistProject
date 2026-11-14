// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Reflection;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed partial class SdkAnalysisWorker
    {
        private Type _classificationRequestType, _classificationTargetType;
        private MethodInfo _classifyExpressions;
        private void InitializeClassifications(Assembly assembly, Type compilerType)
        {
            _classificationRequestType = assembly.GetType("Alarmlist.Analysis.ExpressionClassificationRequest", true);
            _classificationTargetType = assembly.GetType("Alarmlist.Analysis.ExpressionClassificationTarget", true);
            _classifyExpressions = compilerType.GetMethod("ClassifyExpressionsSnapshots", new[] { _classificationRequestType, typeof(CancellationToken) });
            if (_classifyExpressions == null || _classifyExpressions.IsStatic
                || _classifyExpressions.ReturnType != assembly.GetType("Alarmlist.Analysis.ExpressionClassificationResult")
                || _classificationRequestType.GetConstructor(new[] { _referenceRequestType, _classificationTargetType }) == null
                || _classificationTargetType.GetConstructor(new[] { typeof(string), typeof(string), _referenceTextType,
                    _explanationTargetType, _locationType, typeof(bool), typeof(bool) }) == null)
                throw new NotSupportedException("The SDK has no compatible expression classification API.");
        }

        public ClassificationResponse Classify(AnalysisClassificationRequest request, AnalysisCancellationProbe cancellation)
        {
            using (var scope = new WorkerCancellation(cancellation))
            {
                try
                {
                    return WithReferenceRequest(request.Inputs, scope.Token, captured =>
                    {
                        object Location(AnalysisLocation location) => location == null ? null : Activator.CreateInstance(_locationType,
                            location.FilePath, location.LineNumber, location.ColumnNumber, location.EndColumnNumber - location.ColumnNumber, location.SourceVersion);
                        var target = request.Target;
                        var text = Activator.CreateInstance(_referenceTextType, target.Text.Text, target.Text.FilePath, target.Text.Version);
                        var value = target.ValueTarget;
                        object selected = value == null ? null : Activator.CreateInstance(_explanationTargetType,
                            Location(value.AlarmLocation), value.Kind, value.Name, value.FieldName, Location(value.EntryLocation));
                        var compilerTarget = Activator.CreateInstance(_classificationTargetType, target.Id, target.Kind,
                            text, selected, Location(target.ValueLocation), target.IsDraft, target.IsResolved);
                        var compilerRequest = Activator.CreateInstance(_classificationRequestType, captured, compilerTarget);
                        var result = _classifyExpressions.Invoke(_compiler, new object[] { compilerRequest, scope.Token });
                        scope.Token.ThrowIfCancellationRequested();
                        return new ClassificationResponse(AnalysisAvailability.Available, null, _reader.ReadClassifications(result));
                    }, includeAnalyzers: false);
                }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(Unwrap(exception)))
                {
                    var failure = Unwrap(exception);
                    return new ClassificationResponse(failure is NotSupportedException ? AnalysisAvailability.UnsupportedContract : AnalysisAvailability.InvalidInput,
                        "SDK expression classification is unavailable: " + failure.Message);
                }
            }
        }
    }
}
