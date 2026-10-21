// Created/modified by Arkarin0 under one ore more license(s).

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public class MenuIconTests
    {
        [Fact]
        public void IconsAreIndependentDecorativeAndTrackNativeMenuThemeChanges()
        {
            InheritanceAdornerTests.RunOnSta(() =>
            {
                var extension = new MenuIconExtension(KnownMonikers.Inheritance);
                var icon = (CrispImage)extension.ProvideValue(null);
                Assert.NotSame(icon, extension.ProvideValue(null));
                Assert.Equal(KnownMonikers.Inheritance, icon.Moniker);
                Assert.Equal(16, icon.Width);
                Assert.Equal(16, icon.Height);
                Assert.False(icon.Focusable);
                Assert.False(icon.IsHitTestVisible);
                var menu = new ContextMenu();
                object backgroundKey = Assembly.Load("Microsoft.VisualStudio.Shell.15.0")
                    .GetType("Microsoft.VisualStudio.PlatformUI.EnvironmentColors", true)
                    .GetProperty("CommandBarMenuIconBackgroundColorKey").GetValue(null);
                menu.Items.Add(new MenuItem { Header = "Use inherited value", Icon = icon });
                try
                {
                    menu.IsOpen = true;
                    foreach (Color color in new[] { Colors.White, Color.FromRgb(32, 32, 32), Colors.Black })
                    {
                        menu.Background = new SolidColorBrush(color);
                        menu.Resources[backgroundKey] = color;
                        InheritanceAdornerTests.Pump();
                        Assert.Equal(color, icon.GetValue(ImageThemingUtilities.ImageBackgroundColorProperty));
                    }
                }
                finally { menu.IsOpen = false; }
            });
        }
    }
}
