// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Contract 8 adapter. Copies compiler answers without reimplementing resolution.</summary>
    internal sealed class AnalysisContractReader
    {
        private readonly Dictionary<Type, Type> _types = new Dictionary<Type, Type>();

        public AnalysisContractReader(Assembly assembly)
        {
            Add<AnalysisSnapshot>(assembly, "Alarmlist.Analysis.AnalysisResult");
            FieldInfo version = _types[typeof(AnalysisSnapshot)].GetField("CurrentContractVersion", BindingFlags.Public | BindingFlags.Static);
            if (version == null || !version.IsLiteral || !Equals(version.GetRawConstantValue(), 8))
                throw new NotSupportedException("Only analysis contract 8 is supported by this extension. Rebuild matching tools.");
            Type definition = assembly.GetType("Alarmlist.Definition.AlarmDefinition");
            if (!Equals(definition?.GetField("ToolingFingerprint")?.GetRawConstantValue(), Definition.AlarmDefinition.ToolingFingerprint))
                throw new NotSupportedException("The SDK and extension alarm definitions do not match. Rebuild matching tools; XML editing remains available.");
            Add<AnalysisExplanation>(assembly, "Alarmlist.Analysis.ValueExplanationResult");
            Add<AnalysisExplanationTarget>(assembly, "Alarmlist.Analysis.ValueExplanationTarget");
            Add<AnalysisExplanationNode>(assembly, "Alarmlist.Analysis.ValueExplanationNode");
            Add<Definition.AlarmFieldDescriptor>(assembly, "Alarmlist.Definition.AlarmFieldDescriptor");
            Add<Definition.AlarmCollectionDescriptor>(assembly, "Alarmlist.Definition.AlarmCollectionDescriptor");
            Add<AnalysisCollection>(assembly, "Alarmlist.Analysis.AnalyzedCollection");
            Add<AnalysisCollectionEntry>(assembly, "Alarmlist.Analysis.AnalyzedCollectionEntry");
            Add<AnalysisCollectionEntryOrigin>(assembly, "Alarmlist.Analysis.CollectionEntryOrigin");
            Add<AnalysisFieldValueState>(assembly, "Alarmlist.Definition.AlarmFieldValueState");
            Add<AnalysisResource>(assembly, "Alarmlist.Analysis.AnalyzedResource");
            Add<AnalysisLibrary>(assembly, "Alarmlist.Analysis.AnalyzedLibrary");
            Add<AnalysisLibraryIdentity>(assembly, "Alarmlist.Libraries.LibraryIdentity");
            Add<AnalysisLibrarySource>(assembly, "Alarmlist.Analysis.AnalyzedLibrarySource");
            Add<AnalysisLibraryLocation>(assembly, "Alarmlist.Analysis.AnalyzedLibraryLocation");
            Add<AnalysisLibraryDocument>(assembly, "Alarmlist.Analysis.AnalyzedLibraryDocument");
            Add<AnalysisResourceEntry>(assembly, "Alarmlist.Analysis.AnalyzedResourceEntry");
            Add<AnalysisTranslation>(assembly, "Alarmlist.Analysis.AnalyzedTranslation");
            Add<AnalysisLocalizationUse>(assembly, "Alarmlist.Analysis.AnalyzedLocalizationUse");
            Add<AnalysisLookupFile>(assembly, "Alarmlist.Analysis.AnalyzedLookupFile");
            Add<AnalysisLookupPath>(assembly, "Alarmlist.Analysis.AnalyzedLookupPath");
            Add<AnalysisLookupMember>(assembly, "Alarmlist.Analysis.AnalyzedLookupMember");
            Add<AnalysisLookupMetadata>(assembly, "Alarmlist.Analysis.AnalyzedLookupMetadata");
            Add<AnalysisLookupUse>(assembly, "Alarmlist.Analysis.AnalyzedLookupUse");
            Add<AnalysisLookupList>(assembly, "Alarmlist.Analysis.AnalyzedLookupList");
            Add<AnalysisLookupListEntry>(assembly, "Alarmlist.Analysis.AnalyzedLookupListEntry");
            Add<AnalysisListMetadata>(assembly, "Alarmlist.Analysis.AnalyzedListMetadata");
            Add<AnalysisArgumentBinding>(assembly, "Alarmlist.Analysis.AnalyzedArgumentBinding");
            Add<AnalysisSource>(assembly, "Alarmlist.Analysis.AnalyzedSource");
            Add<AnalysisProcedureCreation>(assembly, "Alarmlist.Configuration.ProcedureCreationSettings");
            Add<AnalysisGroupDefault>(assembly, "Alarmlist.Configuration.ProcedureGroupDefault");
            Add<AnalysisAlarm>(assembly, "Alarmlist.Analysis.AnalyzedAlarm");
            Add<AnalysisAlarmOrigin>(assembly, "Alarmlist.Analysis.AlarmOrigin");
            Add<AnalysisField>(assembly, "Alarmlist.Analysis.AnalyzedField");
            Add<AnalysisProperty>(assembly, "Alarmlist.Analysis.AnalyzedProperty");
            Add<AnalysisPropertyOrigin>(assembly, "Alarmlist.Analysis.PropertyOrigin");
            Add<AnalysisSection>(assembly, "Alarmlist.Analysis.AnalyzedProcedureSection");
            Add<AnalysisEntry>(assembly, "Alarmlist.Analysis.AnalyzedProcedureEntry");
            Add<AnalysisEntryOrigin>(assembly, "Alarmlist.Analysis.ProcedureEntryOrigin");
            Add<AnalysisDiagnostic>(assembly, "Alarmlist.Analysis.AnalysisDiagnostic");
            Add<AnalysisLocation>(assembly, "Alarmlist.Text.SourceLocation");
            Add<AnalysisSeverity>(assembly, "Alarmlist.Diagnostics.DiagnosticSeverity");
            Add<AnalysisStepKind>(assembly, "Alarmlist.Syntax.TestProcedureStepKind");
            foreach (KeyValuePair<Type, Type> pair in _types)
            {
                if (pair.Key.IsEnum)
                {
                    if (!pair.Value.IsEnum || !Enum.GetNames(pair.Key).SequenceEqual(Enum.GetNames(pair.Value)))
                        throw new NotSupportedException("Incompatible SDK enum: " + pair.Value.FullName);
                    continue;
                }
                foreach (PropertyInfo property in pair.Key.GetProperties())
                {
                    PropertyInfo sdkProperty = pair.Value.GetProperty(property.Name);
                    if (sdkProperty?.GetGetMethod() == null || sdkProperty.GetGetMethod().IsStatic
                        || sdkProperty.GetIndexParameters().Length != 0 || sdkProperty.PropertyType != SdkType(property.PropertyType))
                        throw new NotSupportedException("Incompatible SDK property: " + pair.Value.FullName + "." + property.Name);
                }
            }
        }

        public AnalysisExplanation ReadExplanation(object result)
        {
            if (result == null || Value<int>(result, "ContractVersion") != 8
                || Value<string>(result, "ToolingFingerprint") != Definition.AlarmDefinition.ToolingFingerprint)
                throw new NotSupportedException("The SDK returned an incompatible explanation. Rebuild matching tools.");
            var target = Value<object>(result, "Target");
            return new AnalysisExplanation(8, Value<string>(result, "ToolingFingerprint"),
                new AnalysisExplanationTarget(Location(Value<object>(target, "AlarmLocation")), Value<string>(target, "Kind"),
                    Value<string>(target, "Name"), Value<string>(target, "FieldName"), Location(Value<object>(target, "EntryLocation"))),
                Value<string>(result, "ProjectPath"), Value<string>(result, "Configuration"), Value<string>(result, "Platform"),
                Value<string>(result, "Culture"), Value<int?>(result, "RootId"), List(result, "Nodes", node =>
                    new AnalysisExplanationNode(Value<int>(node, "Id"), Value<string>(node, "Kind"), Value<string>(node, "Name"),
                        Value<string>(node, "RawValue"), Value<string>(node, "Value"), Value<string>(node, "CanonicalValue"),
                        Value<string>(node, "State"), Value<string>(node, "Type"), Value<string>(node, "Scope"), Value<string>(node, "DefiningAlarm"),
                        Location(Value<object>(node, "Location")), LibraryIdentity(Value<object>(node, "Library")), List(node, "Inputs", id => (int)id),
                        Value<string>(node, "RequestedCulture"), Value<string>(node, "SupplyingCulture"))),
                List(result, "Diagnostics", diagnostic => new AnalysisDiagnostic(Value<string>(diagnostic, "Id"),
                    EnumValue<AnalysisSeverity>(diagnostic, "Severity"), Value<string>(diagnostic, "Message"), Location(Value<object>(diagnostic, "Location")),
                    List(diagnostic, "RelatedLocations", Location))), Value<string>(result, "Message"), Value<bool>(result, "IsAvailable"));
        }

        private void Add<T>(Assembly assembly, string name)
        {
            _types.Add(typeof(T), assembly.GetType(name) ?? throw new NotSupportedException("The SDK has no analysis type " + name + "."));
        }

        private Type SdkType(Type type)
        {
            if (_types.TryGetValue(type, out Type mapped)) return mapped;
            if (type.IsGenericType)
                return type.GetGenericTypeDefinition().MakeGenericType(type.GetGenericArguments().Select(SdkType).ToArray());
            return type;
        }

        public AnalysisSnapshot Read(object result)
        {
            if (result == null || Value<int>(result, "ContractVersion") != 8)
                throw new NotSupportedException("The SDK returned an unsupported analysis contract.");
            if (Value<string>(result, "ToolingFingerprint") != Definition.AlarmDefinition.ToolingFingerprint)
                throw new NotSupportedException("The SDK returned mismatched alarm metadata. Rebuild matching tools; XML editing remains available.");
            return new AnalysisSnapshot(8, Value<bool>(result, "Success"), List(result, "Sources", source =>
                new AnalysisSource(Value<string>(source, "FilePath"), Value<int>(source, "Version"), List(source, "Properties", Property),
                    ProcedureCreation(Value<object>(source, "ProcedureCreation")))),
                List(result, "Alarms", Alarm), List(result, "Diagnostics", diagnostic => new AnalysisDiagnostic(
                    Value<string>(diagnostic, "Id"), EnumValue<AnalysisSeverity>(diagnostic, "Severity"), Value<string>(diagnostic, "Message"),
                    Location(Value<object>(diagnostic, "Location")), List(diagnostic, "RelatedLocations", Location))),
                Value<string>(result, "Culture"), List(result, "Resources", resource =>
                    new AnalysisResource(Value<string>(resource, "FilePath"), Value<int>(resource, "Version"), Value<string>(resource, "Culture"))),
                List(result, "ResourceEntries", entry => new AnalysisResourceEntry(Value<string>(entry, "Key"), Value<string>(entry, "Culture"),
                    Value<string>(entry, "RawValue"), Value<string>(entry, "Comment"), Location(Value<object>(entry, "Location")),
                    Location(Value<object>(entry, "ValueLocation")), Value<bool>(entry, "IsValid"), Value<bool>(entry, "IsAmbiguous"),
                    List(entry, "PlaceholderNames", name => (string)name))),
                List(result, "Translations", Translation), List(result, "LookupFiles", file =>
                    new AnalysisLookupFile(Value<string>(file, "FilePath"), Value<int>(file, "Version"))), List(result, "LookupPaths", LookupPath),
                List(result, "LookupLists", list => new AnalysisLookupList(Value<string>(list, "Key"), List(list, "Entries", ListEntry))),
                List(result, "ArgumentBindings", binding => new AnalysisArgumentBinding(Value<string>(binding, "TemplateKey"),
                    Value<string>(binding, "ArgumentName"), Value<string>(binding, "ListKey"), Location(Value<object>(binding, "Location")),
                    Value<bool>(binding, "IsAvailable"))), List(result, "Libraries", Library), Value<string>(result, "SchemaFingerprint"),
                    Value<string>(result, "ToolingFingerprint"), List(result, "FieldDefinitions", Descriptor),
                    List(result, "CollectionDefinitions", CollectionDescriptor), Value<bool>(result, "HasTestProcedure"));
        }

        private static AnalysisLibraryIdentity LibraryIdentity(object identity) => identity == null ? null
            : new AnalysisLibraryIdentity(Value<string>(identity, "Id"), Value<string>(identity, "Version"));

        private static AnalysisLibrary Library(object library) => new AnalysisLibrary(LibraryIdentity(Value<object>(library, "Identity")),
            List(library, "Dependencies", LibraryIdentity), List(library, "Sources", source => new AnalysisLibrarySource(
                Value<string>(source, "Name"), Value<string>(source, "FilePath"), Value<string>(source, "Definition"),
                List(source, "Properties", Property), List(source, "Origins", origin => new AnalysisLibraryLocation(
                    Value<string>(origin, "Key"), Location(Value<object>(origin, "OriginalLocation")), Location(Value<object>(origin, "DefinitionLocation")))))),
            List(library, "Resources", resource => new AnalysisResource(Value<string>(resource, "FilePath"), Value<int>(resource, "Version"), Value<string>(resource, "Culture"))),
            List(library, "LookupFiles", lookup => new AnalysisLookupFile(Value<string>(lookup, "FilePath"), Value<int>(lookup, "Version"))),
            List(library, "Documents", document => new AnalysisLibraryDocument(Value<string>(document, "Name"), Value<string>(document, "FilePath"),
                Value<string>(document, "Kind"), Value<string>(document, "Culture"), Value<string>(document, "Definition"),
                List(document, "Origins", origin => new AnalysisLibraryLocation(Value<string>(origin, "Key"),
                    Location(Value<object>(origin, "OriginalLocation")), Location(Value<object>(origin, "DefinitionLocation")))))));

        private static AnalysisProcedureCreation ProcedureCreation(object settings) => new AnalysisProcedureCreation(
            List(settings, "Groups", group => new AnalysisGroupDefault(Value<string>(group, "Name"), Value<string>(group, "Title"))), Value<string>(settings, "Error"));

        private static AnalysisAlarm Alarm(object alarm)
        {
            return new AnalysisAlarm(AlarmOrigin(Value<object>(alarm, "Origin")), Value<string>(alarm, "ReferenceName"),
                Value<bool>(alarm, "IsResolved"), List(alarm, "ReferenceChain", AlarmOrigin), List(alarm, "Fields", Field),
                List(alarm, "ProcedureSections", Section), Value<bool>(alarm, "IsAbstract"),
                Value<bool>(alarm, "HasEvaluatedValues"), List(alarm, "Properties", Property), List(alarm, "LocalizationUses", use =>
                    new AnalysisLocalizationUse(Translation(Value<object>(use, "Translation")), Location(Value<object>(use, "UsageLocation")),
                        Value<string>(use, "Value"), Value<bool>(use, "IsValueAvailable"))), List(alarm, "LookupUses", use =>
                    new AnalysisLookupUse(Value<string>(use, "Expression"), Location(Value<object>(use, "DefinitionLocation")),
                        Location(Value<object>(use, "UsageLocation")), Value<string>(use, "Value"), Value<bool>(use, "IsValueAvailable"))),
                List(alarm, "Collections", Collection));
        }

        private static AnalysisTranslation Translation(object translation) => translation == null ? null : new AnalysisTranslation(
            Value<string>(translation, "Key"), Value<string>(translation, "RequestedCulture"), Value<string>(translation, "SupplyingCulture"),
            Value<bool>(translation, "IsKeyword"), Value<bool>(translation, "IsAvailable"), Value<string>(translation, "RawValue"),
            Value<string>(translation, "Comment"), Location(Value<object>(translation, "Location")), Location(Value<object>(translation, "ValueLocation")),
            List(translation, "PlaceholderNames", name => (string)name));

        private static AnalysisLookupPath LookupPath(object path) => new AnalysisLookupPath(
            Value<string>(path, "Key"), Value<bool>(path, "IsAmbiguous"), Location(Value<object>(path, "Location")),
            Value<string>(path, "Expression"), Value<string>(path, "Value"), Value<bool>(path, "IsValueAvailable"),
            List(path, "Segments", Translation), List(path, "Members", member => new AnalysisLookupMember(
                Value<string>(member, "Key"), Value<string>(member, "Kind"), Location(Value<object>(member, "Location")),
                Translation(Value<object>(member, "Label")), Value<string>(member, "Expression"), Value<string>(member, "Value"),
                Value<bool>(member, "IsValueAvailable"), List(member, "Metadata", metadata => new AnalysisLookupMetadata(
                    Value<string>(metadata, "Name"), Value<string>(metadata, "Value"), Location(Value<object>(metadata, "Location")),
                    Value<string>(metadata, "Expression"))))));

        private static AnalysisLookupListEntry ListEntry(object entry) => new AnalysisLookupListEntry(
            Value<string>(entry, "Key"), Value<string>(entry, "Value"), Value<string>(entry, "Description"),
            Translation(Value<object>(entry, "DescriptionTranslation")), Location(Value<object>(entry, "Location")),
            Value<bool>(entry, "IsAmbiguous"), Value<string>(entry, "Expression"), List(entry, "Metadata", metadata =>
                new AnalysisListMetadata(Value<string>(metadata, "Name"), Value<string>(metadata, "Value"), Location(Value<object>(metadata, "Location")))));

        private static AnalysisSection Section(object section)
        {
            return new AnalysisSection(Value<string>(section, "Name"), List(section, "LocalEntries", Entry),
                List(section, "EffectiveEntries", Entry), List(section, "ExcludedEntries", Entry),
                Field(Value<object>(section, "Title")), AlarmOrigin(Value<object>(section, "DefiningAlarm")),
                Location(Value<object>(section, "Location")), Value<bool>(section, "IsLocallyDeclared"));
        }

        private static AnalysisField Field(object field) => new AnalysisField(Value<string>(field, "Name"),
            Value<bool>(field, "IsPresent"), Value<string>(field, "LocalValue"), Location(Value<object>(field, "LocalLocation")),
            Value<string>(field, "EffectiveValue"), AlarmOrigin(Value<object>(field, "DefiningAlarm")),
            Location(Value<object>(field, "ValueLocation")), Value<bool>(field, "IsInherited"),
            Value<string>(field, "EffectiveExpression"), Value<bool>(field, "IsValueAvailable"), Value<string>(field, "Type"),
            EnumValue<AnalysisFieldValueState>(field, "State"), Value<string>(field, "CanonicalValue"), ScalarValue(field), Value<bool>(field, "IsDefault"));

        private static object ScalarValue(object field)
        {
            object value = Value<object>(field, "TypedValue");
            if (value == null || value is string || value is long || value is decimal || value is double || value is bool
                || value is DateTime || value is TimeSpan || value is DateTimeOffset) return value;
            throw new NotSupportedException("The SDK returned an unsupported scalar value type.");
        }

        private static Definition.AlarmFieldDescriptor Descriptor(object field) => new Definition.AlarmFieldDescriptor(
            Value<string>(field, "Name"), Value<string>(field, "Type"), Value<bool>(field, "Required"), Value<string>(field, "Default"),
            Value<string>(field, "Minimum"), Value<string>(field, "Maximum"), Value<string>(field, "Label"), Value<string>(field, "Help"),
            Value<bool>(field, "Multiline"), Value<bool>(field, "PreviewVisible"), Value<double>(field, "PreviewWidth"),
            List(field, "Values", item => (string)item).ToArray(), List(field, "ValueLabels", item => (string)item).ToArray());

        private static AnalysisEntry Entry(object entry)
        {
            AnalysisStepKind? kind = Value<object>(entry, "Kind") == null ? (AnalysisStepKind?)null : EnumValue<AnalysisStepKind>(entry, "Kind");
            return new AnalysisEntry(kind, Value<bool>(entry, "IsClear"), Value<string>(entry, "Text"),
                EntryOrigin(Value<object>(entry, "Origin")), Value<bool>(entry, "IsInherited"), EntryOrigin(Value<object>(entry, "ExcludedBy")),
                Value<string>(entry, "RawText"), Value<bool>(entry, "IsValueAvailable"));
        }

        private static Definition.AlarmCollectionDescriptor CollectionDescriptor(object collection)
            => new Definition.AlarmCollectionDescriptor(Value<string>(collection, "Name"), Value<string>(collection, "Kind"),
                Value<string>(collection, "Label"), Value<string>(collection, "Help"), Value<int>(collection, "MinCount"),
                Value<int?>(collection, "MaxCount"), List(collection, "Fields", Descriptor).ToArray(),
                Value<bool>(collection, "PreviewVisible"), Value<double>(collection, "PreviewWidth"),
                Value<string>(collection, "DisplayType"), Value<string>(collection, "Symbol"), Value<string>(collection, "Separator"),
                Value<string>(collection, "TemplateResource"), Value<string>(collection, "TemplateKey"));

        private static AnalysisCollection Collection(object collection) => new AnalysisCollection(Value<string>(collection, "Name"),
            Value<bool>(collection, "IsLocallyDeclared"), Location(Value<object>(collection, "LocalLocation")),
            List(collection, "LocalEntries", CollectionEntry), List(collection, "EffectiveEntries", CollectionEntry),
            List(collection, "ExcludedEntries", CollectionEntry));

        private static AnalysisCollectionEntry CollectionEntry(object entry) => new AnalysisCollectionEntry(Value<bool>(entry, "IsClear"),
            Value<bool>(entry, "IsInherited"), Value<bool>(entry, "IsValueAvailable"), CollectionOrigin(Value<object>(entry, "Origin")),
            CollectionOrigin(Value<object>(entry, "ExcludedBy")), List(entry, "Fields", Field));

        private static AnalysisCollectionEntryOrigin CollectionOrigin(object origin) => origin == null ? null
            : new AnalysisCollectionEntryOrigin(AlarmOrigin(Value<object>(origin, "Alarm")), Value<string>(origin, "Collection"),
                Value<int>(origin, "Index"), Location(Value<object>(origin, "Location")));

        private static AnalysisProperty Property(object property)
            => new AnalysisProperty(Value<string>(property, "Name"), Value<bool>(property, "IsWellKnown"), Value<bool>(property, "IsLocallyDeclared"),
                Value<string>(property, "LocalValue"), Location(Value<object>(property, "LocalLocation")), Value<string>(property, "EffectiveValue"),
                Value<bool>(property, "IsValueAvailable"), PropertyOrigin(Value<object>(property, "DeclarationOrigin")), PropertyOrigin(Value<object>(property, "ValueOrigin")));

        private static AnalysisPropertyOrigin PropertyOrigin(object origin)
            => origin == null ? null : new AnalysisPropertyOrigin(Value<string>(origin, "Scope"), AlarmOrigin(Value<object>(origin, "Alarm")),
                Location(Value<object>(origin, "Location")), LibraryIdentity(Value<object>(origin, "Library")));

        private static AnalysisEntryOrigin EntryOrigin(object origin)
        {
            return origin == null ? null : new AnalysisEntryOrigin(AlarmOrigin(Value<object>(origin, "Alarm")),
                Value<string>(origin, "Section"), Value<int>(origin, "Index"), Location(Value<object>(origin, "Location")));
        }

        private static AnalysisAlarmOrigin AlarmOrigin(object origin)
        {
            return origin == null ? null : new AnalysisAlarmOrigin(Value<int>(origin, "Index"),
                Value<string>(origin, "FullyQualifiedName"), Location(Value<object>(origin, "Location")), LibraryIdentity(Value<object>(origin, "Library")));
        }

        private static AnalysisLocation Location(object location)
        {
            return location == null ? null : new AnalysisLocation(Value<string>(location, "FilePath"), Value<int>(location, "LineNumber"),
                Value<int>(location, "ColumnNumber"), Value<int>(location, "EndLineNumber"), Value<int>(location, "EndColumnNumber"), Value<int?>(location, "SourceVersion"));
        }

        private static T Value<T>(object instance, string property) => (T)instance.GetType().GetProperty(property).GetValue(instance, null);

        private static T EnumValue<T>(object instance, string property)
        {
            string name = Value<object>(instance, property).ToString();
            if (!Enum.IsDefined(typeof(T), name)) throw new NotSupportedException("The SDK returned an unsupported " + property + ": " + name);
            return (T)Enum.Parse(typeof(T), name);
        }

        private static IReadOnlyList<T> List<T>(object instance, string property, Func<object, T> copy)
        {
            return ((IEnumerable)Value<object>(instance, property)).Cast<object>().Select(copy).ToArray();
        }
    }
}
