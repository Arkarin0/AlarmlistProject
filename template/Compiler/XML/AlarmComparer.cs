using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.XML;

namespace Alarmlist.Compiler
{
    public partial class AlarmComparer : 
        IEqualityComparer<XMLAlarm>
    {
        
        public bool Equals(XMLAlarm x, XMLAlarm y)
        {
            return
                x.Name == y.Name &&
                x.Description == y.Description &&
                x.Code == y.Code &&
                x.Category == y.Category;
        }

       

        public int GetHashCode(XMLAlarm obj)
        {
            throw new NotImplementedException();
        }
    }
}
