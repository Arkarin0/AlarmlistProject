// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Text;

namespace Alarmlist.Syntax
{
    public class SyntaxTreeGenerator
    {
        protected readonly AlarmSyntaxTree syntaxTree;

        protected readonly List<SourceText> sourceTexts;

        public SyntaxTreeGenerator() {
            syntaxTree = new AlarmSyntaxTree();
            sourceTexts = new List<SourceText>();
        }

        public AlarmSyntaxTree Update()
        {
            return syntaxTree;
        }

        public void AddSourceText(SourceText sourceText)
        {
            sourceTexts.Add(sourceText);
        }

        public void AddSourceTexts(IEnumerable<SourceText> sourceTexts)
        {
            this.sourceTexts.AddRange(sourceTexts);
        }

        public void ClearSourceTexts()
        {
            sourceTexts.Clear();
        }

        public void RemoveSourceText(SourceText sourceText)
        {
            sourceTexts.Remove(sourceText);
        }
    }
}
