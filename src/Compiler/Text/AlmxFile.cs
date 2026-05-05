// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Alarmlist.Text
{
    public partial class AlmxFile : SourceText, IXmlSerializable
    {
        public AlmxFile() : base()
        {

        }

        XmlSchema IXmlSerializable.GetSchema() => throw new NotImplementedException();
        void IXmlSerializable.ReadXml(XmlReader reader) => throw new NotImplementedException();
        void IXmlSerializable.WriteXml(XmlWriter writer) => throw new NotImplementedException();
    }
}
