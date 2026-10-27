// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Editor
{
    // Editing hints only: never evaluate MSBuild, restore packages, discover
    // files or execute targets. SDK rule files remain the metadata authority.
    internal static class ProjectXmlCompletion
    {
        internal const string Namespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        private static readonly Lazy<Dictionary<string, Property[]>> s_rules = new Lazy<Dictionary<string, Property[]>>(LoadRules);
        private static readonly string[] s_extraProperties =
        {
            "Configuration", "Platform", "AssemblyName", "BaseOutputPath", "EnableDefaultAlarmlistLookupItems",
            "AlarmlistXliffTargetCultures", "AlarmlistXliffOutputPath", "AlarmlistXliffOverwrite",
            "NoWarn", "TreatWarningsAsErrors", "WarningsAsErrors", "WarningsNotAsErrors"
        };

        private sealed class Property
        {
            public string Name { get; }
            public string Description { get; }
            public string[] Values { get; }
            public Property(string name, string description, string[] values) { Name = name; Description = description; Values = values; }
        }
        private static Dictionary<string, Property[]> LoadRules()
        {
            var rules = new Dictionary<string, Property[]>(StringComparer.Ordinal);
            var assembly = typeof(ProjectXmlCompletion).Assembly;
            foreach (string name in assembly.GetManifestResourceNames().Where(item => item.StartsWith("Alarmlist.VisualStudio.ProjectRules.", StringComparison.Ordinal)))
            {
                using (var stream = assembly.GetManifestResourceStream(name))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                {
                    XElement root = XDocument.Load(reader).Root;
                    XNamespace ns = root.Name.Namespace;
                    string item = (string)root.Element(ns + "Rule.DataSource")?.Element(ns + "DataSource")?.Attribute("ItemType") ?? "";
                    rules[item] = root.Elements().Where(element => element.Name.LocalName.EndsWith("Property", StringComparison.Ordinal)
                        && (string)element.Attribute("ReadOnly") != "True" && (item.Length != 0 || (string)element.Attribute("Visible") != "False"))
                        .Select(element => new Property((string)element.Attribute("Name"), (string)element.Attribute("Description") ?? (string)element.Attribute("DisplayName") ?? "SDK item metadata",
                            element.Name.LocalName == "BoolProperty" ? new[] { "true", "false" }
                                : element.Elements(ns + "EnumValue").Select(value => (string)value.Attribute("Name")).ToArray())).ToArray();
                }
            }
            rules[""] = rules[""].Concat(s_extraProperties.Where(name => !rules[""].Any(property => property.Name == name))
                .Select(name => new Property(name, "Alarmlist SDK build setting", name.StartsWith("EnableDefault", StringComparison.Ordinal)
                    || name == "TreatWarningsAsErrors" || name == "AlarmlistXliffOverwrite" ? new[] { "true", "false" } : Array.Empty<string>()))).ToArray();
            rules["AlarmlistProperty"] = new[] { new Property("Value", "Literal project parameter assignment; omit to leave unassigned", Array.Empty<string>()) };
            return rules;
        }

        public static IReadOnlyList<XmlCompletionCandidate> Complete(string text, XmlCompletionEdit edit)
        {
            if (edit == null || !IsProjectContext(edit)) return Array.Empty<XmlCompletionCandidate>();
            string[] path = edit.Ancestors.Select(node => Local(node.Name)).ToArray();
            string parent = path.LastOrDefault();
            string opening = Local(edit.Opening?.Name);
            string token = text.Substring(edit.Start, edit.Length);
            if (token.Contains(":") && edit.Kind != XmlCompletionKind.Text && edit.Kind != XmlCompletionKind.AttributeValue) return Array.Empty<XmlCompletionCandidate>();
            bool native = edit.Ancestors.Any(node => node.Attributes.TryGetValue("xmlns", out string ns) && ns == Namespace);
            IEnumerable<XmlCompletionCandidate> candidates = Array.Empty<XmlCompletionCandidate>();
            if (edit.Kind == XmlCompletionKind.ClosingElement && path.Length != 0)
                return native ? Array.Empty<XmlCompletionCandidate>() : Names(edit.Ancestors.Last().Name).ToArray();
            if (edit.Kind == XmlCompletionKind.Element)
            {
                if (native && parent != "PropertyGroup" && parent != "ItemGroup" && parent != "ItemDefinitionGroup"
                    && !(path.Length >= 2 && path[path.Length - 2] == "ItemGroup")) return Array.Empty<XmlCompletionCandidate>();
                if (path.Length == 0) candidates = Names("Project");
                else if (parent == "Project") candidates = Names("PropertyGroup", "ItemGroup", "Import", "ImportGroup", "Choose", "Target", "UsingTask", "Sdk", "ItemDefinitionGroup", "ProjectExtensions");
                else if (parent == "ImportGroup") candidates = Names("Import");
                else if (parent == "Choose") candidates = Names("When", "Otherwise");
                else if (parent == "When" || parent == "Otherwise") candidates = Names("Choose", "PropertyGroup", "ItemGroup");
                else if (parent == "Target") candidates = Names("PropertyGroup", "ItemGroup", "OnError");
                else if (parent == "PropertyGroup") candidates = s_rules.Value[""].Select(Hint);
                else if (parent == "ItemGroup" || parent == "ItemDefinitionGroup") candidates = s_rules.Value.Keys.Where(name => name.Length != 0)
                    .Select(name => new XmlCompletionCandidate(name, "Alarmlist SDK input item"));
                else if (path.Length >= 2 && (path[path.Length - 2] == "ItemGroup" || path[path.Length - 2] == "ItemDefinitionGroup")
                    && s_rules.Value.TryGetValue(parent, out Property[] metadata)) candidates = metadata.Select(Hint);
                return candidates.OrderBy(item => item.Label, StringComparer.Ordinal).ToArray();
            }
            if (edit.Kind == XmlCompletionKind.Attribute && opening != null)
            {
                if (native && parent != "ItemGroup") return Array.Empty<XmlCompletionCandidate>();
                string[] attributes = Attributes(opening, parent);
                candidates = Names(attributes).Where(item => !edit.ExistingAttributes.Contains(item.Insertion));
                if (s_rules.Value.TryGetValue(opening, out Property[] metadata) && parent == "ItemGroup")
                    candidates = candidates.Concat(metadata.Where(property => !edit.ExistingAttributes.Contains(property.Name)).Select(Hint));
            }
            else if (edit.Kind == XmlCompletionKind.AttributeValue)
            {
                if (opening == "Import" && edit.AttributeName == "Label") candidates = Names("Shared");
                else if (parent == "ItemGroup" && s_rules.Value.TryGetValue(opening ?? "", out Property[] metadata))
                    candidates = Names(metadata.FirstOrDefault(property => property.Name == edit.AttributeName)?.Values ?? Array.Empty<string>());
            }
            else if (edit.Kind == XmlCompletionKind.Text && path.Length >= 2)
            {
                string owner = path[path.Length - 2];
                if (owner == "PropertyGroup") candidates = Names(s_rules.Value[""].FirstOrDefault(property => property.Name == parent)?.Values ?? Array.Empty<string>());
                else if (path.Length >= 3 && path[path.Length - 3] == "ItemGroup" && s_rules.Value.TryGetValue(owner, out Property[] metadata))
                    candidates = Names(metadata.FirstOrDefault(property => property.Name == parent)?.Values ?? Array.Empty<string>());
            }
            if ((edit.Kind == XmlCompletionKind.Text || edit.Kind == XmlCompletionKind.AttributeValue) && token.StartsWith("$(", StringComparison.Ordinal)) return Array.Empty<XmlCompletionCandidate>();
            return candidates.Select(item => new XmlCompletionCandidate(XmlValueText.Escape(item.Insertion, edit.Quote), item.Description))
                .OrderBy(item => item.Label, StringComparer.Ordinal).ToArray();
        }

        private static string[] Attributes(string name, string parent)
        {
            if (name == "Project") return new[] { "Sdk", "DefaultTargets", "InitialTargets", "ToolsVersion", "TreatAsLocalProperty", "xmlns" };
            if (name == "Import") return new[] { "Project", "Condition", "Label", "Sdk" };
            if (name == "Sdk") return new[] { "Name", "Version", "MinimumVersion" };
            if (name == "Target") return new[] { "Name", "Condition", "Label", "DependsOnTargets", "BeforeTargets", "AfterTargets", "Inputs", "Outputs", "Returns", "KeepDuplicateOutputs" };
            if (name == "ItemGroup" || name == "PropertyGroup" || name == "ImportGroup" || name == "ItemDefinitionGroup") return new[] { "Condition", "Label" };
            if (name == "When") return new[] { "Condition" };
            if (name == "UsingTask") return new[] { "TaskName", "AssemblyFile", "AssemblyName", "Condition", "TaskFactory" };
            if (parent == "ItemGroup") return new[] { "Include", "Exclude", "Remove", "Update", "Condition", "Label", "KeepMetadata", "RemoveMetadata", "KeepDuplicates" };
            if (parent == "PropertyGroup" || s_rules.Value.ContainsKey(parent ?? "")) return new[] { "Condition" };
            return Array.Empty<string>();
        }
        private static bool IsProjectContext(XmlCompletionEdit edit)
        {
            if (edit.Ancestors.Length != 0 && Local(edit.Ancestors[0].Name) != "Project") return false;
            if (edit.Ancestors.Any(node => Local(node.Name) == "ProjectExtensions")) return false;
            foreach (var node in edit.Ancestors.Concat(edit.Opening == null ? Array.Empty<XmlCompletionNode>() : new[] { edit.Opening }))
                if (node.Attributes.TryGetValue("xmlns", out string ns) && ns.Length != 0 && ns != Namespace) return false;
            // Prefixed MSBuild documents are left to native XML namespace tooling.
            return !edit.Ancestors.Any(node => node.Name.Contains(":")) && edit.Opening?.Name.Contains(":") != true;
        }
        private static string Local(string name) => name == null ? null : name.Substring(name.IndexOf(':') + 1);
        private static XmlCompletionCandidate Hint(Property property) => new XmlCompletionCandidate(property.Name, property.Description);
        private static IEnumerable<XmlCompletionCandidate> Names(params string[] names) => names.Select(name => new XmlCompletionCandidate(name, "MSBuild XML authoring"));
    }
}
