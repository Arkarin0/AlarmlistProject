using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler.XML
{
    public class XMLAlarmlistSerilaizer
    {
        public XMLAlarmList Deserialize(string fullFilePath)
        {
            using (FileStream fs = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read))
            {
                var item = Deserialize(fs);
                item.FullFilePath = fullFilePath;
                return item;
            }
        }

        public XMLAlarmList Deserialize(Stream stream)
        {
            return XMLFile.Deserialize<XMLAlarmList>(stream);
        }

        public XMLAlarmList DeserializeMultiple(IEnumerable<Stream> streams)
        {
            var list = new XMLAlarmList();

            foreach (var stream in streams)
            {
                var content = Deserialize(stream);

                list.Alarms.AddRange(content.Alarms);
                list.Imports.AddRange(content.Imports);
                list.Templates.AddRange(content.Templates);
            }

            return list;
        }

        public XMLAlarmList DeserializeMultiple(IEnumerable<string> filepaths)
        {
            var list = new XMLAlarmList();

            foreach (var file in filepaths)
            {
                var content = Deserialize(file);

                list.Alarms.AddRange(content.Alarms);
                list.Imports.AddRange(content.Imports);
                list.Templates.AddRange(content.Templates);
            }

            return list;
        }
    }
}
