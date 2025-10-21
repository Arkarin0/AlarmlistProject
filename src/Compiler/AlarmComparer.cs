using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Alarmlist.Compiler.XML;
using Alarmlist.Syntax;

namespace Alarmlist.Compiler
{
    public partial class AlarmComparer : 
        IEqualityComparer<Alarm>,
        IEqualityComparer<AlarmSolution>
    {
        public bool Equals(Alarm x, Alarm y)
        {
            //return 
            //    x.Name == y.Name &&
            //    x.Description == y.Description &&
            //    x.Code == y.Code &&
            //    x.Category == y.Category &&
            //    Equals(x.SolutionList,y.SolutionList);
            throw new NotImplementedException();
        }

        public bool Equals(AlarmSolution x, AlarmSolution y)
        {
            bool itemsAreEqual = x.Solutions.Count == 0 && y.Solutions.Count == 0;

            if (!itemsAreEqual)
            {

                for (int i = 0; i < x.Solutions.Count; i++)
                {
                    itemsAreEqual = x.Solutions.ElementAtOrDefault(i) == y.Solutions.ElementAtOrDefault(i);

                    if (!itemsAreEqual) return false;
                }
            }


            return
                x.Cause == y.Cause &&
                x.Solutions.Count == y.Solutions.Count &&
                itemsAreEqual;
        }

        public bool Equals(AlarmSolutionList x, AlarmSolutionList y)
        {
            bool itemsAreEqual = x.Count == 0 && y.Count == 0;

            if (!itemsAreEqual)
            {

                for (int i = 0; i < x.Count; i++)
                {
                    itemsAreEqual = Equals(x.ElementAtOrDefault(i), y.ElementAtOrDefault(i));

                    if (!itemsAreEqual) return false;
                }
            }


            return
                itemsAreEqual;
        }

        public int GetHashCode(Alarm obj)
        {
            throw new NotImplementedException();
        }

        public int GetHashCode(AlarmSolution obj)
        {
            throw new NotImplementedException();
        }
    }
}
