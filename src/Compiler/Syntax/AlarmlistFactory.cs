using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Syntax;

namespace Alarmlist.Core
{
   public partial class AlarmlistFactory
    {
        public static AlarmSyntaxNode Alarm() => new();
    }
}
