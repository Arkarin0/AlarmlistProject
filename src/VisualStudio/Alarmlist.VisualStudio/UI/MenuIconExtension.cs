// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Windows;
using System.Windows.Markup;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;

namespace Alarmlist.VisualStudio.UI
{
    /// <summary>Creates a separate themed, DPI-aware image for each menu item.</summary>
    [MarkupExtensionReturnType(typeof(CrispImage))]
    public sealed class MenuIconExtension : MarkupExtension
    {
        public MenuIconExtension(ImageMoniker moniker)
        {
            Moniker = moniker;
        }

        public ImageMoniker Moniker { get; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var image = new CrispImage
            {
                Moniker = Moniker,
                Width = 16,
                Height = 16,
                IsHitTestVisible = false,
                Focusable = false
            };
            // Native menu templates can paint a gradient without setting the
            // ContextMenu.Background property. Use the actual icon-gutter color.
            image.SetResourceReference(ImageThemingUtilities.ImageBackgroundColorProperty,
                EnvironmentColors.CommandBarMenuIconBackgroundColorKey);
            return image;
        }
    }
}
