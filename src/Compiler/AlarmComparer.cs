using System;
using System.Collections.Generic;
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
            return Definition.AlarmDefinition.Fields.All(field => x.GetEffectiveFieldValue(field.Name) == y.GetEffectiveFieldValue(field.Name));
            //    Equals(x.SolutionList,y.SolutionList);            
        }

        public int GetHashCode(AlarmSyntaxNode obj)
        {
            unchecked
            {
                var hash = 17;
                foreach (var field in Definition.AlarmDefinition.Fields)
                    hash = hash * 31 + (obj.GetEffectiveFieldValue(field.Name)?.GetHashCode() ?? 0);
                return hash;
            }
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

        public int GetHashCode(AlarmSyntaxTree obj)
        {
            unchecked
            {
                var hash = 17;
                foreach (var alarm in obj.Alarms)
                    hash = hash * 31 + GetHashCode(alarm);
                return hash;
            }
        }
    }
}
