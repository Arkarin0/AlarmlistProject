// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Binding
{
    public class Binder
    {
        protected readonly AlarmSyntaxTree syntaxTree;

        protected readonly List<SourceText> sourceTexts;

        public Binder() {
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
