using CommandLine;
using System;

namespace Alarmlist.Compiler
{
    partial class Program
    {
        static void Main(string[] args)
        {

            //            args = new string[]
            //            {
            //                "--outfile", "abc",
            //                "--folder",@"Z:\WMSSorter Platform\MachinePLC\src\codesys\Sortierer\WMS3a\V2\V2.4.1.X",
            //                "--filter", @"Errorcode.xml",
            //                "--Latex"
            //            };
            //            Environment.CurrentDirectory = @"Z:\WMSSorter Platform\MachinePLC\src\codesys\Sortierer\WMS3a\V2\V2.4.1.X";

            //args = new string[]
            //{
            //                "--outfile", "list//errorcode.file",
            //                "--folder","list",
            //                "--filter", @"list1.xml",
            //                "--Markdown"
            //};


            CommandLine.Parser.Default.ParseArguments<Errordocumentation>(args)
                .WithParsed<Errordocumentation>( t=> t.Execute() );
        }
    }
}
