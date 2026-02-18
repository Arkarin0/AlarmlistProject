// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Compiler;

namespace Alarmlist.Compiler
{
    public readonly struct Alarm
    {
        public Alarm(string category, string code, string description, string name)
        {
            Category = category;
            Code = code;
            Description = description;
            Name = name;
        }

        public string Category { get; }
        public string Code { get; }
        public string Description { get; }
        public string Name { get; }
    }
}
