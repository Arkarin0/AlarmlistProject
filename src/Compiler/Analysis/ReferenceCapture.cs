// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Binding;
using Alarmlist.Compiler;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    // Request-owned binding observation. Ordinary analysis never constructs this
    // object. Identity comes from syntax declarations and actual resolver choices;
    // the XML map supplies source spans only, never name-based usage discovery.
    internal sealed partial class ReferenceCapture
    {
        private readonly CompilationContext _context;
        private readonly CancellationToken _token;
        private readonly Dictionary<string, Source> _sources = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AlarmSyntaxNode, Symbol> _alarms = new Dictionary<AlarmSyntaxNode, Symbol>();
        private readonly Dictionary<PropertyDefinition, Symbol> _parameters = new Dictionary<PropertyDefinition, Symbol>();
        private readonly List<Symbol> _symbols = new List<Symbol>();
        private readonly List<Use> _uses = new List<Use>();
        private readonly HashSet<AlarmSyntaxNode> _unboundAlarms = new HashSet<AlarmSyntaxNode>();
        private readonly List<string> _coverage = new List<string>();
        private readonly string _replacement;
        private string _requestStamp;
        private IReadOnlyList<AnalysisDiagnostic> _diagnostics = Array.Empty<AnalysisDiagnostic>();

        private ReferenceCapture(CompilationContext context, CancellationToken token, string replacement)
        {
            _context = context; _token = token; _replacement = replacement;
        }

        internal static ReferenceData Capture(AlmxTextSnapshot[] inputs, CompilationContext context,
            CancellationToken token = default, string replacement = null)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (inputs.Any(input => input == null)) throw new ArgumentException("A captured input cannot be null.", nameof(inputs));
            token.ThrowIfCancellationRequested();
            var capture = new ReferenceCapture(context, token, replacement);
            capture._requestStamp = StampInputs(inputs, context, token);
            var binder = new Binder();
            foreach (var input in inputs)
            {
                token.ThrowIfCancellationRequested();
                if (capture._sources.ContainsKey(input.FilePath))
                    throw new ArgumentException("A captured source path must be unambiguous.", nameof(inputs));
                try { capture._sources.Add(input.FilePath, new Source(input, token)); }
                catch (XmlException error)
                {
                    return new ReferenceData(Array.Empty<ReferenceSymbol>(), Array.Empty<ReferenceOccurrence>(),
                        new[] { "References unavailable: invalid XML in " + input.FilePath + ": " + error.Message }, Array.Empty<AnalysisDiagnostic>());
                }
                binder.AddSourceText(input.CreateSourceText());
            }
            capture.AddAuxiliarySources(context);
            var binding = LibraryCompilation.Include(binder.Bind(), context);
            capture.Observe(binding.SyntaxTree);
            token.ThrowIfCancellationRequested();
            capture._diagnostics = Array.AsReadOnly(SnapshotAnalyzer.Compile(binding, context, token).Diagnostics
                .Select(diagnostic => new AnalysisDiagnostic(diagnostic)).ToArray());
            return capture.Freeze();
        }

        private void Observe(AlarmSyntaxTree tree)
        {
            ObserveResources(tree.LibraryGraph);
            ObserveLookups(tree.LibraryGraph);
            ObserveGeneratedCaptions();
            AddParameters(_context.Properties, "Project:" + _context.ProjectPath);
            foreach (var file in tree.Files)
            {
                AddParameters(file.Library?.Properties ?? Enumerable.Empty<PropertyDefinition>(), "LibraryProject:" + file.Library?.Identity);
                AddParameters(file.Properties, FileScope(file));
                foreach (var alarm in file.Alarms)
                {
                    _token.ThrowIfCancellationRequested();
                    var symbol = AddSymbol("Alarm", alarm.FullyQualifiedName, alarm.GetFieldLocation("FullyQualifiedName") ?? alarm.Location);
                    _alarms.Add(alarm, symbol);
                    AddUse(symbol, "Declaration", symbol.Location, alarm.FullyQualifiedName, null, 0, alarm.FullyQualifiedName?.Length ?? 0);
                    AddParameters(alarm.Properties, alarm.FullyQualifiedName);
                }
            }
            var duplicateAlarms = new HashSet<string>(tree.Alarms.GroupBy(alarm => alarm.FullyQualifiedName, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.Key), StringComparer.Ordinal);
            foreach (var alarm in tree.Alarms)
                if (duplicateAlarms.Contains(alarm.FullyQualifiedName) || string.IsNullOrWhiteSpace(alarm.FullyQualifiedName))
                {
                    _alarms[alarm].Ambiguous = true;
                    foreach (var property in alarm.Properties) _parameters[property].Ambiguous = true;
                }
            foreach (var alarm in tree.Alarms)
                if (!CompilationEvaluator.HasValidReferenceChain(alarm, duplicateAlarms)) _unboundAlarms.Add(alarm);
            ObserveGroups(tree);
            foreach (var alarm in tree.Alarms)
            {
                _token.ThrowIfCancellationRequested();
                if (alarm.Reference != null && !duplicateAlarms.Contains(alarm.ReferenceName))
                    AddUse(_alarms[alarm.Reference], "Inheritance", alarm.GetFieldLocation("ReferenceName"), alarm.FullyQualifiedName,
                        null, 0, alarm.ReferenceName.Length);
                else if (!string.IsNullOrWhiteSpace(alarm.ReferenceName))
                    _coverage.Add("Unbound or ambiguous alarm reference: " + alarm.ReferenceName);
                // An inherited declaration and a child's explicit declaration are
                // connected through this actual inheritance chain, not equal names
                // in unrelated files/alarms.
                foreach (var property in alarm.Properties)
                {
                    for (var ancestor = alarm.Reference; ancestor != null; ancestor = ancestor.Reference)
                    {
                        if (_alarms[ancestor].Ambiguous) break;
                        var inherited = ancestor.Properties.Where(item => string.Equals(item.Name, property.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (inherited.Length == 0) continue;
                        if (inherited.Length == 1) Join(_parameters[property], _parameters[inherited[0]]);
                        break;
                    }
                }
                if (_unboundAlarms.Contains(alarm))
                {
                    _coverage.Add("Alarm consumer binding unavailable: " + alarm.FullyQualifiedName);
                    continue;
                }
                var bindings = BindParameters(alarm, alarm.SourceFile);
                ObserveExpressions(alarm, bindings);
            }
            foreach (var file in tree.Files) BindParameters(null, file);
        }

        private Dictionary<string, Symbol> BindParameters(AlarmSyntaxNode alarm, AlarmSyntaxTree file)
        {
            var result = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in PropertyResolver.Resolve(alarm, file, _context))
            {
                Symbol declaration = _parameters[binding.Declaration.Definition];
                if (binding.Local != null) Join(declaration, _parameters[binding.Local.Definition]);
                if (binding.Assignment != null) Join(declaration, _parameters[binding.Assignment.Definition]);
                result.Add(binding.Declaration.Definition.Name, declaration);
                _uses.Add(new Use(declaration, "Binding", declaration.Location, alarm?.FullyQualifiedName ?? FileScope(file), null, null));
            }
            return result;
        }

        private void ObserveExpressions(AlarmSyntaxNode alarm, Dictionary<string, Symbol> bindings)
        {
            foreach (var field in Definition.AlarmDefinition.Fields)
            {
                ObserveDefinitionUse(field.Name, alarm.GetFieldLocation(field.Name), alarm);
                AlarmSyntaxNode origin = alarm.GetFieldSource(field.Name);
                if (origin != null) ObserveExpression(origin.GetLocalFieldValue(field.Name), origin.GetFieldLocation(field.Name), alarm, bindings);
                // Include authored values even if this alarm's inherited selection
                // is unavailable. Defaults and property assignments stay literal.
                if (!ReferenceEquals(origin, alarm)) ObserveExpression(alarm.GetLocalFieldValue(field.Name), alarm.GetFieldLocation(field.Name), alarm, bindings);
            }
            foreach (var group in alarm.ResolvedTestProcedures.Groups)
            {
                ObserveExpression(group.Title, group.TitleLocation, alarm, bindings);
                foreach (var entry in group.Entries.OfType<TestProcedureStepSyntax>()) ObserveExpression(entry.Text, entry.Location, alarm, bindings);
            }
            foreach (var group in alarm.TestProcedure.Groups)
            {
                ObserveExpression(group.Title, group.TitleLocation, alarm, bindings);
                foreach (var entry in group.Entries.OfType<TestProcedureStepSyntax>()) ObserveExpression(entry.Text, entry.Location, alarm, bindings);
            }
            foreach (var descriptor in Definition.AlarmDefinition.Collections)
            {
                ObserveDefinitionUse("Collection:" + descriptor.Name, alarm.GetLocalCollection(descriptor.Name)?.Location, alarm);
                foreach (var item in alarm.GetEffectiveCollection(descriptor.Name)
                    .Concat(alarm.GetLocalCollection(descriptor.Name)?.Entries ?? Enumerable.Empty<IAlarmCollectionItem>())
                    .OfType<AlarmCollectionItemSyntax>().Distinct())
                    foreach (var field in descriptor.Fields)
                    {
                        if (ReferenceEquals(item.Origin, alarm)) ObserveDefinitionUse("Collection:" + descriptor.Name + "." + field.Name, item.GetFieldLocation(field.Name), alarm);
                        ObserveExpression(item.GetFieldValue(field.Name), item.GetFieldLocation(field.Name), alarm, bindings,
                            descriptor.Kind == "Complex", collectionName: descriptor.Name);
                    }
            }
        }

        private void ObserveExpression(string raw, SourceLocation location, AlarmSyntaxNode alarm, Dictionary<string, Symbol> bindings,
            bool record = false, bool formatted = false, bool translation = false, string collectionName = null)
        {
            if (raw == null) return;
            var expression = PropertyExpression.Parse(raw, placeholders: formatted, allowItemReferences: record);
            if (expression == null)
            {
                _coverage.Add("Invalid expression at " + Address(location));
                return;
            }
            if (translation && expression.HasLocalization)
            {
                _coverage.Add("Invalid nested localization expression at " + Address(location));
                return;
            }
            foreach (var token in expression.Tokens) ObserveToken(token);
            void ObserveToken(ExpressionToken token)
            {
                _token.ThrowIfCancellationRequested();
                if (token.Kind == "parameter")
                {
                    if (bindings.TryGetValue(token.Key, out var parameter) && !token.Key.StartsWith("Alarmlist", StringComparison.OrdinalIgnoreCase))
                        AddUse(parameter, "Expression", location, alarm.FullyQualifiedName, null, token.KeyStart, token.KeyLength);
                    else if (!token.Key.StartsWith("Alarmlist", StringComparison.OrdinalIgnoreCase))
                        _coverage.Add("Unbound parameter: " + token.Key + " at " + Address(location));
                    else ObserveWellKnown(token, location, alarm);
                }
                else if (token.Kind == "loc") ObserveLocalization(token, location, alarm, bindings, collectionName);
                else if (token.Kind == "item") ObserveSibling(token, location, alarm, collectionName);
                else ObserveLookupToken(token, location, alarm);
                foreach (var argument in token.Arguments.Values)
                    if (argument.Token != null) ObserveToken(argument.Token);
            }
        }

        private void AddParameters(IEnumerable<PropertyDefinition> properties, string scope)
        {
            var declarations = properties.ToArray();
            var counts = declarations.GroupBy(property => property.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            foreach (var property in declarations)
            {
                if (_parameters.ContainsKey(property)) continue;
                var symbol = AddSymbol("Parameter", property.Name, property.Location);
                symbol.Ambiguous = property.HasInvalidContent || !PropertyExpression.IsValidName(property.Name)
                    || property.Name.StartsWith("Alarmlist", StringComparison.OrdinalIgnoreCase)
                    || counts[property.Name ?? string.Empty] != 1;
                _parameters.Add(property, symbol);
                AddUse(symbol, "Declaration", property.Location, scope, "Name", 0, property.Name?.Length ?? 0);
            }
        }

        private static string FileScope(AlarmSyntaxTree file) => "File:" + (file?.Properties.FirstOrDefault()?.Location?.FilePath ?? "unavailable");

        private Symbol AddSymbol(string kind, string name, SourceLocation location, bool readOnly = false)
        {
            bool mapped = location != null && _sources.TryGetValue(location.FilePath, out var source)
                && location.SourceVersion == source.Input.Version;
            string stamp = mapped ? _sources[location.FilePath].Stamp : "external";
            var symbol = new Symbol(kind + ":" + _requestStamp + ":" + stamp + ":" + Address(location) + ":" + _symbols.Count, kind, name, location, !mapped || readOnly);
            _symbols.Add(symbol);
            return symbol;
        }

        private void AddUse(Symbol symbol, string role, SourceLocation location, string consumer, string attribute, int start, int length)
        {
            MappedTextEdit edit = null;
            string unavailable = null;
            if ((role == "Declaration" && symbol.ReadOnly) || location == null || !_sources.TryGetValue(location.FilePath, out var source) || location.SourceVersion != source.Input.Version)
                unavailable = "The defining source is read-only or was not captured.";
            else
            {
                var node = source.Find(location);
                XObject value = attribute == null ? node : (node as XElement ?? (node as XAttribute)?.Parent)?.Attribute(attribute);
                if (value == null || !source.Map.Values.TryGetValue(value, out var mapped)
                    || !mapped.TryReplace(start, length, _replacement ?? symbol.Name ?? string.Empty, out edit))
                    unavailable = "The token has no contiguous editable source span.";
            }
            _uses.Add(new Use(symbol, role, location, consumer, edit, unavailable));
        }

        private ReferenceData Freeze()
        {
            _token.ThrowIfCancellationRequested();
            foreach (var symbol in _symbols)
                if (symbol.Owner != null && Root(symbol.Owner).Ambiguous) { symbol.Ambiguous = true; Root(symbol).Ambiguous = true; }
            var ambiguous = new HashSet<Symbol>(_symbols.Where(symbol => symbol.Ambiguous).Select(Root));
            foreach (var symbol in ambiguous) _coverage.Add("Ambiguous or invalid declaration: " + symbol.Kind + " " + symbol.Name);
            return new ReferenceData(_symbols.Select(symbol => new ReferenceSymbol(symbol.Id, Root(symbol).Id, symbol.Kind,
                symbol.Name, symbol.Location, symbol.ReadOnly, symbol.Owner == null ? null : Root(symbol.Owner).Id)), _uses.GroupBy(use => (Root(use.Symbol).Id, use.Role,
                    Address(use.Location), use.Consumer, use.Edit?.Start, use.Edit?.Length, use.Unavailable)).Select(group => group.First())
                .Select(use => new ReferenceOccurrence(Root(use.Symbol).Id,
                use.Role, use.Location, use.Consumer, use.Edit?.Start,
                use.Edit?.Length, ambiguous.Contains(Root(use.Symbol)) ? null : use.Edit?.Text,
                ambiguous.Contains(Root(use.Symbol)) ? "Ambiguous or invalid binding; no replacement was guessed." : use.Unavailable)), _coverage, _diagnostics);
        }

        private static string Address(SourceLocation location) => location == null ? "unavailable" :
            location.FilePath + ":" + location.SourceVersion + ":" + location.LineNumber + ":" + location.ColumnNumber;

        private static string StampInputs(AlmxTextSnapshot[] inputs, CompilationContext context, CancellationToken token)
        {
            var text = new StringBuilder();
            void Add(string value)
            {
                token.ThrowIfCancellationRequested();
                text.Append(value?.Length ?? -1).Append(':').Append(value);
            }
            Add(context.ProjectPath); Add(context.Configuration); Add(context.Platform); Add(context.Culture);
            foreach (var input in inputs) { Add(input.FilePath); Add(input.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)); Add(input.Text); }
            foreach (var property in context.Properties) { Add(property.Name); Add(property.Value); Add(Address(property.Location)); }
            foreach (var resource in context.Resources) { Add(resource.FilePath); Add(resource.Culture); Add(resource.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)); Add(resource.Text); }
            foreach (var lookup in context.Lookups) { Add(lookup.FilePath); Add(lookup.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)); Add(lookup.Text); }
            foreach (var root in context.Libraries.Roots) Add(root.ToString());
            foreach (var library in context.Libraries.Artifacts) { Add(library.FilePath); Add(library.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)); Add(library.Text); }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private static Symbol Root(Symbol symbol)
        {
            Symbol root = symbol;
            while (root.Parent != null) root = root.Parent;
            while (symbol.Parent != null)
            {
                Symbol next = symbol.Parent;
                symbol.Parent = root;
                symbol = next;
            }
            return root;
        }
        private static void Join(Symbol left, Symbol right)
        {
            left = Root(left); right = Root(right);
            if (!ReferenceEquals(left, right)) { left.Ambiguous |= right.Ambiguous; right.Parent = left; }
        }

        private sealed class Symbol
        {
            internal Symbol(string id, string kind, string name, SourceLocation location, bool readOnly)
            { Id = id; Kind = kind; Name = name; Location = location; ReadOnly = readOnly; }
            internal readonly string Id, Kind, Name;
            internal readonly SourceLocation Location;
            internal readonly bool ReadOnly;
            internal bool Ambiguous;
            internal Symbol Parent;
            internal Symbol Owner;
        }
        private sealed class Use
        {
            internal Use(Symbol symbol, string role, SourceLocation location, string consumer, MappedTextEdit edit, string unavailable)
            { Symbol = symbol; Role = role; Location = location; Consumer = consumer; Edit = edit; Unavailable = unavailable; }
            internal readonly Symbol Symbol;
            internal readonly string Role, Consumer, Unavailable;
            internal readonly SourceLocation Location;
            internal readonly MappedTextEdit Edit;
        }
        private sealed class Source
        {
            internal readonly AlmxTextSnapshot Input;
            internal readonly XmlValueSourceMap Map;
            internal readonly string Stamp;
            private readonly Dictionary<(int Line, int Column), XObject> _elements;
            private readonly int[] _lineOffsets;
            internal Source(AlmxTextSnapshot input, CancellationToken token)
            {
                Input = input; Map = XmlValueSourceMap.Parse(input.Text, token);
                _elements = Map.Document.Root.DescendantsAndSelf().SelectMany(element => new XObject[] { element }.Concat(element.Attributes()))
                    .ToDictionary(node => (((IXmlLineInfo)node).LineNumber, ((IXmlLineInfo)node).LinePosition));
                var lines = new List<int> { 0 };
                for (int index = 0; index < input.Text.Length; index++)
                {
                    if (input.Text[index] == '\r') { if (index + 1 < input.Text.Length && input.Text[index + 1] == '\n') index++; lines.Add(index + 1); }
                    else if (input.Text[index] == '\n') lines.Add(index + 1);
                }
                _lineOffsets = lines.ToArray();
                using (var hash = SHA256.Create()) Stamp = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(input.Text)));
            }
            internal XObject Find(SourceLocation location) => _elements.TryGetValue((location.LineNumber, location.ColumnNumber), out var element) ? element : null;
            internal int Offset(SourceLocation location)
            {
                if (location.LineNumber < 1 || location.LineNumber > _lineOffsets.Length) throw new InvalidOperationException("A source line is outside its captured source.");
                int offset = _lineOffsets[location.LineNumber - 1] + location.ColumnNumber - 1;
                if (offset < 0 || offset > Input.Text.Length - (location.EndColumnNumber - location.ColumnNumber))
                    throw new InvalidOperationException("A compiler source location is outside its captured source.");
                return offset;
            }
        }
    }
}
