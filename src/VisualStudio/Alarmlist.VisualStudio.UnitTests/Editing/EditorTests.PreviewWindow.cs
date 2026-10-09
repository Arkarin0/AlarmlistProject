// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Editor;
using Alarmlist.VisualStudio.UI;
using Xunit;

#pragma warning disable VSTHRD003
namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Fact]
        public void PreviewFollowsDocumentInitializationSwitchCloseAndHideWithoutCommittingDrafts()
        {
            using (var first = new SemanticSession())
            using (var second = new SemanticSession())
            using (var context = new PreviewDocumentContext())
            {
                var document = new PreviewDocument();
                context.Select(document);
                Assert.Null(context.Source);
                document.Set(first.View);
                Assert.Same(first.View, context.Source);
                first.Field("Code").Value = "Private draft";
                Assert.Empty(context.Source.ResolvedFields);
                Assert.Null(context.Source.CompiledXml);
                var next = new PreviewDocument();
                next.Set(second.View);
                context.Select(next);
                Assert.Equal(0, document.Subscribers);
                Assert.Same(second.View, context.Source);
                Assert.True(first.View.HasPending);
                Assert.Equal(0, first.Buffer.Edits);
                next.Set(null);
                Assert.Null(context.Source);
                context.Select(null);
                Assert.Equal(0, next.Subscribers);
                document.Set(first.View);
                Assert.Null(context.Source);
            }
        }

        [Fact]
        public async Task PreviewDoesNotNavigateAfterSwitchingDocumentsDuringValidationAsync()
        {
            using (var session = new SemanticSession())
            using (var context = new PreviewDocumentContext())
            {
                var document = new PreviewDocument();
                document.Set(session.View);
                context.Select(document);
                var validation = new TaskCompletionSource<bool>();
                session.Analysis.Validation = validation.Task;
                session.View.ResolvedFields[1].GoToSourceCommand.Execute(null);
                context.Select(null);
                validation.SetResult(true);
                await session.View.PendingAnalysisAction;
                Assert.Null(session.Analysis.Navigation);
            }
        }

        [Fact]
        public void VisiblePreviewContextDoesNotRetainClosedDocumentOrAnalysis()
        {
            using (var context = new PreviewDocumentContext())
            {
                WeakReference[] retired = ClosePreviewDocument(context);
                for (int index = 0; index < 3; index++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
                Assert.Null(context.Source);
                Assert.All(retired, item => Assert.False(item.IsAlive));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] ClosePreviewDocument(PreviewDocumentContext context)
        {
            using (var session = new SemanticSession())
            {
                var document = new PreviewDocument();
                document.Set(session.View);
                context.Select(document);
                var retired = new[] { new WeakReference(document), new WeakReference(session.View), new WeakReference(session.Analysis.Current) };
                document.Set(null);
                return retired;
            }
        }

        private sealed class PreviewDocument : IPreviewDocument
        {
            public EditFileControlViewModel PreviewSource { get; private set; }
            public event EventHandler PreviewSourceChanged;
            public int Subscribers => PreviewSourceChanged?.GetInvocationList().Length ?? 0;
            public void Set(EditFileControlViewModel source) { PreviewSource = source; PreviewSourceChanged?.Invoke(this, EventArgs.Empty); }
        }
    }
}
