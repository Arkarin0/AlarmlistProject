using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Core
{
    public partial class AlarmlistFactory
    {
        //public static Compiler.AlarmList ReadFromXMLFiles(IEnumerable<string> filepaths)
        //{
        //    //var abc= new Compiler.XML.XMLAlarmlistSerilaizer().DeserializeMultiple(filepaths);
        //    //return abc.ToErrorDataList();
        //    throw new NotImplementedException();
        //}

        public static Compiler.Alarm CreateAlarmFromSyntaxNode(Alarmlist.Syntax.AlarmSyntaxNode node)
        {
            var alarm = new Compiler.Alarm(name: node.Name, code: node.Code, description: node.Description, category: node.Category);


            return alarm;
        }
    }
}
