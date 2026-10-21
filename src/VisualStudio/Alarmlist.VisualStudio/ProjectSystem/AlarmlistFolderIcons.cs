// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    [Export(typeof(IProjectTreePropertiesProvider))]
    [AppliesTo(AlarmlistProject.Capability)]
    internal sealed class AlarmlistFolderIcons : IProjectTreePropertiesProvider
    {
        public void CalculatePropertyValues(IProjectTreeCustomizablePropertyContext context,
            IProjectTreeCustomizablePropertyValues values)
        {
            // CPS owns the indicators for absent, excluded, virtual and linked items.
            // Do not give those nodes the appearance of an ordinary included folder.
            if (!context.IsFolder || !context.ExistsOnDisk || context.IsNonFileSystemProjectItem
                || values.Flags.Contains(ProjectTreeFlags.Common.NonMemberItem)
                || values.Flags.Contains(ProjectTreeFlags.Common.IncludeInProjectCandidate)
                || values.Flags.Contains(ProjectTreeFlags.Common.HiddenFileSystemEntity)
                || values.Flags.Contains(ProjectTreeFlags.Common.VirtualFolder)
                || values.Flags.Contains("LinkedItem")
                || values.Flags.Contains("LinkedFilesOnlyFolder")
                || (context is IProjectTreeCustomizablePropertyContext3 extended && extended.IsLinkedFilesOnlyFolder))
                return;

            ImageMoniker closed;
            ImageMoniker opened;
            if (string.Equals(context.ItemName, "Properties", StringComparison.OrdinalIgnoreCase))
            {
                closed = KnownMonikers.PropertiesFolderClosed;
                opened = KnownMonikers.PropertiesFolderOpen;
            }
            else if (string.Equals(context.ItemName, "Resources", StringComparison.OrdinalIgnoreCase))
                closed = opened = KnownMonikers.LocalResources;
            else if (string.Equals(context.ItemName, "Translations", StringComparison.OrdinalIgnoreCase))
                closed = opened = KnownMonikers.TranslationView;
            else if (string.Equals(context.ItemName, "Lookups", StringComparison.OrdinalIgnoreCase))
                closed = opened = KnownMonikers.LookupList;
            else
                return;

            values.Icon = new ProjectImageMoniker(closed.Guid, closed.Id);
            values.ExpandedIcon = new ProjectImageMoniker(opened.Guid, opened.Id);
        }
    }
}
