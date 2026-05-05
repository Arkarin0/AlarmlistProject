using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Alarmlist.Compiler.XML
{
    [XmlRoot(ElementName = "Alarm")]
    public class XMLAlarm
    {
        [XmlAttribute]
        public Guid Id { get; set; }

        [XmlElement]
        public string Code { get; set; }

        [XmlElement]
        public string Name { get; set; }

        [XmlElement]
        public string Category { get; set; }

        [XmlElement]
        public string Description { get; set; }
    }
    
}
