// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Alarmlist.MSBuild
{
    /// <summary>Stages a complete batch and rolls back ordinary installation failures.</summary>
    internal sealed class OutputTransaction
    {
        private readonly List<Change> _changes;
        private readonly Action<string> _validatePath;

        internal OutputTransaction(IEnumerable<Change> changes, Action<string> validatePath = null)
        {
            _changes = changes.ToList();
            _validatePath = validatePath;
            if (_changes.Select(change => change.Path).Distinct(BuildOutputPlan.PathComparer).Count() != _changes.Count)
                throw new ArgumentException("Output transaction paths must be unique.");
        }

        internal IEnumerable<string> TemporaryPaths => _changes.SelectMany(change => new[] { change.Stage, change.Backup });

        internal void Commit(Action checkpoint, Action<string> cleanupWarning)
        {
            foreach (Change change in _changes)
            {
                _validatePath?.Invoke(change.Path);
                _validatePath?.Invoke(change.Stage);
                _validatePath?.Invoke(change.Backup);
            }
            var installed = new List<Change>();
            bool preserveBackups = false;
            try
            {
                foreach (Change change in _changes)
                {
                    checkpoint();
                    if (change.Content == null) continue;
                    _validatePath?.Invoke(change.Path);
                    _validatePath?.Invoke(change.Stage);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(change.Path));
                    _validatePath?.Invoke(change.Stage);
                    using (var stream = new FileStream(change.Stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        change.StageCreated = true;
                        stream.Write(change.Content, 0, change.Content.Length);
                    }
                }
                foreach (Change change in _changes)
                {
                    checkpoint();
                    _validatePath?.Invoke(change.Path);
                    _validatePath?.Invoke(change.Stage);
                    _validatePath?.Invoke(change.Backup);
                    if (change.ExpectedBytes != null && (!File.Exists(change.Path)
                        || !File.ReadAllBytes(change.Path).SequenceEqual(change.ExpectedBytes)))
                        throw new IOException("The import destination changed after capture: " + change.Path);
                    change.HadOriginal = File.Exists(change.Path);
                    if (change.Content == null)
                    {
                        if (change.HadOriginal)
                        {
                            File.Move(change.Path, change.Backup);
                            change.BackupCreated = true;
                        }
                    }
                    else if (change.HadOriginal)
                    {
                        if (File.Exists(change.Backup)) throw new IOException("An output backup path is already occupied: " + change.Backup);
                        File.Replace(change.Stage, change.Path, change.Backup);
                        change.BackupCreated = true;
                    }
                    else File.Move(change.Stage, change.Path);
                    change.Installed = true;
                    installed.Add(change);
                }
                checkpoint();
            }
            catch (Exception exception)
            {
                var failures = new List<Exception> { exception };
                foreach (Change change in installed.AsEnumerable().Reverse())
                {
                    try
                    {
                        _validatePath?.Invoke(change.Path);
                        _validatePath?.Invoke(change.Backup);
                        if (change.ExpectedBytes != null && change.Content != null && (!File.Exists(change.Path)
                            || !File.ReadAllBytes(change.Path).SequenceEqual(change.Content)))
                            throw new IOException("The installed import was independently modified; refusing to overwrite the newer edit.");
                        if (change.HadOriginal)
                        {
                            if (File.Exists(change.Path)) File.Replace(change.Backup, change.Path, null);
                            else File.Move(change.Backup, change.Path);
                        }
                        else if (change.Content != null) File.Delete(change.Path);
                    }
                    catch (Exception restore)
                    {
                        preserveBackups = true;
                        failures.Add(new IOException("Could not restore '" + change.Path + "'; retained backup '" + change.Backup + "'.", restore));
                    }
                }
                if (failures.Count > 1) throw new AggregateException("Output installation and restoration failed.", failures);
                throw;
            }
            finally
            {
                foreach (Change change in _changes)
                {
                    if (change.StageCreated) Cleanup(change.Stage, cleanupWarning);
                    if (change.BackupCreated && !preserveBackups) Cleanup(change.Backup, cleanupWarning);
                }
            }
        }

        private void Cleanup(string path, Action<string> warning)
        {
            try { _validatePath?.Invoke(path); File.Delete(path); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { warning("Could not remove owned temporary file '" + path + "': " + exception.Message); }
        }

        internal sealed class Change
        {
            internal Change(string path, string text, byte[] expectedBytes = null)
            {
                Path = System.IO.Path.GetFullPath(path);
                Text = text;
                ExpectedBytes = expectedBytes == null ? null : (byte[])expectedBytes.Clone();
                string prefix = Path + ".alarmlist-" + Guid.NewGuid().ToString("N");
                Stage = prefix + ".stage";
                Backup = prefix + ".backup";
            }

            internal string Path { get; }
            private string _text;
            internal string Text
            {
                get => _text;
                set { _text = value; Content = value == null ? null : new UTF8Encoding(false, true).GetBytes(value); }
            }
            internal byte[] Content { get; private set; }
            internal static Change FromBytes(string path, byte[] bytes) => new Change(path, null)
            { Content = (byte[])(bytes ?? throw new ArgumentNullException(nameof(bytes))).Clone() };
            internal byte[] ExpectedBytes { get; }
            internal bool Installed { get; set; }
            internal string Stage { get; }
            internal string Backup { get; }
            internal bool HadOriginal { get; set; }
            internal bool StageCreated { get; set; }
            internal bool BackupCreated { get; set; }
        }
    }
}
