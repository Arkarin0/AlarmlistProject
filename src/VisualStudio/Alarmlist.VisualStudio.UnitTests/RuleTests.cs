// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xaml;
using Microsoft.Build.Framework.XamlTypes;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public class RuleTests
    {
        [Fact]
        public void PreviewColumnLayoutIsAHiddenConfigurationIndependentUserProperty()
        {
            var rule = Assert.IsType<Rule>(XamlServices.Load(Path.Combine(AppContext.BaseDirectory, "Rules", "General.xaml")));
            var property = Assert.IsType<StringProperty>(rule.Properties.Single(item => item.Name == "AlarmlistPreviewColumnLayout"));
            Assert.False(property.Visible); Assert.Equal("UserFile", property.DataSource.Persistence);
            Assert.False(property.DataSource.HasConfigurationCondition);
        }
        [Theory]
        [InlineData("General")]
        [InlineData("GeneralBrowseObject")]
        [InlineData("GeneralFile")]
        [InlineData("Compile")]
        [InlineData("AlarmlistResource")]
        [InlineData("AlarmlistLookup")]
        [InlineData("AlarmlistAnalyzer")]
        [InlineData("ProjectReference")]
        [InlineData("PackageReference")]
        [InlineData("None")]
        [InlineData("Folder")]
        public void PropertyRulesCanBeLoadedByMSBuild(string name)
        {
            var rule = Assert.IsType<Rule>(XamlServices.Load(Path.Combine(AppContext.BaseDirectory, "Rules", name + ".xaml")));
            Assert.NotEmpty(rule.Name);
            Assert.NotEmpty(rule.Properties);
            Assert.NotNull(rule.DataSource);
        }

        [Fact]
        public void ItemSchemaCanBeLoadedByMSBuild()
        {
            var schema = Assert.IsType<ProjectSchemaDefinitions>(XamlServices.Load(
                Path.Combine(AppContext.BaseDirectory, "Rules", "ProjectItemsSchema.xaml")));
            Assert.Contains(schema.Nodes, node => node is FileExtension extension
                && extension.Name == ".almx" && extension.ContentType == "Alarmlist");
            Assert.Contains(schema.Nodes, node => node is ContentType content
                && content.Name == "Alarmlist" && content.ItemType == "Compile");
            Assert.Contains(schema.Nodes, node => node is FileExtension extension
                && extension.Name == ".almlookup" && extension.ContentType == "AlarmlistLookup");
            Assert.Contains(schema.Nodes, node => node is ContentType content
                && content.Name == "AlarmlistLookup" && content.ItemType == "AlarmlistLookup");
            // Declared source item types are files. CPS handles folders through
            // ProjectFileFolderItems; declaring Folder here breaks empty-folder
            // reload because its persisted identity ends in a path separator.
            Assert.DoesNotContain(schema.Nodes, node => node is ItemType item && item.Name == "Folder");
            var folder = Assert.IsType<Rule>(XamlServices.Load(Path.Combine(AppContext.BaseDirectory, "Rules", "Folder.xaml")));
            Assert.Equal("ProjectFileFolderItems", folder.DataSource.Persistence);
        }
    }
}
