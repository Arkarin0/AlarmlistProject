using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Alarmlist.Compiler.XML
{
    public class XMLFile
    {
        public static T Deserialize<T>(string path) 
        {
            if (File.Exists(path))
            {                
                FileStream read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return Deserialize<T>(read);

            }
            return default;
        }

        public static T Deserialize<T>(Stream stream)
        {
            XmlSerializer xmlread = new XmlSerializer(typeof(T));
            using (var reader = new XmlTextReader(stream))
            {
                reader.Namespaces = false;
                return (T)xmlread.Deserialize(reader);
            }
            
        }
    }
}
