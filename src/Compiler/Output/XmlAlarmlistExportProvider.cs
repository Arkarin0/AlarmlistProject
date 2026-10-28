// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace Alarmlist.Output
{
    public sealed class XmlAlarmlistExportProvider : IAlarmlistExportProvider
    {
        public string Id => "XML";
        public int ContractVersion => ExportDocument.ContractVersion;
        public string RequiredDefinitionFingerprint => null;

        public IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.ValidateCompatibility(this);
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new MemoryStream())
            {
                // Match the legacy XmlWriter file encoding, including its UTF-8 preamble.
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("AlarmList");
                    foreach (ExportAlarm alarm in request.Document.Alarms)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.WriteStartElement("Alarm");
                        writer.WriteElementString("FullyQualifiedName", alarm.FullyQualifiedName);
                        foreach (ExportFieldMetadata field in request.Document.Fields)
                        {
                            ExportScalar value = alarm.Fields.FirstOrDefault(item => item.Name == field.Name);
                            if (field.Type == "String" || value?.CanonicalText != null)
                                writer.WriteElementString(field.Name, value?.CanonicalText);
                        }
                        foreach (ExportCollectionMetadata metadata in request.Document.Collections)
                        {
                            writer.WriteStartElement(metadata.Name);
                            ExportCollection collection = alarm.Collections.First(item => item.Name == metadata.Name);
                            foreach (IReadOnlyList<ExportScalar> item in collection.Items)
                            {
                                writer.WriteStartElement("Item");
                                if (metadata.Kind == "Simple") writer.WriteString(item.FirstOrDefault(field => field.Name == "Item")?.CanonicalText);
                                else foreach (ExportScalar field in item)
                                    if (field.CanonicalText != null) writer.WriteElementString(field.Name, field.CanonicalText);
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                        }
                        if (request.Document.HasTestProcedure)
                        {
                            writer.WriteStartElement("TestProcedure");
                            foreach (ExportProcedureGroup group in alarm.ProcedureGroups)
                            {
                                writer.WriteStartElement("Group");
                                writer.WriteAttributeString("Name", group.Name);
                                writer.WriteElementString("Title", group.Title);
                                foreach (ExportProcedureEntry entry in group.Entries)
                                {
                                    writer.WriteStartElement("TestProcedureItem");
                                    writer.WriteAttributeString("Kind", entry.Kind);
                                    writer.WriteString(entry.Text);
                                    writer.WriteEndElement();
                                }
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
                return Array.AsReadOnly(new[] { new ExportArtifact(request.FileName, stream.ToArray()) });
            }
        }
    }
}
