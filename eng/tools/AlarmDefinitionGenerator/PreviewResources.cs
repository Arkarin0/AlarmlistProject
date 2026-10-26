// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Alarmlist.Tools.AlarmDefinitionGenerator
{
    internal static class PreviewResources
    {
        private static readonly XNamespace s_wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace s_xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        internal static void Capture(DefinitionModel model, string definitionPath)
        {
            string root = Path.GetDirectoryName(Path.GetFullPath(definitionPath));
            var resources = new Dictionary<string, PreviewResource>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in model.Collections.Where(item => item.TemplateFile != null))
            {
                string relative = Normalize(collection.TemplateFile);
                if (!relative.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("TemplateFile must name a .xaml dictionary: " + relative);
                collection.TemplateFile = relative;
                CaptureFile(relative);
                XDocument dictionary = Read(resources[relative].Content);
                if (dictionary.Root?.Name != s_wpf + "ResourceDictionary")
                    throw new InvalidDataException(relative + ": TemplateFile must contain a WPF ResourceDictionary.");
                XElement[] matches = dictionary.Root.Elements().Where(element => (string)element.Attribute(s_xaml + "Key") == collection.TemplateKey).ToArray();
                if (matches.Length != 1 || matches[0].Name != s_wpf + "DataTemplate")
                    throw new InvalidDataException(relative + ": TemplateKey must select exactly one directly declared WPF DataTemplate: " + collection.TemplateKey);
            }
            model.PreviewResources.AddRange(resources.Values.OrderBy(item => item.Path, StringComparer.Ordinal));

            void CaptureFile(string relative)
            {
                if (resources.ContainsKey(relative)) return;
                string file = Path.GetFullPath(Path.Combine(root, relative));
                if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Preview resource escaped the definition directory: " + relative);
                for (string current = file; current != root; current = Path.GetDirectoryName(current))
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Preview resources must not use links or junctions: " + relative);
                byte[] bytes = File.ReadAllBytes(file);
                resources.Add(relative, new PreviewResource { Path = relative, Content = bytes, Hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() });
                if (!relative.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) return;
                XDocument document = Read(bytes);
                foreach (var attribute in document.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "Source" || attribute.Name.LocalName == "UriSource"))
                {
                    string value = attribute.Value;
                    // Resource-key bindings are resolved by WPF; only literal local
                    // dependencies are captured. External URLs/files are not inputs.
                    if (value.StartsWith("{", StringComparison.Ordinal)) continue;
                    string dependency = Normalize(Path.Combine(Path.GetDirectoryName(relative) ?? "", value));
                    CaptureFile(dependency);
                }
            }
        }

        private static string Normalize(string path)
        {
            path = path.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':'))
                throw new InvalidDataException("Preview resources require local relative paths: " + path);
            var parts = new List<string>();
            foreach (string part in path.Split('/'))
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (parts.Count == 0) throw new InvalidDataException("Preview resource escaped its definition directory: " + path);
                    parts.RemoveAt(parts.Count - 1);
                }
                else if (part.Length == 0) throw new InvalidDataException("Invalid preview resource path: " + path);
                else parts.Add(part);
            }
            if (parts.Count == 0) throw new InvalidDataException("Preview resource must name a file.");
            return string.Join("/", parts);
        }

        private static XDocument Read(byte[] content)
        {
            using var stream = new MemoryStream(content);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }

        internal static void Stage(DefinitionModel model, string directory)
        {
            directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(directory);
            string manifest = Path.Combine(directory, "manifest.txt");
            string resourceRoot = Path.Combine(directory, "CollectionTemplates");
            string[] paths = model.PreviewResources.Select(resource => resource.Path).ToArray();
            if (File.Exists(manifest))
                foreach (string old in File.ReadAllLines(manifest).Except(paths, StringComparer.OrdinalIgnoreCase))
                {
                    string obsolete = Path.GetFullPath(Path.Combine(resourceRoot, Normalize(old)));
                    if (!obsolete.StartsWith(resourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Invalid staged preview path.");
                    File.Delete(obsolete);
                }
            foreach (var resource in model.PreviewResources)
            {
                string target = Path.Combine(resourceRoot, resource.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(resource.Content))
                    File.WriteAllBytes(target, resource.Content);
            }
            string text = string.Join(Environment.NewLine, paths);
            if (!File.Exists(manifest) || File.ReadAllText(manifest) != text) File.WriteAllText(manifest, text, new UTF8Encoding(false));
        }

        internal static void Verify(DefinitionModel model, string directory)
        {
            string manifest = Path.Combine(directory, "manifest.txt");
            if (!File.Exists(manifest) || !File.ReadAllLines(manifest).SequenceEqual(model.PreviewResources.Select(resource => resource.Path)))
                throw new InvalidDataException("Preview resource inputs changed during the build. Rebuild matching tools.");
            foreach (var resource in model.PreviewResources)
                if (!File.ReadAllBytes(Path.Combine(directory, "CollectionTemplates", resource.Path)).SequenceEqual(resource.Content))
                    throw new InvalidDataException("Preview resource changed during markup compilation: " + resource.Path + ". Rebuild matching tools.");
        }
    }
}
