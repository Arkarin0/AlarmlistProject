using Xunit;
using Alarmlist.Compiler.ErrorcodeDocu.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler.ErrorcodeDocu.Internal.Tests
{
    public class ErrorcodeGeneratorLatexTests
    {
        private (ErrorcodeGeneratorLatex writer, StringBuilder sb )CreateValidInstance()
        {
            var sb = new StringBuilder();
            var writer = new System.IO.StringWriter(sb);

            return (new ErrorcodeGeneratorLatex(writer), sb);
        }
       
        [Fact()]
        public void Generate()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var list = new ErrorData[]
            {
                new ErrorData(){ Code="1", Name="N1", Description="D1"},
                new ErrorData(){ Code="2", Name="N2", Description="D2"}
            };
            var expected =
@"
\documentclass[a4paper, 12pt]{ scrreprt}

\usepackage[utf8]{ inputenc}
\usepackage[ngerman]{ babel}
\usepackage[T1]{ fontenc}
\usepackage{longtable}


\title{Codeliste V2.3.x}
\subtitle{Dokumentation V12.3.3 }
\date{" + DateTime.Now.ToString() + @"}

\begin{document}

\maketitle
\newpage


\tableofcontents
\newpage

\chapter (Errorcodes)
\begin{center}
\begin{longtable}[c]{|l|l|l|}
\caption{errorcodetable}
\label{tab:Errorcodes}\\
\hline
\textbf{errorcode} & \textbf{Name} & \textbf{Description}\\ \hline
 \hline
\endhead
1 & N1 & D1\\ \hline
2 & N2 & D2\\ \hline
\end{longtable}
\end{center}
\newpage
\end{document}
";
            //action
            obj.Generate(list);
            var actual = sb.ToString();


            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void CtorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new ErrorcodeGeneratorLatex(null));
        }
 [Fact()]
        public void GenerateTableOnlyTest() 
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var list = new ErrorData[]
            {
                new ErrorData(){ Code="1", Name="N1", Description="D1"},
                new ErrorData(){ Code="2", Name="N2", Description="D2"}
            };
            var expected =
@"\chapter (Errorcodes)
\begin{center}
\begin{longtable}[c]{|l|l|l|}
\caption{errorcodetable}
\label{tab:Errorcodes}\\
\hline
\textbf{errorcode} & \textbf{Name} & \textbf{Description}\\ \hline
 \hline
\endhead
1 & N1 & D1\\ \hline
2 & N2 & D2\\ \hline
\end{longtable}
\end{center}
\newpage
";
            //action
            obj.GenerateTableOnly(list);
            var actual = sb.ToString();


            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void PageCreater_WritesValidDataTest()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var expected =
@"
\documentclass[a4paper, 12pt]{ scrreprt}

\usepackage[utf8]{ inputenc}
\usepackage[ngerman]{ babel}
\usepackage[T1]{ fontenc}
\usepackage{longtable}

";

            //action
            obj.PageCreater();
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void TitelPageData_WritesValidDataTest()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var expected =
@"
\title{Codeliste V2.3.x}
\subtitle{Dokumentation V12.3.3 }
\date{" + DateTime.Now.ToString() + @"}

";

            //action
            obj.TitelPageData();
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void DocumentBegin_WritesValidDataTest()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var expected =
@"\begin{document}
";

            //action
            obj.DocumentBegin();
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void DocumentEnd_WritesValidDataTest() 
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var expected =
@"\end{document}
";

            //action
            obj.DocumentEnd();
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void TitelPage_WritesValidDataTest()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var expected =
@"
\maketitle
\newpage

";

            //action
            obj.TitelPage();
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void IndexPage_WritesValidDataTest() 
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var expected =
@"
\tableofcontents
\newpage

";

            //action
            obj.IndexPage();
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void TableBegin_WritesValidDataTest()
        {             
            //arange
            (var obj, var sb) = CreateValidInstance();
            string name = "Errorcodetable";
            string caption = "Errorcode Table.";
            string[] columns = new string[] { "errorcode", "name", "description" };
            var expected =
@"\begin{center}
\begin{longtable}[c]{|l|l|l|}
\caption{Errorcode Table.}
\label{tab:Errorcodetable}\\
\hline
\textbf{errorcode} & \textbf{name} & \textbf{description}\\ \hline
 \hline
\endhead
";

            //action
            obj.TableBegin(name, caption, columns);
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void TableEnd_WritesValidDataTest()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var expected =
@"\end{longtable}
\end{center}
\newpage
";

            //action
            obj.TableEnd();
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact()]
        public void WriteErrorcodeTableTest() {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var list = new ErrorData[]
            {
                new ErrorData(){ Code="1", Name="N1", Description="D1"},
                new ErrorData(){ Code="2", Name="N2", Description="D2"}
            };
            var expected =
@"\chapter (Errorcodes)
\begin{center}
\begin{longtable}[c]{|l|l|l|}
\caption{errorcodetable}
\label{tab:Errorcodes}\\
\hline
\textbf{errorcode} & \textbf{Name} & \textbf{Description}\\ \hline
 \hline
\endhead
1 & N1 & D1\\ \hline
2 & N2 & D2\\ \hline
\end{longtable}
\end{center}
\newpage
";
            //action
            obj.WriteErrorcodeTable(list);
            var actual = sb.ToString();


            //Assert
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void ContentCreater_WritesValidDataTest()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            string Code = "1";
            string Name = "N1";
            string Description = "D1";

            var expected =
@"1 & N1 & D1\\ \hline
";

            //action
            obj.TableContent(Code, Name,Description);
            var actual = sb.ToString();

            //Assert
            Assert.Equal(expected, actual);
        }


    }
}
