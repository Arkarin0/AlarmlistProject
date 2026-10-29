// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Xml;

namespace Alarmlist.Output
{
    /// <summary>Versioned JSON over detached compiler-resolved data, including available keyword-mode results.</summary>
    public sealed class JsonAlarmlistExportProvider : IAlarmlistExportProvider
    {
        public const int FormatVersion = 1;
        public string Id => "JSON";
        public int ContractVersion => ExportDocument.ContractVersion;
        public string RequiredDefinitionFingerprint => null;

        public IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.ValidateCompatibility(this);
            if (request.Options.Count != 0) throw new ArgumentException("JSON format version 1 does not support provider options.", nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false, true), false, true, "  "))
                {
                    Start(writer, "root", "object");
                    Primitive(writer, "formatVersion", "number", FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Text(writer, "culture", request.Document.Culture);
                    Text(writer, "definitionFingerprint", request.Document.DefinitionFingerprint);
                    Boolean(writer, "hasTestProcedure", request.Document.HasTestProcedure);
                    WriteDefinitions(writer, "fieldDefinitions", request.Document.Fields, cancellationToken);
                    Start(writer, "collectionDefinitions", "array");
                    foreach (ExportCollectionMetadata collection in request.Document.Collections)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Start(writer, "item", "object");
                        Text(writer, "name", collection.Name);
                        Text(writer, "kind", collection.Kind);
                        Text(writer, "label", collection.Label);
                        WriteDefinitions(writer, "fields", collection.Fields, cancellationToken);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    Start(writer, "alarms", "array");
                    foreach (ExportAlarm alarm in request.Document.Alarms)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Start(writer, "item", "object");
                        Text(writer, "fullyQualifiedName", alarm.FullyQualifiedName);
                        WriteFields(writer, alarm.Fields, cancellationToken);
                        Start(writer, "collections", "array");
                        foreach (ExportCollection collection in alarm.Collections)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Start(writer, "item", "object");
                            Text(writer, "name", collection.Name);
                            Start(writer, "items", "array");
                            foreach (var item in collection.Items)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                Start(writer, "item", "object");
                                WriteFields(writer, item, cancellationToken);
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        Start(writer, "procedureGroups", "array");
                        foreach (ExportProcedureGroup group in alarm.ProcedureGroups)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Start(writer, "item", "object");
                            Text(writer, "name", group.Name);
                            Text(writer, "title", group.Title);
                            Start(writer, "entries", "array");
                            foreach (ExportProcedureEntry entry in group.Entries)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                Start(writer, "item", "object");
                                Text(writer, "kind", entry.Kind);
                                Text(writer, "text", entry.Text);
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                cancellationToken.ThrowIfCancellationRequested();
                return Array.AsReadOnly(new[] { new ExportArtifact(request.FileName, stream.ToArray()) });
            }
        }

        private static void WriteDefinitions(XmlWriter writer, string name, IEnumerable<ExportFieldMetadata> fields, CancellationToken token)
        {
            Start(writer, name, "array");
            foreach (ExportFieldMetadata field in fields)
            {
                token.ThrowIfCancellationRequested();
                Start(writer, "item", "object");
                Text(writer, "name", field.Name);
                Text(writer, "type", field.Type);
                Text(writer, "label", field.Label);
                Boolean(writer, "required", field.Required);
                Start(writer, "values", "array");
                foreach (string value in field.Values)
                {
                    token.ThrowIfCancellationRequested();
                    Text(writer, "item", value);
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteFields(XmlWriter writer, IEnumerable<ExportScalar> fields, CancellationToken token)
        {
            Start(writer, "fields", "array");
            foreach (ExportScalar field in fields)
            {
                token.ThrowIfCancellationRequested();
                Start(writer, "item", "object");
                Text(writer, "name", field.Name);
                Text(writer, "state", field.IsAbsent ? "absent" : field.IsDeferred ? "deferred" : "valid");
                Boolean(writer, "isDefault", field.IsDefault);
                Text(writer, "canonicalText", field.CanonicalText);
                if (field.Value == null) Primitive(writer, "value", "null", null);
                else if (field.Value is bool boolean) Boolean(writer, "value", boolean);
                else if (field.Value is long || field.Value is decimal || field.Value is double)
                    Primitive(writer, "value", "number", field.CanonicalText);
                else Text(writer, "value", field.CanonicalText);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void Start(XmlWriter writer, string name, string type)
        {
            writer.WriteStartElement(name);
            writer.WriteAttributeString("type", type);
        }
        private static void Primitive(XmlWriter writer, string name, string type, string value)
        {
            Start(writer, name, type);
            if (value != null) writer.WriteString(value);
            writer.WriteEndElement();
        }
        private static void Text(XmlWriter writer, string name, string value) => Primitive(writer, name, value == null ? "null" : "string", value);
        private static void Boolean(XmlWriter writer, string name, bool value) => Primitive(writer, name, "boolean", value ? "true" : "false");
    }
}
