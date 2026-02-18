using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Alarmlist.Syntax;
using Xunit;

namespace Alarmlist.Compiler.Test
{
    internal partial class TestHelper
    {
        public static void AssertAlarmEquals(AlarmSyntaxNode expected, Alarm actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Description, actual.Description);
            Assert.Equal(expected.Code, actual.Code);
            Assert.Equal(expected.Category, actual.Category);
        }


       

        
    }
}
