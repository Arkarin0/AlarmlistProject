// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Evaluation
{
    public class ExpressionTests
    {
        [Theory]
        [InlineData("$(item:Count)")]
        [InlineData("$(loc:Label, value=$(item:Count))")]
        public void RecordReferencesRequireExplicitExpressionContext(string raw)
        {
            Assert.Null(Alarmlist.Evaluation.PropertyExpression.Parse(raw));
            Assert.NotNull(Alarmlist.Evaluation.PropertyExpression.Parse(raw, allowItemReferences: true));
        }

        [Fact]
        public void ExchangeProtectsRecordReferencesButNotEscapedTokens()
        {
            const string raw = "x $(item:Count) $$(item:Count)";
            var span = Assert.Single(Alarmlist.Evaluation.PropertyExpression.ProtectedSpans(raw));
            Assert.Equal("$(item:Count)", raw.Substring(span.Start, span.Length));
            Assert.Equal(2, span.Start);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("1", true)]
        [InlineData(" false ", false)]
        [InlineData("0", false)]
        public void AbstractIsLocalAndDoesNotFlowToChildren(string value, bool isAbstract)
        {
            CompilationResult result = Compile("<Alarm abstract='" + value + "'><FullyQualifiedName>A</FullyQualifiedName><Code>001</Code></Alarm>"
                + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm>");
            Assert.True(result.Success);
            Assert.Equal(isAbstract ? new[] { "B" } : new[] { "A", "B" }, result.AlarmList.Select(alarm => alarm.FullyQualifiedName));
            Assert.All(result.AlarmList, alarm => Assert.Equal("001", alarm.Code));
        }

        [Theory]
        [InlineData("yes")]
        [InlineData("True")]
        [InlineData("")]
        public void InvalidAbstractIsLocatedEvenOnEmptyAlarm(string value)
        {
            Diagnostic diagnostic = Assert.Single(Compile("<Alarm abstract='" + value + "' />").Diagnostics);
            Assert.Equal("ALM0005", diagnostic.Id);
            Assert.Equal(7, diagnostic.Location.SourceVersion);
        }

        [Fact]
        public void AbstractValuesCanBeUnresolvedButConcreteConsumersMustResolveThem()
        {
            const string template = "<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Code>$(missing)</Code></Alarm>";
            CompilationResult abstractOnly = Compile(template);
            Assert.True(abstractOnly.Success);
            Assert.Empty(abstractOnly.AlarmList);
            CompilationResult child = Compile(template + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm>");
            Diagnostic diagnostic = Assert.Single(child.Diagnostics);
            Assert.Equal("ALM0007", diagnostic.Id);
            Assert.Contains("'B'", diagnostic.Message);
        }

        [Theory]
        [InlineData("$(")]
        [InlineData("$()")]
        [InlineData("$(File.Code)")]
        [InlineData("$(Name.Trim())")]
        [InlineData("$(has space)")]
        public void MalformedExpressionsFailEvenOnUnusedTemplates(string expression)
        {
            Diagnostic diagnostic = Assert.Single(Compile("<Alarm abstract='true'><Name>" + expression + "</Name></Alarm>").Diagnostics);
            Assert.Equal("ALM0006", diagnostic.Id);
            Assert.Equal(7, diagnostic.Location.SourceVersion);
        }

        [Fact]
        public void ResolvedFieldsAreDependenciesButInsertedCustomValuesAreLiteral()
        {
            var context = new CompilationContext(properties: new[] { new PropertyDefinition("par1", "001"), new PropertyDefinition("literal", "$(par1) $$") });
            CompilationResult result = Compile("<Alarm><Code>$(PAR1)</Code><Description>Inspect $(AlarmlistAlarmCode)</Description>"
                + "<Name>$(literal)</Name><Category>$(AlarmlistAlarmName)</Category></Alarm>", context);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.True(result.Success);
            Assert.Equal("Inspect 001", alarm.Description);
            Assert.Equal("$(par1) $$", alarm.Name);
            Assert.Equal(alarm.Name, alarm.Category);
        }

        [Fact]
        public void EscapesAreProcessedExactlyOnceAndIdentitiesAreLiteral()
        {
            CompilationResult result = Compile("<Alarm><FullyQualifiedName>$(literal)</FullyQualifiedName>"
                + "<Name>$$(missing) / $$ / $$$$(literal) / $ / $x</Name>"
                + "<Description>$(AlarmlistAlarmFullyQualifiedName)|$(AlarmlistAlarmReferenceName)|$(AlarmlistAlarmCode)</Description></Alarm>");
            Assert.True(result.Success);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.Equal("$(missing) / $ / $$(literal) / $ / $x", alarm.Name);
            Assert.Equal("$(literal)||", alarm.Description);
        }

        [Theory]
        [InlineData("<Name>$(AlarmlistAlarmName)</Name>", "Name -> Name")]
        [InlineData("<Name>$(AlarmlistAlarmCode)</Name><Code>$(AlarmlistAlarmDescription)</Code><Description>$(AlarmlistAlarmName)</Description>", "Name -> Code -> Description -> Name")]
        public void ScalarCyclesFailWithoutPublishingRawFallbacks(string fields, string trace)
        {
            CompilationResult result = Compile("<Alarm><FullyQualifiedName>A</FullyQualifiedName>" + fields + "</Alarm>");
            Diagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("ALM0010", diagnostic.Id);
            Assert.Contains(trace, diagnostic.Message);
            Assert.Empty(result.AlarmList);
            Assert.False(result.Success);
        }

        [Fact]
        public void OverrideCanBreakATemplateCycleAndClearedProcedureValuesAreNotEvaluated()
        {
            CompilationResult result = Compile("<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Name>$(AlarmlistAlarmCode)</Name>"
                + "<Code>$(AlarmlistAlarmName)</Code><TestProcedure><Group Name='Instructions'><Step>$(missing)</Step></Group></TestProcedure></Alarm>"
                + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName><Code>001</Code>"
                + "<TestProcedure><Group Name='Instructions'><Clear/><Hint>Inspect $(AlarmlistAlarmName)</Hint><Warning>$$</Warning></Group>"
                + "<Group Name='Reset'><Note>$(AlarmlistAlarmCode)</Note><Step>Reset</Step></Group></TestProcedure></Alarm>");
            Assert.True(result.Success);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.Equal("001", alarm.Name);
            Assert.Equal(new[] { "Inspect 001", "$" }, alarm.TestProcedure.Groups.Single(group => group.Name == "Instructions").Entries.Select(step => step.Text));
            Assert.Equal(new[] { TestProcedureItemKind.Hint, TestProcedureItemKind.Warning }, alarm.TestProcedure.Groups.Single(group => group.Name == "Instructions").Entries.Select(step => step.Kind));
            Assert.Equal(new[] { "001", "Reset" }, alarm.TestProcedure.Groups.Single(group => group.Name == "Reset").Entries.Select(step => step.Text));
        }

        [Fact]
        public void InheritedExpressionsUseTheConsumingFileAndAlarm()
        {
            string root = Path.GetPathRoot(Path.GetFullPath("."));
            string basePath = Path.Combine(root, "templates", "Base.almx");
            string childPath = Path.Combine(root, "inputs", "Child.almx");
            var context = new CompilationContext(Path.Combine(root, "projects", "Plant.almproj"), "Debug", "AnyCPU");
            CompilationResult result = new AlarmCompiler().CompileSnapshots(new[]
            {
                new AlmxTextSnapshot("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName>"
                    + "<Name>$(AlarmlistThisFileName):$(AlarmlistAlarmFullyQualifiedName)</Name>"
                    + "<Code>$(AlarmlistProjectName)/$(AlarmlistConfiguration)/$(AlarmlistPlatform)</Code>"
                    + "<Description>$(AlarmlistThisFile)|$(AlarmlistThisFileExtension)|$(AlarmlistThisFileDirectory)|$(AlarmlistThisFileFullPath)</Description>"
                    + "<Category>$(AlarmlistProjectFile)|$(AlarmlistProjectExtension)|$(AlarmlistProjectDirectory)|$(AlarmlistProjectFullPath)</Category></Alarm></Alarmlist>", basePath, 1),
                new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm></Alarmlist>", childPath, 2)
            }, context);
            Assert.True(result.Success);
            Alarm alarm = Assert.Single(result.AlarmList);
            Assert.Equal("Child:B", alarm.Name);
            Assert.Equal("Plant/Debug/AnyCPU", alarm.Code);
            Assert.Equal("Child.almx|.almx|" + Path.GetDirectoryName(childPath) + "|" + childPath, alarm.Description);
            Assert.Equal("Plant.almproj|.almproj|" + Path.GetDirectoryName(context.ProjectPath) + "|" + context.ProjectPath, alarm.Category);
        }

        [Theory]
        [InlineData("AlarmlistProjectFullPath")]
        [InlineData("AlarmlistConfiguration")]
        [InlineData("AlarmlistPlatform")]
        [InlineData("AlarmlistThisFileFullPath")]
        public void ContextIsRequiredOnlyWhenRead(string name)
        {
            Assert.True(Compile("<Alarm><Name>literal</Name></Alarm>").Success);
            Diagnostic diagnostic = Assert.Single(Compile("<Alarm><Name>$(" + name + ")</Name></Alarm>").Diagnostics);
            Assert.Equal("ALM0009", diagnostic.Id);
        }

        [Fact]
        public void ContextIsDefensivelyCopiedAndEmptyDiffersFromUnassigned()
        {
            var properties = new[] { new PropertyDefinition("par1", ""), new PropertyDefinition("unused") };
            var context = new CompilationContext(properties: properties);
            properties[0] = new PropertyDefinition("par1", "modified");
            CompilationResult empty = Compile("<Alarm><Name>A$(par1)B</Name></Alarm>", context);
            Assert.True(empty.Success);
            Assert.Equal("AB", Assert.Single(empty.AlarmList).Name);
            Assert.Equal("ALM0008", Assert.Single(Compile("<Alarm><Name>$(unused)</Name></Alarm>", context).Diagnostics).Id);
        }

        [Fact]
        public void SourceSerializationPreservesRawLocalValuesAndAbstract()
        {
            var snapshots = new[] { Snapshot("<Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Name>$$(raw)</Name></Alarm>"
                + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm>") };
            var binder = new Alarmlist.Binding.Binder();
            binder.AddSourceTexts(snapshots.Select(snapshot => snapshot.CreateSourceText()));
            var tree = binder.Bind().SyntaxTree;
            var output = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(output)) AlmxFile.WriteAlarmlistNode(tree, writer);
            var parsed = new AlmxTextSnapshot(output.ToString(), "Roundtrip.almx", 0).Parse();
            Assert.True(parsed.Alarms[0].IsAbstract);
            Assert.Equal("$$(raw)", parsed.Alarms[0].Name);
            Assert.Null(parsed.Alarms[1].Name);
        }

        [Fact]
        public void SyntaxTreeCompilationBindsReferencesAndReportsInvalidChains()
        {
            var tree = Snapshot("<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm>").Parse();
            CompilationResult result = new AlarmCompiler().Compile(tree);
            Assert.Equal("ALM0002", Assert.Single(result.Diagnostics).Id);
            Assert.Empty(result.AlarmList);
        }

        [Fact]
        public void InvalidContextPathsDoNotTriggerDiscoveryOrFailWhenUnused()
        {
            var context = new CompilationContext("invalid\0path");
            Assert.True(Compile("<Alarm><Name>Literal</Name></Alarm>", context).Success);
            Assert.Equal("ALM0009", Assert.Single(Compile("<Alarm><Name>$(AlarmlistProjectFullPath)</Name></Alarm>", context).Diagnostics).Id);
        }

        [Fact]
        public void AnalysisReportsEvaluationErrorsAndSuppressesPreview()
        {
            var result = new AlarmCompiler().AnalyzeSnapshots(new[] { Snapshot("<Alarm><Name>$(missing)</Name></Alarm>") }, true);
            Assert.False(result.Success);
            Assert.Equal("ALM0007", Assert.Single(result.Diagnostics).Id);
            Assert.Null(result.CompiledXml);
        }

        private static CompilationResult Compile(string alarms, CompilationContext context = null)
            => new AlarmCompiler().CompileSnapshots(new[] { Snapshot(alarms) }, context ?? CompilationContext.Empty);

        private static AlmxTextSnapshot Snapshot(string alarms) => new AlmxTextSnapshot("<Alarmlist>" + alarms + "</Alarmlist>", "Test.almx", 7);
    }
}
