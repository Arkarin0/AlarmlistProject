using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.Test;
using Alarmlist.Compiler.XML;

namespace Alarmlist.Compiler.UnitTests.XML
{
    public class XMLAlarmSerilaizerTests
    {
        private readonly string file1=
@"<Alarmlist>
    <Alarm Code=""1"" Name=""N1""/>
    <Alarm Code=""2"" Name=""N2""/>
    <ImportAlarm Code=""3"" Name=""N3"" TemplateID=""13""/>
    <ImportAlarm Code=""4"" Name=""N4"" TemplateID=""14""/>
</Alarmlist>";

        private readonly string file2 =
@"<Alarmlist>
    <Alarm Code=""20"" Name=""N20""/>
    <Alarm Code=""21"" Name=""N21""/>
    <ImportAlarm Code=""22"" Name=""N22"" TemplateID=""20""/>
    <ImportAlarm Code=""23"" Name=""N23"" TemplateID=""21""/>
</Alarmlist>";


        [Fact()]
        public void DeserializeTest()
        {
            var obj = new XMLAlarmlistSerilaizer();
            var text = file1;
            var stream = TestHelper.TextToStream(text);

            var list = obj.Deserialize(stream);

            Assert.NotNull(list);
            Assert.NotEmpty(list.Alarms);
            Assert.Equal(2, list.Alarms.Count);
            Assert.NotEmpty(list.Imports);
            Assert.Equal(2, list.Imports.Count);
        }

        [Fact()]
        public void DeserializeMultipleTest()
        {
            var obj = new XMLAlarmlistSerilaizer();            
            var streams = new[]
            { 
                TestHelper.TextToStream(file1),
                TestHelper.TextToStream(file2),
            };

            var list = obj.DeserializeMultiple(streams);

            Assert.NotNull(list);
            Assert.NotEmpty(list.Alarms);
            Assert.Equal(4, list.Alarms.Count);
            Assert.NotEmpty(list.Imports);
            Assert.Equal(4, list.Imports.Count);
        }



    }
}
