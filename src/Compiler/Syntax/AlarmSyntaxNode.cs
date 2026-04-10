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
        private string _Name;
        public string Name
        {
            get { return _Name ?? Reference?.Name; }
            set { _Name = value; }
        }

        private string _Code;
        public string Code
        {
            get { return _Code ?? Reference?.Code; }
            set { _Code = value; }
        }

        private string _Description;
        public string Description
        {
            get { return _Description ?? Reference?.Description; }
            set { _Description = value; }
        }

        private string _Category;
        public string Category
        {
            get { return _Category ?? Reference?.Category; }
            set { _Category = value; }
        }


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
