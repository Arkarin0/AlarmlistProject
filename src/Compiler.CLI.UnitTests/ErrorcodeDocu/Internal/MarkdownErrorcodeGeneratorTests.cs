using Xunit;
using Alarmlist.Compiler.ErrorcodeDocu.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alarmlist.Compiler.ErrorcodeDocu.Internal.Tests
{
    public class MarkdownErrorcodeGeneratorTests
    {
        private (MarkdownErrorcodeGenerator writer, StringBuilder sb) CreateValidInstance()
        {
            var sb = new StringBuilder();
            var writer = new System.IO.StringWriter(sb);

            return (new MarkdownErrorcodeGenerator(writer), sb);
        }

        [Fact()]
        public void CtorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new MarkdownErrorcodeGenerator(null));
        }

        [Fact()]
        public void GenerateSingleItem()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var item = new ErrorData() { Code = "1", Name = "N1", Description = "D1" };
            var expected =
@"# 1 - N1

## Name

N1

## Code

1

## Category



## Description

D1

## Solutions

<head><link rel=""stylesheet"" href=""Errorcode.css""><meta http-equiv=""content-type"" content=""text/html; charset=utf-8""/></head>
<body><div class=""SolutionList""><ul>
</ul></div></body>
";
            //action
            obj.Generate(item);
            var actual = sb.ToString();


            //Assert
            Assert.Equal(expected, actual);
        }
        

        [Fact()]
        public void GenerateMultipleItems()
        {
            //arange
            (var obj, var sb) = CreateValidInstance();
            var list = new ErrorData[]
            {
                new ErrorData(){ Code="1", Name="N1", Description="D1"},
                new ErrorData(){ Code="2", Name="N2", Description="D2"}
            };
            var expected =
@"# 1 - N1

## Name

N1

## Code

1

## Category



## Description

D1

## Solutions

<head><link rel=""stylesheet"" href=""Errorcode.css""><meta http-equiv=""content-type"" content=""text/html; charset=utf-8""/></head>
<body><div class=""SolutionList""><ul>
</ul></div></body>
# 2 - N2

## Name

N2

## Code

2

## Category



## Description

D2

## Solutions

<head><link rel=""stylesheet"" href=""Errorcode.css""><meta http-equiv=""content-type"" content=""text/html; charset=utf-8""/></head>
<body><div class=""SolutionList""><ul>
</ul></div></body>
";

            //action
            obj.Generate(list);
            var actual = sb.ToString();


            //Assert
            Assert.Equal(expected, actual);
        }


    }
}
