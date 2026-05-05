using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Syntax;

namespace Alarmlist.Compiler
{
    public partial class AlarmComparer : 
        IEqualityComparer<AlarmSyntaxNode>,
        IEqualityComparer<AlarmSyntaxTree>
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
            return HashCode.Combine(
                obj.Name,
                obj.Description,
                obj.Code,
                obj.Category
                //obj.SolutionList
                );
        }


        public bool Equals(AlarmSyntaxTree x, AlarmSyntaxTree y)
        {
            //compare the list of alarms
            if (x.Alarms.Count != y.Alarms.Count)
                return false;
            var comparer = new AlarmComparer();
            for (int i = 0; i < x.Alarms.Count; i++)
            {
                if (!comparer.Equals(x.Alarms[i], y.Alarms[i]))
                    return false;
            }


            return true;
        }

        public int GetHashCode([DisallowNull] AlarmSyntaxTree obj) => HashCode.Combine(obj.Alarms);
    }
}
