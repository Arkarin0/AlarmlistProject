// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Contract 3 adapter. Copies compiler answers without reimplementing resolution.</summary>
    internal sealed class AnalysisContractReader
    {
        private readonly Dictionary<Type, Type> _types = new Dictionary<Type, Type>();

        public AnalysisContractReader(Assembly assembly)
        {
            Add<AnalysisSnapshot>(assembly, "Alarmlist.Analysis.AnalysisResult");
            FieldInfo version = _types[typeof(AnalysisSnapshot)].GetField("CurrentContractVersion", BindingFlags.Public | BindingFlags.Static);
            if (version == null || !version.IsLiteral || !Equals(version.GetRawConstantValue(), 3))
                throw new NotSupportedException("Only analysis contract 3 is supported by this extension.");
            Add<AnalysisResource>(assembly, "Alarmlist.Analysis.AnalyzedResource");
            Add<AnalysisResourceEntry>(assembly, "Alarmlist.Analysis.AnalyzedResourceEntry");
            Add<AnalysisTranslation>(assembly, "Alarmlist.Analysis.AnalyzedTranslation");
            Add<AnalysisLocalizationUse>(assembly, "Alarmlist.Analysis.AnalyzedLocalizationUse");
            Add<AnalysisLookupFile>(assembly, "Alarmlist.Analysis.AnalyzedLookupFile");
            Add<AnalysisLookupPath>(assembly, "Alarmlist.Analysis.AnalyzedLookupPath");
            Add<AnalysisLookupMember>(assembly, "Alarmlist.Analysis.AnalyzedLookupMember");
            Add<AnalysisLookupMetadata>(assembly, "Alarmlist.Analysis.AnalyzedLookupMetadata");
            Add<AnalysisLookupUse>(assembly, "Alarmlist.Analysis.AnalyzedLookupUse");
            Add<AnalysisSource>(assembly, "Alarmlist.Analysis.AnalyzedSource");
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
            if (result == null || Value<int>(result, "ContractVersion") != 3)
                throw new NotSupportedException("The SDK returned an unsupported analysis contract.");
            return new AnalysisSnapshot(3, Value<bool>(result, "Success"), List(result, "Sources", source =>
                new AnalysisSource(Value<string>(source, "FilePath"), Value<int>(source, "Version"), List(source, "Properties", Property))),
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
                    new AnalysisLookupFile(Value<string>(file, "FilePath"), Value<int>(file, "Version"))), List(result, "LookupPaths", LookupPath));
        }

        private static AnalysisAlarm Alarm(object alarm)
        {
            return new AnalysisAlarm(AlarmOrigin(Value<object>(alarm, "Origin")), Value<string>(alarm, "ReferenceName"),
                Value<bool>(alarm, "IsResolved"), List(alarm, "ReferenceChain", AlarmOrigin), List(alarm, "Fields", field =>
                    new AnalysisField(Value<string>(field, "Name"), Value<bool>(field, "IsPresent"), Value<string>(field, "LocalValue"),
                        Location(Value<object>(field, "LocalLocation")), Value<string>(field, "EffectiveValue"),
                        AlarmOrigin(Value<object>(field, "DefiningAlarm")), Location(Value<object>(field, "ValueLocation")), Value<bool>(field, "IsInherited"),
                        Value<string>(field, "EffectiveExpression"), Value<bool>(field, "IsValueAvailable"))),
                Section(Value<object>(alarm, "Instructions")), Section(Value<object>(alarm, "Reset")), Value<bool>(alarm, "IsAbstract"),
                Value<bool>(alarm, "HasEvaluatedValues"), List(alarm, "Properties", Property), List(alarm, "LocalizationUses", use =>
                    new AnalysisLocalizationUse(Translation(Value<object>(use, "Translation")), Location(Value<object>(use, "UsageLocation")),
                        Value<string>(use, "Value"), Value<bool>(use, "IsValueAvailable"))), List(alarm, "LookupUses", use =>
                    new AnalysisLookupUse(Value<string>(use, "Expression"), Location(Value<object>(use, "DefinitionLocation")),
                        Location(Value<object>(use, "UsageLocation")), Value<string>(use, "Value"), Value<bool>(use, "IsValueAvailable"))));
        }

        private static AnalysisTranslation Translation(object translation) => new AnalysisTranslation(
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

        private static AnalysisSection Section(object section)
        {
            return new AnalysisSection(Value<string>(section, "Name"), List(section, "LocalEntries", Entry),
                List(section, "EffectiveEntries", Entry), List(section, "ExcludedEntries", Entry));
        }

        private static AnalysisEntry Entry(object entry)
        {
            AnalysisStepKind? kind = Value<object>(entry, "Kind") == null ? (AnalysisStepKind?)null : EnumValue<AnalysisStepKind>(entry, "Kind");
            return new AnalysisEntry(kind, Value<bool>(entry, "IsClear"), Value<string>(entry, "Text"),
                EntryOrigin(Value<object>(entry, "Origin")), Value<bool>(entry, "IsInherited"), EntryOrigin(Value<object>(entry, "ExcludedBy")),
                Value<string>(entry, "RawText"), Value<bool>(entry, "IsValueAvailable"));
        }

        private static AnalysisProperty Property(object property)
            => new AnalysisProperty(Value<string>(property, "Name"), Value<bool>(property, "IsWellKnown"), Value<bool>(property, "IsLocallyDeclared"),
                Value<string>(property, "LocalValue"), Location(Value<object>(property, "LocalLocation")), Value<string>(property, "EffectiveValue"),
                Value<bool>(property, "IsValueAvailable"), PropertyOrigin(Value<object>(property, "DeclarationOrigin")), PropertyOrigin(Value<object>(property, "ValueOrigin")));

        private static AnalysisPropertyOrigin PropertyOrigin(object origin)
            => origin == null ? null : new AnalysisPropertyOrigin(Value<string>(origin, "Scope"), AlarmOrigin(Value<object>(origin, "Alarm")), Location(Value<object>(origin, "Location")));

        private static AnalysisEntryOrigin EntryOrigin(object origin)
        {
            return origin == null ? null : new AnalysisEntryOrigin(AlarmOrigin(Value<object>(origin, "Alarm")),
                Value<string>(origin, "Section"), Value<int>(origin, "Index"), Location(Value<object>(origin, "Location")));
        }

        private static AnalysisAlarmOrigin AlarmOrigin(object origin)
        {
            return origin == null ? null : new AnalysisAlarmOrigin(Value<int>(origin, "Index"),
                Value<string>(origin, "FullyQualifiedName"), Location(Value<object>(origin, "Location")));
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
