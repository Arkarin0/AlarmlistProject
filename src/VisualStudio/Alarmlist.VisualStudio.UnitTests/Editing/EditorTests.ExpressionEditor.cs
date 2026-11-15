// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void LexicalDraftContextRetainsProvenIdentityWithoutEnablingSemanticActions()
        {
            string source = PropertyChild.Replace("</Alarm>", "<Code>$(par1)</Code></Alarm>");
            using (var session = new SemanticSession(source))
            {
                session.Analyze(PropertyBase); var field = session.Field("Code");
                var original = session.View.ExpressionContext(field); Assert.NotNull(original); Assert.False(original.IsDraft);
                Assert.Equal("Field", original.Target.Kind); Assert.Equal("Code", original.Target.Name); Assert.NotNull(original.Location);
                field.EditValue = "draft $(par1";
                Assert.True(session.View.HasPending); var draft = session.View.ExpressionContext(field); Assert.NotNull(draft);
                Assert.True(draft.IsDraft); Assert.Same(original.State, draft.State); Assert.Same(original.Target, draft.Target);
                Assert.False(session.View.ReferenceActions.CanFindToken(field)); Assert.Equal(source, session.Buffer.Current.Text);
                session.View.CancelPending(); Assert.False(session.View.ExpressionContext(field).IsDraft);
                field.EditValue = "committed"; Assert.True(session.View.CommitPending());
                Assert.Null(session.View.ExpressionContext(field));
                session.Analyze(PropertyBase); Assert.NotNull(session.View.ExpressionContext(field));
            }
        }

        [Fact]
        public void ExpressionContextRejectsPendingAnalysisAndDisposedViews()
        {
            var session = new SemanticSession(PropertyChild);
            var view = session.View; var field = session.Field("Description");
            try
            {
                session.Analyze(PropertyBase); Assert.NotNull(view.ExpressionContext(field));
                session.Analysis.Set(new ProjectAnalysisState(90, session.Analysis.Current.Project, ProjectAnalysisStatus.Pending, "Pending"));
                Assert.Null(view.ExpressionContext(field));
                session.Analyze(PropertyBase); Assert.NotNull(view.ExpressionContext(field));
                Assert.Null(view.ExpressionContext(new object()));
            }
            finally { session.Dispose(); }
            Assert.Null(view.ExpressionContext(field));
        }

        [Fact]
        public async Task TokenRequestsKeepValueBindingAndSeparateCaretDiscoveryFromPublishedIntentAsync()
        {
            string source = PropertyChild.Replace("</Alarm>", "<Code>$(par1)</Code></Alarm>");
            using (var session = new SemanticSession(source))
            using (var caretLifetime = new CancellationTokenSource())
            {
                session.Analyze(PropertyBase); var field = session.Field("Code"); AnalysisReferenceTarget requested = null;
                session.Analysis.ReferencesHandler = (state, target, current, token) => { requested = target; return System.Threading.Tasks.Task.CompletedTask; };
                bool currentCaret = true;
                session.View.ReferenceActions.FindToken(field, 2, () => currentCaret, caretLifetime.Token, definitionsOnly: true);
#pragma warning disable VSTHRD003 // FindToken starts this exact request immediately above in the test's context.
                await session.View.ReferenceActions.Pending;
#pragma warning restore VSTHRD003
                Assert.Equal("ValueToken", requested.Kind); Assert.Equal(2, requested.Offset);
                Assert.True(session.Analysis.ReferencesInvocation.DefinitionsOnly); Assert.False(session.Analysis.ReferencesInvocation.IsRename);
                var invocation = session.Analysis.ReferencesInvocation; Assert.True(invocation.IsCurrent()); Assert.True(invocation.IntentIsCurrent());
                currentCaret = false; caretLifetime.Cancel(); Assert.False(invocation.IsCurrent()); Assert.True(invocation.IntentIsCurrent());
                Assert.True(invocation.Token.IsCancellationRequested); Assert.False(invocation.IntentToken.IsCancellationRequested);
                field.EditValue = "private"; Assert.True(invocation.IntentToken.IsCancellationRequested);
                Assert.Equal(source, session.Buffer.Current.Text); Assert.True(session.View.HasPending);
            }
        }

        [Fact]
        public void TokenCommandsNeverCommitOrDiscoverPrivateDrafts()
        {
            using (var session = new SemanticSession(PropertyChild))
            {
                session.Analyze(PropertyBase); var field = session.Field("Description"); field.EditValue = "$(par1) draft";
                Assert.False(session.View.ReferenceActions.CanFindToken(field));
                session.View.ReferenceActions.FindToken(field, 2, () => true, CancellationToken.None, rename: true);
                Assert.Null(session.Analysis.ReferencesInvocation); Assert.True(session.View.HasPending); Assert.Equal(0, session.Buffer.Edits);
            }
        }
    }
}
