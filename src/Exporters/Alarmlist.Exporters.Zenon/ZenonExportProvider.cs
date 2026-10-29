// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Alarmlist.Output;

namespace Alarmlist.Exporters.Zenon
{
    /// <summary>Existing alarm XML with compiler-owned localization keys rendered for zenon language switching.</summary>
    public sealed class ZenonExportProvider : IAlarmlistExportProvider
    {
        public string Id => "Zenon";
        public int ContractVersion => ExportDocument.ContractVersion;
        public string RequiredDefinitionFingerprint => null;

        public IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.ValidateCompatibility(this);
            if (request.Options.Count != 0) throw new ArgumentException("The zenon keyword XML provider does not support provider options.", nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            return new XmlAlarmlistExportProvider(RenderScalar, RenderText).Export(request, cancellationToken);
        }

        private static string RenderScalar(ExportScalar value)
        {
            if (value == null) return null;
            if (value.Text != null && (value.IsDeferred || value.Text.Segments.Any(segment => segment.Kind == ExportTextSegmentKind.LocalizationKey)))
                return RenderText(value.Text);
            ValidateLiteral(value.CanonicalText);
            return value.CanonicalText;
        }

        private static string RenderText(ExportText text)
        {
            if (text == null) return null;
            var result = new StringBuilder();
            bool previousKey = false;
            foreach (ExportTextSegment segment in text.Segments)
            {
                if (segment.Kind == ExportTextSegmentKind.Literal)
                {
                    ValidateLiteral(segment.Text);
                    result.Append(segment.Text);
                    if (segment.Text.Length != 0) previousKey = false;
                }
                else
                {
                    if (segment.RequiresSubstitution)
                        throw new NotSupportedException("zenon keyword XML cannot represent localization key '" + segment.Text + "' requiring parameter/argument substitution. Use separate plain keyword references and literal values.");
                    if (previousKey)
                        throw new NotSupportedException("zenon keyword XML requires a literal separator between adjacent localization keys.");
                    ValidateLiteral(segment.Text);
                    result.Append('@').Append(segment.Text).Append('@');
                    previousKey = true;
                }
            }
            return result.ToString();
        }

        private static void ValidateLiteral(string text)
        {
            if (text?.IndexOf('@') >= 0)
                throw new NotSupportedException("zenon keyword XML cannot represent a literal '@' delimiter safely.");
        }
    }
}
