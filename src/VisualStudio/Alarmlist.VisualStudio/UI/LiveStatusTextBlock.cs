// Created/modified by Arkarin0 under one ore more license(s).

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Alarmlist.VisualStudio.UI
{
    public sealed class LiveStatusTextBlock : TextBlock
    {
        static LiveStatusTextBlock()
        {
            AutomationProperties.LiveSettingProperty.OverrideMetadata(typeof(LiveStatusTextBlock),
                new FrameworkPropertyMetadata(AutomationLiveSetting.Polite));
            TextProperty.OverrideMetadata(typeof(LiveStatusTextBlock),
                new FrameworkPropertyMetadata(string.Empty, OnTextChanged));
        }

        private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var block = (LiveStatusTextBlock)sender;
            // LiveSetting describes the region but does not announce its updates.
            // Raise only after the displayed text changed; hidden/closed previews
            // and empty status strings should not interrupt the reader.
            if (Equals(args.OldValue, args.NewValue)
                || !block.IsLoaded || !block.IsVisible || string.IsNullOrWhiteSpace(block.Text)
                || !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)) return;

            AutomationPeer peer = UIElementAutomationPeer.FromElement(block)
                ?? UIElementAutomationPeer.CreatePeerForElement(block);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
