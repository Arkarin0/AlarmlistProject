// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Compiler
{
    public readonly struct Alarm
    {
        public Alarm(string category, string code, string description, string name)
            : this(null, category, code, description, name, null)
        {
        }

        public Alarm(string category, string code, string description, string name, TestProcedure testProcedure)
            : this(null, category, code, description, name, testProcedure)
        {
        }

        public Alarm(string fullyQualifiedName, string category, string code, string description, string name, TestProcedure testProcedure = null)
        {
            FullyQualifiedName = fullyQualifiedName;
            Category = category;
            Code = code;
            Description = description;
            Name = name;
            TestProcedure = testProcedure ?? new TestProcedure();
        }

        public string FullyQualifiedName { get; }
        public string Category { get; }
        public string Code { get; }
        public string Description { get; }
        public string Name { get; }
        public TestProcedure TestProcedure { get; }
    }
}
