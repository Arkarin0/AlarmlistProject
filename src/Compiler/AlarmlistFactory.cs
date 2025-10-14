using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler
{
   public partial class AlarmlistFactory
    {
        public static AlarmList ReadFromXMLFiles(IEnumerable<string> filepaths)
        {
            var abc= new XML.XMLAlarmlistSerilaizer().DeserializeMultiple(filepaths);
            return abc.ToErrorDataList();
        }
    }
}
