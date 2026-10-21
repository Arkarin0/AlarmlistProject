using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Alarmlist.MSBuild
{
    /// <summary>Packs the validated output of the preceding library build.</summary>
    public sealed class AlarmlistPackTask : AlarmlistBaseTask
    {
        [Required] public ITaskItem Artifact { get; set; }
        [Required] public string OutputDirectory { get; set; }
        public string Authors { get; set; }
        public string Description { get; set; }
        public ITaskItem[] LibraryReferences { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem[] ProtectedFiles { get; set; } = Array.Empty<ITaskItem>();
        [Output] public ITaskItem[] Packages { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            Packages = Array.Empty<ITaskItem>();
            string temporary = null;
            try
            {
                if (!string.Equals(Artifact.GetMetadata("OutputType"), "Library", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Pack requires a built OutputType=Library artifact.");
                var identity = new Alarmlist.Libraries.LibraryIdentity(Artifact.GetMetadata("PackageId"),
                    NuGetVersion.Parse(Artifact.GetMetadata("PackageVersion")).ToNormalizedString());
                string destination = Path.GetFullPath(Path.Combine(OutputDirectory, identity.Id + "." + identity.Version + ".nupkg"));
                var references = LibraryReferences ?? Array.Empty<ITaskItem>();
                var protectedPaths = new HashSet<string>((ProtectedFiles ?? Array.Empty<ITaskItem>())
                    .Concat(references).Concat(new[] { Artifact }).Select(item => Path.GetFullPath(item.ItemSpec)), BuildOutputPlan.PathComparer);
                protectedPaths.UnionWith(references.Select(item => item.GetMetadata("SourceProject"))
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath));
                if (protectedPaths.Contains(destination)) throw new InvalidDataException("A package must not replace a source input.");
                if (references.Any(item => string.Equals(item.GetMetadata("IsPackable"), "false", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("A library dependency has IsPackable=false and cannot be represented as a package dependency.");
                var dependencies = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
                foreach (ITaskItem reference in references.Where(item => !string.Equals(item.GetMetadata("Direct"), "false", StringComparison.OrdinalIgnoreCase)))
                {
                    string id = reference.GetMetadata("PackageId");
                    NuGetVersion version = NuGetVersion.Parse(reference.GetMetadata("PackageVersion"));
                    if (string.Equals(id, identity.Id, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("A package cannot depend on itself: " + id);
                    if (dependencies.TryGetValue(id, out NuGetVersion existing) && !existing.Equals(version))
                        throw new InvalidDataException("Conflicting package dependency versions: " + id);
                    dependencies[id] = version;
                }
                byte[] artifact = File.ReadAllBytes(Artifact.ItemSpec);
                var package = new PackageBuilder
                {
                    Id = identity.Id, Version = NuGetVersion.Parse(identity.Version),
                    Description = string.IsNullOrWhiteSpace(Description) ? "Reusable alarm library " + identity.Id : Description
                };
                foreach (string author in (string.IsNullOrWhiteSpace(Authors) ? identity.Id : Authors).Split(';').Where(value => !string.IsNullOrWhiteSpace(value)))
                    package.Authors.Add(author.Trim());
                if (dependencies.Count != 0)
                    package.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework,
                        dependencies.Select(pair => new PackageDependency(pair.Key, new VersionRange(pair.Value, true, pair.Value, true)))));

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                temporary = Path.Combine(Path.GetDirectoryName(destination), ".alarmlist-pack-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporary);
                string captured = Path.Combine(temporary, identity.Id + ".almlib");
                File.WriteAllBytes(captured, artifact);
                package.Files.Add(new PhysicalPackageFile { SourcePath = captured, TargetPath = "alarmlist/" + identity.Id + ".almlib" });
                string staged = Path.Combine(temporary, "package.nupkg");
                using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None)) package.Save(stream);
                if (File.Exists(destination)) File.Replace(staged, destination, null);
                else File.Move(staged, destination);
                Packages = new ITaskItem[] { new TaskItem(destination) };
                return true;
            }
            catch (Exception exception) { BuildOutputPlan.LogFailure(Log, exception); return false; }
            finally
            {
                if (temporary != null)
                {
                    try { Directory.Delete(temporary, true); }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    { Log.LogWarning("Could not remove package staging directory: {0}", exception.Message); }
                }
            }
        }
    }
}
