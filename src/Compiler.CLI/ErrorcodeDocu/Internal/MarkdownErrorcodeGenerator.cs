using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler.ErrorcodeDocu.Internal
{
    internal class MarkdownErrorcodeGenerator
    {
        private readonly TextWriter _writer;
        public MarkdownErrorcodeGenerator(TextWriter writer) {
            this._writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Generate (IEnumerable<ErrorData> tree)
        {
            foreach (ErrorData errorData in tree)
            {
                Generate (errorData);
            }
        }
        public void Generate(ErrorData item)
        {
            Header1($"{item.Code} - {item.Name}");
            
            Header2($"Name");
            WriteLine($"{item.Name}");
            EmptyLine ();

            Header2($"Code");
            WriteLine($"{item.Code}");
            EmptyLine();

            Header2($"Category");
            WriteLine($"{item.Category}");
            EmptyLine();

            Header2($"Description");
            WriteLine($"{item.Description}");
            EmptyLine();

            Header2($"Solutions");
            WriteSolution(item.SolutionList);
            //EmptyLine(); //end of file
        }
        internal void Header1(string text)
        {
            WriteLine("# " + text);            
            EmptyLine();
        }
        internal void Header2(string text)
        {
            WriteLine("## " + text);
            EmptyLine();
        }
        internal void Header3(string text)
        {
            WriteLine("### " + text);
            EmptyLine();
        }

        internal void WriteSolution(ErrorcodeDocu.ErrorSolutionList list)
        {
            string head = "<head><link rel=\"stylesheet\" href=\"Errorcode.css\"><meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\"/></head>";
            string solutionError= "<li id=\"SolutionError\">{0}</li>";
            string solutionSolution = "<li id=\"SolutionSolution\">{0}</li>";
            bool isFirstItem = true;

            StringBuilder sb= new StringBuilder();

            sb.AppendLine(head);
            sb.AppendLine("<body><div class=\"SolutionList\"><ul>");
            foreach (var item in list)
            {
                if (isFirstItem) isFirstItem = false;
                else sb.AppendLine("<br/>");

                sb.AppendLine(string.Format(solutionError, item.Cause));
                foreach (var sol in item.Solutions)
                {
                    sb.AppendLine(string.Format(solutionSolution, sol));
                }
            }
            sb.AppendLine("</ul></div></body>");

            Write(sb.ToString());
        }




        private void WriteLine(string value) => _writer.WriteLine(value);

        private void Write(string value) => _writer.Write(value);

        private void EmptyLine() => WriteLine("");

    }
}
