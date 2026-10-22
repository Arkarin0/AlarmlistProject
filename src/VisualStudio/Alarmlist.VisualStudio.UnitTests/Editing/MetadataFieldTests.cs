// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.UI;
using Alarmlist.VisualStudio.Editing;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class MetadataFieldTests
    {
        [Fact]
        public void NewlyAuthoredFieldsFollowDefinitionOrderAndPreserveExistingMarkup()
        {
            var editing = new AlmxEditingService();
            string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><!--keep--><Description>Text</Description><TestProcedure/></Alarm></Alarmlist>";
            var projection = editing.Parse(source);
            string result = editing.SetField(projection, projection.Alarms[0], "Code", "$(Value)").Apply(source);
            Assert.Contains("<!--keep-->", result);
            Assert.Contains("<Description>Text</Description><TestProcedure/>", result);
            Assert.True(result.IndexOf("<Code>", StringComparison.Ordinal) < result.IndexOf("<Description>", StringComparison.Ordinal));
            Assert.Equal("$(Value)", editing.Parse(result).Alarms[0].GetValue("Code"));
        }
        [Theory]
        [InlineData("String")]
        [InlineData("Int64")]
        [InlineData("Decimal")]
        [InlineData("Double")]
        [InlineData("Boolean")]
        [InlineData("Enum")]
        [InlineData("Date")]
        [InlineData("Time")]
        [InlineData("Timestamp")]
        public void MetadataFieldsPreserveExpressionsAcrossAnalysisAndExplicitLiteralActions(string type)
        {
            int changes = 0;
            var descriptor = new AlarmFieldDescriptor("Custom", type, true, null, null, null, "Custom label", "Help text",
                false, true, 120, new[] { "First", "Second" }, new[] { "First label", "Second label" });
            var field = new AlarmFieldViewModel("Custom", () => changes++, descriptor);
            field.Load("$(Parameter)", true);
            field.SetAnalysis(new AnalysisField("Custom", true, "$(Parameter)", null, "value", null, null, false, "$(Parameter)", type: type));
            Assert.True(field.IsScalar);
            Assert.Equal("Custom label", field.Label);
            Assert.Contains("Help text", field.EditHelp);
            Assert.False(field.Multiline);
            Assert.Equal("$(Parameter)", field.EditValue);
            Assert.False(field.HasChanges);
            Assert.Equal(0, changes);
            field.EditValue = "$(Other)";
            field.SetAnalysis(null, false);
            Assert.Equal("$(Other)", field.EditValue);
            Assert.True(field.HasChanges);
            Assert.Equal(1, changes);
            if (type == "Boolean" || type == "Enum")
            {
                Assert.Equal(2, field.LiteralChoices.Count);
                field.LiteralChoices.Last().ApplyCommand.Execute(null);
                Assert.Equal(type == "Boolean" ? "false" : "Second", field.EditValue);
                Assert.Equal(2, changes);
            }
            else Assert.Empty(field.LiteralChoices);
        }

        [Fact]
        public void ExplicitLiteralChoiceCanOverrideAnIdenticalInheritedValue()
        {
            int changes = 0;
            var descriptor = new AlarmFieldDescriptor("Enabled", "Boolean", false, null, null, null, "Enabled", null,
                false, true, 120, Array.Empty<string>(), Array.Empty<string>());
            var field = new AlarmFieldViewModel("Enabled", () => changes++, descriptor);
            field.Load(null, false);
            field.SetAnalysis(new AnalysisField("Enabled", false, null, null, "true", null, null, true, "true", type: "Boolean"));
            Assert.Equal("true", field.EditValue);
            Assert.False(field.IsPresent);
            field.LiteralChoices[0].ApplyCommand.Execute(null);
            Assert.True(field.IsPresent);
            Assert.True(field.HasChanges);
            Assert.False(field.IsInherited);
            Assert.Equal("true", field.Value);
            Assert.Equal(1, changes);
        }

        [Fact]
        public void DefaultDeferredAndInvalidStatesStayCompilerOwnedAndNeverCreateSource()
        {
            var field = new AlarmFieldViewModel("Code", () => throw new Exception("Presentation must not edit source"));
            field.Load(null, false);
            field.SetAnalysis(new AnalysisField("Code", false, null, null, "7", null, null, false, isDefault: true));
            Assert.Contains("Definition default", field.ValueStatus);
            Assert.Equal("", field.EditValue);
            Assert.False(field.IsPresent);
            Assert.False(field.HasChanges);
            field.SetAnalysis(new AnalysisField("Code", false, null, null, "$(loc:Key)", null, null, false, state: AnalysisFieldValueState.Deferred));
            Assert.Contains("deferred", field.ValueStatus);
            field.SetAnalysis(new AnalysisField("Code", false, null, null, null, null, null, false, state: AnalysisFieldValueState.Invalid));
            Assert.Contains("Invalid", field.ValueStatus);
            Assert.False(field.HasChanges);
        }
    }
}
