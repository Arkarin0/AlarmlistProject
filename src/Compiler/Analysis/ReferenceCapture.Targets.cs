// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    public sealed class ReferenceSearchResult
    {
        public int ContractVersion => AnalysisResult.CurrentContractVersion;
        public string ToolingFingerprint => Definition.AlarmDefinition.ToolingFingerprint;
        internal ReferenceSearchResult(ReferenceCompilationSnapshot inputs, ReferenceSymbolTarget target, ReferenceData data,
            ReferenceSymbol symbol, string message, bool targetAbsent = false, ReferenceFamilySlice family = null, bool isAmbiguous = false,
            IEnumerable<ReferenceSymbol> candidates = null, bool isUnsupportedTarget = false)
        {
            SnapshotId = inputs.SnapshotId; Target = target; Symbol = symbol; Message = message; TargetAbsent = targetAbsent; IsAmbiguous = isAmbiguous;
            IsUnsupportedTarget = isUnsupportedTarget;
            ProjectPath = inputs.Context.ProjectPath; Configuration = inputs.Context.Configuration;
            Platform = inputs.Context.Platform; Culture = inputs.Context.Culture;
            Symbols = family?.Symbols ?? Array.AsReadOnly((symbol == null ? Array.Empty<ReferenceSymbol>() : data.Symbols.Where(item => item.Family == symbol.Family)).ToArray());
            Occurrences = family?.Occurrences ?? Array.AsReadOnly((symbol == null ? Array.Empty<ReferenceOccurrence>() : data.Occurrences.Where(item => item.Family == symbol.Family)).ToArray());
            Coverage = data?.Coverage ?? Array.Empty<string>(); Diagnostics = data?.Diagnostics ?? Array.Empty<AnalysisDiagnostic>();
            Candidates = Array.AsReadOnly((candidates ?? Array.Empty<ReferenceSymbol>()).ToArray());
        }
        public string SnapshotId { get; }
        public ReferenceSymbolTarget Target { get; }
        public ReferenceSymbol Symbol { get; }
        public IReadOnlyList<ReferenceSymbol> Symbols { get; }
        public IReadOnlyList<ReferenceOccurrence> Occurrences { get; }
        public IReadOnlyList<string> Coverage { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string Culture { get; }
        public string Message { get; }
        public bool IsAvailable => Symbol != null;
        public bool TargetAbsent { get; }
        public bool IsAmbiguous { get; }
        public bool IsUnsupportedTarget { get; }
        public IReadOnlyList<ReferenceSymbol> Candidates { get; }
    }

    internal sealed partial class ReferenceCapture
    {
        internal static IReadOnlyList<ReferenceSearchResult> FindMany(ReferenceCompilationSnapshot inputs, IEnumerable<ReferenceSymbolTarget> targets, CancellationToken token)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            token.ThrowIfCancellationRequested();
            var requested = targets.ToArray();
            if (requested.Any(target => target == null)) throw new ArgumentException("A reference target cannot be null.", nameof(targets));
            if (requested.Length == 0) return Array.Empty<ReferenceSearchResult>();
            ReferenceData data;
            try { data = Capture(inputs.Sources.ToArray(), inputs.Context, token, projectDocuments: inputs.ProjectDocuments, declaringLibrary: inputs.DeclaringLibrary, readOnlyPaths: inputs.ReadOnlyPaths); }
            catch (XmlException error) { data = new ReferenceData(Array.Empty<ReferenceSymbol>(), Array.Empty<ReferenceOccurrence>(), new[] { "References unavailable: " + error.Message }, Array.Empty<AnalysisDiagnostic>()); }
            var results = new List<ReferenceSearchResult>();
            var index = new ReferenceTargetIndex(data, token);
            foreach (var target in requested)
            {
                token.ThrowIfCancellationRequested();
                results.Add(target.SnapshotId == inputs.SnapshotId ? ResolveTarget(inputs, target, data, token, index)
                    : new ReferenceSearchResult(inputs, target, data, null, "Captured inputs changed. Select the symbol again."));
            }
            return Array.AsReadOnly(results.ToArray());
        }

        internal static ReferenceSearchResult Find(ReferenceCompilationSnapshot inputs, ReferenceSymbolTarget target, CancellationToken token = default)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (target == null) throw new ArgumentNullException(nameof(target));
            token.ThrowIfCancellationRequested();
            ReferenceSearchResult Unavailable(string message, ReferenceData captured = null, bool absent = false)
                => new ReferenceSearchResult(inputs, target, captured, null, message, absent);
            if (target.SnapshotId != inputs.SnapshotId) return Unavailable("Captured inputs changed. Select the symbol again.");
            ReferenceData data;
            try { data = Capture(inputs.Sources.ToArray(), inputs.Context, token, projectDocuments: inputs.ProjectDocuments, declaringLibrary: inputs.DeclaringLibrary, readOnlyPaths: inputs.ReadOnlyPaths); }
            catch (XmlException exception) { return Unavailable("References unavailable: " + exception.Message); }
            return ResolveTarget(inputs, target, data, token);
        }

        private static ReferenceSearchResult ResolveTarget(ReferenceCompilationSnapshot inputs, ReferenceSymbolTarget target,
            ReferenceData data, CancellationToken token, ReferenceTargetIndex index = null)
        {
            ReferenceSearchResult Unavailable(string message, bool absent = false)
                => new ReferenceSearchResult(inputs, target, data, null, message, absent);
            if (data.Coverage.Any(message => message.StartsWith("References unavailable:", StringComparison.Ordinal)))
                return Unavailable("Parsing prevents a reliable reference search.");
            IEnumerable<ReferenceSymbol> matches;
            if (index != null) matches = index.Candidates(target);
            else if (target.Kind == "Identity") matches = data.Symbols.Where(symbol => symbol.DeclarationId == target.DeclarationId);
            else if (target.Kind == "BuiltInName")
            {
                var builtIn = BuiltInTarget(target.Name);
                matches = data.Symbols.Where(symbol => symbol.Kind == builtIn.Kind && string.Equals(symbol.Name, builtIn.Name, StringComparison.OrdinalIgnoreCase));
            }
            else if (target.Kind == "Token")
            {
                var probes = data.TokenProbes.Concat(data.Occurrences.Where(use => use.Start.HasValue && use.Length.HasValue)
                    .Select(use => new ReferenceTokenProbe(use.Family, use.Location, use.Start.Value, use.Length.Value)));
                var uses = probes.Where(use => use.Location?.FilePath == target.Location.FilePath
                    && use.Location.SourceVersion == target.Location.SourceVersion && use.Start <= target.Offset).ToArray();
                var containing = uses.Where(use => target.Offset < use.Start + use.Length).ToArray();
                var families = new HashSet<string>(containing.Where(use => use.Family != null).Select(use => use.Family), StringComparer.Ordinal);
                // Editor carets at the end of a name still touch that compiler
                // token. Prefer a containing token before considering left ends.
                if (containing.Length == 0) families.UnionWith(uses.Where(use => use.Family != null && use.Start < target.Offset && target.Offset == use.Start + use.Length).Select(use => use.Family));
                matches = data.Symbols.Where(symbol => families.Contains(symbol.Family));
            }
            else if (target.Kind == "Value")
            {
                var families = new HashSet<string>(data.Occurrences.Where(use => IsValueReference(use) && SameTargetLocation(use.Location, target.Location)
                    && (target.Name == null || use.Consumer == target.Name)).Select(use => use.Family), StringComparer.Ordinal);
                matches = data.Symbols.Where(symbol => families.Contains(symbol.Family));
            }
            else if (target.Kind == "AlarmParameter" || target.Kind == "SourceParameter")
            {
                var families = new HashSet<string>(data.ContextBindings.Where(binding => binding.Kind == target.Kind
                    && SameTargetLocation(binding.Location, target.Location) && string.Equals(binding.Name, target.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(binding => binding.Family), StringComparer.Ordinal);
                matches = data.Symbols.Where(symbol => families.Contains(symbol.Family));
            }
            else matches = data.Symbols.Where(symbol => symbol.Kind == target.Kind && SameTargetLocation(symbol.TargetLocation, target.Location)
                && (target.Name == null || string.Equals(symbol.Name, target.Name,
                    target.Kind == "Parameter" || target.Kind == "Group" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)));
            var selected = matches.ToArray();
            if (selected.Select(symbol => symbol.Family).Distinct(StringComparer.Ordinal).Count() > 1)
                return new ReferenceSearchResult(inputs, target, data, null, "Choose a symbol bound at this value or token.", isAmbiguous: true,
                    candidates: selected.GroupBy(symbol => symbol.Family, StringComparer.Ordinal).Select(group => group.First()));
            if (selected.Length == 0)
            {
                bool supportedToken = target.Kind == "Token" && (data.UnavailableSources.Contains(target.Location.FilePath, StringComparer.Ordinal)
                    || (index != null ? index.SupportsToken(target) : data.TokenProbes.Any(probe =>
                    probe.Location?.FilePath == target.Location.FilePath && probe.Location.SourceVersion == target.Location.SourceVersion
                    && probe.Start <= target.Offset && target.Offset <= probe.Start + probe.Length)));
                return new ReferenceSearchResult(inputs, target, data, null,
                    supportedToken ? "The compiler could not bind this reference. Review the diagnostics and coverage." : "No supported symbol is bound at this target.",
                    targetAbsent: target.Kind == "Identity", isUnsupportedTarget: target.Kind == "Token" && !supportedToken);
            }
            token.ThrowIfCancellationRequested();
            return new ReferenceSearchResult(inputs, target, data, selected[0], null, family: index?.Family(selected[0].Family));
        }

        internal static ReferenceRenamePreview Preview(ReferenceCompilationSnapshot inputs, ReferenceSymbolTarget target, string newName, CancellationToken token = default,
            IReadOnlyCollection<string> includedGroups = null, ReferenceCompilationSnapshot validationInputs = null)
        {
            var found = Find(inputs, target, token);
            if (!found.IsAvailable) return new ReferenceRenamePreview(Array.Empty<ReferenceRenameEdit>(), found.Coverage, found.Message, found.Diagnostics, references: found, newName: newName);
            return Preview(inputs.Sources.ToArray(), inputs.Context, found.Symbol.Id, newName, token, inputs.ProjectDocuments, inputs.DeclaringLibrary, includedGroups, validationInputs,
                readOnlyPaths: inputs.ReadOnlyPaths)
                .WithReferences(found, newName);
        }

        internal static ReferenceRenamePreview PreviewMany(ReferenceCompilationSnapshot inputs, IEnumerable<ReferenceSymbolTarget> targets,
            string newName, CancellationToken token, IReadOnlyCollection<string> includedGroups, ReferenceCompilationSnapshot validationInputs)
        {
            var found = FindMany(inputs, targets, token);
            if (found.Count == 0) throw new ArgumentException("At least one symbol target is required.", nameof(targets));
            var unavailable = found.FirstOrDefault(result => !result.IsAvailable);
            if (unavailable != null) return new ReferenceRenamePreview(Array.Empty<ReferenceRenameEdit>(), found.SelectMany(result => result.Coverage),
                unavailable.Message, found.SelectMany(result => result.Diagnostics)).WithReferences(found, newName);
            var selected = found.GroupBy(result => result.Symbol.Family, StringComparer.Ordinal).Select(group => group.First().Symbol.Id).ToArray();
            return Preview(inputs.Sources.ToArray(), inputs.Context, selected[0], newName, token, inputs.ProjectDocuments,
                inputs.DeclaringLibrary, includedGroups, validationInputs, selected, inputs.ReadOnlyPaths).WithReferences(found, newName);
        }

        private static bool SameTargetLocation(SourceLocation left, SourceLocation right)
            => left != null && right != null && left.FilePath == right.FilePath && left.SourceVersion == right.SourceVersion
                && left.LineNumber == right.LineNumber && left.ColumnNumber == right.ColumnNumber;
        internal static bool IsValueReference(ReferenceOccurrence use) => use.Role == "Expression" || use.Role == "Localization" || use.Role == "Lookup"
            || use.Role == "BuiltIn" || use.Role == "Argument" || use.Role == "Placeholder" || use.Role == "LookupLabel" || use.Role == "ListBinding" || use.Role == "Inheritance";
        internal static (string Kind, string Name) BuiltInTarget(string name)
        {
            string field = Definition.AlarmDefinition.Fields.FirstOrDefault(candidate => string.Equals("AlarmlistAlarm" + candidate.Name, name, StringComparison.OrdinalIgnoreCase))?.Name;
            return (field == null ? "WellKnown" : "DefinitionField", field ?? name);
        }
    }
}
