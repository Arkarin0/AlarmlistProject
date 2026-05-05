//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Alarmlist.Compiler.XML;
//using Alarmlist.Syntax;

//namespace Alarmlist.Compiler
//{
//    public partial class AlarmlistFactory
//    {
//        public static void AddFileToSyntaxTree(AlarmSyntaxTree tree, XMLAlarmList content)
//        {
//            foreach (var alarm in content.Alarms)
//            {
//                var syntaxAlarm = FromXMLAlarm(alarm);
//                tree.Alarms.Add(syntaxAlarm);
//            }
//        }

//        public static AlarmSyntaxNode FromXMLAlarm(XMLAlarm alarm)
//        {
//            var syntaxAlarm = new AlarmSyntaxNode
//            {
//                Name = alarm.Name,
//                Description = alarm.Description,
//                Code = alarm.Code,
//                Category = alarm.Category
//            };
//            return syntaxAlarm;
//        }
//    }
//}
