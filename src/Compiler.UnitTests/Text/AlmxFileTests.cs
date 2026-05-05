// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Compiler.Test;
using Alarmlist.Syntax;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Text.Tests
{
    public class AlmxFileTests
    {

        [Fact()]
        public void Ctor()
        {
            var obj = new AlmxFile();

            Assert.NotNull(obj.Alarms);
        }

        
    }
}
