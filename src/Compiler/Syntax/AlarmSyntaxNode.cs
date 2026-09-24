using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler;
using Alarmlist.Text;

namespace Alarmlist.Syntax
{
    public class AlarmSyntaxNode
    {
        private readonly Dictionary<string, SourceLocation> _fieldLocations = new Dictionary<string, SourceLocation>(StringComparer.Ordinal);

        /// <summary>The parsed alarm declaration, or null for syntax constructed without source information.</summary>
        public SourceLocation Location { get; internal set; }

        /// <summary>Gets the local field's declaration location, without following references.</summary>
        public SourceLocation GetFieldLocation(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            return _fieldLocations.TryGetValue(fieldName, out SourceLocation location) ? location : null;
        }

        internal void SetFieldLocation(string fieldName, SourceLocation location)
        {
            _fieldLocations[fieldName] = location;
        }

        private string _Name;
        public string Name
        {
            get { return GetEffectiveFieldValue(nameof(Name)); }
            set { _Name = value; }
        }

        private string _Code;
        public string Code
        {
            get { return GetEffectiveFieldValue(nameof(Code)); }
            set { _Code = value; }
        }

        private string _Description;
        public string Description
        {
            get { return GetEffectiveFieldValue(nameof(Description)); }
            set { _Description = value; }
        }

        private string _Category;
        public string Category
        {
            get { return GetEffectiveFieldValue(nameof(Category)); }
            set { _Category = value; }
        }


        internal string GetLocalFieldValue(string fieldName)
        {
            switch (fieldName)
            {
                case nameof(Name): return _Name;
                case nameof(Code): return _Code;
                case nameof(Description): return _Description;
                case nameof(Category): return _Category;
                default: throw new ArgumentException("Unknown inheritable field.", nameof(fieldName));
            }
        }

        internal AlarmSyntaxNode GetFieldSource(string fieldName)
        {
            return string.IsNullOrEmpty(GetLocalFieldValue(fieldName)) ? Reference?.GetFieldSource(fieldName) : this;
        }

        private string GetEffectiveFieldValue(string fieldName)
        {
            return GetFieldSource(fieldName)?.GetLocalFieldValue(fieldName);
        }

        private AlarmSyntaxNode _reference;
        private string _referenceName;

        public string FullyQualifiedName { get; set; }

        public TestProcedureSyntax TestProcedure { get; } = new TestProcedureSyntax();

        public TestProcedureSyntax ResolvedTestProcedures
        {
            get { return TestProcedure.ResolveFrom(Reference?.ResolvedTestProcedures); }
        }

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
