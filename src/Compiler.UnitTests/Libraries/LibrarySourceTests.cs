// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Libraries;
using Alarmlist.Syntax;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Libraries
{
    public class LibrarySourceTests
    {
        [Fact]
        public void NormalizationIgnoresFormattingCommentsAndPhysicalPaths()
        {
            var first = new AlmxTextSnapshot("<Alarmlist>\r\n<!-- original formatting -->\r\n"
                + "<Alarm abstract='true'><FullyQualifiedName>Base</FullyQualifiedName><Name>$(Part)</Name></Alarm></Alarmlist>",
                "C:/checkout/alarms.almx", 19);
            var second = new AlmxTextSnapshot("<Alarmlist><Alarm abstract=\"true\"><Name>$(Part)</Name>"
                + "<FullyQualifiedName>Base</FullyQualifiedName></Alarm></Alarmlist>", "/other/alarms.almx", 2);

            LibrarySource left = LibrarySource.Capture("definitions/alarms.almx", first.Parse());
            LibrarySource right = LibrarySource.Capture("definitions/alarms.almx", second.Parse());

            Assert.Equal(left.Definition, right.Definition);
            Assert.Equal("definitions/alarms.almx", left.Name);
            Assert.Contains("$(Part)", left.Definition);
            Assert.DoesNotContain("checkout", left.Definition);
            Assert.DoesNotContain("original formatting", left.Definition);
            Assert.DoesNotContain("\r", left.Definition);
        }

        [Fact]
        public void CaptureKeepsLocalOverridesAndProcedureDirectivesAfterBinding()
        {
            const string text = """
                <Alarmlist>
                  <Properties><Property Name="Part" Value="file" /></Properties>
                  <Alarm abstract="true">
                    <FullyQualifiedName>Base</FullyQualifiedName><Name>$(Part)</Name>
                    <TestProcedure><Group Name="Checks"><Title>$(Part)</Title><Step>Inherited</Step></Group></TestProcedure>
                  </Alarm>
                  <Alarm>
                    <FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName>
                    <Properties><Property Name="Part" Value="" /><Property Name="Unassigned" /></Properties>
                    <TestProcedure><Group Name="Checks"><Clear/><Warning>$(loc:Warning)</Warning></Group></TestProcedure>
                  </Alarm>
                </Alarmlist>
                """;
            AlarmSyntaxTree syntax = new AlmxTextSnapshot(text, "physical.almx", 5).Parse();
            var compiler = new AlarmCompiler();
            Assert.True(compiler.Compile(syntax).Success);
            Assert.NotNull(syntax.Alarms[1].Reference);
            LibrarySource source = LibrarySource.Capture("alarms.almx", syntax);
            syntax.Alarms[0].Name = "Changed after capture";
            syntax.Alarms[1].Properties.Clear();

            AlarmSyntaxTree roundTrip = new AlmxTextSnapshot(source.Definition, source.Name, 0).Parse();
            Assert.True(roundTrip.Alarms[0].IsAbstract);
            Assert.Equal("$(Part)", roundTrip.Alarms[0].Name);
            Assert.Null(roundTrip.Alarms[1].Name);
            Assert.Equal("Base", roundTrip.Alarms[1].ReferenceName);
            Assert.Equal("", roundTrip.Alarms[1].Properties[0].Value);
            Assert.Null(roundTrip.Alarms[1].Properties[1].Value);
            Assert.Equal("file", Assert.Single(roundTrip.Properties).Value);
            ProcedureGroupSyntax group = Assert.Single(roundTrip.Alarms[1].TestProcedure.Groups);
            Assert.Null(group.Title);
            Assert.IsType<Clear>(group.Entries[0]);
            Assert.Equal("$(loc:Warning)", Assert.IsType<TestProcedureStepSyntax>(group.Entries[1]).Text);
            Assert.True(compiler.Compile(roundTrip).Success);
            Assert.Equal("$(Part)", Assert.Single(roundTrip.Alarms[1].ResolvedTestProcedures.Groups).Title);
        }

        [Theory]
        [InlineData("alarms/0", 3)]
        [InlineData("alarms/0/fields/Name", 5)]
        [InlineData("alarms/0/groups/0", 7)]
        [InlineData("alarms/0/groups/0/title", 8)]
        [InlineData("alarms/0/groups/0/entries/0", 9)]
        [InlineData("properties/0", 2)]
        public void ProvenanceKeepsOriginalSpansWithPortableNamesAndNoBufferVersion(string key, int line)
        {
            const string text = """
                <Alarmlist>
                  <Properties><Property Name="Part" Value="X"/></Properties>
                  <Alarm>
                    <FullyQualifiedName>A</FullyQualifiedName>
                    <Name>$(Part)</Name>
                    <TestProcedure>
                      <Group Name="Checks">
                        <Title>Inspect</Title>
                        <Clear/>
                      </Group>
                    </TestProcedure>
                  </Alarm>
                </Alarmlist>
                """;
            LibrarySource source = LibrarySource.Capture("sources/A.almx",
                new AlmxTextSnapshot(text, "C:/machine/checkout/A.almx", 999).Parse());
            SourceLocation location = Assert.Single(source.Locations.Where(item => item.Key == key)).Location;
            Assert.Equal(line, location.LineNumber);
            Assert.True(location.EndColumnNumber > location.ColumnNumber);
            Assert.All(source.Locations, item =>
            {
                Assert.Equal("sources/A.almx", item.Location.FilePath);
                Assert.Null(item.Location.SourceVersion);
            });
        }

        [Theory]
        [InlineData("/absolute.almx")]
        [InlineData("C:/absolute.almx")]
        [InlineData("../outside.almx")]
        [InlineData("folder/../outside.almx")]
        [InlineData("folder\\windows.almx")]
        [InlineData("folder//alarm.almx")]
        [InlineData("./alarm.almx")]
        [InlineData("")]
        public void ArtifactNamesCannotDependOnHostPathResolution(string name)
            => Assert.Throws<ArgumentException>(() => LibrarySource.Capture(name, new AlarmSyntaxTree()));
    }
}
