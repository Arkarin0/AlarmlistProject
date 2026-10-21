// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Immutable;
using Alarmlist.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public class FolderIconTests
    {
        [Theory]
        [InlineData("Properties")]
        [InlineData("Resources")]
        [InlineData("Translations")]
        [InlineData("Lookups")]
        public void RecognizedFoldersUseNativeIconsWithoutChangingFlags(string name)
        {
            ImageMoniker closed = name == "Properties" ? KnownMonikers.PropertiesFolderClosed
                : name == "Resources" ? KnownMonikers.LocalResources
                : name == "Translations" ? KnownMonikers.TranslationView : KnownMonikers.LookupList;
            ImageMoniker open = name == "Properties" ? KnownMonikers.PropertiesFolderOpen : closed;
            foreach (string spelling in new[] { name, name.ToLowerInvariant(), name.ToUpperInvariant() })
            {
                var values = new Values();
                ProjectTreeFlags flags = values.Flags;
                new AlarmlistFolderIcons().CalculatePropertyValues(new Context { ItemName = spelling }, values);
                Assert.Equal(new ProjectImageMoniker(closed.Guid, closed.Id), values.Icon);
                Assert.Equal(new ProjectImageMoniker(open.Guid, open.Id), values.ExpandedIcon);
                Assert.Equal(flags, values.Flags);
            }
        }

        [Theory]
        [InlineData("ordinary")]
        [InlineData("file")]
        [InlineData("missing")]
        [InlineData("virtual")]
        [InlineData("linked-context")]
        [InlineData("NonMemberItem")]
        [InlineData("IncludeInProjectCandidate")]
        [InlineData("HiddenFileSystemEntity")]
        [InlineData("VirtualFolder")]
        [InlineData("LinkedItem")]
        [InlineData("LinkedFilesOnlyFolder")]
        public void SpecialStatesAndUnrecognizedNamesKeepCpsIcons(string state)
        {
            var context = new Context();
            var values = new Values();
            if (state == "ordinary") context.ItemName = "Other";
            else if (state == "file") context.IsFolder = false;
            else if (state == "missing") context.ExistsOnDisk = false;
            else if (state == "virtual") context.IsNonFileSystemProjectItem = true;
            else if (state == "linked-context") context.IsLinkedFilesOnlyFolder = true;
            else values.Flags = values.Flags.Add(state);
            ProjectImageMoniker closed = values.Icon, open = values.ExpandedIcon;
            new AlarmlistFolderIcons().CalculatePropertyValues(context, values);
            Assert.Same(closed, values.Icon);
            Assert.Same(open, values.ExpandedIcon);
        }

        // CPS supplies fresh default values each time (including a rename), and
        // the provider may customize only the icons, not item identity or flags.
        private sealed class Values : IProjectTreeCustomizablePropertyValues
        {
            public ProjectTreeFlags Flags { get; set; } = ProjectTreeFlags.Create("Folder");
            public ProjectImageMoniker Icon { get; set; } = new ProjectImageMoniker(KnownMonikers.FolderClosed.Guid, KnownMonikers.FolderClosed.Id);
            public ProjectImageMoniker ExpandedIcon { get; set; } = new ProjectImageMoniker(KnownMonikers.FolderOpened.Guid, KnownMonikers.FolderOpened.Id);
        }

        private sealed class Context : IProjectTreeCustomizablePropertyContext3
        {
            public string ItemType => "Folder";
            public string ItemName { get; set; } = "Properties";
            public IImmutableDictionary<string, string> Metadata => ImmutableDictionary<string, string>.Empty;
            public ProjectTreeFlags ParentNodeFlags => ProjectTreeFlags.Empty;
            public bool ExistsOnDisk { get; set; } = true;
            public IImmutableDictionary<string, string> ProjectTreeSettings => ImmutableDictionary<string, string>.Empty;
            public bool IsFolder { get; set; } = true;
            public bool IsNonFileSystemProjectItem { get; set; }
            public bool MultipleIncludedItem => false;
            public bool IsLinkedFilesOnlyFolder { get; set; }
            public bool IsDuplicateItem => false;
        }
    }
}
