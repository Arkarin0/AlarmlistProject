// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Alarmlist.VisualStudio.Editor;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Threading;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public class InheritanceAdornerTests
    {
        [Fact]
        public void ClosingDesignerDrainsQueuedNotificationsWithoutJoiningItsOwnTask()
        {
            RunOnSta(() =>
            {
                using (var threading = new JoinableTaskContext(Thread.CurrentThread, new DispatcherSynchronizationContext()))
                {
                    int notifications = 0;
                    var view = new DesignerAnalysisNotifications(threading, () => notifications++);
                    // Leave a UI dispatch queued while closing the view. This
                    // background action itself never waits for the UI thread.
#pragma warning disable VSTHRD002, xUnit1031
                    Assert.True(Task.Run(view.Post).Wait(TimeSpan.FromSeconds(5)));
#pragma warning restore VSTHRD002, xUnit1031
                    view.Dispose();
                    Assert.Equal(0, notifications);
                    view.Post();
                    Pump();
                    Assert.Equal(0, notifications);
                }
            });
        }

        [Fact]
        public void AdornerTracksStateThemeDataContextAndUnloadWithoutInterceptingInput()
        {
            RunOnSta(() =>
            {
                var field = new TextBox { Text = "Inherited value", Padding = new Thickness(26, 3, 5, 3),
                    IsReadOnly = true, Foreground = Brushes.DarkBlue };
                var decorator = new AdornerDecorator { Child = field, Width = 320, Height = 40 };
                InheritedValueBehavior.SetIsInherited(field, true);
                using (var host = new HwndSource(new HwndSourceParameters("ALMX adorner test")
                { Width = 360, Height = 100, WindowStyle = unchecked((int)0x80000000) }))
                {
                    host.RootVisual = decorator;
                    Pump();
                    AdornerLayer layer = AdornerLayer.GetAdornerLayer(field);
                    var adorner = Assert.IsType<InheritedValueAdorner>(Assert.Single(layer.GetAdorners(field)));
                    Assert.False(adorner.IsHitTestVisible);
                    Assert.False(adorner.Focusable);
                    Assert.Same(Brushes.DarkBlue, adorner.Brush);
                    field.Foreground = Brushes.White;
                    Pump();
                    Assert.Same(Brushes.White, adorner.Brush);
                    field.DataContext = new object();
                    Pump();
                    Assert.Single(layer.GetAdorners(field));
                    InheritedValueBehavior.SetIsInherited(field, false);
                    Assert.Null(layer.GetAdorners(field));
                    InheritedValueBehavior.SetIsInherited(field, true);
                    Assert.Single(layer.GetAdorners(field));
                    host.RootVisual = null;
                    Pump();
                    Assert.Null(layer.GetAdorners(field));
                    host.RootVisual = decorator;
                    Pump();
                    Assert.Single(layer.GetAdorners(field));
                }
            });
        }

        internal static void RunOnSta(Action action, TimeSpan? timeout = null)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception exception) { failure = exception; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(timeout ?? TimeSpan.FromSeconds(15)), "WPF test did not complete.");
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        // Pump the dedicated test STA through layout/binding work, outside VS.
#pragma warning disable VSTHRD001
        internal static void Pump() => Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);
#pragma warning restore VSTHRD001
    }
}
