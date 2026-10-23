// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Alarmlist.Tools.AlarmDefinitionGenerator
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if ((args.Length != 3 || (args[2] != "compiler" && args[2] != "host" && args[2] != "resources" && args[2] != "verify-resources"))
                && (args.Length != 4 || args[2] != "template"))
            {
                Console.Error.WriteLine("AlarmDefinitionGenerator <definition.xml> <output> <compiler|host|template|resources|verify-resources> [template-input]");
                return 1;
            }
            try
            {
                DefinitionModel fields = Definition.Read(args[0]);
                if (args[2] == "verify-resources") { PreviewResources.Verify(fields, args[1]); return 0; }
                if (args[2] == "resources")
                {
                    Directory.CreateDirectory(args[1]);
                    using FileStream resourceGate = AcquireLock(Path.Combine(args[1], "resources.lock"));
                    PreviewResources.Stage(fields, args[1]);
                    return 0;
                }
                string source = args[2] == "template" ? GenerateTemplate(fields, File.ReadAllText(args[3])) : Generate(fields, args[2] == "host");
                string path = Path.GetFullPath(args[1]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using FileStream gate = AcquireLock(path + ".lock");
                if (File.Exists(path) && File.ReadAllText(path) == source) return 0;
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, source, new UTF8Encoding(false));
                    File.Move(temporary, path, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return 0;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is System.Xml.XmlException || error is ArgumentException)
            {
                Console.Error.WriteLine("error ALMGEN001: " + error.Message);
                return 1;
            }
        }

        internal static string GenerateTemplate(IReadOnlyList<Field> fields, string source)
        {
            using var reader = System.Xml.XmlReader.Create(new StringReader(source), new System.Xml.XmlReaderSettings
            { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
            var document = System.Xml.Linq.XDocument.Load(reader);
            foreach (var alarm in document.Root.Elements("Alarm"))
            {
                if (fields is DefinitionModel definition && !definition.HasTestProcedure)
                    foreach (var procedure in alarm.Elements("TestProcedure").ToArray()) procedure.Remove();
                // Sample prose only makes sense for string fields. Omitted fields
                // stay absent: compiler defaults and required diagnostics apply.
                var ordinary = alarm.Elements().Where(element => element.Name.LocalName != "FullyQualifiedName"
                    && element.Name.LocalName != "ReferenceName" && element.Name.LocalName != "TestProcedure"
                    && element.Name.LocalName != "Properties").ToArray();
                foreach (var element in ordinary)
                    if (!fields.Any(field => field.Name == element.Name.LocalName && field.Type == "String")) element.Remove();
                var retained = ordinary.Where(element => element.Parent != null)
                    .OrderBy(element => fields.ToList().FindIndex(field => field.Name == element.Name.LocalName)).ToArray();
                foreach (var element in retained) element.Remove();
                var identity = alarm.Element("FullyQualifiedName");
                if (identity != null) identity.AddAfterSelf(retained);
                else alarm.AddFirst(retained);
            }
            return document.ToString() + Environment.NewLine;
        }

        private static FileStream AcquireLock(string path)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) when (attempt < 300) { Thread.Sleep(100); }
            }
        }

        internal static string Generate(IReadOnlyList<Field> fields, bool host)
        {
            DefinitionModel definition = fields as DefinitionModel;
            IReadOnlyList<CollectionDefinition> collections = definition?.Collections ?? new List<CollectionDefinition>();
            bool hasTestProcedure = definition?.HasTestProcedure ?? true;
            int version = definition?.Version ?? 1;
            string semantic = version == 1 ? JsonSerializer.Serialize(fields.Select(SemanticField))
                : JsonSerializer.Serialize(new
                {
                    Fields = fields.Select(SemanticField), HasTestProcedure = hasTestProcedure,
                    Collections = collections.Select(collection => new
                    {
                        collection.Name, collection.Kind, collection.MinCount, collection.MaxCount,
                        Fields = collection.Fields.Select(SemanticField)
                    })
                });
            string schemaHash = Hash("alarmlist-definition-" + version + "\nreserved-format-1\n" + semantic);
            string toolingHash = Hash("alarmlist-generator-5\n" + JsonSerializer.Serialize(new
            { Version = version, Fields = fields.ToArray(), Collections = collections, HasTestProcedure = hasTestProcedure,
                PreviewResources = definition?.PreviewResources ?? new List<PreviewResource>() }));
            var code = new StringBuilder("// <auto-generated />\n// Generated from AlarmDefinition.xml. Do not edit.\n\n");
            code.Append("using System;\nusing System.Collections.Generic;\n\nnamespace ")
                .Append(host ? "Alarmlist.VisualStudio.Definition" : "Alarmlist.Definition").Append("\n{\n");
            string visibility = host ? "internal" : "public";
            code.Append("    [Serializable]\n    ").Append(visibility).Append(" sealed class AlarmFieldDescriptor\n    {\n");
            foreach (string name in new[] { "Name", "Type", "Default", "Minimum", "Maximum", "Label", "Help" })
                code.Append("        public string ").Append(name).Append(" { get; }\n");
            foreach (string name in new[] { "Required", "Multiline", "PreviewVisible" })
                code.Append("        public bool ").Append(name).Append(" { get; }\n");
            code.Append("        public double PreviewWidth { get; }\n        public IReadOnlyList<string> Values { get; }\n        public IReadOnlyList<string> ValueLabels { get; }\n");
            code.Append("        internal AlarmFieldDescriptor(string name, string type, bool required, string defaultValue, string minimum, string maximum, string label, string help, bool multiline, bool previewVisible, double previewWidth, string[] values, string[] valueLabels)\n        {\n")
                .Append("            Name = name; Type = type; Required = required; Default = defaultValue; Minimum = minimum; Maximum = maximum;\n")
                .Append("            Label = label; Help = help; Multiline = multiline; PreviewVisible = previewVisible; PreviewWidth = previewWidth;\n")
                .Append("            Values = Array.AsReadOnly(values); ValueLabels = Array.AsReadOnly(valueLabels);\n        }\n    }\n");
            code.Append("    [Serializable]\n    ").Append(visibility).Append(" sealed class AlarmCollectionDescriptor\n    {\n")
                .Append("        public string Name { get; }\n        public string Kind { get; }\n        public string Label { get; }\n        public string Help { get; }\n")
                .Append("        public int MinCount { get; }\n        public int? MaxCount { get; }\n        public IReadOnlyList<AlarmFieldDescriptor> Fields { get; }\n")
                .Append("        public bool PreviewVisible { get; }\n        public double PreviewWidth { get; }\n        public string DisplayType { get; }\n        public string Symbol { get; }\n        public string Separator { get; }\n")
                .Append("        public string TemplateResource { get; }\n        public string TemplateKey { get; }\n")
                .Append("        internal AlarmCollectionDescriptor(string name, string kind, string label, string help, int minCount, int? maxCount, AlarmFieldDescriptor[] fields, bool previewVisible = true, double previewWidth = 180, string displayType = null, string symbol = \"Bullet\", string separator = \", \", string templateResource = null, string templateKey = null)\n        {\n")
                .Append("            Name = name; Kind = kind; Label = label; Help = help; MinCount = minCount; MaxCount = maxCount; Fields = Array.AsReadOnly(fields);\n")
                .Append("            PreviewVisible = previewVisible; PreviewWidth = previewWidth; DisplayType = displayType ?? (kind == \"Simple\" ? \"List\" : \"Xaml\"); Symbol = symbol; Separator = separator;\n")
                .Append("            TemplateResource = templateResource; TemplateKey = templateKey;\n        }\n    }\n");
            code.Append("    ").Append(visibility).Append(" static class AlarmDefinition\n    {\n")
                .Append("        public const string SchemaFingerprint = ").Append(Quote(schemaHash)).Append(";\n")
                .Append("        public const string ToolingFingerprint = ").Append(Quote(toolingHash)).Append(";\n")
                .Append("        public static bool HasTestProcedure { get; } = ").Append(Bool(hasTestProcedure)).Append(";\n")
                .Append("        public static IReadOnlyList<AlarmFieldDescriptor> Fields { get; } = Array.AsReadOnly(new AlarmFieldDescriptor[]\n        {\n");
            foreach (Field field in fields)
                code.Append("            ").Append(FieldDescriptor(field)).Append(",\n");
            code.Append("        });\n        public static IReadOnlyList<AlarmCollectionDescriptor> Collections { get; } = Array.AsReadOnly(new AlarmCollectionDescriptor[]\n        {\n");
            foreach (CollectionDefinition collection in collections)
            {
                code.Append("            new AlarmCollectionDescriptor(").Append(Quote(collection.Name)).Append(", ")
                    .Append(Quote(collection.Kind)).Append(", ").Append(Quote(collection.Label)).Append(", ").Append(Quote(collection.Help)).Append(", ")
                    .Append(collection.MinCount.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(", ")
                    .Append(collection.MaxCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")
                    .Append(", new AlarmFieldDescriptor[] { ").Append(string.Join(", ", collection.Fields.Select(FieldDescriptor))).Append(" }, ")
                    .Append(Bool(collection.PreviewVisible)).Append(", ").Append(collection.PreviewWidth.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append(", ").Append(Quote(collection.DisplayType)).Append(", ").Append(Quote(collection.Symbol)).Append(", ").Append(Quote(collection.Separator))
                    .Append(", ").Append(Quote(collection.TemplateFile == null ? null : "CollectionTemplates/" + collection.TemplateFile))
                    .Append(", ").Append(Quote(collection.TemplateKey)).Append("),\n");
            }
            code.Append("        });\n    }\n}\n");
            if (!host) GenerateModels(code, fields);
            return code.ToString();
        }

        private static object SemanticField(Field field) => new
        {
            field.Name, field.Type, field.Required, field.Default, field.Minimum, field.Maximum, field.Values
        };

        private static string FieldDescriptor(Field field) => "new AlarmFieldDescriptor(" + string.Join(", ", new[]
        {
            Quote(field.Name), Quote(field.Type), Bool(field.Required), Quote(field.Default), Quote(field.Minimum), Quote(field.Maximum),
            Quote(field.Label), Quote(field.Help), Bool(field.Multiline), Bool(field.PreviewVisible), field.PreviewWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Strings(field.Values), Strings(field.ValueLabels)
        }) + ")";

        private static void GenerateModels(StringBuilder code, IReadOnlyList<Field> fields)
        {
            code.Append("\nnamespace Alarmlist.Syntax\n{\n    public partial class AlarmSyntaxNode\n    {\n");
            foreach (Field field in fields)
                code.Append("        public string ").Append(field.Name).Append(" { get => GetEffectiveFieldValue(").Append(Quote(field.Name))
                    .Append("); set => SetLocalFieldValue(").Append(Quote(field.Name)).Append(", value); }\n");
            code.Append("    }\n}\n\nnamespace Alarmlist.Compiler\n{\n");
            foreach (Field field in fields.Where(field => field.Type == "Enum"))
                code.Append("    public enum ").Append(field.Name).Append("Value { ").Append(string.Join(", ", field.Values)).Append(" }\n");
            code.Append("    public readonly partial struct Alarm\n    {\n");
            code.Append("        public string FullyQualifiedName { get; }\n        public TestProcedure TestProcedure { get; }\n");
            code.Append("        public IReadOnlyList<Alarmlist.Definition.AlarmFieldValue> Fields { get; }\n");
            code.Append("        public IReadOnlyList<Alarmlist.Definition.AlarmCollectionValue> Collections { get; }\n");
            foreach (Field field in fields)
                code.Append("        public ").Append(TypeName(field)).Append(' ').Append(field.Name).Append(" { get; }\n");
            // Preserve the established constructors for the standard schema. Fork
            // APIs follow their definitions; only this baseline overload order is legacy.
            bool baseline = fields.Count == 4 && fields.All(field => field.Type == "String")
                && new[] { "Category", "Code", "Description", "Name" }.All(name => fields.Any(field => field.Name == name));
            Field[] parameters = baseline ? new[] { "Category", "Code", "Description", "Name" }.Select(name => fields.Single(field => field.Name == name)).ToArray() : fields.ToArray();
            string declarations = string.Join(", ", parameters.Select(field => TypeName(field) + " @" + field.Name));
            string arguments = string.Join(", ", parameters.Select(field => "@" + field.Name));
            if (baseline)
            {
                // Lowercase parameter names are retained for callers using named arguments.
                declarations = string.Join(", ", parameters.Select(field => "string " + field.Name.ToLowerInvariant()));
                arguments = string.Join(", ", parameters.Select(field => field.Name.ToLowerInvariant()));
                code.Append("        public Alarm(").Append(declarations).Append(") : this(null, ").Append(arguments).Append(", null) { }\n")
                    .Append("        public Alarm(").Append(declarations).Append(", TestProcedure testProcedure) : this(null, ").Append(arguments).Append(", testProcedure) { }\n");
            }
            code.Append("        public Alarm(string fullyQualifiedName").Append(declarations.Length == 0 ? "" : ", " + declarations)
                .Append(", TestProcedure testProcedure = null) : this(fullyQualifiedName, CreateFieldValues(new object[] { ")
                .Append(string.Join(", ", fields.Select(field => "(object)" + (baseline ? field.Name.ToLowerInvariant() : "@" + field.Name)))).Append(" }), testProcedure) { }\n");
            code.Append("        internal Alarm(string identity, IReadOnlyList<Alarmlist.Definition.AlarmFieldValue> fields, TestProcedure procedure, IReadOnlyList<Alarmlist.Definition.AlarmCollectionValue> collections = null)\n        {\n            FullyQualifiedName = identity; TestProcedure = procedure ?? new TestProcedure(); Fields = fields; Collections = collections ?? global::System.Array.Empty<Alarmlist.Definition.AlarmCollectionValue>();\n");
            for (int index = 0; index < fields.Count; index++)
            {
                Field field = fields[index];
                string value = "fields[" + index + "].Value";
                string converted = field.Type == "Enum" ? value + " == null ? (" + TypeName(field) + ")null : (" + field.Name + "Value)global::System.Enum.Parse(typeof(" + field.Name + "Value), (string)" + value + ")" : "(" + TypeName(field) + ")" + value;
                code.Append("            this.").Append(field.Name).Append(" = ").Append(converted).Append(";\n");
            }
            code.Append("        }\n    }\n}\n");
        }

        private static string TypeName(Field field)
        {
            switch (field.Type)
            {
                case "String": return "string";
                case "Int64": return "long?";
                case "Decimal": return "decimal?";
                case "Double": return "double?";
                case "Boolean": return "bool?";
                case "Enum": return field.Name + "Value?";
                case "Date": return "DateTime?";
                case "Time": return "TimeSpan?";
                case "Timestamp": return "DateTimeOffset?";
                default: throw new ArgumentException("Unknown scalar type.");
            }
        }
        private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        private static string Quote(string value) => value == null ? "null" : JsonSerializer.Serialize(value);
        private static string Bool(bool value) => value ? "true" : "false";
        private static string Strings(IEnumerable<string> values) => "new string[] { " + string.Join(", ", values.Select(Quote)) + " }";
    }
}
