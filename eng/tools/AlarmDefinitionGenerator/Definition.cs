// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Alarmlist.Tools.AlarmDefinitionGenerator
{
    internal sealed class Field
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Required { get; set; }
        public string Default { get; set; }
        public string Minimum { get; set; }
        public string Maximum { get; set; }
        public string Label { get; set; }
        public string Help { get; set; }
        public bool Multiline { get; set; }
        public bool PreviewVisible { get; set; }
        public decimal PreviewWidth { get; set; }
        public string[] Values { get; set; }
        public string[] ValueLabels { get; set; }
    }

    internal static class Definition
    {
        private static readonly HashSet<string> s_reserved = new HashSet<string>((
            "FullyQualifiedName ReferenceName IsAbstract Abstract TestProcedure Group Title Step Hint Warning Note Clear Properties Property Parameter Parameters " +
            "Location SourceFile Reference ResolvedTestProcedures GetFieldLocation SetFieldLocation GetLocalFieldValue " +
            "SetLocalFieldValue GetEffectiveFieldValue GetFieldSource SetReference RemoveReference ClearResolvedReference " +
            "InvalidAbstractValue Fields GetField TypedFields GetTypedField Definition SchemaFingerprint ToolingFingerprint " +
            "Equals GetHashCode GetType ToString Deconstruct Alarm AlarmSyntaxNode CreateFieldValues").Split(' '), StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> s_keywords = new HashSet<string>((
            "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double " +
            "else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal " +
            "is lock long namespace new null object operator out override params private protected public readonly ref return " +
            "sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked " +
            "unsafe ushort using virtual void volatile while __arglist __makeref __reftype __refvalue").Split(' '), StringComparer.Ordinal);

        internal static IReadOnlyList<Field> Read(string path)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(path, settings);
            XDocument document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            var schemas = new XmlSchemaSet { XmlResolver = null };
            using Stream schema = typeof(Definition).Assembly.GetManifestResourceStream("AlarmDefinition.xsd");
            using var schemaReader = XmlReader.Create(schema, settings);
            schemas.Add(null, schemaReader);
            document.Validate(schemas, (sender, args) => throw new InvalidDataException(
                path + "(" + args.Exception.LineNumber + "," + args.Exception.LinePosition + "): " + args.Message));
            var fields = new List<Field>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement element in document.Root.Elements("Field"))
            {
                string name = (string)element.Attribute("Name");
                void Fail(string message) => throw new InvalidDataException(path + "(" + ((IXmlLineInfo)element).LineNumber + ",1): " + message);
                if (!Identifier(name) || s_reserved.Contains(name) || name.StartsWith("Alarmlist", StringComparison.OrdinalIgnoreCase)
                    || !names.Add(name)) Fail("Invalid, reserved or duplicate field name: " + name);
                var field = new Field
                {
                    Name = name, Type = (string)element.Attribute("Type"),
                    Required = Boolean(element, "Required", false), Default = (string)element.Attribute("Default"),
                    Minimum = (string)element.Attribute("Minimum"), Maximum = (string)element.Attribute("Maximum"),
                    Label = (string)element.Attribute("Label") ?? name, Help = (string)element.Attribute("Help") ?? "",
                    Multiline = Boolean(element, "Multiline", false), PreviewVisible = Boolean(element, "PreviewVisible", true),
                    PreviewWidth = (decimal?)element.Attribute("PreviewWidth") ?? 180,
                    Values = element.Elements("Value").Select(value => (string)value.Attribute("Name")).ToArray(),
                    ValueLabels = element.Elements("Value").Select(value => (string)value.Attribute("Label") ?? (string)value.Attribute("Name")).ToArray()
                };
                if (field.PreviewWidth < 40 || field.PreviewWidth > 2000) Fail("PreviewWidth must be between 40 and 2000.");
                if (field.Multiline && field.Type != "String") Fail("Only String fields may be multiline.");
                if (field.Type == "Enum" ? field.Values.Length == 0 : field.Values.Length != 0) Fail("Only Enum fields require and accept Value declarations.");
                if (field.Values.Any(value => !Identifier(value)) || field.Values.Distinct(StringComparer.Ordinal).Count() != field.Values.Length)
                    Fail("Enum tokens must be unique identifiers.");
                if (field.Type == "Enum" && (field.Values.Contains("value__") || field.Values.Contains(field.Name + "Value"))) Fail("The enum token collides with a generated member.");
                bool numeric = field.Type == "Int64" || field.Type == "Decimal" || field.Type == "Double";
                if (!numeric && (field.Minimum != null || field.Maximum != null)) Fail("Bounds are supported only for numeric fields.");
                foreach (string bound in new[] { field.Minimum, field.Maximum })
                    if (bound != null && !ValidLiteral(field, bound)) Fail("Invalid numeric bound: " + bound);
                if (field.Minimum != null && field.Maximum != null && Compare(field, field.Minimum, field.Maximum) > 0) Fail("Minimum exceeds Maximum.");
                if (field.Default != null)
                {
                    if (!ValidLiteral(field, field.Default) || (field.Required && field.Default.Length == 0)) Fail("Invalid literal default.");
                    if (field.Minimum != null && Compare(field, field.Default, field.Minimum) < 0
                        || field.Maximum != null && Compare(field, field.Default, field.Maximum) > 0) Fail("Default is outside numeric bounds.");
                }
                fields.Add(field);
            }
            // Generated enum type names share a namespace with resolved model types.
            foreach (Field field in fields.Where(field => field.Type == "Enum"))
                if (names.Contains(field.Name + "Value")) throw new InvalidDataException(path + ": Field collides with generated enum type " + field.Name + "Value.");
            return fields;
        }

        private static bool Boolean(XElement element, string name, bool fallback)
            => element.Attribute(name) == null ? fallback : XmlConvert.ToBoolean((string)element.Attribute(name));
        private static bool Identifier(string name) => name != null && Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant) && !s_keywords.Contains(name);
        private static int Compare(Field field, string left, string right) => field.Type == "Double"
            ? double.Parse(left, CultureInfo.InvariantCulture).CompareTo(double.Parse(right, CultureInfo.InvariantCulture))
            : decimal.Parse(left, CultureInfo.InvariantCulture).CompareTo(decimal.Parse(right, CultureInfo.InvariantCulture));
        internal static bool ValidLiteral(Field field, string raw)
            => Alarmlist.Definition.ScalarCodec.TryParse(field.Type, field.Values, raw, out _, out _);
    }
}
