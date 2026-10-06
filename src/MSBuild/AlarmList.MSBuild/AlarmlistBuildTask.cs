using System;
using System.IO;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Output;
using Alarmlist.Text;
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

        public string ProjectPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public ITaskItem[] Properties { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public string OutputFile { get; set; }

        public AlarmlistBuildTask()
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

                var context = new CompilationContext(ProjectPath, Configuration, Platform,
                    (Properties ?? Array.Empty<ITaskItem>()).Select(item => new PropertyDefinition(item.ItemSpec,
                        item.MetadataNames.Cast<string>().Any(name => string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase))
                            ? item.GetMetadata("Value") : null)));
                var result = _compileAction == null ? new AlarmCompiler().CompileFiles(sourceFiles, context) : _compileAction(sourceFiles);
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
                SourceLocation location = diagnostic.Location;
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        Log.LogError(null, diagnostic.Id, null, location?.FilePath,
                            location?.LineNumber ?? 0, location?.ColumnNumber ?? 0,
                            location?.EndLineNumber ?? 0, location?.EndColumnNumber ?? 0, "{0}", diagnostic.Message);
                        break;

                    case DiagnosticSeverity.Warning:
                        Log.LogWarning(null, diagnostic.Id, null, location?.FilePath,
                            location?.LineNumber ?? 0, location?.ColumnNumber ?? 0,
                            location?.EndLineNumber ?? 0, location?.EndColumnNumber ?? 0, "{0}", diagnostic.Message);
                        break;

                    default:
                        Log.LogMessage(MessageImportance.Normal, "{0}: {1}", diagnostic.Id, diagnostic.Message);
                        break;
                }
            }
        }

    }
}
