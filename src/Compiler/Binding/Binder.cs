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
            syntaxTree.Alarms.Clear();

            foreach (var item in sourceTexts)
            {
                item.Alarms.ToList().ForEach(alarm => syntaxTree.Alarms.Add(alarm));
            }

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
