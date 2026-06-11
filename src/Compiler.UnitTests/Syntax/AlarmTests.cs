using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.Test;
using Alarmlist.Syntax;
using Alarmlist.Core;

namespace Alarmlist.Syntax.Tests
{
    public class AlarmTests
    {
        [Fact]
        public void SetReferenceResultsInNonNullRefecedInstance()
        {
            var alarm = AlarmlistFactory.Alarm();
            var reference = AlarmlistFactory.Alarm();

            AlarmSyntaxNode.SetReference(alarm, reference);

            Assert.Same(reference, alarm.Reference);
        }

        [Fact]
        public void SetReferenceUpdatesReferenceName()
        {
            var alarm = AlarmlistFactory.Alarm("alarm1");
            var reference = AlarmlistFactory.Alarm("refAlarm1");

            AlarmSyntaxNode.SetReference(alarm, reference);

            Assert.Equal(reference.FullyQualifiedName, alarm.ReferenceName);
        }

        [Fact]
        public void ChangingReferenceNameClearsResolvedReference()
        {
            var alarm = AlarmlistFactory.Alarm("alarm1");
            var reference = AlarmlistFactory.Alarm("refAlarm1");

            AlarmSyntaxNode.SetReference(alarm, reference);
            alarm.ReferenceName = "refAlarm2";

            Assert.Null(alarm.Reference);
        }

        [Fact]
        public void RemoveReferenceResultsInNullRefecedInstance()
        {
            var alarm = AlarmlistFactory.Alarm();
            var reference = AlarmlistFactory.Alarm();

            AlarmSyntaxNode.SetReference(alarm, reference);
            AlarmSyntaxNode.RemoveReference(alarm);

            Assert.Null(alarm.Reference);
        }

        [Fact]
        public void FQNIsNotinheritedFromTheReferencedInstance()
        {
            var alarm = AlarmlistFactory.Alarm();
            var reference = AlarmlistFactory.Alarm("refAlarm1");

            AlarmSyntaxNode.SetReference(alarm, reference);

            Assert.NotEqual(alarm.FullyQualifiedName, alarm.Reference.FullyQualifiedName);
        }

        [Fact]
        public void ReferencingItselfDoesNotCauseACyclicRaceSituation()
        {
            var alarm = AlarmlistFactory.Alarm("alarm1");
            var reference = AlarmlistFactory.Alarm("refAlarm2");

            AlarmSyntaxNode.SetReference(alarm, alarm);

            Assert.Null(alarm.Reference);            
        }

        [Fact]
        public void InstanceUsesReferencedPropertiesAsItsOwnProperties()
        {
            var alarm = AlarmlistFactory.Alarm();
            var reference = AlarmlistFactory.Alarm();
            TestHelper.FillData(reference, 1);

            AlarmSyntaxNode.SetReference(alarm, reference);

            Assert.Equal(alarm, reference, TestHelper.comparer);
        }

        [Fact]
        public void InstanceUsesReferencedPropertiesAndLocalProperties()
        {
            var alarm = AlarmlistFactory.Alarm();
            alarm.Name = "AlarmLocal";
            var reference = AlarmlistFactory.Alarm();
            TestHelper.FillData(reference, 1);

            AlarmSyntaxNode.SetReference(alarm, reference);

            Assert.Equal(alarm.Description, reference.Description);
            Assert.NotEqual(alarm.Name, reference.Name);
        }

        [Fact]
        public void InstanceUsesNoReferencedPropertiesAndLocalOnlyProperties()
        {
            var alarm = AlarmlistFactory.Alarm();
            TestHelper.FillData(alarm, 1);
            var reference = AlarmlistFactory.Alarm();
            TestHelper.FillData(reference, 2);

            AlarmSyntaxNode.SetReference(alarm, reference);

            Assert.NotEqual(alarm, reference, TestHelper.comparer);
        }

        [Fact]
        public void ResolvedTestProceduresReturnsInheritedStepsBeforeLocalSteps()
        {
            var alarm = AlarmlistFactory.Alarm("alarm1");
            var reference = AlarmlistFactory.Alarm("refAlarm1");
            var inheritedStep = new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Inherited instruction");
            var localStep = new TestProcedureStepSyntax(TestProcedureStepKind.Warning, "Local warning");
            reference.TestProcedure.Instructions.Add(inheritedStep);
            alarm.TestProcedure.Instructions.Add(localStep);

            AlarmSyntaxNode.SetReference(alarm, reference);

            var actual = alarm.ResolvedTestProcedures;

            Assert.Equal(new ITestProcedureItem[] { inheritedStep, localStep }, actual.Instructions);
        }

        [Fact]
        public void ResolvedTestProceduresResolvesResetAndInstructionsIndependently()
        {
            var alarm = AlarmlistFactory.Alarm("alarm1");
            var reference = AlarmlistFactory.Alarm("refAlarm1");
            var inheritedInstruction = new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Inherited instruction");
            var inheritedReset = new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Inherited reset");
            var localReset = new TestProcedureStepSyntax(TestProcedureStepKind.Note, "Local reset note");
            reference.TestProcedure.Instructions.Add(inheritedInstruction);
            reference.TestProcedure.Reset.Add(inheritedReset);
            alarm.TestProcedure.Instructions.Add(new Clear());
            alarm.TestProcedure.Reset.Add(localReset);

            AlarmSyntaxNode.SetReference(alarm, reference);

            var actual = alarm.ResolvedTestProcedures;

            Assert.Empty(actual.Instructions);
            Assert.Equal(new ITestProcedureItem[] { inheritedReset, localReset }, actual.Reset);
        }
    }
}
