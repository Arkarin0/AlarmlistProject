using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler;
using Alarmlist.Text;

namespace Alarmlist.Syntax
{
    public partial class AlarmSyntaxNode
    {
        private readonly Dictionary<string, SourceLocation> _fieldLocations = new Dictionary<string, SourceLocation>(StringComparer.Ordinal);

        /// <summary>The parsed alarm declaration, or null for syntax constructed without source information.</summary>
        public SourceLocation Location { get; internal set; }

        public bool IsAbstract { get; set; }

        internal string InvalidAbstractValue { get; set; }

        public List<PropertyDefinition> Properties { get; } = new List<PropertyDefinition>();

        internal AlarmSyntaxTree SourceFile { get; set; }

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

        private readonly Dictionary<string, string> _localFields = new Dictionary<string, string>(StringComparer.Ordinal);

        public string GetLocalFieldValue(string fieldName)
        {
            if (!Definition.AlarmDefinition.Fields.Any(field => field.Name == fieldName))
                throw new ArgumentException("Unknown inheritable field.", nameof(fieldName));
            return _localFields.TryGetValue(fieldName, out string value) ? value : null;
        }

        public void SetLocalFieldValue(string fieldName, string value)
        {
            if (!Definition.AlarmDefinition.Fields.Any(field => field.Name == fieldName))
                throw new ArgumentException("Unknown inheritable field.", nameof(fieldName));
            _localFields[fieldName] = value;
        }

        internal AlarmSyntaxNode GetFieldSource(string fieldName)
        {
            return string.IsNullOrEmpty(GetLocalFieldValue(fieldName)) ? Reference?.GetFieldSource(fieldName) : this;
        }

        public string GetEffectiveFieldValue(string fieldName)
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
