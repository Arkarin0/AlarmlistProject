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
    public class AlarmlistFactoryTests
    {
        [Fact]
        public void AlarmDoesNotHavePublicContructors()
        {

            bool hasPulicContructors = typeof(Alarm).GetConstructors().Where(x=> x.IsPublic).Any();

            Assert.False(hasPulicContructors);
        }

        [Fact]
        public void CreateDefaultAlarmDoesNotHaveEmptyID()
        {

            var actual = AlarmlistFactory.Alarm();

            Assert.NotNull(actual);
            Assert.NotEqual(Guid.Empty, actual.ID);
        }

        [Fact]
        public void CreateDefaultAlarmWithPredifinedGUID()
        {
            var expected = Guid.NewGuid();

            var actual = AlarmlistFactory.Alarm(expected);

            Assert.Equal(expected,actual.ID);
        }
    }
}
