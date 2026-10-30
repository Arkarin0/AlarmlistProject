// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using Alarmlist.Compiler;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    public sealed class ReferenceRenameEdit
    {
        internal ReferenceRenameEdit(string path, int version, int start, int length, string text)
        { Path = path; Version = version; Start = start; Length = length; Text = text; }
        public string Path { get; }
        public int Version { get; }
        public int Start { get; }
        public int Length { get; }
        public string Text { get; }
        internal string Apply(string source) => source.Remove(Start, Length).Insert(Start, Text);
    }

    public sealed class ReferenceRenamePreview
    {
        public int ContractVersion => AnalysisResult.CurrentContractVersion;
        public string ToolingFingerprint => Definition.AlarmDefinition.ToolingFingerprint;
        internal ReferenceRenamePreview(IEnumerable<ReferenceRenameEdit> edits, IEnumerable<string> skipped,
            string failure, IEnumerable<AnalysisDiagnostic> diagnostics, bool requiresProjectEvaluation = false,
            IEnumerable<ReferenceRenameGroup> groups = null, ReferenceSearchResult references = null, string newName = null,
            IEnumerable<ReferenceSearchResult> referenceSearches = null, IEnumerable<string> nonApplicableFamilies = null)
        {
            Edits = Array.AsReadOnly(edits.ToArray()); Skipped = Array.AsReadOnly(skipped.Distinct(StringComparer.Ordinal).ToArray());
            Failure = failure; Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            RequiresProjectEvaluation = requiresProjectEvaluation;
            Groups = Array.AsReadOnly((groups ?? Array.Empty<ReferenceRenameGroup>()).ToArray());
            References = references; NewName = newName;
            ReferenceSearches = Array.AsReadOnly((referenceSearches ?? (references == null ? Array.Empty<ReferenceSearchResult>() : new[] { references })).ToArray());
            NonApplicableFamilies = Array.AsReadOnly((nonApplicableFamilies ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray());
        }
        public ReferenceSearchResult References { get; }
        public string NewName { get; }
        public IReadOnlyList<ReferenceSearchResult> ReferenceSearches { get; }
        public IReadOnlyList<string> NonApplicableFamilies { get; }
        internal ReferenceRenamePreview WithReferences(ReferenceSearchResult references, string newName)
            => WithReferences(new[] { references }, newName);
        internal ReferenceRenamePreview WithReferences(IReadOnlyList<ReferenceSearchResult> references, string newName)
            => new ReferenceRenamePreview(Edits, Skipped, Failure, Diagnostics, RequiresProjectEvaluation, Groups, references.FirstOrDefault(), newName, references,
                Failure == null ? NonApplicableFamilies : NonApplicableFamilies.Concat(references.Where(result => result.IsAvailable).Select(result => result.Symbol.Family)));
        public IReadOnlyList<ReferenceRenameEdit> Edits { get; }
        public IReadOnlyList<string> Skipped { get; }
        public string Failure { get; }
        public bool IsApplicable => Failure == null && Edits.Count != 0 && !RequiresProjectEvaluation;
        public bool RequiresProjectEvaluation { get; }
        public IReadOnlyList<ReferenceRenameGroup> Groups { get; }
        public bool RequiresAcknowledgement => Skipped.Count != 0;
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
    }

    internal sealed partial class ReferenceCapture
    {
        internal static ReferenceRenamePreview Preview(AlmxTextSnapshot[] inputs, CompilationContext context,
            string symbolId, string newName, CancellationToken token = default, IReadOnlyList<ReferenceTextSnapshot> projectDocuments = null,
            ReferenceLibraryScope declaringLibrary = null, IReadOnlyCollection<string> includedGroups = null,
            ReferenceCompilationSnapshot validationInputs = null, IReadOnlyCollection<string> coordinatedSymbols = null,
            IReadOnlyList<string> readOnlyPaths = null)
        {
            ReferenceRenamePreview Failed(string message) => new ReferenceRenamePreview(Array.Empty<ReferenceRenameEdit>(),
                Array.Empty<string>(), message, Array.Empty<AnalysisDiagnostic>());
            token.ThrowIfCancellationRequested();
            var data = Capture(inputs, context, token, newName, projectDocuments, declaringLibrary, readOnlyPaths);
            var target = data.Symbols.SingleOrDefault(symbol => symbol.Id == symbolId);
            if (target == null) return Failed("The symbol or captured inputs changed. Find references again.");
            var selectedIds = new HashSet<string>(coordinatedSymbols ?? new[] { symbolId }, StringComparer.Ordinal) { symbolId };
            var selected = data.Symbols.Where(symbol => selectedIds.Contains(symbol.Id)).ToArray();
            if (selected.Length != selectedIds.Count) return Failed("A coordinated symbol or its captured inputs changed. Find references again.");
            var comparer = target.Kind == "Parameter" || target.Kind == "Group" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            bool ResourceKind(string kind) => kind == "Resource" || kind == "Template";
            if (selected.Any(symbol => (symbol.Kind != target.Kind && !(ResourceKind(symbol.Kind) && ResourceKind(target.Kind))) || !comparer.Equals(symbol.Name, target.Name)))
                return Failed("A coordinated rename requires compatible declarations of the same named symbol.");
            var selectedFamilies = new HashSet<string>(selected.Select(symbol => symbol.Family), StringComparer.Ordinal);
            var nonApplicable = new HashSet<string>(StringComparer.Ordinal);
            var unavailableFindings = new List<string>();
            foreach (string family in selectedFamilies.ToArray())
                if (data.Symbols.Any(symbol => symbol.Family == family && symbol.IsAmbiguous))
                { selectedFamilies.Remove(family); nonApplicable.Add(family); unavailableFindings.Add("The selected family has ambiguous or invalid bindings; no edit was guessed."); }
            if (target.Kind == "DefinitionField" || target.Kind == "WellKnown") return Failed("Built-in and generated definition names are immutable.");
            if (string.IsNullOrWhiteSpace(newName)) return Failed("A nonempty name is required.");
            if (target.Kind == "Parameter" && (!Evaluation.PropertyExpression.IsValidName(newName)
                || newName.StartsWith("Alarmlist", StringComparison.OrdinalIgnoreCase))) return Failed("This parameter name is invalid or reserved.");
            if ((target.Kind == "Resource" || target.Kind == "Template") && !Localization.ResourceCatalogue.IsValidKey(newName)) return Failed("This localization/template key is invalid.");
            if (target.Kind == "Group" && !Syntax.ProcedureGroupSyntax.IsValidName(newName)) return Failed("This procedure group name is invalid.");
            if (target.Kind.StartsWith("Lookup", StringComparison.Ordinal) && !Lookups.LookupCatalogue.IsValidKey(newName)) return Failed("This lookup identity is invalid.");
            if (target.Kind == "Argument" && !Evaluation.PropertyExpression.IsValidName(newName)) return Failed("This argument name is invalid.");
            try { XmlConvert.VerifyXmlChars(newName); }
            catch (XmlException) { return Failed("The name contains invalid XML characters."); }
            var collisionFindings = new List<string>();
            foreach (string family in selectedFamilies.ToArray())
            {
                // A case-only change in an insensitive namespace introduces no
                // new collision with an existing independent/shadowing family.
                if (comparer.Equals(target.Name, newName)) continue;
                var consumers = new HashSet<string>(data.Occurrences.Where(use => use.Family == family).Select(use => use.Consumer), StringComparer.Ordinal);
                var members = data.Symbols.Where(symbol => symbol.Family == family).ToArray();
                var kinds = new HashSet<string>(members.Select(symbol => symbol.Kind), StringComparer.Ordinal);
                if (kinds.Contains("Resource") || kinds.Contains("Template")) { kinds.Add("Resource"); kinds.Add("Template"); }
                var scopes = new HashSet<string>(members.Select(symbol => symbol.Scope), StringComparer.Ordinal);
                foreach (var other in data.Symbols.Where(symbol => kinds.Contains(symbol.Kind) && symbol.Family != family && comparer.Equals(symbol.Name, newName)))
                {
                    bool scoped = target.Kind == "LookupMember" || target.Kind == "LookupMetadata" || target.Kind == "LookupEntry" || target.Kind == "Argument";
                    if (!(scoped ? scopes.Contains(other.Scope) : target.Kind == "Alarm" || target.Kind == "Resource" || target.Kind == "Template"
                        || target.Kind == "LookupPath" || target.Kind == "LookupList" || data.Occurrences.Any(use => use.Family == other.Family && consumers.Contains(use.Consumer)))) continue;
                    if (selected.Length == 1) return Failed("The new name collides with another declaration in a consuming context.");
                    selectedFamilies.Remove(family);
                    nonApplicable.Add(family);
                    collisionFindings.Add("Skipped colliding family at " + members[0].Location?.FilePath + ": " + target.Name + " remains unchanged.");
                    break;
                }
            }
            if (selectedFamilies.Count == 0) return new ReferenceRenamePreview(Array.Empty<ReferenceRenameEdit>(),
                data.Coverage.Concat(unavailableFindings).Concat(collisionFindings), "No selected family can be renamed reliably.", data.Diagnostics, nonApplicableFamilies: nonApplicable);
            var uses = data.Occurrences.Where(use => selectedFamilies.Contains(use.Family) && use.Role != "Binding").ToArray();
            var skipped = new List<string>(data.Coverage);
            skipped.AddRange(collisionFindings);
            skipped.AddRange(unavailableFindings);
            skipped.AddRange(uses.Where(use => use.Unavailable != null).Select(use => use.Unavailable + " " + use.Location?.FilePath));
            var edits = new List<ReferenceRenameEdit>();
            var physicalFamilies = data.Occurrences.Where(use => use.Start != null)
                .GroupBy(use => (use.Location.FilePath, use.Start, use.Length))
                .ToDictionary(group => group.Key, group => group.Select(use => use.Family).Distinct(StringComparer.Ordinal).ToArray());
            foreach (var group in uses.Where(use => use.Start != null && use.Unavailable == null && use.Spelling != newName).GroupBy(use => new { use.Location.FilePath, use.Location.SourceVersion, use.Start, use.Length }))
            {
                token.ThrowIfCancellationRequested();
                if (physicalFamilies[(group.Key.FilePath, group.Key.Start, group.Key.Length)].Any(family => !selectedFamilies.Contains(family)))
                {
                    skipped.Add("The same physical token binds to different families between consumers: " + group.Key.FilePath);
                    continue;
                }
                if (group.Select(use => use.Replacement).Distinct(StringComparer.Ordinal).Count() != 1) return Failed("Proposed replacements disagree for one physical token.");
                edits.Add(new ReferenceRenameEdit(group.Key.FilePath, group.Key.SourceVersion.Value,
                    group.Key.Start.Value, group.Key.Length.Value, group.First().Replacement));
            }
            foreach (var file in edits.GroupBy(edit => edit.Path, StringComparer.Ordinal))
            {
                int end = -1;
                foreach (var edit in file.OrderBy(edit => edit.Start))
                {
                    if (edit.Start < end) return Failed("Proposed replacements overlap.");
                    end = edit.Start + edit.Length;
                }
            }
            string selectionIdentity = string.Join("\n", selected.Select(symbol => symbol.Id).OrderBy(id => id, StringComparer.Ordinal));
            // One proven physical replacement per compiler group lets a solution
            // coordinator exclude an incompatible shared token without dropping
            // other proven edits in that same file. Views can aggregate by Path.
            var groups = edits.Select(edit => new ReferenceRenameGroup(selectionIdentity, newName, edit.Path, new[] { edit }, includedGroups)).ToArray();
            if (includedGroups != null)
            {
                if (includedGroups.Distinct(StringComparer.Ordinal).Count() != includedGroups.Count
                    || includedGroups.Any(id => !groups.Any(group => group.Id == id)))
                    return Failed("The selected change groups are stale or unknown. Preview the rename again.");
                foreach (var file in groups.Where(group => !group.IsIncluded).GroupBy(group => group.Path, StringComparer.Ordinal))
                    skipped.Add("Excluded change groups: " + file.Key + " (" + file.Sum(group => group.Edits.Count) + " changes remain)");
                edits = groups.Where(group => group.IsIncluded).SelectMany(group => group.Edits).ToList();
            }
            var overlay = inputs.Select(input =>
            {
                string text = input.Text;
                foreach (var edit in edits.Where(edit => string.Equals(edit.Path, input.FilePath, StringComparison.Ordinal)).OrderByDescending(edit => edit.Start))
                    text = edit.Apply(text);
                return new AlmxTextSnapshot(text, input.FilePath, input.Version);
            }).ToArray();
            string OverlayText(string original, string path)
            {
                foreach (var edit in edits.Where(edit => string.Equals(edit.Path, path, StringComparison.Ordinal)).OrderByDescending(edit => edit.Start)) original = edit.Apply(original);
                return original;
            }
            var overlayContext = new CompilationContext(
                context.Resources.Select(resource => new ResxTextSnapshot(OverlayText(resource.Text, resource.FilePath), resource.FilePath, resource.Version, resource.Culture)),
                context.Lookups.Select(lookup => new LookupTextSnapshot(OverlayText(lookup.Text, lookup.FilePath), lookup.FilePath, lookup.Version)),
                context.Culture, context.ProjectPath, context.Configuration, context.Platform, context.Properties,
                context.DiagnosticOptions, context.Analyzers).WithLibraries(context.Libraries);
            var compiler = new AlarmCompiler();
            if (validationInputs != null)
            {
                var before = new ReferenceCompilationSnapshot(inputs, context, projectDocuments, declaringLibrary, token, readOnlyPaths ?? Array.Empty<string>());
                string invalid = ValidateProjectedInputs(before, validationInputs, edits, token);
                if (invalid != null) return Failed(invalid);
                overlay = validationInputs.Sources.ToArray(); overlayContext = validationInputs.Context;
            }
            bool library = (validationInputs?.DeclaringLibrary ?? declaringLibrary) != null;
            var after = library ? compiler.AnalyzeLibrarySnapshots(overlay, overlayContext, token)
                : compiler.AnalyzeSnapshots(overlay, overlayContext, false, token);
            // New diagnostics are allowed by the approved partial-rename contract;
            // the final UI must show and explicitly acknowledge their differences.
            var remainingDiagnostics = data.Diagnostics.GroupBy(diagnostic => (diagnostic.Id, diagnostic.Message))
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (var diagnostic in after.Diagnostics)
            {
                var key = (diagnostic.Id, diagnostic.Message);
                if (remainingDiagnostics.TryGetValue(key, out int count) && count != 0) remainingDiagnostics[key] = count - 1;
                else
                    skipped.Add("Expected diagnostic after rename: " + diagnostic.Id + " " + diagnostic.Message);
            }
            token.ThrowIfCancellationRequested();
            bool projectEdits = projectDocuments?.Any(document => edits.Any(edit => edit.Path == document.FilePath)) == true;
            return new ReferenceRenamePreview(edits, skipped, null, after.Diagnostics, projectEdits && validationInputs == null, groups, nonApplicableFamilies: nonApplicable);
        }
    }
}
