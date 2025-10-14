using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Alarmlist.Compiler.XML
{
    [XmlRoot(ElementName = "ImportTemplate")]
    public class XMLAlarmTemplate
    {
        [XmlAttribute (AttributeName = "file")]
        public string File { get; set; } = string.Empty;


        [XmlElement(ElementName = "ImportAlarm", Type = typeof(XMLImportAlarm))]
        public List<XMLImportAlarm> Children { get; set; } = new List<XMLImportAlarm>();
    }
}
