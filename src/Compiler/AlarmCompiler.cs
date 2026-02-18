// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Core;

namespace Alarmlist.Compiler
{
    public class AlarmCompiler
    {
        public AlarmCompiler() { }
        public AlarmList Compile(Alarmlist.Syntax.AlarmSyntaxTree syntaxTree)
        {
            var alarmList = new AlarmList();
            foreach (var alarmSyntax in syntaxTree.Alarms)
            {
                var alarm = AlarmlistFactory.CreateAlarmFromSyntaxNode(alarmSyntax);
                alarmList.Add(alarm);
            }
            return alarmList;
        }
    }
}
