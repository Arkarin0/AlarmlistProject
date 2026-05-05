// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Text
{
    public abstract class SourceText
    {
        public ObservableCollection<Syntax.AlarmSyntaxNode> Alarms { get; protected set; }

        public SourceText()
        {
            Alarms = new ObservableCollection<Syntax.AlarmSyntaxNode>();
        }
    }
}
