// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Syntax;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class SnapshotAnalysisTests
    {
        [Fact]
        public void FieldsRetainLocalPresenceAndActualOriginThroughMultipleReferences()
        {
            AlmxTextSnapshot[] inputs =
            {
                Snapshot("Base", "<Name>Shared</Name><Code>B</Code><Description>Base description</Description>", "Base.almx", 1),
                Snapshot("Middle", "<Code>B</Code><Category> </Category>", "Middle.almx", 2, "Base"),
                Snapshot("Child", "<Name/>", "Child.almx", 3, "Middle"),
                new AlmxTextSnapshot("<Alarmlist/>", "Empty.almx", 4)
            };

            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(inputs);

            Assert.True(result.Success);
            Assert.Equal(1, result.ContractVersion);
            Assert.Equal(inputs.Select(input => input.FilePath), result.Sources.Select(source => source.FilePath));
            Assert.Equal(new[] { 1, 2, 3, 4 }, result.Sources.Select(source => source.Version));
            AnalyzedAlarm child = result.Alarms[2];
            Assert.True(child.IsResolved);
            Assert.Equal("Middle", child.ReferenceName);
            Assert.Equal(new[] { "Middle", "Base" }, child.ReferenceChain.Select(origin => origin.FullyQualifiedName));
            AnalyzedField name = Field(child, "Name");
            Assert.True(name.IsPresent);
            Assert.Equal(string.Empty, name.LocalValue);
            Assert.Equal("Shared", name.EffectiveValue);
            Assert.True(name.IsInherited);
            Assert.Same(result.Alarms[0].Origin, name.DefiningAlarm);
            Assert.Equal("Child.almx", name.LocalLocation.FilePath);
            Assert.Equal(3, name.LocalLocation.SourceVersion);
            Assert.Equal("Base.almx", name.ValueLocation.FilePath);
            Assert.Equal(1, name.ValueLocation.SourceVersion);
            AnalyzedField code = Field(child, "Code");
            Assert.False(code.IsPresent);
            Assert.Null(code.LocalValue);
            Assert.Equal("B", code.EffectiveValue);
            Assert.Same(result.Alarms[1].Origin, code.DefiningAlarm);
            Assert.False(Field(result.Alarms[1], "Code").IsInherited);
            Assert.Equal(" ", Field(child, "Category").EffectiveValue);
            Assert.Null(Field(result.Alarms[0], "Category").DefiningAlarm);
            AssertMatchesCompilation(inputs, result);
        }

        [Fact]
        public void EqualProcedureTextRetainsDistinctOriginsAndLocalPositions()
        {
            AlmxTextSnapshot[] inputs =
            {
                Snapshot("Base", Procedure("<Step>Same</Step><Step>Same</Step>"), "Base.almx", 6),
                Snapshot("Middle", "", "Middle.almx", 7, "Base"),
                Snapshot("Child", Procedure("<Step>Same</Step>"), "Child.almx", 8, "Middle")
            };

            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(inputs);
            AnalyzedProcedureSection instructions = result.Alarms[2].Instructions;

            Assert.Equal(3, instructions.EffectiveEntries.Count);
            Assert.Equal(new[] { 0, 1, 0 }, instructions.EffectiveEntries.Select(entry => entry.Origin.Index));
            Assert.Equal(new[] { "Base", "Base", "Child" }, instructions.EffectiveEntries.Select(entry => entry.Origin.Alarm.FullyQualifiedName));
            Assert.Equal(new[] { true, true, false }, instructions.EffectiveEntries.Select(entry => entry.IsInherited));
            Assert.All(instructions.EffectiveEntries, entry => Assert.Equal("Same", entry.Text));
            Assert.NotSame(instructions.EffectiveEntries[0].Origin, instructions.EffectiveEntries[1].Origin);
            Assert.Equal(8, Assert.Single(instructions.LocalEntries).Origin.Location.SourceVersion);
            Assert.Empty(instructions.ExcludedEntries);
            AssertMatchesCompilation(inputs, result);
        }

        [Fact]
        public void ClearExplanationsSurviveMultipleLevelsAndDoNotAffectReset()
        {
            AlmxTextSnapshot[] inputs =
            {
                Snapshot("Base", Procedure("<Step>Old</Step><Clear/><Hint>Base alive</Hint>", "<Step>Base reset</Step>"), "Base.almx", 1),
                Snapshot("Middle", Procedure("<Warning>Before</Warning><Clear/><Note>Middle alive</Note>", "<Note>Middle reset</Note>"), "Middle.almx", 2, "Base"),
                Snapshot("Child", Procedure("<Step>Child before</Step><Clear/><Warning>Discarded</Warning><Clear/><Step>Final</Step>", "<Hint>Child reset</Hint>"), "Child.almx", 3, "Middle")
            };

            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(inputs);
            AnalyzedAlarm child = result.Alarms[2];
            AnalyzedProcedureSection instructions = child.Instructions;

            AnalyzedProcedureEntry final = Assert.Single(instructions.EffectiveEntries);
            Assert.Equal("Final", final.Text);
            Assert.Equal(4, final.Origin.Index);
            Assert.False(final.IsInherited);
            Assert.Equal(new[] { "Old", "Base alive", "Before", "Middle alive", "Child before", "Discarded" },
                instructions.ExcludedEntries.Select(entry => entry.Text));
            Assert.Equal(new[] { "Base", "Middle", "Middle", "Child", "Child", "Child" },
                instructions.ExcludedEntries.Select(entry => entry.ExcludedBy.Alarm.FullyQualifiedName));
            Assert.Equal(new[] { 1, 1, 1, 1, 1, 3 }, instructions.ExcludedEntries.Select(entry => entry.ExcludedBy.Index));
            Assert.All(instructions.ExcludedEntries, entry =>
            {
                Assert.False(entry.IsClear);
                Assert.Equal("Instructions", entry.ExcludedBy.Section);
                Assert.NotNull(entry.ExcludedBy.Location);
            });
            Assert.Equal(5, instructions.LocalEntries.Count);
            Assert.True(instructions.LocalEntries[1].IsClear);
            Assert.Null(instructions.LocalEntries[1].Text);
            Assert.Same(instructions.LocalEntries[1].Origin, instructions.LocalEntries[0].ExcludedBy);
            Assert.Equal(new[] { "Base reset", "Middle reset", "Child reset" }, child.Reset.EffectiveEntries.Select(entry => entry.Text));
            Assert.Empty(child.Reset.ExcludedEntries);
            AssertMatchesCompilation(inputs, result);
        }

        [Fact]
        public void EndingWithClearProducesAnEmptyEffectiveSectionAndKeepsTheDirective()
        {
            AlmxTextSnapshot[] inputs =
            {
                Snapshot("Base", Procedure("<Step>Inherited</Step>"), "Base.almx", 0),
                Snapshot("Child", Procedure("<Clear/>"), "Child.almx", 1, "Base")
            };
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(inputs);
            AnalyzedProcedureSection section = result.Alarms[1].Instructions;

            Assert.Empty(section.EffectiveEntries);
            Assert.True(Assert.Single(section.LocalEntries).IsClear);
            AnalyzedProcedureEntry excluded = Assert.Single(section.ExcludedEntries);
            Assert.True(excluded.IsInherited);
            Assert.Same(section.LocalEntries[0].Origin, excluded.ExcludedBy);
            AssertMatchesCompilation(inputs, result);
        }

        [Theory]
        [InlineData("missing", "ALM0002")]
        [InlineData("self", "ALM0003")]
        [InlineData("cycle", "ALM0004")]
        [InlineData("duplicate", "ALM0001")]
        public void InvalidReferenceChainsExposeLocalDataWithoutMisleadingEffectiveValues(string scenario, string id)
        {
            var inputs = new List<AlmxTextSnapshot>
            {
                Snapshot("Child", "<Name>Local</Name>" + Procedure("<Step>Local step</Step>"), "Child.almx", 3, "A"),
                Snapshot("Good", "<Name>Independent</Name>", "Good.almx", 4)
            };
            switch (scenario)
            {
                case "missing": inputs.Add(Snapshot("A", "", "A.almx", 0, "Missing")); break;
                case "self": inputs.Add(Snapshot("A", "", "A.almx", 0, "A")); break;
                case "cycle":
                    inputs.Add(Snapshot("A", "", "A.almx", 0, "B"));
                    inputs.Add(Snapshot("B", "", "B.almx", 0, "A"));
                    break;
                case "duplicate":
                    inputs.Add(Snapshot("A", "<Name>First</Name>", "First.almx", 0));
                    inputs.Add(Snapshot("A", "<Name>Second</Name>", "Second.almx", 0));
                    break;
            }

            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(inputs);

            Assert.False(result.Success);
            Assert.Equal(id, Assert.Single(result.Diagnostics).Id);
            Assert.NotNull(result.Diagnostics[0].Location);
            AnalyzedAlarm child = result.Alarms[0];
            Assert.False(child.IsResolved);
            Assert.Empty(child.ReferenceChain);
            Assert.Equal("Local", Field(child, "Name").LocalValue);
            Assert.True(Field(child, "Name").IsPresent);
            Assert.All(child.Fields, field =>
            {
                Assert.Null(field.EffectiveValue);
                Assert.Null(field.DefiningAlarm);
                Assert.False(field.IsInherited);
            });
            Assert.Equal("Local step", Assert.Single(child.Instructions.LocalEntries).Text);
            Assert.Empty(child.Instructions.EffectiveEntries);
            Assert.Empty(child.Instructions.ExcludedEntries);
            Assert.True(result.Alarms[1].IsResolved);
            Assert.Equal("Independent", Field(result.Alarms[1], "Name").EffectiveValue);
        }

        [Fact]
        public void UnnamedDeclarationsHaveDifferentOriginsEvenInTheSameFile()
        {
            var snapshot = new AlmxTextSnapshot("<Alarmlist><Alarm><Name>One</Name></Alarm><Alarm><Name>Two</Name></Alarm></Alarmlist>", "Same.almx", 2);
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { snapshot });

            Assert.True(result.Success);
            Assert.All(result.Alarms, alarm => Assert.Null(alarm.Origin.FullyQualifiedName));
            Assert.NotEqual(result.Alarms[0].Origin.Index, result.Alarms[1].Origin.Index);
            Assert.NotEqual(result.Alarms[0].Origin.Location.ColumnNumber, result.Alarms[1].Origin.Location.ColumnNumber);
            Assert.Same(result.Alarms[1].Origin, Field(result.Alarms[1], "Name").DefiningAlarm);
        }

        [Fact]
        public void ProcedureLocationsPreserveNamespacesVersionsAndSourceOrderWhileSkippingUnknownNodes()
        {
            const string text = "<a:Alarmlist xmlns:a='urn:alarms'>\r\n  <a:Alarm>\r\n    <a:TestProcedure>\r\n"
                + "      <a:Instructions>\r\n        <a:Unknown><a:Step>Ignore</a:Step></a:Unknown>\r\n"
                + "        <a:Step>Same</a:Step>\r\n        <a:Clear/>\r\n        <a:Note>Last</a:Note>\r\n"
                + "      </a:Instructions>\r\n    </a:TestProcedure>\r\n  </a:Alarm>\r\n</a:Alarmlist>";
            AnalyzedProcedureSection section = new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot(text, "Namespaced.almx", 19) }).Alarms[0].Instructions;

            Assert.Equal(3, section.LocalEntries.Count);
            Assert.Equal(new[] { 6, 7, 8 }, section.LocalEntries.Select(entry => entry.Origin.Location.LineNumber));
            Assert.All(section.LocalEntries, entry =>
            {
                Assert.Equal(10, entry.Origin.Location.ColumnNumber);
                Assert.Equal(19, entry.Origin.Location.SourceVersion);
                Assert.Equal("Namespaced.almx", entry.Origin.Location.FilePath);
            });
            Assert.Equal(10 + "a:Clear".Length, section.LocalEntries[1].Origin.Location.EndColumnNumber);
            Assert.Equal("Last", Assert.Single(section.EffectiveEntries).Text);
            Assert.Equal("Same", Assert.Single(section.ExcludedEntries).Text);
        }

        [Fact]
        public void ResultsCannotBeMutatedAndLaterAnalysesDoNotChangeEarlierResults()
        {
            var compiler = new AlarmCompiler();
            AlmxTextSnapshot[] inputs = { Snapshot("A", "<Name>Before</Name>" + Procedure("<Step>Before</Step>"), "A.almx", 1) };
            AnalysisResult before = compiler.AnalyzeSnapshots(inputs);
            inputs[0] = Snapshot("A", "<Name>After</Name>", "A.almx", 2);
            AnalysisResult after = compiler.AnalyzeSnapshots(inputs);

            Assert.Equal("Before", Field(before.Alarms[0], "Name").EffectiveValue);
            Assert.Equal("Before", Assert.Single(before.Alarms[0].Instructions.EffectiveEntries).Text);
            Assert.Equal(1, before.Sources[0].Version);
            Assert.Equal("After", Field(after.Alarms[0], "Name").EffectiveValue);
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedAlarm>)before.Alarms).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedField>)before.Alarms[0].Fields).RemoveAt(0));
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedProcedureEntry>)before.Alarms[0].Instructions.EffectiveEntries).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AnalyzedSource>)before.Sources).Clear());
        }

        [Theory]
        [InlineData("<Alarmlist><Alarm>")]
        [InlineData("<!DOCTYPE Alarmlist [<!ENTITY text 'Unsafe'>]><Alarmlist/>")]
        public void InvalidXmlNeverProducesAPartialAnalysisResult(string text)
        {
            Assert.Throws<XmlException>(() => new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot(text, "Invalid.almx", 0) }));
        }

        private static AlmxTextSnapshot Snapshot(string name, string fields, string path, int version, string reference = null)
        {
            return new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>" + name + "</FullyQualifiedName>"
                + (reference == null ? "" : "<ReferenceName>" + reference + "</ReferenceName>") + fields + "</Alarm></Alarmlist>", path, version);
        }

        private static string Procedure(string instructions, string reset = "")
        {
            return "<TestProcedure><Instructions>" + instructions + "</Instructions><Reset>" + reset + "</Reset></TestProcedure>";
        }

        private static AnalyzedField Field(AnalyzedAlarm alarm, string name) => alarm.Fields.Single(field => field.Name == name);

        private static void AssertMatchesCompilation(AlmxTextSnapshot[] inputs, AnalysisResult analysis)
        {
            CompilationResult compilation = new AlarmCompiler().CompileSnapshots(inputs);
            Assert.True(compilation.Success);
            for (int i = 0; i < compilation.AlarmList.Count; i++)
            {
                Alarm compiled = compilation.AlarmList[i];
                Assert.Equal(new[] { compiled.Name, compiled.Code, compiled.Category, compiled.Description }, analysis.Alarms[i].Fields.Select(field => field.EffectiveValue));
                Assert.Equal(compiled.TestProcedure.Instructions.Select(entry => entry.Text), analysis.Alarms[i].Instructions.EffectiveEntries.Select(entry => entry.Text));
                Assert.Equal(compiled.TestProcedure.Instructions.Select(entry => entry.Kind.ToString()), analysis.Alarms[i].Instructions.EffectiveEntries.Select(entry => entry.Kind.ToString()));
                Assert.Equal(compiled.TestProcedure.Reset.Select(entry => entry.Text), analysis.Alarms[i].Reset.EffectiveEntries.Select(entry => entry.Text));
                Assert.Equal(compiled.TestProcedure.Reset.Select(entry => entry.Kind.ToString()), analysis.Alarms[i].Reset.EffectiveEntries.Select(entry => entry.Kind.ToString()));
            }
        }
    }
}
