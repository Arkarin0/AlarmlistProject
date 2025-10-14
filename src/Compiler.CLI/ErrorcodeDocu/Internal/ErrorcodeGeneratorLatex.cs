using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler.ErrorcodeDocu.Internal
{
    internal class ErrorcodeGeneratorLatex
    {
        private readonly TextWriter _writer;
        public ErrorcodeGeneratorLatex(TextWriter writer) {
            this._writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Generate (IEnumerable<ErrorData> tree)
        {
            PageCreater();
            TitelPageData();
            DocumentBegin();
            TitelPage();
            IndexPage();
            WriteErrorcodeTable(tree);
            DocumentEnd();
        }
        public void GenerateTableOnly(IEnumerable<ErrorData> tree)
        {

            WriteErrorcodeTable(tree);

        }
        internal void PageCreater()
        {
            EmptyLine();
            WriteLine("\\documentclass[a4paper, 12pt]{ scrreprt}");
            EmptyLine();
            WriteLine("\\usepackage[utf8]{ inputenc}");
            WriteLine("\\usepackage[ngerman]{ babel}");
            WriteLine("\\usepackage[T1]{ fontenc}");
            WriteLine("\\usepackage{longtable}");
            EmptyLine();

        }
        internal void TitelPageData()
        {
            EmptyLine();
            WriteLine("\\title{Codeliste V2.3.x}");
            WriteLine("\\subtitle{Dokumentation V12.3.3 }");
            //WriteLine("\\author{Weingart & Kubrat GmbH}");
            WriteLine("\\date{"+DateTime.Now.ToString()+"}");
            EmptyLine();
        }

        internal void DocumentBegin() => WriteLine("\\begin{document}");

        internal void DocumentEnd() => WriteLine("\\end{document}");

        internal void TitelPage() 
        {
            EmptyLine();
            WriteLine("\\maketitle");
            NewPage();
            EmptyLine();
        }

        internal void IndexPage() 
        {
            EmptyLine();
            WriteLine("\\tableofcontents");
            NewPage();
            EmptyLine();
        }

        internal void TableBegin(string tablename, string caption, params string[] ColumnHeaderNames) 
        {
            WriteLine("\\begin{center}");
            WriteLine("\\begin{longtable}[c]{|l|l|l|}");
            WriteLine("\\caption{" + caption + "}");
            WriteLine("\\label{tab:"+tablename+"}\\\\");
            WriteLine("\\hline");
            TableContent(ColumnHeaderNames.Select(v => $"\\textbf{{{v}}}").ToArray());
            WriteLine(" \\hline");
            WriteLine("\\endhead");

        }
        internal void TableContent(params string[] columncontent)
        {
            WriteLine(string.Join(" & ", columncontent) + "\\\\ \\hline");
        }

        internal void TableEnd() 
        {
            WriteLine("\\end{longtable}");
            WriteLine("\\end{center}");
            WriteLine("\\newpage");
        }

        internal void WriteErrorcodeTable(IEnumerable<ErrorData> list) {
            WriteLine("\\chapter (Errorcodes)");
            TableBegin("Errorcodes", "errorcodetable", ColumnHeaderNames: new string[] { "errorcode", "Name", "Description" });
            foreach (var item in list) TableContent(item.Code.ToString(), item.Name, item.Description);
            TableEnd();
        }



        private void WriteLine(string value) => _writer.WriteLine(value);

        private void Write(string value) => _writer.Write(value);

        private void EmptyLine() => WriteLine("");

        private void NewPage() => WriteLine("\\newpage");

    }
}
