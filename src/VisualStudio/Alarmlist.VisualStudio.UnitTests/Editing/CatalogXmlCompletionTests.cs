// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Editor;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class CatalogXmlCompletionTests
    {
        [Theory]
        [InlineData("<Lo¦", "Lookup")]
        [InlineData("<Lookup ¦", "xmlns")]
        [InlineData("<Lookup xmlns='¦'", "urn:alarmlist:lookup:1")]
        [InlineData("<l:Lookup ¦", "xmlns:l")]
        [InlineData("<l:Lookup xmlns:l='¦'", "urn:alarmlist:lookup:1")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><¦", "Path")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><¦", "List")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><¦", "Template")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path ¦", "Key")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P'><¦", "Segment")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='P'><Property ¦", "LabelKey")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='L'><Entry ¦", "DescriptionKey")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Template Key='T'><Argument ¦", "List")]
        [InlineData("<l:Lookup xmlns:l='urn:alarmlist:lookup:1'><l:Pa¦", "l:Path")]
        [InlineData("<l:Lookup xmlns:l='urn:alarmlist:lookup:1'><Pa¦", "l:Path")]
        [InlineData("<l:Lookup xmlns:l='urn:alarmlist:lookup:1'></l:Lo¦", "l:Lookup")]
        public void LookupCandidatesComeFromTheAuthoritativeNamespaceAndSchema(string marked, string expected)
            => Assert.Contains(expected, Candidates(marked, lookup: true));

        [Theory]
        [InlineData("<Lookup><¦")]
        [InlineData("<Lookup xmlns='urn:other'><¦")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><!-- <¦ -->")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><List><Entry Value='$(¦)'/>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='¦'/>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><Path><Segment LabelKey='¦'/>")]
        [InlineData("<Lookup xmlns='urn:alarmlist:lookup:1'><List><Path ¦")]
        [InlineData("<!DOCTYPE Lookup><Lookup xmlns='urn:alarmlist:lookup:1'><¦")]
        public void LookupLiteralValuesAndInvalidScopesDoNotInventCandidates(string marked)
            => Assert.Empty(Candidates(marked, lookup: true));

        [Fact]
        public void LookupAttributesAreNotRepeatedAndSourceIsNeverChanged()
        {
            const string marked = "<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='L'><Entry Key='E' Val¦='literal' Description='text'/></List></Lookup>";
            string[] candidates = Candidates(marked, lookup: true);
            Assert.Contains("Value", candidates);
            Assert.DoesNotContain("Key", candidates);
            Assert.DoesNotContain("Description", candidates);
        }

        [Theory]
        [InlineData("<Pr¦", "Project")]
        [InlineData("<Project><¦", "PropertyGroup")]
        [InlineData("<Project><¦", "Import")]
        [InlineData("<Project><PropertyGroup><Al¦", "AlarmlistCultures")]
        [InlineData("<Project><PropertyGroup><Al¦", "AlarmlistCulture")]
        [InlineData("<Project><PropertyGroup><¦", "EnableDefaultAlarmlistLookupItems")]
        [InlineData("<Project><ItemGroup><¦", "Compile")]
        [InlineData("<Project><ItemGroup><¦", "AlarmlistProperty")]
        [InlineData("<Project><ItemGroup><¦", "AlarmlistLookup")]
        [InlineData("<Project><ItemGroup><¦", "AlarmlistAnalyzer")]
        [InlineData("<Project><ItemGroup><¦", "PackageReference")]
        [InlineData("<Project><ItemGroup><AlarmlistResource ¦", "Culture")]
        [InlineData("<Project><ItemGroup><AlarmlistResource ¦", "Link")]
        [InlineData("<Project><ItemGroup><AlarmlistProperty ¦", "Value")]
        [InlineData("<Project><ItemGroup><AlarmlistResource><¦", "Culture")]
        [InlineData("<Project><ItemGroup><PackageReference ¦", "Version")]
        [InlineData("<Project><ItemGroup><ProjectReference ¦", "ReferenceOutputAssembly")]
        [InlineData("<Project><Import ¦", "Project")]
        [InlineData("<Project><Import Label='¦'", "Shared")]
        [InlineData("<Project><PropertyGroup><OutputType>¦", "Library")]
        [InlineData("<Project><PropertyGroup><EnableDefaultCompileItems>¦", "false")]
        [InlineData("<Project><ItemGroup><ProjectReference ReferenceOutputAssembly='¦'", "true")]
        [InlineData("<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'><ItemGroup><Al¦", "AlarmlistLookup")]
        public void ProjectHintsFollowSdkRulesAndMsbuildScope(string marked, string expected)
            => Assert.Contains(expected, Candidates(marked, lookup: false));

        [Theory]
        [InlineData("<Project xmlns='urn:other'><¦")]
        [InlineData("<Project><ProjectExtensions><¦")]
        [InlineData("<Project><ProjectExtensions><PropertyGroup><¦")]
        [InlineData("<Project><PropertyGroup><OutputType>$(¦)")]
        [InlineData("<Project><ItemGroup><AlarmlistProperty Value='$(¦)'/>")]
        [InlineData("<Project><ItemGroup><AlarmlistResource Culture='¦'/>")]
        [InlineData("<Project><!-- <¦ -->")]
        [InlineData("<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'><¦")]
        [InlineData("<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'><Target Name='Build'><¦")]
        public void UnsupportedProjectScopesAndNativeGeneralContextsAreLeftAlone(string marked)
            => Assert.Empty(Candidates(marked, lookup: false));

        [Fact]
        public void ExportSelectionAndOutputKindComeFromEmbeddedSdkRules()
        {
            Assert.Contains("AlarmlistExportTypes", Candidates("<Project><PropertyGroup><\u00a6", lookup: false));
            Assert.Contains("Exports", Candidates("<Project><PropertyGroup><OutputType>\u00a6", lookup: false));
        }

        [Fact]
        public void ProjectHintsExcludeReadonlyAndUserOnlyProperties()
        {
            string[] properties = Candidates("<Project><PropertyGroup><¦", lookup: false);
            Assert.DoesNotContain("ShowAllFiles", properties);
            Assert.DoesNotContain("AlarmlistPreviewColumnLayout", properties);
            string[] metadata = Candidates("<Project><ItemGroup><AlarmlistResource ¦", lookup: false);
            Assert.DoesNotContain("FullPath", metadata);
            Assert.DoesNotContain("Identity", metadata);
            Assert.Equal(properties.OrderBy(name => name, StringComparer.Ordinal), properties);
        }

        [Fact]
        public void EmbeddedRuleHintsMatchSdkRuleAssets()
        {
            foreach (string name in new[] { "General", "Compile", "AlarmlistResource", "AlarmlistLookup", "AlarmlistAnalyzer", "ProjectReference", "PackageReference" })
            {
                using (var stream = typeof(ProjectXmlCompletion).Assembly.GetManifestResourceStream("Alarmlist.VisualStudio.ProjectRules." + name + ".xaml"))
                    Assert.True(XNode.DeepEquals(XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Rules", name + ".xaml")), XDocument.Load(stream)), name);
            }
        }

        private static string[] Candidates(string marked, bool lookup)
        {
            string text = marked.Replace("¦", "");
            var edit = XmlCompletionContext.Find(text, marked.IndexOf('¦'));
            return (lookup ? LookupXmlCompletion.Complete(text, edit) : ProjectXmlCompletion.Complete(text, edit)).Select(item => item.Insertion).ToArray();
        }
    }
}
