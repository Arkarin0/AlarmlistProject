// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Alarmlist.Compiler;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Localization;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed partial class ReferenceCapture
    {
        private ResourceCatalogue _resources;
        private readonly Dictionary<ResourceEntry, Symbol> _resourceSymbols = new Dictionary<ResourceEntry, Symbol>();
        private readonly Dictionary<(LibraryIdentity Owner, string Key), Symbol> _providerKeys = new Dictionary<(LibraryIdentity, string), Symbol>();

        private void AddAuxiliarySources(CompilationContext context)
        {
            foreach (var resource in context.Resources) Add(resource.Text, resource.FilePath, resource.Version);
            foreach (var lookup in context.Lookups) Add(lookup.Text, lookup.FilePath, lookup.Version);
            void Add(string text, string path, int version)
            {
                _token.ThrowIfCancellationRequested();
                if (_sources.TryGetValue(path, out var existing))
                {
                    if (existing.Input.Text != text || existing.Input.Version != version)
                        throw new ArgumentException("A captured physical input has conflicting versions: " + path);
                    return;
                }
                try { _sources.Add(path, new Source(new AlmxTextSnapshot(text, path, version), _token)); }
                catch (XmlException) { _unavailableSources.Add(path); _coverage.Add("Source spans unavailable for invalid XML: " + path); }
            }
        }

        private void ObserveResources(LibraryGraph graph)
        {
            _resources = graph == null ? new ResourceCatalogue(_context.Resources, Register)
                : new ResourceCatalogue(_context.Resources, graph, Register, (selected, original) => Join(_resourceSymbols[selected], _resourceSymbols[original]));
            foreach (var entry in _resources.Entries)
                if (!entry.IsValid || _resources.IsAmbiguous(entry)) _resourceSymbols[entry].Ambiguous = true;

            void Register(ResourceEntry entry, LibraryIdentity owner)
            {
                _token.ThrowIfCancellationRequested();
                var symbol = AddSymbol("Resource", entry.Key, entry.Location, owner != null);
                _resourceSymbols.Add(entry, symbol);
                var key = (owner, entry.Key);
                if (_providerKeys.TryGetValue(key, out var family)) Join(family, symbol);
                else _providerKeys.Add(key, symbol);
                AddUse(symbol, "Declaration", entry.Location, "ResourceProvider:" + (owner?.ToString() ?? "Project"), null, 0, entry.Key.Length);
            }
        }

        private Symbol ResourceSymbol(string key)
        {
            if (_resources.TryResolve(key, _context.Culture, out var translation)) return _resourceSymbols[translation.Entry];
            var candidates = _resourceSymbols.Where(pair => pair.Key.Key == key).ToArray();
            var families = candidates.Select(pair => Root(pair.Value)).Distinct().ToArray();
            if (families.Length == 1 && candidates.All(pair => pair.Key.IsValid && !_resources.IsAmbiguous(pair.Key))) return families[0];
            _coverage.Add("Unavailable or ambiguous localization key: " + key);
            return null;
        }

        private void ObserveLocalization(ExpressionToken token, SourceLocation location, AlarmSyntaxNode alarm, Dictionary<string, Symbol> bindings, string collectionName)
        {
            var symbol = ResourceSymbol(token.Key);
            if (symbol == null && !_resources.Entries.Any(entry => entry.Key == token.Key)) symbol = TemplateSymbol(token.Key);
            if (symbol == null) return;
            AddUse(symbol, "Localization", location, alarm.FullyQualifiedName, null, token.KeyStart, token.KeyLength);
            ObserveArguments(token, location, alarm, symbol);
            if (_resources.TryResolve(token.Key, _context.Culture, out var translation))
                ObserveExpression(translation.Entry.Value, translation.Entry.ValueLocation, alarm, bindings,
                    record: true, formatted: token.Arguments.Count != 0, translation: true, collectionName: collectionName);
        }

        private void ObserveGeneratedCaptions()
        {
            foreach (string key in Definition.AlarmDefinition.Fields.Concat(Definition.AlarmDefinition.Collections.SelectMany(collection => collection.Fields))
                .SelectMany(field => field.ValueLabelKeys).Distinct(StringComparer.Ordinal))
            {
                var symbol = ResourceSymbol(key);
                if (symbol != null) _uses.Add(new Use(symbol, "GeneratedCaption", null, "GeneratedDefinition", null,
                    "Generated definition caption keys are immutable."));
            }
        }

        private void ObserveArguments(ExpressionToken token, SourceLocation location, AlarmSyntaxNode alarm, Symbol resource)
        {
            if (token.Arguments.Count == 0) return;
            var declarations = _lookups.Bindings.Where(binding => binding.TemplateKey == token.Key && _lookups.IsAvailable(binding)).ToArray();
            foreach (var argument in token.Arguments)
                foreach (var binding in declarations.Where(binding => binding.ArgumentName == argument.Key))
                    AddUse(_lookupSymbols[binding], "Argument", location, alarm.FullyQualifiedName, null, argument.Value.NameStart, argument.Value.NameLength);
            foreach (var entry in _resourceSymbols.Where(pair => ReferenceEquals(Root(pair.Value), Root(resource))).Select(pair => pair.Key))
            {
                var expression = PropertyExpression.Parse(entry.Value, placeholders: true, allowItemReferences: true);
                if (expression == null || expression.HasLocalization) continue;
                foreach (var placeholder in expression.Placeholders)
                {
                    Probe(null, entry.ValueLocation, null, placeholder.Start, placeholder.Length);
                    foreach (var binding in declarations.Where(binding => binding.ArgumentName == placeholder.Name))
                        AddUse(_lookupSymbols[binding], "Placeholder", entry.ValueLocation, alarm.FullyQualifiedName, null, placeholder.Start, placeholder.Length);
                }
            }
        }
    }
}
