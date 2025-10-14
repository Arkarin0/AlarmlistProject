using CommandLine;
using System;

namespace Alarmlist.Compiler
{
    partial class Program
    {
        [Verb("docErrorcode", HelpText = "Generates a documentatoin file based on the errorcodes .")]
        class Errordocumentation : ICommand
        {
            [Option("outfile", HelpText = "Provide the full file path of the resulting file(s).", Required = true)]
            public string OutFile { get; set; } 

            [Option("folder", HelpText = "Change the directory for the search. By default the current directory is used.")]
            public string Folder { get; set; }= System.IO.Directory.GetCurrentDirectory();

            [Option('f', "filter", Default = "Errorcode.xml", HelpText = "Apply a searchfilter to the search.")]
            public string Filter { get; set; } = "Errorcode.xml";

            [Option('r', "recursive", HelpText ="Include subfolders into the search.")]
            public bool Recursive { get; set; }

            [Option("Latex", Group = "Outputformats", HelpText = "Generates LaTex file containing all errorcodes.")]
            public bool Latex { get; set; }

            [Option("Markdown", Group = "Outputformats", HelpText = "Generates a markdown file for each errorcode.")]
            public bool Markdown { get; set; }

            public void Execute()
            {
                //1. collect files
                var files = System.IO.Directory.GetFiles(
                    Folder,
                    Filter,
                    Recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly
                    );

                //2. generate the error tree
                var codes = ErrorcodeDocu.ErrorcodeFactory.ReadFromXMLFiles(files);

                //3. output desired formats
                string fullfilepath = OutFile;
                string extension = System.IO.Path.GetExtension(OutFile);
                //string fullfilepathWithoutextension = fullfilepath.TrimEnd(extension.ToCharArray());
                string fullfilepathWithoutextension = System.IO.Path.ChangeExtension(OutFile,"").TrimEnd('.');

                if (Latex)
                {
                    fullfilepath = System.IO.Path.ChangeExtension(fullfilepath, FileExtensions.Latex);
                    using (var writer = new System.IO.StreamWriter(System.IO.File.Create(fullfilepath)))
                    {
                        ErrorcodeDocu.ErrorcodeFactory.WriteLatex(writer, codes);
                    }
                }

                if (Markdown)
                    foreach (var code in codes)
                    {
                        fullfilepath = $"{fullfilepathWithoutextension}.{code.Code}{FileExtensions.Markdown}";

                        using (var writer = new System.IO.StreamWriter(System.IO.File.Create(fullfilepath)))
                        {
                            ErrorcodeDocu.ErrorcodeFactory.WriteMarkdown(writer, code);
                        }
                    }

            }
        }
    }
}
