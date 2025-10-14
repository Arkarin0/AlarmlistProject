using System;

namespace Alarmlist.Compiler
{
    partial class Program
    {
        interface ICommand
        {
            void Execute();
        }
    }
}
