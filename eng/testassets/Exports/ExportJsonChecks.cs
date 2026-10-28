using System;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Output;

internal static class ExportJsonChecks
{
    internal static XElement Read(CompilationResult result, string culture = "keyword")
    {
        byte[] bytes = new JsonAlarmlistExportProvider().Export(new ExportRequest(ExportDocument.Create(result, culture), "alarms.json"), CancellationToken.None).Single().GetContent();
#if NET8_0_OR_GREATER
        using (var document = System.Text.Json.JsonDocument.Parse(bytes))
            if (document.RootElement.GetProperty("formatVersion").GetInt32() != 1) throw new Exception("Invalid JSON format version.");
#endif
        using (var hash = SHA256.Create()) Console.WriteLine("JSON_SHA256:" + Convert.ToBase64String(hash.ComputeHash(bytes)));
        using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max)) return XDocument.Load(reader).Root;
    }

    internal static XElement Alarm(XElement root) => root.Element("alarms").Elements("item").Single();
    internal static XElement Field(XElement container, string name) => container.Element("fields").Elements("item").Single(field => field.Element("name").Value == name);
}
