// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Alarmlist.VisualStudio.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;

namespace Alarmlist.VisualStudio.Editor
{
    // The shell routes toolbar commands to the active document. No global view
    // registry or retained editor is needed, including for linked project contexts.
    internal static class PreviewLanguageCommands
    {
        internal static readonly Guid CommandSet = new Guid("c37b75da-f827-4d1a-837f-7f766368d478");
        internal const int Language = 0x0100;
        internal const int Languages = 0x0101;

        internal static bool Contains(uint command) => command == Language || command == Languages;

        internal static uint Status(EditFileControlViewModel view) => (uint)(OLECMDF.OLECMDF_SUPPORTED
            | (view?.CanChangePreviewCulture == true ? OLECMDF.OLECMDF_ENABLED : 0));

        internal static object Value(EditFileControlViewModel view, uint command) => command == Languages
            ? (object)(view?.PreviewCultures.ToArray() ?? Array.Empty<string>())
            : view?.SelectedPreviewCulture ?? string.Empty;

        internal static int Select(EditFileControlViewModel view, string culture)
        {
            if (view?.CanChangePreviewCulture != true) return (int)Constants.OLECMDERR_E_DISABLED;
            if (culture == null || !view.PreviewCultures.Contains(culture)) return VSConstants.E_INVALIDARG;
            view.SelectedPreviewCulture = culture;
            return VSConstants.S_OK;
        }

        internal static int Execute(EditFileControlViewModel view, uint command, IntPtr input, IntPtr output)
        {
            if (output != IntPtr.Zero)
            {
                Marshal.GetNativeVariantForObject(Value(view, command), output);
                return VSConstants.S_OK;
            }
            return command == Language && input != IntPtr.Zero
                ? Select(view, Marshal.GetObjectForNativeVariant(input) as string) : VSConstants.E_INVALIDARG;
        }
    }
}
