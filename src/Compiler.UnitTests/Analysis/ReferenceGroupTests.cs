// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Analysis
{
    public class ReferenceGroupTests
    {
        private static AlmxTextSnapshot Input(string alarms) => new AlmxTextSnapshot("<Alarmlist>" + alarms + "</Alarmlist>", "groups.almx", 5);
        private static string Alarm(string name, string body) => "<Alarm><FullyQualifiedName>" + name + "</FullyQualifiedName>" + body + "</Alarm>";

        [Fact]
        public void GroupFamiliesFollowInheritanceAndExposeClearWithoutEditingEntryText()
        {
            var input = Input(Alarm("Base", "<TestProcedure><Group Name='Actions'><Title>Actions literal</Title><Step>Base</Step></Group></TestProcedure>")
                + Alarm("Child", "<ReferenceName>Base</ReferenceName><TestProcedure><Group Name='actions'><Clear/><Step>Actions literal</Step></Group></TestProcedure>")
                + Alarm("Other", "<TestProcedure><Group Name='Actions'><Step>Other</Step></Group></TestProcedure>"));
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            var groups = data.Symbols.Where(symbol => symbol.Kind == "Group").ToArray();
            Assert.Equal(groups[0].Family, groups[1].Family); Assert.NotEqual(groups[0].Family, groups[2].Family);
            Assert.Contains(data.Occurrences, use => use.Family == groups[0].Family && use.Role == "ClearControl" && use.Consumer == "Child");
            var preview = ReferenceCapture.Preview(new[] { input }, CompilationContext.Empty, groups[0].Id, "NewActions");
            Assert.True(preview.IsApplicable, preview.Failure); Assert.Equal(2, preview.Edits.Count);
            string text = input.Text;
            foreach (var edit in preview.Edits.OrderByDescending(edit => edit.Start)) text = edit.Apply(text);
            Assert.Contains("<Clear/>", text); Assert.Contains("<Title>Actions literal</Title>", text); Assert.Contains("<Step>Actions literal</Step>", text);
        }

        [Theory]
        [InlineData("Other")]
        [InlineData("bad name")]
        public void GroupCollisionsAndInvalidNamesDoNotApply(string newName)
        {
            var input = Input(Alarm("A", "<TestProcedure><Group Name='Actions'/><Group Name='Other'/></TestProcedure>"));
            var target = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty).Symbols.First(symbol => symbol.Kind == "Group");
            Assert.False(ReferenceCapture.Preview(new[] { input }, CompilationContext.Empty, target.Id, newName).IsApplicable);
        }

        [Theory]
        [InlineData("AlarmlistConfiguration", "WellKnown")]
        [InlineData("AlarmlistAlarmName", "DefinitionField")]
        public void WellKnownAndGeneratedNamesAreSearchableAndImmutable(string name, string kind)
        {
            var input = Input(Alarm("A", "<Name>Text</Name><Code>$(" + name + ")</Code>"));
            var data = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            var target = data.Symbols.Single(symbol => symbol.Kind == kind && symbol.Name == (kind == "DefinitionField" ? "Name" : name));
            Assert.True(target.IsReadOnly);
            Assert.Contains(data.Occurrences, use => use.Family == target.Family && use.Role == "BuiltIn" && use.Start != null);
            Assert.False(ReferenceCapture.Preview(new[] { input }, CompilationContext.Empty, target.Id, "Next").IsApplicable);
            if (kind == "DefinitionField") Assert.Contains(data.Occurrences, use => use.Family == target.Family && use.Role == "DefinitionUse"
                && input.Text.Substring(use.Start.Value, use.Length.Value) == "Name");
        }
    }
}
