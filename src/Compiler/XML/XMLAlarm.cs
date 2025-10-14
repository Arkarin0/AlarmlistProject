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
        public string Code { get; set; }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Category { get; set; }

        [XmlElement]
        public string Description { get; set; }

        [XmlElement]
        public XMLSolutionList Solutions { get; set; } = new XMLSolutionList();
    }
    
}
