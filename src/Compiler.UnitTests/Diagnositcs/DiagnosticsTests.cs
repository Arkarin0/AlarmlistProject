using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.Test;
using Alarmlist.Syntax;
using Alarmlist.Core;

namespace Alarmlist.Diagnostics.Tests
{
    public class DiagnosticsTests
    {
        [Fact]
        public void AlarmReferencesItselfResltsInDiagnosticMassage()
        {
            var alarm = AlarmlistFactory.Alarm();
            
            Alarm.SetReference(alarm, alarm);

            //Assert.Same(reference, alarm.Reference);
            Assert.Fail("notIMplemented");
        }
    }
}
