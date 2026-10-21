// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClosedDesignerDoesNotKeepItsModelOrAnalysisAliveThroughWpfViews(bool withError)
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                WeakReference[] retired = CreateAndCloseDesigner(withError);
                // WPF releases weak listeners through dispatcher cleanup after
                // collection. Keep this STA alive and allow that cleanup to run.
                for (int pass = 0; pass < 3; pass++)
                {
                    InheritanceAdornerTests.Pump();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                Assert.All(retired, reference => Assert.False(reference.IsAlive));
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] CreateAndCloseDesigner(bool withError)
        {
            using (var session = new SemanticSession(WithProcedure(ChildAlarm,
                "<Group Name='Instructions'><Note>Local note</Note></Group><Group Name='Reset'><Clear/></Group>")))
            {
                session.Analyze(ProcedureBase);
                var control = new EditFileControl(session.View) { Width = 950, Height = 900 };
                // Exercise real BAML bindings without an HWND. External UIA
                // clients and native window teardown have separate lifetimes.
                control.Measure(new Size(950, 900));
                control.Arrange(new Rect(0, 0, 950, 900));
                InheritanceAdornerTests.Pump();
                foreach (Expander panel in VisualChildren(control).OfType<Expander>().ToArray()) panel.IsExpanded = true;
                control.UpdateLayout();
                InheritanceAdornerTests.Pump();
                Assert.Contains(VisualChildren(control).OfType<TextBox>(), box => box.Text == "Inherited");
                Assert.Contains(VisualChildren(control).OfType<TextBox>(), box => box.Text == "Open");
                if (withError)
                {
                    session.Buffer.SetText(session.Buffer.Current.Text.Replace("<ReferenceName>Base</ReferenceName>", "<ReferenceName>Missing</ReferenceName>"));
                    session.Analyze(ProcedureBase);
                }
                control.UpdateLayout();
                InheritanceAdornerTests.Pump();
                if (withError) Assert.Contains(VisualChildren(control).OfType<TextBlock>(),
                    block => block.Text == session.View.Diagnostics[0].Message);
                var retired = new[] { new WeakReference(control), new WeakReference(session.View),
                    new WeakReference(session.Analysis.Current), new WeakReference(session.Analysis.Current.Project) };
                return retired;
            }
        }
    }
}
