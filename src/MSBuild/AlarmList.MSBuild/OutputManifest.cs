// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Localization;

namespace Alarmlist.MSBuild
{
    internal sealed class OutputManifest
    {
        internal IReadOnlyList<string> Files { get; private set; } = Array.Empty<string>();
        internal IReadOnlyList<string> TemporaryFiles { get; private set; } = Array.Empty<string>();

        internal static OutputManifest Read(string path, string project, string configuration, string platform)
        {
            var result = new OutputManifest();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;
            using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                XElement root = XDocument.Load(reader).Root;
                string version = (string)root?.Attribute("version");
                string outputType = (string)root?.Attribute("outputType");
                if (root?.Name != "AlarmlistOutputs" || (version != "1" && version != "2")
                    || (version == "2" && outputType != "Library" && outputType != "Application")
                    || !BuildOutputPlan.PathComparer.Equals((string)root.Attribute("project"), ProjectIdentity(project))
                    || (string)root.Attribute("configuration") != (configuration ?? string.Empty)
                    || (string)root.Attribute("platform") != (platform ?? string.Empty)
                    || root.Elements().Any(element => element.Name != "File" && element.Name != "Temporary"))
                    throw new InvalidDataException("The output ownership manifest does not match this project and configuration: " + path);
                bool library = version == "2" && outputType == "Library";
                string[] files = root.Elements("File").Select(element => ValidateOutput((string)element.Attribute("path"), library)).ToArray();
                string[] temporary = root.Elements("Temporary").Select(element => (string)element.Attribute("path")).ToArray();
                foreach (string candidate in temporary)
                {
                    Match suffix = Regex.Match(candidate ?? string.Empty, @"\.alarmlist-[0-9a-f]{32}\.(stage|backup)$");
                    if (!suffix.Success)
                        throw new InvalidDataException("Invalid temporary file in output ownership manifest: " + path);
                    string owner = candidate.Substring(0, suffix.Index);
                    if (!BuildOutputPlan.PathComparer.Equals(owner, Path.GetFullPath(path))) ValidateOutput(owner, version == "2");
                }
                result.Files = Array.AsReadOnly(files.Distinct(BuildOutputPlan.PathComparer).ToArray());
                result.TemporaryFiles = Array.AsReadOnly(temporary.Distinct(BuildOutputPlan.PathComparer).ToArray());
            }
            return result;
        }

        internal static string Write(string project, string configuration, string platform, IEnumerable<string> files, IEnumerable<string> temporary,
            string outputType = null)
        {
            bool library = BuildOutputPlan.IsLibrary(outputType);
            return new XDocument(new XElement("AlarmlistOutputs", new XAttribute("version", "2"),
                new XAttribute("outputType", library ? "Library" : "Application"),
                new XAttribute("project", ProjectIdentity(project)), new XAttribute("configuration", configuration ?? string.Empty),
                new XAttribute("platform", platform ?? string.Empty),
                files.Distinct(BuildOutputPlan.PathComparer).Select(path => new XElement("File", new XAttribute("path", ValidateOutput(path, library)))),
                temporary.Distinct(BuildOutputPlan.PathComparer).Select(path => new XElement("Temporary", new XAttribute("path", path))))).ToString();
        }

        private static string ProjectIdentity(string project)
        {
            if (string.IsNullOrWhiteSpace(project)) throw new ArgumentException("ProjectPath is required when tracking owned outputs.");
            return Path.GetFullPath(project);
        }

        private static string ValidateOutput(string path, bool library = false)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !BuildOutputPlan.PathComparer.Equals(path, Path.GetFullPath(path)))
                throw new InvalidDataException("An owned output must have an absolute canonical path.");
            BuildOutputPlan.ValidateFileName(Path.GetFileName(path));
            if (library) return path;
            string culture = Path.GetFileName(Path.GetDirectoryName(path));
            if (string.IsNullOrEmpty(culture) || !LocalizationCulture.TryGetSelection(culture, out string normalized) || culture != normalized)
                throw new InvalidDataException("An owned output must be in a normalized culture directory.");
            return path;
        }
    }
}
