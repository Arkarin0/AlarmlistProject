// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Alarmlist.VisualStudio.Editor
{
    public enum SourceNavigationView { Designer, XML }

    [ComVisible(true)]
    [Guid("7e4b0cc1-faea-4f10-b652-5b1da1b5291c")]
    public sealed class NavigationOptions : DialogPage
    {
        [Category("Navigation")]
        [DisplayName("Open ALMX sources in")]
        [Description("Choose Designer or XML for Go to source, preview and diagnostic links. Unsupported source locations use XML. Find continues to use XML.")]
        [DefaultValue(SourceNavigationView.Designer)]
        public SourceNavigationView SourceView { get; set; } = SourceNavigationView.Designer;
    }
}
