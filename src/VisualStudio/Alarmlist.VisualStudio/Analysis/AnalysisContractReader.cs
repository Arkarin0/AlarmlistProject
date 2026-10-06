// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Contract 2 adapter. Copies compiler answers without reimplementing resolution.</summary>
    internal sealed class AnalysisContractReader
    {
        private readonly Dictionary<Type, Type> _types = new Dictionary<Type, Type>();

        public AnalysisContractReader(Assembly assembly)
        {
            Add<AnalysisSnapshot>(assembly, "Alarmlist.Analysis.AnalysisResult");
            FieldInfo version = _types[typeof(AnalysisSnapshot)].GetField("CurrentContractVersion", BindingFlags.Public | BindingFlags.Static);
            if (version == null || !version.IsLiteral || !Equals(version.GetRawConstantValue(), 2))
                throw new NotSupportedException("Only analysis contract 2 is supported by this extension.");
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
            if (result == null || Value<int>(result, "ContractVersion") != 2)
                throw new NotSupportedException("The SDK returned an unsupported analysis contract.");
            return new AnalysisSnapshot(2, Value<bool>(result, "Success"), List(result, "Sources", source =>
                new AnalysisSource(Value<string>(source, "FilePath"), Value<int>(source, "Version"), List(source, "Properties", Property))),
                List(result, "Alarms", Alarm), List(result, "Diagnostics", diagnostic => new AnalysisDiagnostic(
                    Value<string>(diagnostic, "Id"), EnumValue<AnalysisSeverity>(diagnostic, "Severity"), Value<string>(diagnostic, "Message"),
                    Location(Value<object>(diagnostic, "Location")), List(diagnostic, "RelatedLocations", Location))));
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
                Value<bool>(alarm, "HasEvaluatedValues"), List(alarm, "Properties", Property));
        }

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
