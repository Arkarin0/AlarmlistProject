// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.Editing
{
    internal sealed class ReferenceRenameBuffers
    {
        internal ReferenceRenameBuffers(SolutionRenameProposal proposal, IEnumerable<RenameDocumentChange> changes)
        { Proposal = proposal; Changes = Array.AsReadOnly(changes.ToArray()); }
        internal SolutionRenameProposal Proposal { get; }
        internal IReadOnlyList<RenameDocumentChange> Changes { get; }
    }

    // Converts the exact approved physical set into the production linked edit.
    // SDK saved inputs use version zero; an already open resource/project buffer
    // can have another version. Adopt its actual version only after exact text
    // comparison, before approval, then require that version through Query Edit.
    internal sealed class ReferenceRenameApplication
    {
        internal ReferenceRenameBuffers Capture(IRenameEditWorkspace workspace, SolutionRenameProposal proposal,
            Action validateCapturedInputs, CancellationToken token = default)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (proposal == null) throw new ArgumentNullException(nameof(proposal));
            if (validateCapturedInputs == null) throw new ArgumentNullException(nameof(validateCapturedInputs));
            if (!proposal.IsApplicable) throw new InvalidOperationException("The selected rename has not passed validation.");
            token.ThrowIfCancellationRequested(); validateCapturedInputs();
            var changes = new List<RenameDocumentChange>();
            foreach (var file in proposal.Selection.Changes.GroupBy(change => change.Edit.Path, StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested(); validateCapturedInputs();
                var edits = file.ToArray();
                string before = edits[0].Before;
                if (edits.Any(edit => edit.Before != before)) throw new InvalidOperationException("The approved changes have incompatible captured text: " + file.Key);
                var document = workspace.Open(file.Key);
                var actual = document.Capture();
                if (document.IsReadOnly) throw new InvalidOperationException("The document is read-only: " + file.Key);
                if (actual.Text != before) throw new InvalidOperationException("An editable buffer differs from the captured inputs. Preview again: " + file.Key);
                changes.Add(new RenameDocumentChange(file.Key, actual, edits.Select(change => new SourceEdit(change.Edit.Start, change.Edit.Length, change.Edit.Text))));
            }
            token.ThrowIfCancellationRequested(); validateCapturedInputs();
            return new ReferenceRenameBuffers(proposal, changes);
        }

        internal void Apply(IRenameEditWorkspace workspace, SolutionRenameProposal proposal, ReferenceRenameBuffers buffers,
            bool acknowledged, Action validateCapturedInputs, CancellationToken token = default)
        {
            if (proposal == null) throw new ArgumentNullException(nameof(proposal));
            if (buffers == null) throw new ArgumentNullException(nameof(buffers));
            if (!ReferenceEquals(buffers.Proposal, proposal) || !proposal.IsApplicable)
                throw new InvalidOperationException("The reviewed rename or its selected changes changed. Preview again.");
            if (proposal.RequiresAcknowledgement && !acknowledged)
                throw new InvalidOperationException("Acknowledge skipped findings, remaining references and expected diagnostics before applying.");
            // This token belongs to the explicit Apply operation. The rename's
            // own buffer changes obsolete search/preview sessions, but must not
            // cancel their own in-progress linked transaction.
            new LinkedDocumentEdit().Apply(workspace, buffers.Changes, validateCapturedInputs, token);
        }
    }
}
