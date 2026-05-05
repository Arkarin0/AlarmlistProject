using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler;
using Alarmlist.Compiler.Test;
using Alarmlist.Core;
using Alarmlist.Syntax;
using Xunit;

namespace Alarmlist.Syntax.Tests
{
    public class ComparerTests
    {
        public static IEnumerable<object[]> GetExpectedComparerImplementations()
        {
            yield return new object[] { typeof(IEqualityComparer<AlarmSyntaxNode>) };
            yield return new object[] { typeof(IEqualityComparer<AlarmSyntaxTree>) };
        }


        [Theory]
        [MemberData(nameof(GetExpectedComparerImplementations))]
        public void ImplementsComparer(Type type)
        {
            var comparer = new AlarmComparer();

            Assert.IsAssignableFrom(type, comparer);
        }

        #region AlarmSyntaxNode

        [Fact]
        public void EqualsOfAlarmSyntaxNodeReturnsTrue()
        {
            var comparer = new AlarmComparer();
            var x = TestHelper.CreateAlarmSyntaxNode("1");
            var y = TestHelper.CreateAlarmSyntaxNode("1");

            var result = comparer.Equals(x, y);

            Assert.True(result);
        }

        [Fact]
        public void EqualsOfAlarmSyntaxNodeReturnsFalse()
        {
            var comparer = new AlarmComparer();
            var x = TestHelper.CreateAlarmSyntaxNode("1");
            var y = TestHelper.CreateAlarmSyntaxNode("2");

            var result = comparer.Equals(x, y);

            Assert.False(result);
        }

        [Fact]
        public void GetHashOfAlarmSyntaxNodeDoesNotThrowNotImplementedException()
        {
            var comparer = new AlarmComparer();
            var x = TestHelper.CreateAlarmSyntaxNode("1");

            var result = comparer.GetHashCode(x);

            Assert.NotEqual(0, result);
        }

        #endregion

        #region AlarmSyntaxTree

        [Fact]
        public void EqualsOfAlarmSyntaxTreeReturnsTrue()
        {
            var comparer = new AlarmComparer();
            var x = TestHelper.CreateAlarmSyntaxTree("1");
            var y = TestHelper.CreateAlarmSyntaxTree("1");
            var result = comparer.Equals(x, y);

            Assert.True(result);
        }

        [Fact]
        public void EqualsOfAlarmSyntaxTreeReturnsFalse()
        {
            var comparer = new AlarmComparer();
            var x = TestHelper.CreateAlarmSyntaxTree("1");
            var y = TestHelper.CreateAlarmSyntaxTree("2");
            var result = comparer.Equals(x, y);

            Assert.False(result);
        }

        [Fact]
        public void GetHashOfAlarmSyntaxTreeDoesNotThrowNotImplementedException()
        {
            var comparer = new AlarmComparer();
            var x = TestHelper.CreateAlarmSyntaxTree("1");

            var result = comparer.GetHashCode(x);

            Assert.NotEqual(0, result);
        }

        #endregion
    }
}
