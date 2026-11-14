// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Binding;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Localization;
using Alarmlist.Lookups;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed partial class ExpressionClassifier
    {
        private string ResourceDocument()
        {
            var saved = _request.Inputs.Context.Resources.Where(resource => resource.FilePath == Target.Text.FilePath).ToArray();
            if (saved.Length != 1) return "The resource document needs one captured consumer input.";
            if (!Target.IsDraft && saved[0].Text != Target.Text.Text) return "The resource display differs from the saved input. Request lexical draft classification.";
            var document = new Document(Target.Text, _token);
            if (document.Map.Document.Root.Name != "root") return "The document is not a RESX resource.";
            // The presentation snapshot is deliberately not substituted into the
            // saved context used for resource/dependency analysis or commands.
            var resource = new ResxTextSnapshot(Target.Text.Text, Target.Text.FilePath, Target.Text.Version, saved[0].Culture);
            var catalogue = new ResourceCatalogue(new[] { resource });
            var formatted = new HashSet<string>(StringComparer.Ordinal);
            var plain = new HashSet<string>(StringComparer.Ordinal);
            var literal = new HashSet<string>(Definition.AlarmDefinition.Fields
                .Concat(Definition.AlarmDefinition.Collections.SelectMany(collection => collection.Fields))
                .SelectMany(field => field.ValueLabelKeys), StringComparer.Ordinal);
            var binder = new Binder();
            foreach (var source in _request.Inputs.Sources)
            { _token.ThrowIfCancellationRequested(); binder.AddSourceText(source.CreateSourceText()); }
            var binding = LibraryCompilation.Include(binder.Bind(), _request.Inputs.Context);
            foreach (var value in Values(binding.SyntaxTree))
            {
                var syntax = ExpressionSyntax.Parse(value.Raw, allowItemReferences: value.Record, cancellationToken: _token);
                if (syntax.HasErrors) continue;
                foreach (var reference in syntax.Parts.OfType<ExpressionReferenceSyntax>().Where(node => node.Kind == ExpressionSyntaxKind.LocalizationReference))
                    (reference.Arguments.Count != 0 ? formatted : plain).Add(reference.Key);
            }
            _token.ThrowIfCancellationRequested();
            var lookups = new LookupCatalogue(_request.Inputs.Context.Lookups, binding.SyntaxTree.LibraryGraph);
            foreach (var path in lookups.Paths)
            {
                foreach (var label in path.Segments.Concat(path.Members.Select(member => member.Label)).Where(label => label != null)) literal.Add(label.Key);
            }
            foreach (var declared in lookups.Bindings) formatted.Add(declared.TemplateKey);
            foreach (var entry in catalogue.Entries)
            {
                _token.ThrowIfCancellationRequested();
                if (!entry.IsValid || literal.Contains(entry.Key) && !formatted.Contains(entry.Key) && !plain.Contains(entry.Key)) continue;
                var mapped = document.FindValue(entry.ValueLocation);
                if (mapped == null || mapped.Value != entry.Value) return "A resource value cannot be mapped to its displayed snapshot.";
                bool mixed = formatted.Contains(entry.Key) && plain.Contains(entry.Key);
                Emit(entry.Value, true, formatted.Contains(entry.Key) && !mixed, entry.ValueLocation, mapped, true);
                var syntax = ExpressionSyntax.Parse(entry.Value, allowItemReferences: true, cancellationToken: _token);
                foreach (var nested in syntax.Parts.OfType<ExpressionReferenceSyntax>().Where(node => node.Kind == ExpressionSyntaxKind.LocalizationReference))
                    AddIssue("Localization cannot be nested inside a translation.", nested.Start, nested.Length);
                if (mixed) AddIssue("This resource has formatted and unformatted uses; placeholder classification is unavailable for that mixed context.", 0, entry.Value.Length);

                void AddIssue(string message, int start, int length)
                {
                    var ranges = mapped.SourceRanges(start, length, _token).Select(range => new ExpressionClassificationRange(range.Start, range.Length)).ToArray();
                    int offset = ranges.Length == 0 ? 0 : ranges[0].Start;
                    _diagnostics.Add(new ExpressionClassificationIssue(message, offset, ranges.Length == 0 ? 0 : ranges[0].Length, entry.ValueLocation, ranges));
                }
            }
            return null;
        }
    }
}
