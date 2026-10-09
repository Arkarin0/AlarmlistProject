// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Evaluation
{
    public class ScopedPropertyTests
    {
        [Theory]
        [InlineData(" Value='alarm'", " Value='file'", "alarm")]
        [InlineData("", " Value='file'", "file")]
        [InlineData("", "", "project")]
        [InlineData(" Value=''", " Value='file'", "")]
        [InlineData("", " Value=''", "")]
        public void CurrentAlarmThenFileThenProjectAssignmentsWin(string alarmAssignment, string fileAssignment, string expected)
        {
            CompilationResult result = Compile("<Properties><Property Name='P'" + fileAssignment + "/></Properties>"
                + "<Alarm><Properties><Property Name='p'" + alarmAssignment + "/></Properties><Code>$(P)</Code></Alarm>",
                new CompilationContext(properties: new[] { new PropertyDefinition("p", "project") }));
            Assert.True(result.Success);
            Assert.Equal(expected, Assert.Single(result.AlarmList).Code);
        }

        [Fact]
        public void TemplateDeclarationsFlowButNeitherBaseNorBaseFileAssignmentsFlow()
        {
            const string template = "<Alarmlist><Properties><Property Name='p' Value='base file'/></Properties>"
                + "<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='p' Value='base'/></Properties>"
                + "<Code>$(p)</Code><Description>Inspect $(AlarmlistAlarmCode)</Description></Alarm>"
                + "<Alarm abstract='true'><FullyQualifiedName>Middle</FullyQualifiedName><ReferenceName>A</ReferenceName>"
                + "<Properties><Property Name='p' Value='middle'/></Properties></Alarm></Alarmlist>";
            const string child = "<Alarmlist><Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>Middle</ReferenceName></Alarm>"
                + "<Properties><Property Name='p' Value='001'/></Properties></Alarmlist>";
            var compiler = new AlarmCompiler();
            var source = new AlmxTextSnapshot(template, "Templates.almx", 1);
            CompilationResult success = compiler.CompileSnapshots(new[] { source, new AlmxTextSnapshot(child, "Child.almx", 2) });
            Assert.True(success.Success);
            Assert.Equal("Inspect 001", Assert.Single(success.AlarmList).Description);
            CompilationResult unassigned = compiler.CompileSnapshots(new[] { source, new AlmxTextSnapshot(child.Replace(" Value='001'", ""), "Child.almx", 3) });
            Assert.Equal("ALM0008", Assert.Single(unassigned.Diagnostics).Id);
            Assert.Equal("Templates.almx", unassigned.Diagnostics[0].Location.FilePath);
            Assert.Equal(1, unassigned.Diagnostics[0].Location.SourceVersion);
            Assert.Contains("'B'", unassigned.Diagnostics[0].Message);
        }

        [Fact]
        public void InheritedDeclarationWithoutAnyOuterDeclarationIsUnassignedRatherThanUnknown()
        {
            CompilationResult result = Compile("<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName>"
                + "<Properties><Property Name='p' Value='ignored'/></Properties><Code>$(p)</Code></Alarm>"
                + "<Alarm><ReferenceName>A</ReferenceName></Alarm>");
            Assert.Equal("ALM0008", Assert.Single(result.Diagnostics).Id);
        }

        [Fact]
        public void OrdinaryReferencedAlarmsKeepTheirOwnAssignmentsOnlyForTheirOwnOutput()
        {
            CompilationResult result = Compile("<Properties><Property Name='p' Value='file'/></Properties>"
                + "<Alarm><FullyQualifiedName>A</FullyQualifiedName><Properties><Property Name='p' Value='base'/></Properties><Code>$(p)</Code></Alarm>"
                + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm>");
            Assert.True(result.Success);
            Assert.Equal(new[] { "base", "file" }, result.AlarmList.Select(alarm => alarm.Code));
        }

        [Theory]
        [InlineData("<Property/>")]
        [InlineData("<Property Name='bad-name'/>")]
        [InlineData("<Property Name='9bad'/>")]
        [InlineData("<Property Name='alarmlistAnything'/>")]
        [InlineData("<Property Name='p'>text</Property>")]
        [InlineData("<Property Name='p'><Value>text</Value></Property>")]
        public void InvalidDeclarationsAreDiagnosedEvenWithoutConcreteAlarms(string declaration)
        {
            var result = Compile("<Properties>" + declaration + "</Properties>");
            Assert.Equal("ALM0011", Assert.Single(result.Diagnostics).Id);
            Assert.Equal(4, result.Diagnostics[0].Location.SourceVersion);
        }

        [Fact]
        public void DuplicateNamesAreCaseInsensitiveAcrossContainersInEachScope()
        {
            const string duplicate = "<Properties><Property Name='p'/></Properties><Properties><Property Name='P'/></Properties>";
            CompilationResult result = Compile(duplicate + "<Alarm abstract='true'>" + duplicate + "</Alarm>",
                new CompilationContext(properties: new[] { new PropertyDefinition("p"), new PropertyDefinition("P") }));
            Assert.Equal(3, result.Diagnostics.Count);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("ALM0012", diagnostic.Id));
            Assert.All(result.Diagnostics.Skip(1), diagnostic => Assert.Single(diagnostic.RelatedLocations));
        }

        [Fact]
        public void UnusedUnassignedValuesAndUnknownXmlAreAllowed()
        {
            CompilationResult result = Compile("<Unknown><Property Name='invalid-name'/></Unknown>"
                + "<Properties><Unknown/><Property Name='unused'> <![CDATA[  ]]> </Property></Properties>"
                + "<Alarm><Properties><Property Name='p' Value='$(unused) $$ &amp; &quot;quoted&quot;'/></Properties><Code>$(p)</Code></Alarm>");
            Assert.True(result.Success);
            Assert.Equal("$(unused) $$ & \"quoted\"", Assert.Single(result.AlarmList).Code);
        }

        [Fact]
        public void RoundtripPreservesFileAlarmDeclarationsAndAssignmentPresence()
        {
            var source = new AlmxTextSnapshot("<Alarmlist><Properties><Property Name='file' Value='001'/></Properties>"
                + "<Alarm abstract='true'><Properties><Property Name='unassigned'/><Property Name='empty' Value=''/></Properties></Alarm></Alarmlist>", "Input.almx", 0);
            var file = new AlmxFile(source.Parse());
            var serializer = new XmlSerializer(typeof(AlmxFile), new XmlRootAttribute("Alarmlist"));
            var text = new StringBuilder();
            using (var writer = new StringWriter(text)) serializer.Serialize(writer, file);
            AlmxFile restored;
            using (var reader = new StringReader(text.ToString())) restored = (AlmxFile)serializer.Deserialize(reader);
            var tree = restored.Read();
            Assert.Equal("001", Assert.Single(tree.Properties).Value);
            Assert.Null(tree.Alarms[0].Properties[0].Value);
            Assert.Equal("", tree.Alarms[0].Properties[1].Value);
            Assert.True(tree.Alarms[0].IsAbstract);
        }

        [Fact]
        public void FreshSnapshotParsingAndFileScopesAreIndependent()
        {
            var snapshot = new AlmxTextSnapshot("<Alarmlist><Properties><Property Name='p' Value='001'/></Properties><Alarm><Code>$(p)</Code></Alarm></Alarmlist>", "Input.almx", 0);
            var first = snapshot.Parse();
            first.Properties.Clear();
            var second = snapshot.Parse();
            Assert.Single(second.Properties);
            Assert.Equal("001", Assert.Single(new AlarmCompiler().Compile(second).AlarmList).Code);
        }

        private static CompilationResult Compile(string content, CompilationContext context = null)
            => new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist>" + content + "</Alarmlist>", "Scopes.almx", 4) }, context ?? CompilationContext.Empty);
    }
}
