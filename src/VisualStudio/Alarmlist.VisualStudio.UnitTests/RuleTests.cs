// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Xaml;
using Microsoft.Build.Framework.XamlTypes;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public class RuleTests
    {
        [Theory]
        [InlineData("General")]
        [InlineData("GeneralBrowseObject")]
        [InlineData("GeneralFile")]
        [InlineData("Compile")]
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
        }
    }
}
