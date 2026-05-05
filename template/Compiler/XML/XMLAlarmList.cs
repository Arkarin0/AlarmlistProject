using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Alarmlist.Compiler.XML
{
    [XmlRoot(ElementName = "Alarmlist")]
    public partial class XMLAlarmList
    {
        public string FullFilePath { get; set; }

        [XmlElement(ElementName = "Alarm")]
        public List<XMLAlarm> Alarms { get; set; } = new List<XMLAlarm>();
    }
}
