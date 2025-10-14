using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.XML;
using Xunit;

namespace Alarmlist.Compiler.UnitTests.XML
{
    public class AlarmComparerTests
    {
        public static IEnumerable<object[]> GetExpectedComparerImplementations()
        {
            yield return new object[] { typeof(IEqualityComparer<AlarmData>) };
            yield return new object[] { typeof(IEqualityComparer<AlarmSolution>) };
            yield return new object[] { typeof(IEqualityComparer<XMLAlarm>) };
            yield return new object[] { typeof(IEqualityComparer<XMLImportAlarm>) };
            yield return new object[] { typeof(IEqualityComparer<XMLAlarmSolution>) };
            yield return new object[] { typeof(IEqualityComparer<XMLSolutionList>) };
        }

        [Theory]
        [MemberData(nameof(GetExpectedComparerImplementations))]
        public void ImplementsComparer(Type type)
        {
            var comparer = new AlarmComparer();

            Assert.IsAssignableFrom(type, comparer);
        }

        [Theory(Skip = "not implemented")]
        [MemberData(nameof(GetExpectedComparerImplementations))]
        public void EqualsReturnTrue(Type type)
        {
            var comparer = new AlarmComparer();

            Assert.Fail("not implemented");
        }

        [Theory(Skip = "not implemented")]
        [MemberData(nameof(GetExpectedComparerImplementations))]
        public void EqualsReturnFalse(Type type)
        {
            var comparer = new AlarmComparer();

            Assert.Fail("not implemented");
        }

        [Theory(Skip = "not implemented")]
        [MemberData(nameof(GetExpectedComparerImplementations))]
        public void GetHashCodeDoesNotThowNotIMplementedException(Type type)
        {
            var comparer = new AlarmComparer();

            Assert.Fail("not implemented");
        }


    }
}
