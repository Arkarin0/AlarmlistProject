using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.XML;
using Alarmlist.Syntax;

namespace Alarmlist.Compiler
{
   public partial class AlarmlistFactory
    {
        internal static Exception NoReferenceException(XMLAlarmList.MergedAlarm item)
        {
            return new NoReferenceException(item.ID, item.GetChildren().Select(x => x.ID));
        }

        internal static Exception DuplicateErrocodeException(XMLAlarmList.MergedAlarm item)
        {
            return new DuplicateErrocodeException(item.ID, item.GetDuplicates().Select(x => 
            $"{x.ID}" +
            (string.IsNullOrWhiteSpace(x.SourceFile) ? $"(source: \"{x.SourceFile}\")" :string.Empty)
            ));
        }

        internal static Exception CircleDependancyException(XMLAlarmList.MergedAlarm item, IEnumerable<XMLAlarmList.MergedAlarm> trace)
        {
            return new CircleDependancyException(item.ID, trace.Select(x => x.ID));
        }

        internal static Exception ReferenceException(XMLAlarmTemplate item, Exception innerException= null)
        {
            return new XMLImportTemplateReferenceException(item.File, innerException);
        }

        internal static AlarmSolution GetErrorSolution(XMLAlarmSolution item)
        {
            return new AlarmSolution() {
                Cause = item.Cause, 
                Solutions= item.Solutions
            };
        }
    }
}
