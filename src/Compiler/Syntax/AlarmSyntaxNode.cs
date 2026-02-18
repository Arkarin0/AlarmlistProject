using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler;

namespace Alarmlist.Syntax
{
    public class AlarmSyntaxNode
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }

        AlarmSyntaxNode _refence;

        internal AlarmSyntaxNode() : this(Guid.NewGuid())
        {

        }
        internal AlarmSyntaxNode(Guid id)
        {
            this.ID = id;
        }

        public Guid ID { get; init; }
        public AlarmSyntaxNode Reference => _refence;

       

        public static void SetReference(AlarmSyntaxNode alarm, AlarmSyntaxNode reference)
        {
            alarm._refence = reference;
        }
        public static void RemoveReference(AlarmSyntaxNode alarm)
        {
            alarm._refence = null;
        }

    }
}
