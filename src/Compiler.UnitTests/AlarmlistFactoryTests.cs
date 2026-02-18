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

        
    }
}
