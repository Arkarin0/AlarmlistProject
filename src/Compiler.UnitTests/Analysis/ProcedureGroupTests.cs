// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public sealed class ProcedureGroupTests
    {
        private static AlmxTextSnapshot Source(string alarms, string path = "Groups.almx")
            => new AlmxTextSnapshot("<Alarmlist>" + alarms + "</Alarmlist>", path, 7);

        [Fact]
        public void GroupsMergeIgnoringCaseAndRetainBaseOrderSpellingAndTitleProvenance()
        {
            var sources = new[]
            {
                Source("<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><TestProcedure>"
                    + "<Group Name='Verify'><Title>Check $(AlarmlistAlarmCode)</Title><Step>old</Step></Group>"
                    + "<Group Name='Shutdown'/></TestProcedure></Alarm>", "Base.almx"),
                Source("<Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Code>42</Code>"
                    + "<TestProcedure><Group Name='Local'><Title>Local heading</Title></Group>"
                    + "<Group Name='verify'><Clear/><Warning>new</Warning></Group></TestProcedure></Alarm>")
            };
            var result = new AlarmCompiler().AnalyzeSnapshots(sources, includeCompiledXml: true);
            Assert.True(result.Success);
            var child = result.Alarms[1];
            Assert.Equal(new[] { "Verify", "Shutdown", "Local" }, child.ProcedureSections.Select(group => group.Name));
            var verify = child.ProcedureSections[0];
            Assert.Equal("Check 42", verify.Title.EffectiveValue);
            Assert.Equal("Check $(AlarmlistAlarmCode)", verify.Title.EffectiveExpression);
            Assert.True(verify.Title.IsInherited);
            Assert.Equal("Base.almx", verify.Title.ValueLocation.FilePath);
            Assert.Equal(7, verify.Title.ValueLocation.SourceVersion);
            Assert.True(verify.IsLocallyDeclared);
            Assert.Equal("new", Assert.Single(verify.EffectiveEntries).Text);
            Assert.Equal("old", Assert.Single(verify.ExcludedEntries).Text);
            Assert.Equal("Child", verify.ExcludedEntries[0].ExcludedBy.Alarm.FullyQualifiedName);
            var groups = XDocument.Parse(result.CompiledXml).Descendants("Group").ToArray();
            Assert.Equal(3, groups.Length);
            Assert.Equal("Check 42", groups[0].Element("Title").Value);
            Assert.Equal("Shutdown", groups[1].Element("Title").Value);
            Assert.Empty(groups[1].Elements("TestProcedureItem"));
        }

        [Fact]
        public void ExplicitEmptyTitleStopsInheritanceAndMissingTitleUsesIdentity()
        {
            var result = new AlarmCompiler().AnalyzeSnapshots(new[] { Source(
                "<Alarm abstract='true'><FullyQualifiedName>B</FullyQualifiedName><TestProcedure><Group Name='Verify'>"
                + "<Title>$(missing)</Title></Group></TestProcedure></Alarm>"
                + "<Alarm><ReferenceName>B</ReferenceName><TestProcedure><Group Name='VERIFY'><Title/></Group>"
                + "<Group Name='Local'/></TestProcedure></Alarm>") });
            Assert.True(result.Success);
            Assert.Equal(new[] { "Verify", "Local" }, result.Alarms[1].ProcedureSections.Select(group => group.Title.EffectiveValue));
            Assert.Equal("", result.Alarms[1].ProcedureSections[0].Title.LocalValue);
            Assert.False(result.Alarms[1].ProcedureSections[0].Title.IsInherited);
        }

        [Theory]
        [InlineData("<Group/>", "ALM0026")]
        [InlineData("<Group Name='bad name'/>", "ALM0026")]
        [InlineData("<Group Name='A'/><Group Name='a'/>", "ALM0027")]
        public void InvalidGroupsAreMandatoryErrors(string groups, string id)
        {
            var result = new AlarmCompiler().AnalyzeSnapshots(new[] { Source("<Alarm><TestProcedure>" + groups + "</TestProcedure></Alarm>") }, true);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == id);
            Assert.Null(result.CompiledXml);
        }

        [Fact]
        public void LocalizedTitleUsesConsumingParametersAndRecordsResourceOrigin()
        {
            var resource = new ResxTextSnapshot("<root><data name='Heading'><value>Verify $(p)</value></data></root>", "Titles.de.resx", 3, "de");
            var context = new CompilationContext(new[] { resource }, "de-DE");
            var source = Source("<Alarm abstract='true'><FullyQualifiedName>B</FullyQualifiedName><Properties><Property Name='p'/></Properties>"
                + "<TestProcedure><Group Name='Verify'><Title>$(loc:Heading)</Title></Group></TestProcedure></Alarm>"
                + "<Alarm><ReferenceName>B</ReferenceName><Properties><Property Name='p' Value='42'/></Properties></Alarm>");
            var result = new AlarmCompiler().AnalyzeSnapshots(new[] { source }, context, true);
            Assert.True(result.Success);
            Assert.Equal("Verify 42", result.Alarms[1].ProcedureSections[0].Title.EffectiveValue);
            Assert.Equal("de", Assert.Single(result.Alarms[1].LocalizationUses).Translation.SupplyingCulture);
            var keyword = new AlarmCompiler().AnalyzeSnapshots(new[] { source });
            Assert.Equal("$(loc:Heading)", keyword.Alarms[1].ProcedureSections[0].Title.EffectiveValue);
        }

        [Theory]
        [InlineData("Instructions")]
        [InlineData("Reset")]
        public void LegacyGroupsFailExplicitly(string element)
        {
            Assert.Throws<XmlException>(() => new AlarmCompiler().CompileSnapshots(new[] {
                Source("<Alarm><TestProcedure><" + element + "/></TestProcedure></Alarm>") }));
        }
    }
}
