// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Alarmlist.VisualStudio.Testing
{
    public static partial class VisualStudioAutomation
    {
        // External native feasibility probe. Uses only public shell/automation
        // contracts; no Resource Explorer implementation types are loaded.
        public static Dictionary<string, object> CaptureResourceTableCapabilities(object automation, string path)
        {
            var report = new Dictionary<string, object>();
            var dte = (DTE)automation;
            var monitor = Service<IVsMonitorSelection>(automation, typeof(SVsShellMonitorSelection));
            IntPtr hierarchy = IntPtr.Zero, container = IntPtr.Zero;
            try
            {
                int hr = monitor.GetCurrentSelection(out hierarchy, out uint item, out _, out container);
                report["SelectionHResult"] = hr; report["ItemId"] = item;
                report["HasSelectionContainer"] = container != IntPtr.Zero;
                var selected = new List<Dictionary<string, object>>(); report["SelectedObjects"] = selected;
                if (hr >= 0 && container != IntPtr.Zero)
                {
                    var selection = (ISelectionContainer)Marshal.GetObjectForIUnknown(container);
                    hr = selection.CountObjects((uint)Microsoft.VisualStudio.Shell.Interop.Constants.GETOBJS_SELECTED, out uint count);
                    report["SelectionCountHResult"] = hr; report["SelectedCount"] = count;
                    if (hr >= 0 && count <= 32)
                    {
                        var objects = new object[count];
                        hr = selection.GetObjects((uint)Microsoft.VisualStudio.Shell.Interop.Constants.GETOBJS_SELECTED, count, objects);
                        report["GetObjectsHResult"] = hr;
                        if (hr >= 0)
                            foreach (var value in objects)
                            {
                                var properties = new Dictionary<string, object>(); selected.Add(properties);
                                // Property descriptors are the public Properties-window
                                // contract, not reflection over private editor members.
                                foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(value))
                                {
                                    try
                                    {
                                        object content = property.GetValue(value);
                                        if (content == null || content is string || content is bool || content is int || content is uint)
                                            properties[property.Name] = content;
                                    }
                                    catch (Exception error) when (error is COMException || error is InvalidOperationException || error is NotSupportedException)
                                    { properties[property.Name + "Error"] = error.Message; }
                                }
                            }
                    }
                }
            }
            finally { if (container != IntPtr.Zero) Marshal.Release(container); if (hierarchy != IntPtr.Zero) Marshal.Release(hierarchy); }
            var rdt = Service<IVsRunningDocumentTable>(automation, typeof(SVsRunningDocumentTable));
            IntPtr data = IntPtr.Zero;
            try
            {
                int hr = rdt.FindAndLockDocument(0, path, out _, out _, out data, out uint cookie);
                report["RdtHResult"] = hr; report["RdtCookie"] = cookie; report["HasDocumentData"] = data != IntPtr.Zero;
                if (hr >= 0 && data != IntPtr.Zero)
                {
                    var document = Marshal.GetObjectForIUnknown(data);
                    report["HasTextLines"] = document is IVsTextLines;
                    report["HasTextBufferProvider"] = document is IVsTextBufferProvider;
                    if (document is IVsPersistDocData persist)
                    { report["DirtyHResult"] = persist.IsDocDataDirty(out int dirty); report["IsDirty"] = dirty != 0; }
                }
            }
            finally { if (data != IntPtr.Zero) Marshal.Release(data); }
            var open = dte.Documents.Cast<Document>().FirstOrDefault(document => string.Equals(document.FullName, path, StringComparison.OrdinalIgnoreCase));
            report["HasDteDocument"] = open != null;
            if (open != null)
            {
                report["DteSaved"] = open.Saved;
                try { report["HasDteTextDocument"] = open.Object("TextDocument") is TextDocument; }
                catch (COMException error) { report["DteTextDocumentError"] = error.Message; }
            }
            return report;
        }
    }
}
