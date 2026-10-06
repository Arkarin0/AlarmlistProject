// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler;

namespace Alarmlist.Syntax
{
    public class AlarmSyntaxTree
    {
        public List<AlarmSyntaxNode> Alarms { get; } = new List<AlarmSyntaxNode>();
        public List<PropertyDefinition> Properties { get; } = new List<PropertyDefinition>();
        internal List<AlarmSyntaxTree> Files { get; } = new List<AlarmSyntaxTree>();
    }
}
