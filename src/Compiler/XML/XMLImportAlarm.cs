using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Alarmlist.Compiler.XML
{
    [XmlRoot(ElementName = "ImportAlarm")]
    public class XMLImportAlarm:XMLAlarm
    {
        [XmlAttribute]
        public string TemplateID { get; set; }
    }
}
