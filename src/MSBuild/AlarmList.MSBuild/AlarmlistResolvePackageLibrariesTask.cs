// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Alarmlist.MSBuild
{
    /// <summary>Discovers library files only from NuGet's selected restore graph.</summary>
    public sealed class AlarmlistResolvePackageLibrariesTask : AlarmlistBaseTask
    {
        public string AssetsFile { get; set; }
        public string FrameworkMoniker { get; set; } = "native,Version=v0.0";
        public ITaskItem[] PackageReferences { get; set; } = Array.Empty<ITaskItem>();
        [Output] public ITaskItem[] LibraryReferences { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            LibraryReferences = Array.Empty<ITaskItem>();
            try
            {
                ITaskItem[] references = PackageReferences ?? Array.Empty<ITaskItem>();
                if (references.Length == 0) return true;
                if (string.IsNullOrWhiteSpace(AssetsFile) || !File.Exists(AssetsFile))
                    throw new InvalidDataException("Package assets are missing. Run Restore before building alarm libraries.");
                LibraryReferences = LibraryPackageResolver.Resolve(File.ReadAllText(AssetsFile), AssetsFile, FrameworkMoniker,
                    references.Select(reference => reference.ItemSpec).ToArray(), references.Select(reference => reference.GetMetadata("Version")).ToArray())
                    .Select(package =>
                    {
                        var item = new TaskItem(package.FilePath);
                        item.SetMetadata("PackageId", package.PackageId);
                        item.SetMetadata("PackageVersion", package.PackageVersion);
                        item.SetMetadata("Direct", package.IsDirect ? "true" : "false");
                        return (ITaskItem)item;
                    }).ToArray();
                return true;
            }
            catch (Exception exception)
            {
                BuildOutputPlan.LogFailure(Log, exception);
                return false;
            }
        }

    }
}
