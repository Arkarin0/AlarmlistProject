using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.Test;
using Alarmlist.Syntax;
using Alarmlist.Core;

namespace Alarmlist.Compiler.Tests
{
    public class AlarmCompilerTests
    {
        [Theory]
        [InlineData(new object[] { 1 })]
        [InlineData(new object[] { 10 })]
        [InlineData(new object[] { 100 })]
        [InlineData(new object[] { 1000 })]
        public void CompileReturnsResolvedListFromASimpleSyntaxTree(int count)
        {
            var syntaxTree = new Syntax.AlarmSyntaxTree();
            var expected = new List<AlarmSyntaxNode>();
            for (int i = 0; i < count; i++)
            {
                var node = TestHelper.CreateAlarmSyntaxNode(i.ToString());
                syntaxTree.Alarms.Add(node);
                expected.Add(node);
            }

            var compiler = new AlarmCompiler();

            var actual = compiler.Compile(syntaxTree);

            for (int i = 0; i < count; i++)
                TestHelper.AssertAlarmEquals(expected[i], actual[i]);
        }

        
    }
}
