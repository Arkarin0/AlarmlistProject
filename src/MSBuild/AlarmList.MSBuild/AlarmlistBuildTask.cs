using System;
using System.IO;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Output;
using Microsoft.Build.Framework;

namespace Alarmlist.MSBuild
{
    public class AlarmlistBuildTask : AlarmlistBaseTask
    {
        private readonly Func<string[], CompilationResult> _compileAction;

        [Required]
        public string OutputDirectory { get; set; }

        public string OutputFileName { get; set; } = "Alarmlist.xml";

        [Required]
        public ITaskItem[] Compile { get; set; }

        [Output]
        public string OutputFile { get; set; }

        public AlarmlistBuildTask()
            : this(filePaths => new AlarmCompiler().CompileFiles(filePaths))
        {
        }

        internal AlarmlistBuildTask(Func<string[], CompilationResult> compileAction)
        {
            _compileAction = compileAction ?? throw new ArgumentNullException(nameof(compileAction));
        }

        public override bool Execute()
        {
            try
            {
                if (!base.Execute())
                    return false;

                if (Compile == null || Compile.Length == 0)
                {
                    Log.LogError("No ALMX files were provided to the Alarmlist compiler.");
                    return false;
                }

                var sourceFiles = Compile.ToIncludeList();
                Log.LogMessage(MessageImportance.High, "Compiling {0} ALMX file(s).", sourceFiles.Length);

                var result = _compileAction(sourceFiles);
                LogDiagnostics(result);

                if (!result.Success)
                    return false;

                Directory.CreateDirectory(OutputDirectory);
                OutputFile = Path.GetFullPath(Path.Combine(OutputDirectory, OutputFileName));
                var outputGenerator = new AlarmlistOutputGenerator();
                outputGenerator.Write(result.AlarmList, new AlarmlistOutputOptions(OutputFile));
                Log.LogMessage(MessageImportance.High, "Wrote compiled alarmlist to '{0}'.", OutputFile);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);

                return false;
            }
        }

        private void LogDiagnostics(CompilationResult result)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        Log.LogError(null, diagnostic.Id, null, null, 0, 0, 0, 0, diagnostic.Message);
                        break;

                    case DiagnosticSeverity.Warning:
                        Log.LogWarning(null, diagnostic.Id, null, null, 0, 0, 0, 0, diagnostic.Message);
                        break;

                    default:
                        Log.LogMessage(MessageImportance.Normal, "{0}: {1}", diagnostic.Id, diagnostic.Message);
                        break;
                }
            }
        }

    }
}
