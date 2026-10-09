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

        internal OutputTransaction(IEnumerable<Change> changes)
        {
            _changes = changes.ToList();
            if (_changes.Select(change => change.Path).Distinct(BuildOutputPlan.PathComparer).Count() != _changes.Count)
                throw new ArgumentException("Output transaction paths must be unique.");
        }

        internal IEnumerable<string> TemporaryPaths => _changes.SelectMany(change => new[] { change.Stage, change.Backup });

        internal void Commit(Action checkpoint, Action<string> cleanupWarning)
        {
            var installed = new List<Change>();
            bool preserveBackups = false;
            try
            {
                foreach (Change change in _changes)
                {
                    checkpoint();
                    if (change.Text == null) continue;
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(change.Path));
                    using (var stream = new FileStream(change.Stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        change.StageCreated = true;
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true))) writer.Write(change.Text);
                    }
                }
                foreach (Change change in _changes)
                {
                    checkpoint();
                    if (change.ExpectedBytes != null && (!File.Exists(change.Path)
                        || !File.ReadAllBytes(change.Path).SequenceEqual(change.ExpectedBytes)))
                        throw new IOException("The import destination changed after capture: " + change.Path);
                    change.HadOriginal = File.Exists(change.Path);
                    if (change.Text == null)
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
                        if (change.ExpectedBytes != null && change.Text != null && (!File.Exists(change.Path)
                            || !File.ReadAllBytes(change.Path).SequenceEqual(new UTF8Encoding(false, true).GetBytes(change.Text))))
                            throw new IOException("The installed import was independently modified; refusing to overwrite the newer edit.");
                        if (change.HadOriginal)
                        {
                            if (File.Exists(change.Path)) File.Replace(change.Backup, change.Path, null);
                            else File.Move(change.Backup, change.Path);
                        }
                        else if (change.Text != null) File.Delete(change.Path);
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

        private static void Cleanup(string path, Action<string> warning)
        {
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { warning("Could not remove owned temporary file '" + path + "': " + exception.Message); }
        }

        internal sealed class Change
        {
            internal Change(string path, string text, byte[] expectedBytes = null)
            {
                Path = System.IO.Path.GetFullPath(path);
                Text = text;
                ExpectedBytes = expectedBytes;
                string prefix = Path + ".alarmlist-" + Guid.NewGuid().ToString("N");
                Stage = prefix + ".stage";
                Backup = prefix + ".backup";
            }

            internal string Path { get; }
            internal string Text { get; set; }
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
