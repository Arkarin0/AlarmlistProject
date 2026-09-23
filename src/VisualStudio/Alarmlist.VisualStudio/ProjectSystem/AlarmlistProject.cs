// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    [Export]
    [AppliesTo(Capability)]
    [ProjectTypeRegistration(ProjectTypeGuid, "Alarmlist", "#110", ProjectExtension, "Alarmlist",
        resourcePackageGuid: AlarmlistPackage.PackageGuid,
        PossibleProjectExtensions = ProjectExtension,
        ProjectTemplatesDir = "ProjectTemplates")]
    [ProvideProjectItem(ProjectTypeGuid, "Alarmlist", "ItemTemplates", 100)]
    internal sealed class AlarmlistProject
    {
        public const string ProjectTypeGuid = "117e1e3b-8dc0-44bf-a627-4e41f84721d8";
        public const string ProjectExtension = "almproj";
        public const string Capability = "Alarmlist";
    }
}
