// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.Output;
using Microsoft.Build.Framework;

namespace Alarmlist.MSBuild
{
    /// <summary>Build-only configuration. Captures only explicitly selected providers and declared dependencies.</summary>
    internal sealed class ExportConfiguration
    {
        internal IReadOnlyList<string> SelectedIds { get; }
        private readonly Dictionary<string, ITaskItem> _registrations;
        private readonly string _projectDirectory;

        internal ExportConfiguration(string outputType, string exportTypes, IEnumerable<ITaskItem> registrations, string projectPath, bool requireSelectedProviders = true)
        {
            bool library = BuildOutputPlan.IsLibrary(outputType);
            if (library && !string.IsNullOrWhiteSpace(exportTypes))
                throw new ArgumentException("OutputType=Library does not accept AlarmlistExportTypes.");
            var selected = library ? Array.Empty<string>() : (string.IsNullOrWhiteSpace(exportTypes) ? "XML" : exportTypes).Split(';').Select(id => id.Trim()).ToArray();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in selected)
            {
                ExportProviderReference.ValidateId(id);
                if (!unique.Add(id)) throw new ArgumentException("Duplicate selected export provider ID '" + id + "'.");
            }
            SelectedIds = Array.AsReadOnly(selected);
            _registrations = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            foreach (ITaskItem item in registrations ?? Array.Empty<ITaskItem>())
            {
                if (item == null) throw new ArgumentException("Export provider registrations cannot be null.");
                ExportProviderReference.ValidateId(item.ItemSpec);
                if (_registrations.ContainsKey(item.ItemSpec)) throw new ArgumentException("Duplicate export provider registration '" + item.ItemSpec + "'.");
                bool builtin = IsBuiltin(item.ItemSpec);
                if (builtin && (!string.IsNullOrWhiteSpace(item.GetMetadata("AssemblyPath")) || !string.IsNullOrWhiteSpace(item.GetMetadata("TypeName"))))
                    throw new ArgumentException("Built-in export providers cannot be replaced: " + item.ItemSpec);
                _registrations.Add(item.ItemSpec, item);
            }
            foreach (string id in selected)
                if (requireSelectedProviders && !IsBuiltin(id) && !_registrations.ContainsKey(id))
                    throw new ArgumentException("Export provider '" + id + "' is not registered. Add its NuGet package or an AlarmlistExportProvider DLL declaration.");
            _projectDirectory = string.IsNullOrWhiteSpace(projectPath) ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(projectPath));
        }

        internal static bool IsBuiltin(string id) => string.Equals(id, "XML", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "JSON", StringComparison.OrdinalIgnoreCase);

        internal IReadOnlyList<ExportProviderReference> Capture(CancellationToken cancellationToken)
        {
            var result = new List<ExportProviderReference>();
            foreach (string id in SelectedIds.Where(id => !IsBuiltin(id)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ITaskItem item = _registrations[id];
                string path = AssemblyPath(id, item);
                var images = new List<ExportAssemblyImage>();
                foreach (string dependency in ReferencePaths(path, item))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.Equals(Path.GetExtension(dependency), ".dll", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Provider assemblies and dependencies must be DLLs: " + dependency);
                    if (string.Equals(Path.GetFileName(dependency), "Alarmlist.Core.dll", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Provider dependencies must not replace the build host's Alarmlist.Core contract.");
                    images.Add(new ExportAssemblyImage(dependency, File.ReadAllBytes(dependency)));
                }
                result.Add(new ExportProviderReference(id, path, item.GetMetadata("TypeName"), images));
            }
            return result.AsReadOnly();
        }

        internal IEnumerable<string> DeclaredInputPaths()
        {
            foreach (var registration in _registrations.Where(pair => !IsBuiltin(pair.Key)))
            {
                if (string.IsNullOrWhiteSpace(registration.Value.GetMetadata("AssemblyPath"))) continue;
                foreach (string path in ReferencePaths(AssemblyPath(registration.Key, registration.Value), registration.Value)) yield return path;
            }
        }

        private string AssemblyPath(string id, ITaskItem item)
        {
            string path = item.GetMetadata("AssemblyPath");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Export provider '" + id + "' requires AssemblyPath.");
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(_projectDirectory, path));
        }

        private static IEnumerable<string> ReferencePaths(string path, ITaskItem item)
            => new[] { path }.Concat(item.GetMetadata("Dependencies").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(Path.GetDirectoryName(path), value))));

        internal string FileName(string id, string outputFileName)
        {
            _registrations.TryGetValue(id, out ITaskItem registration);
            string fileName = registration?.GetMetadata("OutputFileName");
            if (string.IsNullOrEmpty(fileName)) fileName = string.Equals(id, "XML", StringComparison.OrdinalIgnoreCase)
                ? outputFileName : Path.ChangeExtension(outputFileName, id.ToLowerInvariant());
            BuildOutputPlan.ValidateFileName(fileName);
            return fileName;
        }

        internal ExportRequest Request(string id, ExportDocument document, string outputFileName)
        {
            _registrations.TryGetValue(id, out ITaskItem registration);
            string fileName = FileName(id, outputFileName);
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            if (registration != null)
                foreach (string name in registration.MetadataNames)
                    if (name.StartsWith("Option_", StringComparison.Ordinal)) options.Add(name.Substring(7), registration.GetMetadata(name));
            return new ExportRequest(document, fileName, options);
        }
    }
}
