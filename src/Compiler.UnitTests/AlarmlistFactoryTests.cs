using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.Test;
using Alarmlist.Syntax;
using Alarmlist.Core;

namespace Alarmlist.Tests
{
    public class AlarmlistFactoryTests
    {
        [Fact]
        public void CreateAlarmFromSyntaxNodeReturnsCorrectAlarm()
        {

            var expected = TestHelper.CreateAlarmSyntaxNode("1");

            var actual = AlarmlistFactory.CreateAlarmFromSyntaxNode(expected);

            TestHelper.AssertAlarmEquals(expected, actual);
        }

        [Fact]
        public void CreateAlarmFromSyntaxNodeDoesNotEmitClearInstructions()
        {
            var reference = AlarmlistFactory.Alarm("Reference");
            reference.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Instruction, "Inherited instruction"));
            var alarm = AlarmlistFactory.Alarm("Alarm");
            alarm.TestProcedure.Instructions.Add(new Clear());
            alarm.TestProcedure.Instructions.Add(new TestProcedureStepSyntax(TestProcedureStepKind.Warning, "Local warning"));
            AlarmSyntaxNode.SetReference(alarm, reference);

            var actual = AlarmlistFactory.CreateAlarmFromSyntaxNode(alarm);

            Assert.Single(actual.TestProcedure.Instructions);
            Assert.Equal("Local warning", actual.TestProcedure.Instructions.Single().Text);
        }

        
    }
}
