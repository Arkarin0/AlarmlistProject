// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Text;

namespace Alarmlist.Core.UnitTests.Fakes
{
    public class SyntaxTreeGeneratorMock: Syntax.SyntaxTreeGenerator
    {
        public SyntaxTreeGeneratorMock(): base()
        { }

        public ICollection<SourceText> GetSourceTexts()
        {
            return sourceTexts;
        }
    }
}
