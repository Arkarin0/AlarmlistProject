using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Syntax;

namespace Alarmlist.Compiler
{
    public partial class AlarmComparer : 
        IEqualityComparer<AlarmSyntaxNode>
    {
        public bool Equals(AlarmSyntaxNode x, AlarmSyntaxNode y)
        {
            return
                x.Name == y.Name &&
                x.Description == y.Description &&
                x.Code == y.Code &&
                x.Category == y.Category;
            //    Equals(x.SolutionList,y.SolutionList);            
        }

       

        public int GetHashCode(AlarmSyntaxNode obj)
        {
            throw new NotImplementedException();
        }
    }
}
