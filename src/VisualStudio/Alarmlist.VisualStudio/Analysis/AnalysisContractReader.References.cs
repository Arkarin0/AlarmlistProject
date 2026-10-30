// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed partial class AnalysisContractReader
    {
        private static void ValidateReferences(object result)
        {
            if (result == null || Value<int>(result, "ContractVersion") != 9
                || Value<string>(result, "ToolingFingerprint") != Definition.AlarmDefinition.ToolingFingerprint)
                throw new NotSupportedException("The SDK returned incompatible reference data. Rebuild matching tools.");
        }

        internal IReadOnlyList<AnalysisReferenceSearch> ReadReferenceBatch(IEnumerable<object> results, CancellationToken token)
        {
            var families = new Dictionary<(string Snapshot, string Family), AnalysisReferenceSearch>();
            var copied = new List<AnalysisReferenceSearch>();
            foreach (var result in results)
            { token.ThrowIfCancellationRequested(); copied.Add(ReadReferences(result, families)); }
            return copied.AsReadOnly();
        }

        internal AnalysisReferenceSearch ReadReferences(object result) => ReadReferences(result, null);

        private AnalysisReferenceSearch ReadReferences(object result, Dictionary<(string Snapshot, string Family), AnalysisReferenceSearch> families)
        {
            ValidateReferences(result);
            var target = Value<object>(result, "Target");
            var symbol = ReferenceSymbol(Value<object>(result, "Symbol"));
            string snapshot = Value<string>(result, "SnapshotId");
            AnalysisReferenceSearch shared = null;
            if (symbol != null) families?.TryGetValue((snapshot, symbol.Family), out shared);
            var copied = new AnalysisReferenceSearch(9, Value<string>(result, "ToolingFingerprint"), snapshot,
                new AnalysisReferenceTarget(Value<string>(target, "Kind"), Location(Value<object>(target, "Location")),
                    Value<string>(target, "Name"), Value<int?>(target, "Offset"), Value<string>(target, "DeclarationId"), Value<string>(target, "SnapshotId")),
                symbol, shared?.Symbols ?? List(result, "Symbols", ReferenceSymbol), shared?.Occurrences ?? List(result, "Occurrences", use =>
                    new AnalysisReferenceOccurrence(Value<string>(use, "Family"), Value<string>(use, "Role"), Location(Value<object>(use, "Location")),
                        Value<string>(use, "Consumer"), Value<int?>(use, "Start"), Value<int?>(use, "Length"), Value<string>(use, "Replacement"), Value<string>(use, "Unavailable"), Value<string>(use, "Spelling"),
                        Value<string>(use, "DeclarationId"), Value<string>(use, "SourceTokenId"))),
                shared?.Coverage ?? List(result, "Coverage", value => (string)value), shared?.Diagnostics ?? List(result, "Diagnostics", ReferenceDiagnostic),
                Value<string>(result, "ProjectPath"), Value<string>(result, "Configuration"), Value<string>(result, "Platform"), Value<string>(result, "Culture"),
                Value<string>(result, "Message"), Value<bool>(result, "IsAvailable"), Value<bool>(result, "TargetAbsent"), shared, Value<bool>(result, "IsAmbiguous"));
            if (symbol != null && families != null) families[(snapshot, symbol.Family)] = copied;
            return copied;
        }

        internal AnalysisRenamePreview ReadRename(object result)
        {
            ValidateReferences(result);
            return new AnalysisRenamePreview(9, Value<string>(result, "ToolingFingerprint"), List(result, "Edits", RenameEdit),
                List(result, "Skipped", value => (string)value), Value<string>(result, "Failure"), List(result, "Diagnostics", ReferenceDiagnostic),
                Value<bool>(result, "RequiresProjectEvaluation"), List(result, "Groups", group => new AnalysisRenameGroup(Value<string>(group, "Id"),
                    Value<string>(group, "Path"), List(group, "Edits", RenameEdit), Value<bool>(group, "IsIncluded"))), Value<bool>(result, "IsApplicable"),
                Value<bool>(result, "RequiresAcknowledgement"), ReadReferences(Value<object>(result, "References")), Value<string>(result, "NewName"),
                ReadReferenceBatch(((System.Collections.IEnumerable)Value<object>(result, "ReferenceSearches")).Cast<object>(), CancellationToken.None),
                List(result, "NonApplicableFamilies", value => (string)value));
        }

        private static AnalysisReferenceSymbol ReferenceSymbol(object symbol) => symbol == null ? null : new AnalysisReferenceSymbol(
            Value<string>(symbol, "Id"), Value<string>(symbol, "Family"), Value<string>(symbol, "Kind"), Value<string>(symbol, "Name"),
            Location(Value<object>(symbol, "Location")), Value<bool>(symbol, "IsReadOnly"), Value<string>(symbol, "Scope"),
            Value<string>(symbol, "DeclarationId"), Location(Value<object>(symbol, "TargetLocation")), Value<bool>(symbol, "IsAmbiguous"));

        private static AnalysisRenameEdit RenameEdit(object edit) => new AnalysisRenameEdit(Value<string>(edit, "Path"), Value<int>(edit, "Version"),
            Value<int>(edit, "Start"), Value<int>(edit, "Length"), Value<string>(edit, "Text"));

        private static AnalysisDiagnostic ReferenceDiagnostic(object diagnostic) => new AnalysisDiagnostic(Value<string>(diagnostic, "Id"),
            EnumValue<AnalysisSeverity>(diagnostic, "Severity"), Value<string>(diagnostic, "Message"), Location(Value<object>(diagnostic, "Location")),
            List(diagnostic, "RelatedLocations", Location));
    }
}
