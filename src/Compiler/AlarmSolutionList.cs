using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler
{
    /// <summary>
    /// A list of <see cref="AlarmSolution"/> items.
    /// </summary>
    /// <seealso cref="List{ErrorSolution}" />
    public class AlarmSolutionList : List<AlarmSolution>
    {
        public AlarmSolutionList()
        {
        }

        public AlarmSolutionList(IEnumerable<AlarmSolution> collection) : base(collection)
        {
        }

        public AlarmSolutionList(int capacity) : base(capacity)
        {
        }
    }
}
