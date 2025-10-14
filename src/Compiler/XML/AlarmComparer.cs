using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler.XML;

namespace Alarmlist.Compiler
{
    public partial class AlarmComparer : 
        IEqualityComparer<XMLAlarm>,
        IEqualityComparer<XMLImportAlarm>,
        IEqualityComparer<XMLAlarmSolution>,
        IEqualityComparer<XMLSolutionList>
    {
        
        public bool Equals(XMLAlarm x, XMLAlarm y)
        {
            return
                x.Name == y.Name &&
                x.Description == y.Description &&
                x.Code == y.Code &&
                x.Category == y.Category &&
                Equals(x.Solutions,y.Solutions);
        }

        public bool Equals(XMLImportAlarm x, XMLImportAlarm y)
        {
            return
               x.Name == y.Name &&
               x.Description == y.Description &&
               x.Code == y.Code &&
               x.Category == y.Category &&
               x.TemplateID == y.TemplateID &&
                Equals(x.Solutions, y.Solutions);
        }

        public bool Equals(XMLAlarmSolution x, XMLAlarmSolution y)
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

        public bool Equals(XMLSolutionList x, XMLSolutionList y)
        {
            bool itemsAreEqual = x.Items.Count ==0 && y.Items.Count ==0;

            if (!itemsAreEqual)
            {
                for (int i = 0; i < (x.Items?.Count ?? 0); i++)
                {
                    itemsAreEqual = Equals(x.Items.ElementAtOrDefault(i), y.Items.ElementAtOrDefault(i));

                    if (!itemsAreEqual) return false;
                }
            }

            return
                x.ClearContent == y.ClearContent &&
                itemsAreEqual;

        }

        public int GetHashCode(XMLAlarm obj)
        {
            throw new NotImplementedException();
        }

        public int GetHashCode(XMLImportAlarm obj)
        {
            throw new NotImplementedException();
        }

        public int GetHashCode(XMLAlarmSolution obj)
        {
            throw new NotImplementedException();
        }

        public int GetHashCode(XMLSolutionList obj)
        {
            throw new NotImplementedException();
        }
    }
}
