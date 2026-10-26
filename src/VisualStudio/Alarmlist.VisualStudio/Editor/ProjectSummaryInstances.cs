// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;

namespace Alarmlist.VisualStudio.Editor
{
    // Session-only identities. No frames, views or analysis snapshots are retained.
    internal sealed class ProjectSummaryInstances
    {
        private int _nextId;
        private readonly Dictionary<string, int> _names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public ProjectSummaryIdentity Create(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A project path is required.", nameof(path));
            path = Path.GetFullPath(path);
            string name = Path.GetFileNameWithoutExtension(path);
            _names.TryGetValue(name, out int count);
            _names[name] = ++count;
            return new ProjectSummaryIdentity(checked(++_nextId), path, "Alarm Summary — " + name + (count == 1 ? "" : " (" + count + ")"));
        }
    }

    internal sealed class ProjectSummaryIdentity
    {
        public int Id { get; }
        public string ProjectPath { get; }
        public string Caption { get; }
        public ProjectSummaryIdentity(int id, string path, string caption) { Id = id; ProjectPath = path; Caption = caption; }
    }
}
