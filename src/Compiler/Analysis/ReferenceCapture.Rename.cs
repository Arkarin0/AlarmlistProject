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
    internal sealed class ReferenceRenameEdit
    {
        internal ReferenceRenameEdit(string path, int version, int start, int length, string text)
        { Path = path; Version = version; Start = start; Length = length; Text = text; }
        internal string Path { get; }
        internal int Version { get; }
        internal int Start { get; }
        internal int Length { get; }
        internal string Text { get; }
        internal string Apply(string source) => source.Remove(Start, Length).Insert(Start, Text);
    }

    internal sealed class ReferenceRenamePreview
    {
        internal ReferenceRenamePreview(IEnumerable<ReferenceRenameEdit> edits, IEnumerable<string> skipped,
            string failure, IEnumerable<AnalysisDiagnostic> diagnostics)
        {
            Edits = Array.AsReadOnly(edits.ToArray()); Skipped = Array.AsReadOnly(skipped.Distinct(StringComparer.Ordinal).ToArray());
            Failure = failure; Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        }
        internal IReadOnlyList<ReferenceRenameEdit> Edits { get; }
        internal IReadOnlyList<string> Skipped { get; }
        internal string Failure { get; }
        internal bool IsApplicable => Failure == null && Edits.Count != 0;
        internal bool RequiresAcknowledgement => Skipped.Count != 0;
        internal IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
    }

    internal sealed partial class ReferenceCapture
    {
        internal static ReferenceRenamePreview Preview(AlmxTextSnapshot[] inputs, CompilationContext context,
            string symbolId, string newName, CancellationToken token = default)
        {
            ReferenceRenamePreview Failed(string message) => new ReferenceRenamePreview(Array.Empty<ReferenceRenameEdit>(),
                Array.Empty<string>(), message, Array.Empty<AnalysisDiagnostic>());
            token.ThrowIfCancellationRequested();
            var data = Capture(inputs, context, token, newName);
            var target = data.Symbols.SingleOrDefault(symbol => symbol.Id == symbolId);
            if (target == null) return Failed("The symbol or captured inputs changed. Find references again.");
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
            var comparer = target.Kind == "Parameter" || target.Kind == "Group" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var consumers = new HashSet<string>(data.Occurrences.Where(use => use.Family == target.Family).Select(use => use.Consumer), StringComparer.Ordinal);
            var kinds = new HashSet<string>(data.Symbols.Where(symbol => symbol.Family == target.Family).Select(symbol => symbol.Kind), StringComparer.Ordinal);
            if (kinds.Contains("Resource") || kinds.Contains("Template")) { kinds.Add("Resource"); kinds.Add("Template"); }
            var scopes = new HashSet<string>(data.Symbols.Where(symbol => symbol.Family == target.Family).Select(symbol => symbol.Scope), StringComparer.Ordinal);
            foreach (var other in data.Symbols.Where(symbol => kinds.Contains(symbol.Kind) && symbol.Family != target.Family && comparer.Equals(symbol.Name, newName)))
            {
                bool scoped = target.Kind == "LookupMember" || target.Kind == "LookupMetadata" || target.Kind == "LookupEntry" || target.Kind == "Argument";
                if (scoped ? scopes.Contains(other.Scope) : target.Kind == "Alarm" || target.Kind == "Resource" || target.Kind == "Template"
                    || target.Kind == "LookupPath" || target.Kind == "LookupList" || data.Occurrences.Any(use => use.Family == other.Family && consumers.Contains(use.Consumer)))
                    return Failed("The new name collides with another declaration in a consuming context.");
            }
            var uses = data.Occurrences.Where(use => use.Family == target.Family && use.Role != "Binding").ToArray();
            var skipped = new List<string>(data.Coverage);
            skipped.AddRange(uses.Where(use => use.Unavailable != null).Select(use => use.Unavailable + " " + use.Location?.FilePath));
            var edits = new List<ReferenceRenameEdit>();
            var physicalFamilies = data.Occurrences.Where(use => use.Start != null)
                .GroupBy(use => (use.Location.FilePath, use.Start, use.Length))
                .ToDictionary(group => group.Key, group => group.Select(use => use.Family).Distinct(StringComparer.Ordinal).Count());
            foreach (var group in uses.Where(use => use.Start != null && use.Unavailable == null).GroupBy(use => new { use.Location.FilePath, use.Location.SourceVersion, use.Start, use.Length }))
            {
                token.ThrowIfCancellationRequested();
                if (physicalFamilies[(group.Key.FilePath, group.Key.Start, group.Key.Length)] > 1)
                {
                    skipped.Add("The same physical token binds to different families between consumers: " + group.Key.FilePath);
                    continue;
                }
                edits.Add(new ReferenceRenameEdit(group.Key.FilePath, group.Key.SourceVersion.Value,
                    group.Key.Start.Value, group.Key.Length.Value, group.First().Replacement));
            }
            foreach (var file in edits.GroupBy(edit => edit.Path, StringComparer.OrdinalIgnoreCase))
            {
                int end = -1;
                foreach (var edit in file.OrderBy(edit => edit.Start))
                {
                    if (edit.Start < end) return Failed("Proposed replacements overlap.");
                    end = edit.Start + edit.Length;
                }
            }
            var overlay = inputs.Select(input =>
            {
                string text = input.Text;
                foreach (var edit in edits.Where(edit => string.Equals(edit.Path, input.FilePath, StringComparison.OrdinalIgnoreCase)).OrderByDescending(edit => edit.Start))
                    text = edit.Apply(text);
                return new AlmxTextSnapshot(text, input.FilePath, input.Version);
            }).ToArray();
            string OverlayText(string original, string path)
            {
                foreach (var edit in edits.Where(edit => string.Equals(edit.Path, path, StringComparison.OrdinalIgnoreCase)).OrderByDescending(edit => edit.Start)) original = edit.Apply(original);
                return original;
            }
            var overlayContext = new CompilationContext(
                context.Resources.Select(resource => new ResxTextSnapshot(OverlayText(resource.Text, resource.FilePath), resource.FilePath, resource.Version, resource.Culture)),
                context.Lookups.Select(lookup => new LookupTextSnapshot(OverlayText(lookup.Text, lookup.FilePath), lookup.FilePath, lookup.Version)),
                context.Culture, context.ProjectPath, context.Configuration, context.Platform, context.Properties,
                context.DiagnosticOptions, context.Analyzers).WithLibraries(context.Libraries);
            var compiler = new AlarmCompiler();
            var after = compiler.AnalyzeSnapshots(overlay, overlayContext, false, token);
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
            return new ReferenceRenamePreview(edits, skipped, null, after.Diagnostics);
        }
    }
}
