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
        public void RemoveReferenceResultsInNullRefecedInstance()
        {
            var alarm = AlarmlistFactory.Alarm();
            var reference = AlarmlistFactory.Alarm();

            AlarmSyntaxNode.SetReference(alarm, reference);
            AlarmSyntaxNode.RemoveReference(alarm);

            Assert.Null(alarm.Reference);
        }

        [Fact]
        public void IDIsNotinheritedFromTheReferencedInstance()
        {
            var alarm = AlarmlistFactory.Alarm();
            var reference = AlarmlistFactory.Alarm();

            AlarmSyntaxNode.SetReference(alarm, reference);

            Assert.NotEqual(alarm.ID, alarm.Reference.ID);
        }

        [Fact]
        public void ReferencingItselfDoesNotCauseACyclicRaceSituation()
        {
            var alarm = AlarmlistFactory.Alarm();
            var reference = AlarmlistFactory.Alarm();

            AlarmSyntaxNode.SetReference(alarm, alarm);

            Assert.NotEqual(reference.ID, alarm.Reference.ID);
        }
    }
}
