// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class AnalysisProject
    {
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string CompilerPath { get; }
        public IReadOnlyList<string> Inputs { get; }
        public AnalysisContext Context { get; }

        public AnalysisProject(string projectPath, string configuration, string compilerPath, IEnumerable<string> inputs, AnalysisContext context = null)
        {
            ProjectPath = projectPath;
            Configuration = configuration;
            CompilerPath = compilerPath;
            Inputs = Array.AsReadOnly(inputs.ToArray());
            Context = context ?? new AnalysisContext(projectPath, configuration);
        }

        public bool HasSameCompilerContext(AnalysisProject other)
        {
            return other != null && ProjectPath == other.ProjectPath && Configuration == other.Configuration && CompilerPath == other.CompilerPath
                && Context.Matches(other.Context);
        }
    }

    internal enum ProjectAnalysisStatus { Pending, Available, Incomplete, Unavailable, Stopped }

    internal sealed class ProjectAnalysisState
    {
        public long Generation { get; }
        public AnalysisProject Project { get; }
        public ProjectAnalysisStatus Status { get; }
        public string Message { get; }
        public AnalysisResponse Response { get; }
        public IReadOnlyList<CapturedAnalysisInput> Inputs { get; }

        public ProjectAnalysisState(long generation, AnalysisProject project, ProjectAnalysisStatus status, string message,
            AnalysisResponse response = null, IReadOnlyList<CapturedAnalysisInput> inputs = null)
        {
            Generation = generation;
            Project = project;
            Status = status;
            Message = message;
            Response = response;
            Inputs = Array.AsReadOnly((inputs ?? Array.Empty<CapturedAnalysisInput>()).ToArray());
        }
    }

    internal sealed class CapturedAnalysisInput
    {
        public AnalysisInput Input { get; }
        // Null for disk input; an immutable ITextSnapshot for a live document.
        public object BufferSnapshot { get; }

        public CapturedAnalysisInput(AnalysisInput input, object bufferSnapshot = null)
        { Input = input; BufferSnapshot = bufferSnapshot; }

        public bool Matches(CapturedAnalysisInput other)
        {
            return Input.FilePath == other.Input.FilePath && Input.Version == other.Input.Version
                && Input.Text == other.Input.Text && ReferenceEquals(BufferSnapshot, other.BufferSnapshot);
        }
    }

    internal sealed class AnalysisFileChangedEventArgs : EventArgs
    {
        // Null means that all watched inputs must be refreshed (e.g. watcher overflow).
        public string Path { get; }
        public AnalysisFileChangedEventArgs(string path) { Path = path; }
    }

    internal interface IProjectAnalysisInputSource : IDisposable
    {
        event EventHandler<AnalysisFileChangedEventArgs> Changed;
        void Watch(AnalysisProject project);
        Task<IReadOnlyList<CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token);
    }
}
