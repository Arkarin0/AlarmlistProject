// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Alarmlist.VisualStudio.Analysis
{
    internal interface IOpenAnalysisDocuments
    {
        event EventHandler<AnalysisFileChangedEventArgs> Changed;
        Task<IReadOnlyDictionary<string, CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token);
    }

    /// <summary>Watches only evaluated inputs and the selected compiler; never discovers project membership.</summary>
    internal sealed class ProjectAnalysisInputSource : IProjectAnalysisInputSource
    {
        private readonly IOpenAnalysisDocuments _documents;
        private readonly object _gate = new object();
        private readonly object _lifecycleGate = new object();
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private string[] _paths = Array.Empty<string>();
        private string[] _documentPaths = Array.Empty<string>();
        private bool _disposed;
        private bool _restartWatchers;
        public event EventHandler<AnalysisFileChangedEventArgs> Changed;

        public ProjectAnalysisInputSource(IOpenAnalysisDocuments documents)
        {
            _documents = documents;
            _documents.Changed += OnDocumentChanged;
        }

        public void Watch(AnalysisProject project)
        {
            string[] paths = project == null ? Array.Empty<string>() : project.Inputs
                .Concat(project.Resources.Select(resource => resource.FilePath))
                .Concat(project.Lookups)
                .Concat(string.IsNullOrWhiteSpace(project.CompilerPath) ? Array.Empty<string>() : new[] { project.CompilerPath })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            lock (_lifecycleGate)
            {
                FileSystemWatcher[] previous;
                lock (_gate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(ProjectAnalysisInputSource));
                    _documentPaths = project?.Inputs.ToArray() ?? Array.Empty<string>();
                    if (!_restartWatchers && paths.SequenceEqual(_paths, StringComparer.Ordinal)) return;
                    previous = _watchers.ToArray();
                    _watchers.Clear();
                    _paths = Array.Empty<string>();
                }
                // Framework FileSystemWatcher invokes callbacks under its own
                // monitor; Component.Dispose takes the same monitor. Never hold
                // our callback gate while disposing or changing a watcher.
                foreach (FileSystemWatcher watcher in previous) watcher.Dispose();
                var created = new List<FileSystemWatcher>();
                try
                {
                    foreach (string directory in paths.Select(ExistingParent).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var watcher = new FileSystemWatcher(directory)
                        {
                            IncludeSubdirectories = true,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                        };
                        created.Add(watcher);
                        watcher.Changed += OnDiskChanged;
                        watcher.Created += OnDiskChanged;
                        watcher.Deleted += OnDiskChanged;
                        watcher.Renamed += OnDiskRenamed;
                        watcher.Error += OnWatcherError;
                    }
                    lock (_gate)
                    {
                        _paths = paths;
                        _watchers.AddRange(created);
                        _restartWatchers = false;
                    }
                    foreach (FileSystemWatcher watcher in created) watcher.EnableRaisingEvents = true;
                }
                catch
                {
                    lock (_gate) { _watchers.Clear(); _paths = Array.Empty<string>(); }
                    foreach (FileSystemWatcher watcher in created) watcher.Dispose();
                    throw;
                }
            }
        }

        private static string ExistingParent(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            // Watch an existing ancestor so a missing directory/input can recover.
            while (directory != null && !Directory.Exists(directory)) directory = Path.GetDirectoryName(directory);
            return directory ?? throw new IOException("Cannot watch analysis input " + path);
        }

        public async Task<IReadOnlyList<CapturedAnalysisInput>> CaptureAsync(IReadOnlyList<string> paths, CancellationToken token)
        {
            IReadOnlyDictionary<string, CapturedAnalysisInput> open = await _documents.CaptureAsync(paths, token).ConfigureAwait(false);
            var captured = new List<CapturedAnalysisInput>();
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                if (open.TryGetValue(path, out CapturedAnalysisInput input)) captured.Add(input);
                else
                {
                    // Disk versions are scoped to the project generation. Comparing
                    // captured text on publication detects same-timestamp rewrites.
                    try { captured.Add(new CapturedAnalysisInput(new AnalysisInput(ReadXmlText(path), path, 0))); }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is XmlException)
                    { throw new IOException("Cannot capture ALMX input " + path + ": " + exception.Message, exception); }
                }
            }
            return captured.AsReadOnly();
        }

        internal static string ReadXmlText(string path)
        {
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var captured = new MemoryStream())
            { stream.CopyTo(captured); bytes = captured.ToArray(); }
            Encoding encoding;
            using (var stream = new MemoryStream(bytes, false))
            using (var xml = new XmlTextReader(stream) { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })
            {
                // Detect encoding from one captured byte sequence, preserving text.
                // The SDK diagnoses malformed XML, including located RESX errors.
                try { xml.Read(); }
                catch (XmlException) { }
                encoding = xml.Encoding ?? Encoding.UTF8;
            }
            string text = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes);
            return text.Length != 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }

        public Task<IReadOnlyList<AnalysisResourceInput>> CaptureResourcesAsync(IReadOnlyList<AnalysisResourceItem> resources, CancellationToken token)
        {
            // Never ask the open-document provider for resource text, even if a
            // RESX file is open in the XML editor instead of the native editor.
            var saved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var captured = new List<AnalysisResourceInput>();
            foreach (AnalysisResourceItem resource in resources)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (!saved.TryGetValue(resource.FilePath, out string text)) saved.Add(resource.FilePath, text = ReadXmlText(resource.FilePath));
                    captured.Add(new AnalysisResourceInput(text, resource.FilePath, 0, resource.Culture));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is XmlException || exception is ArgumentException)
                { throw new IOException("Cannot capture saved RESX input " + resource.FilePath + ": " + exception.Message, exception); }
            }
            return Task.FromResult<IReadOnlyList<AnalysisResourceInput>>(captured.AsReadOnly());
        }

        public Task<IReadOnlyList<AnalysisLookupInput>> CaptureLookupsAsync(IReadOnlyList<string> paths, CancellationToken token)
        {
            // Generated catalogues are consumed from saved bytes, never editor drafts.
            var saved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var captured = new List<AnalysisLookupInput>();
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (!saved.TryGetValue(path, out string text)) saved.Add(path, text = ReadXmlText(path));
                    captured.Add(new AnalysisLookupInput(text, path, 0));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is XmlException || exception is ArgumentException)
                { throw new IOException("Cannot capture saved lookup input " + path + ": " + exception.Message, exception); }
            }
            return Task.FromResult<IReadOnlyList<AnalysisLookupInput>>(captured.AsReadOnly());
        }

        private void OnDocumentChanged(object sender, AnalysisFileChangedEventArgs args)
        {
            string[] selected;
            lock (_gate)
            {
                if (_disposed) return;
                selected = args.Path == null ? _documentPaths : _documentPaths.Where(path =>
                    string.Equals(path, args.Path, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            foreach (string path in selected) Notify(path);
        }
        private void OnDiskChanged(object sender, FileSystemEventArgs args) => Notify(args.FullPath);
        private void OnDiskRenamed(object sender, RenamedEventArgs args) { Notify(args.OldFullPath); Notify(args.FullPath); }
        private void OnWatcherError(object sender, ErrorEventArgs args)
        {
            lock (_gate) _restartWatchers = true;
            Notify(null);
        }

        private void Notify(string changedPath)
        {
            string[] affected;
            lock (_gate)
            {
                if (_disposed) return;
                // Directory creation/deletion also invalidates selected descendants.
                affected = changedPath == null ? new string[] { null } : _paths.Where(path =>
                    string.Equals(path, changedPath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(changedPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            foreach (string path in affected) Changed?.Invoke(this, new AnalysisFileChangedEventArgs(path));
        }

        public void Dispose()
        {
            lock (_lifecycleGate)
            {
                FileSystemWatcher[] previous;
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    previous = _watchers.ToArray();
                    _watchers.Clear();
                    _paths = Array.Empty<string>();
                    _documentPaths = Array.Empty<string>();
                }
                _documents.Changed -= OnDocumentChanged;
                foreach (FileSystemWatcher watcher in previous) watcher.Dispose();
            }
        }
    }
}
