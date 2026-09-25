// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public class LiveStatusTests
    {
        [Fact]
        public void ChangedVisibleStatusRaisesLiveRegionEventWithUpdatedText()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var status = new LiveStatusTextBlock { Text = "Waiting for analysis." };
                using (var host = new HwndSource(new HwndSourceParameters("ALMX live status test")
                { Width = 480, Height = 100, WindowStyle = unchecked((int)0x90000000) }))
                {
                    host.RootVisual = new StackPanel { Children = { status } };
                    InheritanceAdornerTests.Pump();
                    Assert.True(status.IsLoaded);
                    Assert.True(status.IsVisible);
                    Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
                    IntPtr handle = host.Handle;
                    Dispatcher dispatcher = status.Dispatcher;
                    var completed = new DispatcherFrame();
                    Task client = Task.Run(() =>
                    {
                        var announcements = new ConcurrentQueue<string>();
                        using (var changed = new AutoResetEvent(false))
                        {
                            AutomationElement root = AutomationElement.FromHandle(handle);
                            AutomationEventHandler handler = (sender, args) =>
                            {
                                announcements.Enqueue(((AutomationElement)sender).Current.Name);
                                changed.Set();
                            };
                            Automation.AddAutomationEventHandler(AutomationElementIdentifiers.LiveRegionChangedEvent,
                                root, TreeScope.Subtree, handler);
                            try
                            {
                                foreach (string message in new[] { "The reference could not be resolved.", "Values are current." })
                                {
#pragma warning disable VSTHRD001
                                    dispatcher.Invoke(() => status.Text = message);
#pragma warning restore VSTHRD001
                                    Assert.True(changed.WaitOne(TimeSpan.FromSeconds(5)), "No live-region announcement was received.");
                                    Assert.True(announcements.TryDequeue(out string announced));
                                    Assert.Equal(message, announced);
                                }
                            }
                            finally
                            {
                                Automation.RemoveAutomationEventHandler(AutomationElementIdentifiers.LiveRegionChangedEvent, root, handler);
                            }
                        }
                    });
#pragma warning disable VSTHRD001
                    Task completion = client.ContinueWith(task => dispatcher.BeginInvoke(new Action(() => completed.Continue = false)), TaskScheduler.Default);
#pragma warning restore VSTHRD001
                    Dispatcher.PushFrame(completed);
#pragma warning disable VSTHRD002, xUnit1031
                    client.GetAwaiter().GetResult();
                    completion.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002, xUnit1031
                }
            });
        }
    }
}
