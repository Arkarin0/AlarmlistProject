using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Alarmlist.Compiler.XML
{
    [XmlRoot(ElementName = "ImportAlarm")]
    public class XMLImportAlarm
    {
        [XmlAttribute]
        public string TemplateID { get; set; }

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
