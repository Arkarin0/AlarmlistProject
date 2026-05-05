// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Core.UnitTests.Fakes
{
    internal class SourceTextMock : SourceText
    {
        public SourceTextMock(Syntax.AlarmSyntaxTree syntaxTree) : base()
        {
            var observableCollection = new System.Collections.ObjectModel.ObservableCollection<AlarmSyntaxNode>(syntaxTree.Alarms);
            this.Alarms = observableCollection;
        }
    }
}
