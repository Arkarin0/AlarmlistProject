// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class PropertyAnalysisTests
    {
        [Fact]
        public void RawExpressionsEvaluatedValuesAndDistinctPropertyOriginsAreDetached()
        {
            var inputs = new[]
            {
                new AlmxTextSnapshot("<Alarmlist><Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName>"
                    + "<Properties><Property Name='p' Value='base'/></Properties><Code>$(p)</Code>"
                    + "<Description>Inspect $(AlarmlistAlarmCode)</Description><TestProcedure><Instructions>"
                    + "<Step>Inspect $(AlarmlistAlarmCode) $$(p)</Step></Instructions></TestProcedure></Alarm></Alarmlist>", "Base.almx", 1),
                new AlmxTextSnapshot("<Alarmlist><Properties><Property Name='p' Value='001'/></Properties>"
                    + "<Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName></Alarm></Alarmlist>", "Child.almx", 2)
            };
            var compiler = new AlarmCompiler();
            AnalysisResult result = compiler.AnalyzeSnapshots(inputs, CompilationContext.Empty, true);
            Assert.True(result.Success);
            Assert.True(result.Alarms[0].IsAbstract);
            Assert.False(result.Alarms[0].HasEvaluatedValues);
            Assert.Null(result.Alarms[0].Fields.Single(field => field.Name == "Code").EffectiveValue);
            AnalyzedAlarm child = result.Alarms[1];
            Assert.True(child.HasEvaluatedValues);
            AnalyzedField code = child.Fields.Single(field => field.Name == "Code");
            Assert.Equal("$(p)", code.EffectiveExpression);
            Assert.Equal("001", code.EffectiveValue);
            Assert.True(code.IsValueAvailable);
            Assert.True(code.IsInherited);
            AnalyzedProperty parameter = child.Properties.Single(property => property.Name == "p");
            Assert.Equal("A", parameter.DeclarationOrigin.Alarm.FullyQualifiedName);
            Assert.Equal("Alarm", parameter.DeclarationOrigin.Scope);
            Assert.Equal("File", parameter.ValueOrigin.Scope);
            Assert.Equal("Child.almx", parameter.ValueOrigin.Location.FilePath);
            Assert.Equal(2, parameter.ValueOrigin.Location.SourceVersion);
            Assert.False(parameter.IsLocallyDeclared);
            Assert.False(parameter.HasLocalValue);
            Assert.Equal("001", parameter.EffectiveValue);
            AnalyzedProcedureEntry step = Assert.Single(child.Instructions.EffectiveEntries);
            Assert.Equal("Inspect $(AlarmlistAlarmCode) $$(p)", step.RawText);
            Assert.Equal("Inspect 001 $(p)", step.Text);
            Assert.True(step.IsValueAvailable);
            Assert.Equal("001", child.Properties.Single(property => property.Name == "AlarmlistAlarmCode").EffectiveValue);
            Assert.Contains("<Description>Inspect 001</Description>", result.CompiledXml);
            Assert.DoesNotContain("<FullyQualifiedName>A</FullyQualifiedName>", result.CompiledXml);

            inputs[1] = new AlmxTextSnapshot(inputs[1].Text.Replace("001", "002"), "Child.almx", 3);
            Assert.Equal("002", compiler.AnalyzeSnapshots(inputs).Alarms[1].Fields.Single(field => field.Name == "Code").EffectiveValue);
            Assert.Equal("001", code.EffectiveValue);
            Assert.Equal("001", parameter.EffectiveValue);
        }

        [Fact]
        public void FileOnlyScopesExposeProjectFallbackAndExplicitEmptyAssignments()
        {
            var context = new CompilationContext(properties: new[] { new PropertyDefinition("p", "project"), new PropertyDefinition("unassigned") });
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot(
                "<Alarmlist><Properties><Property Name='p'/><Property Name='empty' Value=''/></Properties></Alarmlist>", "Empty.almx", 5) }, context);
            Assert.True(result.Success);
            Assert.Empty(result.Alarms);
            AnalyzedProperty property = result.Sources[0].Properties.Single(item => item.Name == "p");
            Assert.True(property.IsLocallyDeclared);
            Assert.False(property.HasLocalValue);
            Assert.Equal("File", property.DeclarationOrigin.Scope);
            Assert.Equal("Project", property.ValueOrigin.Scope);
            Assert.Null(property.ValueOrigin.Location);
            Assert.Equal("project", property.EffectiveValue);
            AnalyzedProperty empty = result.Sources[0].Properties.Single(item => item.Name == "empty");
            Assert.True(empty.HasLocalValue);
            Assert.True(empty.IsValueAvailable);
            Assert.Equal("", empty.EffectiveValue);
            Assert.False(result.Sources[0].Properties.Single(item => item.Name == "unassigned").IsValueAvailable);
        }

        [Fact]
        public void ExpressionFailureDoesNotImplyBrokenReferenceChainOrExposeRawAsAValue()
        {
            AnalysisResult result = new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot(
                "<Alarmlist><Alarm><Name>$(AlarmlistAlarmName)</Name><Code>OK</Code>"
                + "<TestProcedure><Instructions><Step>$(AlarmlistAlarmName)</Step></Instructions></TestProcedure></Alarm></Alarmlist>", "Input.almx", 4) }, true);
            AnalyzedAlarm alarm = Assert.Single(result.Alarms);
            Assert.True(alarm.IsResolved);
            Assert.False(alarm.HasEvaluatedValues);
            AnalyzedField name = alarm.Fields.Single(field => field.Name == "Name");
            Assert.Equal("$(AlarmlistAlarmName)", name.EffectiveExpression);
            Assert.False(name.IsValueAvailable);
            Assert.Null(name.EffectiveValue);
            Assert.Equal("OK", alarm.Fields.Single(field => field.Name == "Code").EffectiveValue);
            Assert.Null(Assert.Single(alarm.Instructions.EffectiveEntries).Text);
            Assert.Equal("$(AlarmlistAlarmName)", Assert.Single(alarm.Instructions.LocalEntries).RawText);
            Assert.Null(result.CompiledXml);
            Assert.Equal("ALM0010", Assert.Single(result.Diagnostics).Id);
        }
    }
}
