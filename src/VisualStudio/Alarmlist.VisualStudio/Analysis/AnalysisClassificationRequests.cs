// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Frozen original compiler inputs and a separately versioned presentation target.</summary>
    [Serializable]
    internal sealed class AnalysisClassificationRequest
    {
        public AnalysisClassificationRequest(AnalysisReferenceRequest inputs, AnalysisClassificationTarget target)
        { Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs)); Target = target ?? throw new ArgumentNullException(nameof(target)); }
        public AnalysisReferenceRequest Inputs { get; }
        public AnalysisClassificationTarget Target { get; }
    }

    [Serializable]
    internal sealed class ClassificationResponse
    {
        public ClassificationResponse(AnalysisAvailability availability, string message, AnalysisClassificationResult result = null)
        { Availability = availability; Message = message; Result = result; }
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public AnalysisClassificationResult Result { get; }
    }
}
