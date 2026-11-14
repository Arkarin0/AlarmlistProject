// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed partial class SdkAnalysisBridge
    {
        public ClassificationResponse Classify(AnalysisClassificationRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_domain == null) throw new ObjectDisposedException(nameof(SdkAnalysisBridge));
                cancellationToken.ThrowIfCancellationRequested();
                ClassificationResponse response;
                try { response = WithHostAssemblyResolution(() => _worker.Classify(request, new AnalysisCancellationProbe(cancellationToken))); }
                catch (Exception exception) when (IsRecoverable(exception))
                { response = new ClassificationResponse(AnalysisAvailability.AnalysisFailed, "SDK expression classification failed: " + exception.Message); }
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
        }
    }
}
