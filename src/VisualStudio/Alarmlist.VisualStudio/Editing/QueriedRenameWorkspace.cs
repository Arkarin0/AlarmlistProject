// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Alarmlist.VisualStudio.Editing
{
    // Checkout can reload projects and buffers. Perform it before the caller's
    // asynchronous recapture, then allow exactly one linked transaction for the
    // same approved physical set. A second checkout after recapture would undo
    // that freshness guarantee. Buffer/version checks still run in LinkedDocumentEdit.
    internal sealed class QueriedRenameWorkspace : IRenameEditWorkspace
    {
        private readonly IRenameEditWorkspace _workspace;
        private readonly string[] _paths;
        private bool _consumed;

        private QueriedRenameWorkspace(IRenameEditWorkspace workspace, string[] paths)
        { _workspace = workspace; _paths = paths; }

        internal static QueriedRenameWorkspace Prepare(IRenameEditWorkspace workspace, IEnumerable<string> paths)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            var approved = paths.Select(Path.GetFullPath).ToArray();
            if (approved.Length == 0 || approved.Distinct(StringComparer.OrdinalIgnoreCase).Count() != approved.Length)
                throw new InvalidOperationException("Checkout requires the complete approved physical file set.");
            workspace.QueryEdit(Array.AsReadOnly(approved));
            return new QueriedRenameWorkspace(workspace, approved);
        }
        public IRenameDocument Open(string path)
        {
            if (!_paths.Contains(Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("The approved rename file set changed after checkout.");
            return _workspace.Open(path);
        }
        public void QueryEdit(IReadOnlyList<string> paths)
        {
            if (_consumed || !_paths.SequenceEqual(paths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("The approved rename file set changed after checkout. Preview again.");
            _consumed = true;
        }
        public IRenameUndoScope BeginUndo(string description)
        {
            if (!_consumed) throw new InvalidOperationException("Revalidate the complete rename file set before editing.");
            return _workspace.BeginUndo(description);
        }
    }
}
