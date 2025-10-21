using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler;

namespace Alarmlist.Syntax
{
    public class Alarm
    {
        Alarm _refence;

        internal Alarm() : this(Guid.NewGuid())
        {

        }
        internal Alarm(Guid id)
        {
            this.ID = id;
        }

        public Guid ID { get; init; }
        public Alarm Reference => _refence;

       

        public static void SetReference(Alarm alarm, Alarm reference)
        {
            alarm._refence = reference;
        }
        public static void RemoveReference(Alarm alarm)
        {
            alarm._refence = null;
        }

    }
}
