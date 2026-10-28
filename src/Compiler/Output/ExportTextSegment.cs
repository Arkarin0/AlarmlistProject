// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Output
{
    public enum ExportTextSegmentKind { Literal, LocalizationKey }

    /// <summary>A compiler-owned text boundary; providers never infer keys from rendered text.</summary>
    public sealed class ExportTextSegment
    {
        private ExportTextSegment(ExportTextSegmentKind kind, string text, ExportText expansion = null,
            bool requiresSubstitution = false)
        { Kind = kind; Text = text; Expansion = expansion; RequiresSubstitution = requiresSubstitution; }

        public ExportTextSegmentKind Kind { get; }
        public string Text { get; }
        /// <summary>Compiler-resolved translation expansion, unavailable in keyword mode.</summary>
        public ExportText Expansion { get; }
        /// <summary>The key requires parameter or argument substitution beyond HMI keyword lookup.</summary>
        public bool RequiresSubstitution { get; }

        public static ExportTextSegment Literal(string text) => new ExportTextSegment(
            ExportTextSegmentKind.Literal, text ?? throw new ArgumentNullException(nameof(text)));

        public static ExportTextSegment LocalizationKey(string key, ExportText expansion = null, bool requiresSubstitution = false)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A localization key is required.", nameof(key));
            return new ExportTextSegment(ExportTextSegmentKind.LocalizationKey, key, expansion, requiresSubstitution);
        }
    }

    /// <summary>Resolved text and its ordered keyword-preserving representation.</summary>
    public sealed class ExportText
    {
        public ExportText(string resolvedText, IEnumerable<ExportTextSegment> segments)
        {
            ResolvedText = resolvedText ?? throw new ArgumentNullException(nameof(resolvedText));
            var captured = (segments ?? throw new ArgumentNullException(nameof(segments))).ToArray();
            if (captured.Any(segment => segment == null)) throw new ArgumentException("Text segments cannot be null.", nameof(segments));
            Segments = Array.AsReadOnly(captured);
        }

        public string ResolvedText { get; }
        public IReadOnlyList<ExportTextSegment> Segments { get; }
    }
}
