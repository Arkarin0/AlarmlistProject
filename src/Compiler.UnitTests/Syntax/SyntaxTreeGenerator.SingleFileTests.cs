using Xunit;
using Alarmlist.Syntax;
// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Text;
using Moq;
using Alarmlist.Compiler.Test;

namespace Alarmlist.Syntax.Tests
{
    public class SyntaxTreeGeneratorSingleFileTests
    {
        public static Core.UnitTests.Fakes.SyntaxTreeGeneratorMock CreateInstance()
        {
            return SyntaxTreeGeneratorTests.CreateInstance();
        }

        public static SourceText CreateSourceText()
        {
            var mock = new Mock<SourceText>();
            return mock.Object;
        }

        public static SourceText CreateSourceText(Syntax.AlarmSyntaxTree alarmSyntaxTree)
        {
            var mock = new Core.UnitTests.Fakes.SourceTextMock(alarmSyntaxTree);
                        
            return mock;
        }



        [Fact()]
        public void SimpleAlarmsAreAddedToTheSyntaxTreeTest()
        {
            var obj = CreateInstance();
            var expected = TestHelper.CreateAlarmSyntaxTreeByItemCounts(1);
            var src = CreateSourceText(expected);

            obj.AddSourceText(src);
            var actual = obj.Update();


            TestHelper.AssertContains(actual, expected);
        }

    }
}
