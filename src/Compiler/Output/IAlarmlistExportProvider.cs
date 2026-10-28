// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace Alarmlist.Output
{
    /// <summary>Trusted build extension. Return artifacts; the host owns publication.</summary>
    public interface IAlarmlistExportProvider
    {
        string Id { get; }
        int ContractVersion { get; }
        /// <summary>Null accepts any definition through metadata; otherwise require this semantic fingerprint.</summary>
        string RequiredDefinitionFingerprint { get; }
        IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken);
    }

    public sealed class ExportRequest
    {
        public ExportRequest(ExportDocument document, string fileName, IReadOnlyDictionary<string, string> options = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("An output filename is required.", nameof(fileName));
            FileName = fileName;
            Options = new ReadOnlyDictionary<string, string>((options ?? new Dictionary<string, string>())
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        }
        public ExportDocument Document { get; }
        public string FileName { get; }
        public IReadOnlyDictionary<string, string> Options { get; }

        public void ValidateCompatibility(IAlarmlistExportProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (provider.ContractVersion != ExportDocument.ContractVersion)
                throw new NotSupportedException("Export provider '" + provider.Id + "' has an incompatible contract. Rebuild the provider against the matching tooling.");
            if (provider.RequiredDefinitionFingerprint != null && !string.Equals(provider.RequiredDefinitionFingerprint, Document.DefinitionFingerprint, StringComparison.Ordinal))
                throw new NotSupportedException("Export provider '" + provider.Id + "' requires a different alarm definition. Rebuild the provider against the matching definition.");
        }
    }
}
