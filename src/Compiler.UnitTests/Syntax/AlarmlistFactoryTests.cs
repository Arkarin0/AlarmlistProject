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

            bool hasPublicContructors = typeof(AlarmSyntaxNode).GetConstructors().Where(x=> x.IsPublic).Any();

            Assert.False(hasPublicContructors);
        }
    }
}
