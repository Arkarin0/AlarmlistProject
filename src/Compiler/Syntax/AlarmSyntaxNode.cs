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
            get { return string.IsNullOrEmpty(_Name) ? Reference?.Name : _Name; }
            set { _Name = value; }
        }

        private string _Code;
        public string Code
        {
            get { return string.IsNullOrEmpty(_Code) ? Reference?.Code : _Code; }
            set { _Code = value; }
        }

        private string _Description;
        public string Description
        {
            get { return string.IsNullOrEmpty(_Description) ? Reference?.Description : _Description; }
            set { _Description = value; }
        }

        private string _Category;
        public string Category
        {
            get { return string.IsNullOrEmpty(_Category) ? Reference?.Category : _Category; }
            set { _Category = value; }
        }


        private AlarmSyntaxNode _reference;
        private string _referenceName;

        public string FullyQualifiedName { get; set; }

        public string ReferenceName
        {
            get { return _referenceName; }
            set
            {
                if (_referenceName == value)
                    return;

                _referenceName = value;
                _reference = null;
            }
        }

        public AlarmSyntaxNode Reference
        {
            get
            {
                if (_reference == null)
                    return null;

                return _reference.FullyQualifiedName == ReferenceName
                    ? _reference
                    : null;
            }
        }

       
        internal AlarmSyntaxNode() { }

        public static void SetReference(AlarmSyntaxNode alarm, AlarmSyntaxNode reference)
        {
            if (alarm == reference)
            {
                RemoveReference(alarm);
                return;
            }

            alarm._referenceName = reference?.FullyQualifiedName;
            alarm._reference = reference;
        }

        public static void RemoveReference(AlarmSyntaxNode alarm)
        {
            alarm._referenceName = null;
            alarm._reference = null;
        }

        internal static void ClearResolvedReference(AlarmSyntaxNode alarm)
        {
            alarm._reference = null;
        }

    }
}
