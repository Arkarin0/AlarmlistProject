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
        private readonly Dictionary<string, TemporaryOwnerScope> _ownerScopes = new Dictionary<string, TemporaryOwnerScope>(BuildOutputPlan.PathComparer);
        internal IReadOnlyList<string> Files { get; private set; } = Array.Empty<string>();
        internal string OutputRoot { get; private set; }
        internal IReadOnlyList<ExportOutput> Exports { get; private set; } = Array.Empty<ExportOutput>();
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
                if (root?.Name != "AlarmlistOutputs" || (version != "1" && version != "2" && version != "3")
                    || (version == "2" && outputType != "Library" && outputType != "Application")
                    || (version == "3" && outputType != "Exports")
                    || !BuildOutputPlan.PathComparer.Equals((string)root.Attribute("project"), ProjectIdentity(project))
                    || (string)root.Attribute("configuration") != (configuration ?? string.Empty)
                    || (string)root.Attribute("platform") != (platform ?? string.Empty)
                    || root.Elements().Any(element => element.Name != "File" && element.Name != "Temporary"))
                    throw new InvalidDataException("The output ownership manifest does not match this project and configuration: " + path);
                bool library = version == "2" && outputType == "Library";
                string[] files;
                if (version == "3")
                {
                    result.OutputRoot = (string)root.Attribute("outputRoot");
                    ExportOutputPaths.ValidateRoot(result.OutputRoot);
                    var exports = root.Elements("File").Select(element => new ExportOutput(
                        ExportOutputPaths.ValidateOwned(result.OutputRoot, (string)element.Attribute("path"), (string)element.Attribute("culture")),
                        (string)element.Attribute("culture"), (string)element.Attribute("provider"))).ToArray();
                    foreach (var export in exports)
                    {
                        if (string.IsNullOrWhiteSpace(export.Culture)) throw new InvalidDataException("Export manifest culture is required.");
                        Output.ExportProviderReference.ValidateId(export.ProviderId);
                    }
                    if (exports.Length == 0) throw new InvalidDataException("An export manifest must own at least one artifact.");
                    files = exports.Select(export => export.Path).ToArray();
                    if (files.Distinct(BuildOutputPlan.PathComparer).Count() != files.Length)
                        throw new InvalidDataException("Export manifest paths must be unique.");
                    result.Exports = Array.AsReadOnly(exports);
                }
                else files = root.Elements("File").Select(element => ValidateOutput((string)element.Attribute("path"), library)).ToArray();
                foreach (string file in files)
                    result._ownerScopes[file] = version == "3" ? new TemporaryOwnerScope(result.OutputRoot, true)
                        : new TemporaryOwnerScope(library ? Path.GetDirectoryName(file) : Path.GetDirectoryName(Path.GetDirectoryName(file)), !library);
                var temporaryElements = root.Elements("Temporary").ToArray();
                string[] temporary = temporaryElements.Select(element => (string)element.Attribute("path")).ToArray();
                foreach (var temporaryElement in temporaryElements)
                {
                    string candidate = (string)temporaryElement.Attribute("path");
                    Match suffix = Regex.Match(candidate ?? string.Empty, @"\.alarmlist-[0-9a-f]{32}\.(stage|backup)$");
                    if (!suffix.Success)
                        throw new InvalidDataException("Invalid temporary file in output ownership manifest: " + path);
                    string owner = candidate.Substring(0, suffix.Index);
                    if (!BuildOutputPlan.PathComparer.Equals(owner, Path.GetFullPath(path)))
                    {
                        TemporaryOwnerScope scope;
                        if (version == "3")
                        {
                            string scopeRoot = (string)temporaryElement.Attribute("ownerRoot");
                            string scopeKind = (string)temporaryElement.Attribute("ownerScope");
                            if (scopeRoot == null && scopeKind == null) scope = new TemporaryOwnerScope(result.OutputRoot, true);
                            else if (scopeRoot != null && (scopeKind == "File" || scopeKind == "Exports"))
                                scope = new TemporaryOwnerScope(scopeRoot, scopeKind == "Exports");
                            else throw new InvalidDataException("Invalid retained temporary ownership scope.");
                            scope.Validate(owner);
                        }
                        else
                        {
                            ValidateOutput(owner, version == "2");
                            scope = new TemporaryOwnerScope(version == "2" ? Path.GetDirectoryName(owner) : Path.GetDirectoryName(Path.GetDirectoryName(owner)), version != "2");
                        }
                        result._ownerScopes[owner] = scope;
                    }
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

        internal static string WriteExports(string project, string configuration, string platform, string outputRoot,
            IEnumerable<ExportOutput> exports, IEnumerable<string> temporary, string manifestFile = null, OutputManifest previous = null)
        {
            string root = Path.GetFullPath(outputRoot);
            var captured = exports.ToArray();
            if (captured.Length == 0) throw new InvalidDataException("An export manifest must own at least one artifact.");
            foreach (var export in captured)
            {
                ExportOutputPaths.ValidateOwned(root, export.Path, export.Culture);
                Output.ExportProviderReference.ValidateId(export.ProviderId);
            }
            if (captured.Select(export => export.Path).Distinct(BuildOutputPlan.PathComparer).Count() != captured.Length)
                throw new InvalidDataException("Export manifest paths must be unique.");
            string[] temporaryFiles = temporary.Distinct(BuildOutputPlan.PathComparer).ToArray();
            var temporaryElements = new List<XElement>();
            foreach (string path in temporaryFiles)
            {
                var element = new XElement("Temporary", new XAttribute("path", path));
                Match suffix = Regex.Match(path ?? string.Empty, @"\.alarmlist-[0-9a-f]{32}\.(stage|backup)$");
                if (!suffix.Success) throw new InvalidDataException("Invalid export temporary file path.");
                string owner = path.Substring(0, suffix.Index);
                if (string.IsNullOrEmpty(manifestFile) || !BuildOutputPlan.PathComparer.Equals(owner, Path.GetFullPath(manifestFile)))
                {
                    try { ExportOutputPaths.ValidateOwned(root, owner); }
                    catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException)
                    {
                        if (previous == null || !previous._ownerScopes.TryGetValue(owner, out TemporaryOwnerScope scope))
                            throw new InvalidDataException("Temporary path has no captured previous output ownership.", exception);
                        scope.Validate(owner);
                        element.Add(new XAttribute("ownerRoot", scope.Root), new XAttribute("ownerScope", scope.CultureScoped ? "Exports" : "File"));
                    }
                }
                temporaryElements.Add(element);
            }
            return new XDocument(new XElement("AlarmlistOutputs", new XAttribute("version", "3"),
                new XAttribute("outputType", "Exports"), new XAttribute("outputRoot", root),
                new XAttribute("project", ProjectIdentity(project)), new XAttribute("configuration", configuration ?? string.Empty),
                new XAttribute("platform", platform ?? string.Empty),
                captured.Select(export => new XElement("File", new XAttribute("path", export.Path),
                    new XAttribute("culture", export.Culture), new XAttribute("provider", export.ProviderId))),
                temporaryElements)).ToString();
        }

        private sealed class TemporaryOwnerScope
        {
            internal TemporaryOwnerScope(string root, bool cultureScoped) { Root = root; CultureScoped = cultureScoped; }
            internal string Root { get; }
            internal bool CultureScoped { get; }
            internal void Validate(string owner)
            {
                if (CultureScoped) ExportOutputPaths.ValidateOwned(Root, owner);
                else
                {
                    ExportOutputPaths.ValidateRoot(Root);
                    ValidateOutput(owner, true);
                    if (!BuildOutputPlan.PathComparer.Equals(Path.GetDirectoryName(owner), Root))
                        throw new InvalidDataException("Retained temporary owner escapes its recorded directory.");
                }
            }
        }

        internal sealed class ExportOutput
        {
            internal ExportOutput(string path, string culture, string providerId)
            { Path = path; Culture = culture; ProviderId = providerId; }
            internal string Path { get; }
            internal string Culture { get; }
            internal string ProviderId { get; }
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
