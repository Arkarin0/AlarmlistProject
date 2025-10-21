// Created/modified by Arkarin0 under one ore more license(s).

using System;
using Alarmlist.Compiler;

namespace Alarmlist.Syntax
{
    public interface IAlarm
    {
        string Category { get; set; }
        string Code { get; set; }
        string Description { get; set; }
        Guid ID { get; }
        string Name { get; set; }
        AlarmSolutionList SolutionList { get; }
    }
}
