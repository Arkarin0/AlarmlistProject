// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Lookups;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed partial class ReferenceCapture
    {
        private LookupCatalogue _lookups;
        private readonly Dictionary<object, Symbol> _lookupSymbols = new Dictionary<object, Symbol>();
        private readonly Dictionary<string, List<Symbol>> _listSymbols = new Dictionary<string, List<Symbol>>(StringComparer.Ordinal);
        private readonly Dictionary<LookupArgumentBinding, Symbol> _bindingTemplates = new Dictionary<LookupArgumentBinding, Symbol>();
        private readonly Dictionary<(LibraryIdentity Owner, string Key), Symbol> _templates = new Dictionary<(LibraryIdentity, string), Symbol>();
        private readonly List<(LibraryIdentity Owner, LookupDocument Document)> _lookupDocuments = new List<(LibraryIdentity, LookupDocument)>();

        private void ObserveLookups(LibraryGraph graph)
        {
            _lookups = new LookupCatalogue(_context.Lookups, graph, RegisterLookupDocument, JoinLookupOverride);
            foreach (var template in _lookups.Bindings.GroupBy(binding => binding.TemplateKey, StringComparer.Ordinal))
            {
                var declarations = template.Select(binding => _bindingTemplates[binding]).ToArray();
                foreach (var symbol in declarations.Skip(1)) Join(declarations[0], symbol);
            }
            foreach (var list in _lookups.Lists)
            {
                var declarations = _listSymbols[list.Key];
                foreach (var symbol in declarations.Skip(1)) Join(declarations[0], symbol);
            }
            foreach (var document in _lookupDocuments)
            {
                foreach (var path in document.Document.Paths)
                    if (_lookups.IsAmbiguous(path)) _lookupSymbols[path].Ambiguous = true;
                foreach (var list in document.Document.Lists)
                    foreach (var entry in list.Entries)
                        if (_lookups.IsAmbiguous(list.Key, entry.Key)) _lookupSymbols[entry].Ambiguous = true;
                foreach (var binding in document.Document.Bindings)
                {
                    if (!_lookups.IsAvailable(binding)) _lookupSymbols[binding].Ambiguous = true;
                    if (_listSymbols.TryGetValue(binding.ListKey, out var lists))
                        AddUse(lists[0], "ListBinding", binding.ListLocation, "Template:" + binding.TemplateKey, null, 0, binding.ListKey.Length);
                    else _coverage.Add("Unavailable argument-binding list: " + binding.ListKey);
                }
            }
            // Duplicate provider declarations remain ambiguous even if another
            // provider overrides them. Observe the original parsed documents.
            foreach (var owner in _lookupDocuments.GroupBy(document => document.Owner))
            {
                foreach (var duplicate in owner.SelectMany(document => document.Document.Paths).GroupBy(path => path.Key, StringComparer.Ordinal).Where(group => group.Count() > 1))
                    foreach (var path in duplicate) _lookupSymbols[path].Ambiguous = true;
                foreach (var list in owner.SelectMany(document => document.Document.Lists).GroupBy(list => list.Key, StringComparer.Ordinal))
                    foreach (var duplicate in list.SelectMany(item => item.Entries).GroupBy(entry => entry.Key, StringComparer.Ordinal).Where(group => group.Count() > 1))
                        foreach (var entry in duplicate) _lookupSymbols[entry].Ambiguous = true;
            }
        }

        private void RegisterLookupDocument(LookupDocument document, LibraryIdentity owner)
        {
            _lookupDocuments.Add((owner, document));
            Symbol Declare(object model, string kind, string name, SourceLocation location, Symbol parent = null)
            {
                _token.ThrowIfCancellationRequested();
                var symbol = AddSymbol(kind, name, location, owner != null); symbol.Owner = parent;
                _lookupSymbols.Add(model, symbol);
                AddUse(symbol, "Declaration", location, "LookupProvider:" + (owner?.ToString() ?? "Project"), null, 0, name.Length);
                return symbol;
            }
            foreach (var path in document.Paths)
            {
                var pathSymbol = Declare(path, "LookupPath", path.Key, path.Location);
                foreach (var label in path.Segments) ObserveLabel(label);
                foreach (var member in path.Members)
                {
                    var memberSymbol = Declare(member, "LookupMember", member.Key, member.Location, pathSymbol);
                    ObserveLabel(member.Label);
                    foreach (var metadata in member.Metadata) Declare(metadata, "LookupMetadata", metadata.Name, metadata.NameLocation, memberSymbol);
                }
            }
            foreach (var list in document.Lists)
            {
                var listSymbol = Declare(list, "LookupList", list.Key, list.Location);
                if (!_listSymbols.TryGetValue(list.Key, out var symbols)) _listSymbols.Add(list.Key, symbols = new List<Symbol>());
                symbols.Add(listSymbol);
                foreach (var entry in list.Entries)
                {
                    var entrySymbol = Declare(entry, "LookupEntry", entry.Key, entry.Location, listSymbol);
                    if (entry.DescriptionKey != null) ObserveLabel(entry.DescriptionKey);
                    foreach (var metadata in entry.Metadata) Declare(metadata, "LookupMetadata", metadata.Name, metadata.NameLocation, entrySymbol);
                }
            }
            foreach (var binding in document.Bindings)
            {
                var template = AddSymbol("Template", binding.TemplateKey, binding.TemplateLocation, owner != null);
                AddUse(template, "Declaration", binding.TemplateLocation, "LookupProvider:" + (owner?.ToString() ?? "Project"), null, 0, binding.TemplateKey.Length);
                var providerKey = (owner, binding.TemplateKey);
                if (_templates.TryGetValue(providerKey, out var previous)) Join(previous, template);
                else _templates.Add(providerKey, template);
                var resource = ResourceSymbol(binding.TemplateKey);
                if (resource != null) Join(resource, template);
                _bindingTemplates.Add(binding, template);
                Declare(binding, "Argument", binding.ArgumentName, binding.Location, template);
            }
        }

        private void ObserveLabel(LookupLabel label)
        {
            var resource = ResourceSymbol(label.Key);
            if (resource != null) AddUse(resource, "LookupLabel", label.Location, "LookupLabel", null, 0, label.Key.Length);
        }

        private void JoinLookupOverride(object selected, object original)
        {
            Join(_lookupSymbols[selected], _lookupSymbols[original]);
            if (selected is LookupPath path && original is LookupPath inherited)
            {
                foreach (var member in path.Members)
                {
                    var prior = inherited.Members.FirstOrDefault(candidate => candidate.Key == member.Key);
                    if (prior == null) continue;
                    Join(_lookupSymbols[member], _lookupSymbols[prior]);
                    foreach (var metadata in member.Metadata)
                    {
                        var priorMetadata = prior.Metadata.FirstOrDefault(candidate => candidate.Name == metadata.Name);
                        if (priorMetadata != null) Join(_lookupSymbols[metadata], _lookupSymbols[priorMetadata]);
                    }
                }
            }
            if (selected is LookupListEntry entry && original is LookupListEntry priorEntry)
                foreach (var metadata in entry.Metadata)
                {
                    var prior = priorEntry.Metadata.FirstOrDefault(candidate => candidate.Name == metadata.Name);
                    if (prior != null) Join(_lookupSymbols[metadata], _lookupSymbols[prior]);
                }
            if (selected is LookupArgumentBinding binding && original is LookupArgumentBinding priorBinding)
                Join(_bindingTemplates[binding], _bindingTemplates[priorBinding]);
        }

        private void ObserveLookupToken(ExpressionToken token, SourceLocation location, AlarmSyntaxNode alarm)
        {
            var binding = LookupTokenBinding.Resolve(token, _lookups);
            if (binding.Failure != null) _coverage.Add("Unavailable lookup reference: " + binding.Failure + " at " + Address(location));
            string[] keys = binding.Keys;
            int position = token.KeyStart;
            void Use(Symbol symbol, string key)
            {
                AddUse(symbol, "Lookup", location, alarm.FullyQualifiedName, null, position, key.Length);
                position += key.Length + 1;
            }
            if (token.Kind == "list")
            {
                if (_listSymbols.TryGetValue(keys[0], out var lists)) Use(lists[0], keys[0]);
                else position += keys[0].Length + 1;
                if (binding.Entry != null) Use(_lookupSymbols[binding.Entry], keys[1]);
                return;
            }
            if (binding.Path != null) Use(_lookupSymbols[binding.Path], keys[0]);
            if (binding.Member != null) Use(_lookupSymbols[binding.Member], keys[1]);
            if (binding.Metadata != null) Use(_lookupSymbols[binding.Metadata], keys[2]);
        }

        private Symbol TemplateSymbol(string key)
        {
            var declarations = _lookups.Bindings.Where(binding => binding.TemplateKey == key)
                .Select(binding => Root(_bindingTemplates[binding])).Distinct().ToArray();
            return declarations.Length == 1 ? declarations[0] : null;
        }
    }
}
