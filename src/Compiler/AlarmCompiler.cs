// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Binding;
using Alarmlist.Core;
using Alarmlist.Text;

namespace Alarmlist.Compiler
{
    public class AlarmCompiler
    {
        public AlarmCompiler() { }

        public AlarmList Compile(IEnumerable<SourceText> sourceTexts)
        {
            if (sourceTexts == null)
                throw new ArgumentNullException(nameof(sourceTexts));

            var binder = new Binder();
            binder.AddSourceTexts(sourceTexts);
            return Compile(binder.Update());
        }

        public AlarmList CompileFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
                throw new ArgumentNullException(nameof(filePaths));

            var sources = filePaths.Select(filePath =>
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("A file path must not be null or empty.", nameof(filePaths));

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("The ALMX file was not found.", filePath);

                return new AlmxFile(filePath);
            });

            return Compile(sources);
        }

        public AlarmList Compile(Alarmlist.Syntax.AlarmSyntaxTree syntaxTree)
        {
            if (syntaxTree == null)
                throw new ArgumentNullException(nameof(syntaxTree));

            var alarmList = new AlarmList();
            foreach (var alarmSyntax in syntaxTree.Alarms)
            {
                var alarm = AlarmlistFactory.CreateAlarmFromSyntaxNode(alarmSyntax);
                alarmList.Add(alarm);
            }
            return alarmList;
        }
    }
}
