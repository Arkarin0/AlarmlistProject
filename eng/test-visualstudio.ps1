# Run with Windows PowerShell 5.1 -STA after deploying to the AlarmlistExp instance.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$VisualStudioPath,
  [string]$Configuration = 'Debug',
  [string]$RootSuffix = 'AlarmlistExp',
  [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
  throw 'Run this script with powershell.exe -STA (Windows PowerShell 5.1).'
}
$repoRoot = Split-Path $PSScriptRoot -Parent
$runDirectory = Join-Path $repoRoot "artifacts/log/$Configuration/VisualStudioSmoke/$([Guid]::NewGuid().ToString('N'))"
$projectDirectory = Join-Path $runDirectory 'Plant'
New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
Copy-Item (Join-Path $repoRoot 'src/SDK/Alarmlist.SDK.Tests/testassets/boilerplate/Directory.Build.*') $runDirectory
# This retained consumer lives inside the checkout, whose source mapping otherwise
# routes every package to nuget.org. Give the isolated feed an exact SDK mapping.
@'
<configuration>
  <config><add key="globalPackagesFolder" value=".packages" /></config>
  <packageSources><clear /><add key="local" value="feed" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource>
  </packageSourceMapping>
</configuration>
'@ | Set-Content (Join-Path $runDirectory 'NuGet.Config')
New-Item -ItemType Directory -Path (Join-Path $runDirectory 'feed') | Out-Null
Copy-Item (Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.MSBuild.SDK.*.nupkg") (Join-Path $runDirectory 'feed')
Write-Output "Smoke-test files and logs: $runDirectory"
# Opening a solution enters the IDE directly. A bare devenv launch may remain on
# the Start window and never publish its DTE automation object.
$solutionPath = Join-Path $runDirectory 'Smoke.sln'
@'
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Global
EndGlobal
'@ | Set-Content $solutionPath

# Bind only to the process started below, never to an existing user IDE session.
$interopAssembly = Join-Path $VisualStudioPath 'Common7/IDE/PublicAssemblies/Microsoft.VisualStudio.Interop.dll'
[Reflection.Assembly]::LoadFrom($interopAssembly) | Out-Null
Add-Type -ReferencedAssemblies $interopAssembly -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
[ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAlarmlistMessageFilter
{
    [PreserveSig] int HandleInComingCall(int type, IntPtr caller, int ticks, IntPtr info);
    [PreserveSig] int RetryRejectedCall(IntPtr callee, int ticks, int rejection);
    [PreserveSig] int MessagePending(IntPtr callee, int ticks, int pending);
}
[ComVisible(true)]
public sealed class AlarmlistMessageFilter : IAlarmlistMessageFilter
{
    public int HandleInComingCall(int type, IntPtr caller, int ticks, IntPtr info) { return 0; }
    public int RetryRejectedCall(IntPtr callee, int ticks, int rejection)
    {
        // Retry only transient COM call rejection, never a failed test operation.
        return (rejection == 1 || rejection == 2) && ticks < 10000 ? 100 : -1;
    }
    public int MessagePending(IntPtr callee, int ticks, int pending) { return 2; }
}
public static class AlarmlistRunningObjectTable
{
    private static IAlarmlistMessageFilter previousFilter;
    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IAlarmlistMessageFilter filter, out IAlarmlistMessageFilter previous);
    public static void RegisterMessageFilter()
    {
        Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(new AlarmlistMessageFilter(), out previousFilter));
    }
    public static void RestoreMessageFilter()
    {
        IAlarmlistMessageFilter current;
        Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(previousFilter, out current));
    }
    private static T Service<T>(object automation, Type service)
    {
        var provider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)automation;
        Guid serviceId = service.GUID;
        Guid interfaceId = typeof(T).GUID;
        IntPtr pointer;
        Marshal.ThrowExceptionForHR(provider.QueryService(ref serviceId, ref interfaceId, out pointer));
        try { return (T)Marshal.GetObjectForIUnknown(pointer); }
        finally { Marshal.Release(pointer); }
    }
    public static void VerifyEditor(object automation, string path)
    {
        var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
        Guid editor = new Guid("6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f");
        Guid primary = Guid.Empty;
        Microsoft.VisualStudio.OLE.Interop.IServiceProvider site;
        Microsoft.VisualStudio.Shell.Interop.IVsUIHierarchy hierarchy;
        uint item;
        Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame first;
        Console.WriteLine("Opening the ALMX editor through the Visual Studio shell.");
        Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref editor, "", ref primary, out site, out hierarchy, out item, out first));
        Marshal.ThrowExceptionForHR(first.Show());
        var dte = (EnvDTE.DTE)automation;
        var document = dte.ActiveDocument;
        var originalWindows = new System.Collections.Generic.HashSet<IntPtr>();
        foreach (EnvDTE.Window window in document.Windows) originalWindows.Add(GetIdentity(window));
        // VS2026 renamed command 265 from Window.NewWindow to Window.NewTab;
        // its Window.NewWindow now floats the existing tab instead of copying it.
        string duplicateCommand = dte.Commands.Item("{5EFC7975-14BC-11CF-9B2B-00AA00573819}", 265).Name;
        // A fresh experimental profile may still be initializing the native
        // editor's command routing when Show returns. Wait for readiness before
        // invoking the command, just as we wait for the new window below.
        for (int attempt = 0; attempt < 50 && !dte.Commands.Item(duplicateCommand, 0).IsAvailable; attempt++)
        {
            document.Activate();
            System.Threading.Thread.Sleep(100);
        }
        dte.ExecuteCommand(duplicateCommand, "");
        // Activation can lag behind ExecuteCommand on newer hosts. Close only
        // the newly created window, never whichever window is active at return.
        EnvDTE.Window newWindow = null;
        for (int attempt = 0; attempt < 50 && newWindow == null; attempt++)
        {
            foreach (EnvDTE.Window window in document.Windows)
                if (!originalWindows.Contains(GetIdentity(window))) { newWindow = window; break; }
            if (newWindow == null) System.Threading.Thread.Sleep(100);
        }
        if (newWindow == null) throw new Exception("New Window did not create an additional document window. Before: " + originalWindows.Count + "; after: " + document.Windows.Count);
        Console.WriteLine("New Window created an additional document window.");
        newWindow.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
        Marshal.ThrowExceptionForHR(first.Show());
        Console.WriteLine("The standard duplicate-view command succeeded: " + duplicateCommand);
        object view;
        Marshal.ThrowExceptionForHR(first.GetProperty(-3001, out view));
        var code = (Microsoft.VisualStudio.TextManager.Interop.IVsCodeWindow)view;
        Microsoft.VisualStudio.TextManager.Interop.IVsTextView textView;
        Marshal.ThrowExceptionForHR(code.GetPrimaryView(out textView));
        Console.WriteLine("The embedded source view is available.");
        if (textView == null) throw new Exception("The embedded XML view was not created.");
        Microsoft.VisualStudio.TextManager.Interop.IVsTextLines buffer;
        Marshal.ThrowExceptionForHR(code.GetBuffer(out buffer));
        int lastLine, lastColumn;
        Marshal.ThrowExceptionForHR(buffer.GetLastLineIndex(out lastLine, out lastColumn));
        string before;
        Marshal.ThrowExceptionForHR(buffer.GetLineText(0, 0, lastLine, lastColumn, out before));
        if (!before.Contains("Alarmlist")) throw new Exception("The custom editor did not load the ALMX source.");
        Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame second;
        Marshal.ThrowExceptionForHR(open.OpenCopyOfStandardEditor(first, ref primary, out second));
        Console.WriteLine("Opened a second editor window.");
        Marshal.ThrowExceptionForHR(second.Show());
        object secondData, firstData;
        first.GetProperty(-4004, out firstData);
        second.GetProperty(-4004, out secondData);
        IntPtr firstPointer = Marshal.GetIUnknownForObject(firstData);
        IntPtr secondPointer = Marshal.GetIUnknownForObject(secondData);
        try { if (firstPointer != secondPointer) throw new Exception("New Window created a different document buffer."); }
        finally { Marshal.Release(firstPointer); Marshal.Release(secondPointer); }
        const string comment = "<!-- shared editor smoke -->";
        // DTE dispatches editing to the IDE UI thread. Direct cross-process calls
        // into a managed IVsTextView do not have that threading guarantee.
        dte.ExecuteCommand("View.ViewCode", "");
        Console.WriteLine("View Code command succeeded.");
        var selection = (EnvDTE.TextSelection)dte.ActiveDocument.Selection;
        Console.WriteLine("Text selection automation is available.");
        selection.EndOfDocument(false);
        selection.Insert(comment, (int)EnvDTE.vsInsertFlags.vsInsertFlagsContainNewText);
        Console.WriteLine("Inserted text through DTE.");
        Marshal.ThrowExceptionForHR(buffer.GetLastLineIndex(out lastLine, out lastColumn));
        string changed;
        Marshal.ThrowExceptionForHR(buffer.GetLineText(0, 0, lastLine, lastColumn, out changed));
        if (!changed.Contains(comment)) throw new Exception("Native XML editing failed.");
        dte.ExecuteCommand("Edit.Undo", "");
        string undone = ((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).StartPoint.CreateEditPoint().GetText(((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).EndPoint);
        if (undone.Contains(comment)) throw new Exception("Shared document undo failed.");
        dte.ExecuteCommand("Edit.Redo", "");
        dte.ExecuteCommand("File.SaveAll", "");
        if (!System.IO.File.ReadAllText(path).Contains(comment)) throw new Exception("Shared document save failed.");
        Marshal.ThrowExceptionForHR(second.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
        Marshal.ThrowExceptionForHR(first.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
        Console.WriteLine("Custom split editor loaded; native XML view, New Window shared data, editing, undo/redo, Save All, and close passed.");
        Guid xmlEditor = new Guid("fa3cd31e-987b-443a-9b81-186104e8dac1");
        Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref xmlEditor, "", ref primary, out site, out hierarchy, out item, out first));
        if (first == null) throw new Exception("The XML editor did not return a document frame.");
        Marshal.ThrowExceptionForHR(first.Show());
        Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref editor, "", ref primary, out site, out hierarchy, out item, out second));
        if (second == null) throw new Exception("The custom editor did not return a document frame.");
        Marshal.ThrowExceptionForHR(second.Show());
        first.GetProperty(-4004, out firstData);
        second.GetProperty(-4004, out secondData);
        firstPointer = Marshal.GetIUnknownForObject(firstData);
        secondPointer = Marshal.GetIUnknownForObject(secondData);
        try { if (firstPointer != secondPointer) throw new Exception("XML-first opening created a separate document."); }
        finally { Marshal.Release(firstPointer); Marshal.Release(secondPointer); }
        Marshal.ThrowExceptionForHR(second.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
        Marshal.ThrowExceptionForHR(first.Show());
        if (!((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).StartPoint.CreateEditPoint().GetText(((EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument")).EndPoint).Contains(comment))
            throw new Exception("The XML view lost content after closing the designer.");
        Marshal.ThrowExceptionForHR(first.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
        Console.WriteLine("XML-first/custom-editor sharing and XML view survival passed.");
    }
    private static IntPtr GetIdentity(object value)
    {
        IntPtr identity = Marshal.GetIUnknownForObject(value);
        Marshal.Release(identity);
        return identity;
    }
    public static void VerifySaveAs(object automation, string path)
    {
        var dte = (EnvDTE.DTE)automation;
        var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
        var rdt = Service<Microsoft.VisualStudio.Shell.Interop.IVsRunningDocumentTable>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsRunningDocumentTable));
        foreach (string editorId in new[] { "6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f", "fa3cd31e-987b-443a-9b81-186104e8dac1" })
        {
            Guid editor = new Guid(editorId), primary = Guid.Empty;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider site;
            Microsoft.VisualStudio.Shell.Interop.IVsUIHierarchy hierarchy;
            Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame first, second;
            uint item;
            Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref editor, "", ref primary, out site, out hierarchy, out item, out first));
            Marshal.ThrowExceptionForHR(first.Show());
            Marshal.ThrowExceptionForHR(open.OpenCopyOfStandardEditor(first, ref primary, out second));
            Marshal.ThrowExceptionForHR(second.Show());
            string local = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "SavedAs-" + editorId + ".almx");
            object originalFirstData, originalSecondData;
            Marshal.ThrowExceptionForHR(first.GetProperty(-4004, out originalFirstData));
            Marshal.ThrowExceptionForHR(second.GetProperty(-4004, out originalSecondData));
            if (GetIdentity(originalFirstData) != GetIdentity(originalSecondData)) throw new Exception("Save As setup did not share document data.");
            string external = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path)), "Linked-" + editorId + ".almx");
            foreach (string target in new[] { local, external, path })
            {
                Console.WriteLine("Save As destination: " + target);
                string old = dte.ActiveDocument.FullName;
                string originalText = System.IO.File.ReadAllText(old);
                Microsoft.VisualStudio.Shell.Interop.IVsHierarchy owner;
                uint oldItem, cookie; IntPtr data;
                Marshal.ThrowExceptionForHR(rdt.FindAndLockDocument(0, old, out owner, out oldItem, out data, out cookie));
                if (data == IntPtr.Zero) throw new Exception("Save As source is not in the RDT.");
                try
                {
                    int cancelled;
                    Marshal.ThrowExceptionForHR(((Microsoft.VisualStudio.Shell.Interop.IVsPersistHierarchyItem)owner).SaveItem(
                        Microsoft.VisualStudio.Shell.Interop.VSSAVEFLAGS.VSSAVE_SilentSave, target, oldItem, data, out cancelled));
                    if (cancelled != 0) throw new Exception("The test Save As was cancelled.");
                    if (!String.Equals(dte.ActiveDocument.FullName, target, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Save As did not update the document identity.");
                    uint flags, readLocks, editLocks, newItem; string moniker; IntPtr current;
                    Marshal.ThrowExceptionForHR(rdt.GetDocumentInfo(cookie, out flags, out readLocks, out editLocks, out moniker, out owner, out newItem, out current));
                    try
                    {
                        if (data != current || !String.Equals(moniker, target, StringComparison.OrdinalIgnoreCase))
                            throw new Exception("Save As changed the buffer or left the RDT at the old path.");
                    }
                    finally { if (current != IntPtr.Zero) Marshal.Release(current); }
                    object firstData, secondData;
                    Marshal.ThrowExceptionForHR(first.GetProperty(-4004, out firstData));
                    Marshal.ThrowExceptionForHR(second.GetProperty(-4004, out secondData));
                    if (GetIdentity(firstData) != GetIdentity(originalFirstData) || GetIdentity(secondData) != GetIdentity(originalSecondData))
                        throw new Exception("Save As detached an open view from the shared buffer.");
                    var textDocument = (EnvDTE.TextDocument)dte.ActiveDocument.Object("TextDocument");
                    string marker = "<!-- save-as " + System.IO.Path.GetFileName(target) + " -->";
                    textDocument.EndPoint.CreateEditPoint().Insert(marker);
                    dte.ExecuteCommand("File.SaveAll", "");
                    if (!System.IO.File.ReadAllText(target).Contains(marker) || System.IO.File.ReadAllText(old) != originalText)
                        throw new Exception("The follow-up save did not write only the new path.");
                }
                finally { Marshal.Release(data); }
            }
            Marshal.ThrowExceptionForHR(second.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
            Marshal.ThrowExceptionForHR(first.CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
        }
        Console.WriteLine("Project Save As: both editors, two shared views, RDT identity, local/linked paths, and subsequent saves passed.");
    }
    // PowerShell's COM adapter exposes the default interfaces, which omit these
    // newer Solution2/DTE2 members. Query the required interfaces explicitly.
    public static string GetTemplate(object automation, bool item)
    {
        var solution = (EnvDTE80.Solution2)((EnvDTE.DTE)automation).Solution;
        string path = item ? solution.GetProjectItemTemplate("AlarmList.vstemplate", "Alarmlist")
                           : solution.GetProjectTemplate("Alarmlist.vstemplate", "Alarmlist");
        Console.WriteLine((item ? "Item template: " : "Project template: ") + path);
        return path;
    }
    public static bool HasCompilerError(object automation)
    {
        return HasErrorDescription(automation, "Alarm 'Invalid' references missing alarm 'Missing'.");
    }
    public static bool HasErrorDescription(object automation, string description)
    {
        var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
        for (int i = 1; i <= errors.Count; i++)
        {
            var error = errors.Item(i);
            if (error.ErrorLevel == EnvDTE80.vsBuildErrorLevel.vsBuildErrorLevelHigh
                && error.Description == description) return true;
        }
        return false;
    }
    public static void NavigateError(object automation, string description)
    {
        var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
        for (int i = 1; i <= errors.Count; i++)
            if (errors.Item(i).Description == description) { errors.Item(i).Navigate(); return; }
        throw new Exception("The live diagnostic is not present for navigation.");
    }
    public static bool ReplaceXmlBuffer(object automation, string path, string text)
    {
        var open = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShellOpenDocument>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShellOpenDocument));
        Guid xmlEditor = new Guid("fa3cd31e-987b-443a-9b81-186104e8dac1");
        Guid primary = Guid.Empty;
        Microsoft.VisualStudio.OLE.Interop.IServiceProvider site;
        Microsoft.VisualStudio.Shell.Interop.IVsUIHierarchy hierarchy;
        uint item;
        Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame frame;
        // Solution reopening can restore the earlier navigation window. Close
        // every view for this test document so the edit proves XML-only analysis.
        var shell = Service<Microsoft.VisualStudio.Shell.Interop.IVsUIShell>(automation, typeof(Microsoft.VisualStudio.Shell.Interop.SVsUIShell));
        Microsoft.VisualStudio.Shell.Interop.IEnumWindowFrames windows;
        Marshal.ThrowExceptionForHR(shell.GetDocumentWindowEnum(out windows));
        var frames = new Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame[1];
        uint fetched;
        while (windows.Next(1, frames, out fetched) == 0 && fetched == 1)
        {
            object moniker;
            Marshal.ThrowExceptionForHR(frames[0].GetProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSFPROPID.VSFPROPID_pszMkDocument, out moniker));
            if (String.Equals(moniker as string, path, StringComparison.OrdinalIgnoreCase))
                Marshal.ThrowExceptionForHR(frames[0].CloseFrame((uint)Microsoft.VisualStudio.Shell.Interop.__FRAMECLOSE.FRAMECLOSE_NoSave));
        }
        Marshal.ThrowExceptionForHR(open.OpenDocumentViaProjectWithSpecific(path, 0, ref xmlEditor, "", ref primary, out site, out hierarchy, out item, out frame));
        if (frame == null) return false;
        Guid actualEditor;
        Marshal.ThrowExceptionForHR(frame.GetGuidProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSFPROPID.VSFPROPID_guidEditorType, out actualEditor));
        if (actualEditor != xmlEditor) throw new Exception("The unsaved live-analysis test must use the XML editor only.");
        Marshal.ThrowExceptionForHR(frame.Show());
        // DTE dispatches edits to the UI thread. Document activation may lag
        // behind OpenDocument after reopening a solution, so select by path.
        foreach (EnvDTE.Document openDocument in ((EnvDTE.DTE)automation).Documents)
        {
            if (!String.Equals(openDocument.FullName, path, StringComparison.OrdinalIgnoreCase)) continue;
            var document = openDocument.Object("TextDocument") as EnvDTE.TextDocument;
            if (document == null) return false;
            EnvDTE.TextPoint start = document.StartPoint;
            EnvDTE.TextPoint end = document.EndPoint;
            if (start == null || end == null) return false;
            EnvDTE.EditPoint edit = start.CreateEditPoint();
            if (edit == null) return false;
            edit.ReplaceText(end, text, (int)EnvDTE.vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
            return true;
        }
        return false;
    }
    public static bool IsAtLocation(object automation, string path, int line, int column)
    {
        var document = ((EnvDTE.DTE)automation).ActiveDocument;
        if (document == null || !String.Equals(document.FullName, path, StringComparison.OrdinalIgnoreCase)) return false;
        var selection = document.Selection as EnvDTE.TextSelection;
        return selection != null && selection.CurrentLine == line && selection.CurrentColumn == column;
    }
    public static void DumpBuildOutput(object automation)
    {
        Console.WriteLine("IDE status: " + ((EnvDTE.DTE)automation).StatusBar.Text);
        var errors = ((EnvDTE80.DTE2)automation).ToolWindows.ErrorList.ErrorItems;
        for (int i = 1; i <= errors.Count; i++) Console.WriteLine(errors.Item(i).Description);
        foreach (EnvDTE.OutputWindowPane pane in ((EnvDTE80.DTE2)automation).ToolWindows.OutputWindow.OutputWindowPanes)
        {
            try
            {
                var start = pane.TextDocument.StartPoint.CreateEditPoint();
                Console.WriteLine(pane.Name + ": " + start.GetText(pane.TextDocument.EndPoint));
            }
            catch (COMException) { }
        }
    }
    public static object GetProject(object automation)
    {
        return ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
    }
    public static void AddItem(object automation, string template)
    {
        var project = ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
        var folder = project.ProjectItems.AddFolder("sources", EnvDTE.Constants.vsProjectItemKindPhysicalFolder);
        folder.ProjectItems.AddFromTemplate(template, "Additional.almx");
    }
    public static bool FindItem(object automation, string folder, string name, string operation)
    {
        var project = ((EnvDTE.DTE)automation).Solution.Projects.Item(1);
        var items = String.IsNullOrEmpty(folder) ? project.ProjectItems : project.ProjectItems.Item(folder).ProjectItems;
        foreach (EnvDTE.ProjectItem item in items)
        {
            if (item.Name != name) continue;
            if (operation == "rename") { item.Name = "Renamed.almx"; project.Save(""); }
            if (operation == "delete") item.Delete();
            return true;
        }
        return false;
    }
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);
    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);
    public static object Find(int processId)
    {
        IRunningObjectTable table;
        IBindCtx context;
        Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out table));
        Marshal.ThrowExceptionForHR(CreateBindCtx(0, out context));
        IEnumMoniker enumerator = null;
        try
        {
            table.EnumRunning(out enumerator);
            IMoniker[] monikers = new IMoniker[1];
            while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                try
                {
                    string name;
                    monikers[0].GetDisplayName(context, null, out name);
                    if (name.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal)
                        && name.EndsWith(":" + processId, StringComparison.Ordinal))
                    {
                        object result;
                        table.GetObject(monikers[0], out result);
                        return result;
                    }
                }
                finally { Marshal.ReleaseComObject(monikers[0]); }
            }
            return null;
        }
        finally
        {
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            Marshal.ReleaseComObject(context);
            Marshal.ReleaseComObject(table);
        }
    }
}
'@

function Wait-For($Action, [string]$Description) {
  $deadline = [DateTime]::UtcNow.AddSeconds(120)
  do {
    try {
      $result = & $Action
      if ($result) { return $result }
    } catch [Runtime.InteropServices.COMException] {
      if ($_.Exception.HResult -notin @(-2147418111, -2147417846)) { throw }
    }
    Start-Sleep -Milliseconds 500
  } while ([DateTime]::UtcNow -lt $deadline)
  throw "Timed out: $Description"
}

function Assert-AlarmCount([int]$Expected) {
  [xml]$compiled = Get-Content (Join-Path $projectDirectory 'bin/Debug/Plant.Alarmlist.xml')
  if (@($compiled.AlarmList.Alarm).Count -ne $Expected) {
    throw "Expected $Expected compiled alarms."
  }
}

$ide = $null
$dte = $null
$retainInstance = $false
[AlarmlistRunningObjectTable]::RegisterMessageFilter()
try {
  $ide = Start-Process -FilePath (Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe') `
    -ArgumentList @(('"' + $solutionPath + '"'), '/RootSuffix', $RootSuffix, '/Log', ('"' + (Join-Path $runDirectory 'ActivityLog.xml') + '"')) `
    -WindowStyle Hidden -PassThru
  $dte = Wait-For { [AlarmlistRunningObjectTable]::Find($ide.Id) } 'Visual Studio automation startup'
  # The ROT entry may be published before the IDE accepts automation calls.
  Wait-For {
    $dte.SuppressUI = $true
    $dte.MainWindow.Visible = $false
    $dte.Solution.IsOpen
  } 'empty solution loading' | Out-Null
  $projectTemplate = [AlarmlistRunningObjectTable]::GetTemplate($dte, $false)
  $itemTemplate = [AlarmlistRunningObjectTable]::GetTemplate($dte, $true)
  if (!(Test-Path $projectTemplate) -or !(Test-Path $itemTemplate)) { throw 'Installed Alarmlist templates were not found.' }
  $dte.Solution.AddFromTemplate($projectTemplate, $projectDirectory, 'Plant', $false) | Out-Null
  $project = Wait-For { if ($dte.Solution.Projects.Count -eq 1) { [AlarmlistRunningObjectTable]::GetProject($dte) } } 'project creation'
  $dte.Solution.SaveAs((Join-Path $runDirectory 'Smoke.sln'))
  $dte.Solution.SolutionBuild.Build($true)
  if ($dte.Solution.SolutionBuild.LastBuildInfo -ne 0) { throw 'The new project did not build.' }
  Assert-AlarmCount 1
  Write-Output 'Created a project from the installed template and built its starter alarm.'
  if ($KeepOpen) { $dte.MainWindow.Visible = $true }
  [AlarmlistRunningObjectTable]::VerifyEditor($dte, (Join-Path $projectDirectory 'Alarms.almx'))
  [AlarmlistRunningObjectTable]::VerifySaveAs($dte, (Join-Path $projectDirectory 'Alarms.almx'))
  $dte.Solution.SolutionBuild.Build($true)
  Assert-AlarmCount 1

  [AlarmlistRunningObjectTable]::AddItem($dte, $itemTemplate)
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, 'sources', 'Additional.almx', '') } 'new item' | Out-Null
  $dte.Solution.SolutionBuild.Build($true)
  Assert-AlarmCount 2
  [AlarmlistRunningObjectTable]::FindItem($dte, 'sources', 'Additional.almx', 'rename') | Out-Null
  $dte.Solution.Close($true)
  $dte.Solution.Open((Join-Path $runDirectory 'Smoke.sln'))
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, 'sources', 'Renamed.almx', 'delete') } 'project reopening and item deletion' | Out-Null
  $dte.Solution.SolutionBuild.Build($true)
  Assert-AlarmCount 1
  Write-Output 'Added an item and folder, renamed the item, reopened the solution, and deleted the item.'

  $outputFile = Join-Path $projectDirectory 'bin/Debug/Plant.Alarmlist.xml'
  $before = [IO.File]::ReadAllText($outputFile)
  $livePath = Join-Path $projectDirectory 'Live.almx'
  $liveText = "<Alarmlist>`r`n  <Alarm><FullyQualifiedName>Live</FullyQualifiedName>`r`n    <ReferenceName>MissingLive</ReferenceName></Alarm>`r`n</Alarmlist>"
  $liveMessage = "Alarm 'Live' references missing alarm 'MissingLive'."
  [IO.File]::WriteAllText($livePath, $liveText)
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, $null, 'Live.almx', '') } 'live input discovery' | Out-Null
  Wait-For { [AlarmlistRunningObjectTable]::HasErrorDescription($dte, $liveMessage) } 'live diagnostic before Build' | Out-Null
  [AlarmlistRunningObjectTable]::NavigateError($dte, $liveMessage)
  Wait-For { [AlarmlistRunningObjectTable]::IsAtLocation($dte, $livePath, 3, 6) } 'live diagnostic source navigation' | Out-Null
  Write-Output 'Live diagnostic navigation reached its source position.'
  # Closing the solution also drains the source view before opening the same
  # file explicitly through a different editor factory after reopening.
  $dte.Solution.Close($false)
  Wait-For { ![AlarmlistRunningObjectTable]::HasErrorDescription($dte, $liveMessage) } 'project unload clears live diagnostics' | Out-Null
  $dte.Solution.Open($solutionPath)
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, $null, 'Live.almx', '') } 'reopened project item tree' | Out-Null
  Wait-For { [AlarmlistRunningObjectTable]::HasErrorDescription($dte, $liveMessage) } 'reopened project republishes live diagnostics' | Out-Null
  Write-Output 'Live diagnostic publication and project unload/reopen passed.'
  Wait-For { [AlarmlistRunningObjectTable]::ReplaceXmlBuffer($dte, $livePath, $liveText.Replace('<ReferenceName>MissingLive</ReferenceName>', '')) } 'XML-only buffer correction' | Out-Null
  Wait-For { ![AlarmlistRunningObjectTable]::HasErrorDescription($dte, $liveMessage) } 'unsaved XML correction clears live error' | Out-Null
  if ([IO.File]::ReadAllText($livePath) -ne $liveText) { throw 'Live analysis saved the XML buffer.' }
  if ([IO.File]::ReadAllText($outputFile) -ne $before) { throw 'Live analysis changed compiled output.' }
  $dte.Documents.Item($livePath).Close(2)
  Wait-For { [AlarmlistRunningObjectTable]::HasErrorDescription($dte, $liveMessage) } 'closing unsaved XML restores disk diagnostics' | Out-Null
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, $null, 'Live.almx', 'delete') } 'live input deletion' | Out-Null
  Wait-For { ![AlarmlistRunningObjectTable]::HasErrorDescription($dte, $liveMessage) } 'removed input clears live diagnostic' | Out-Null
  Write-Output 'Live Error List: no-Build publication, project unload/reopen, navigation, XML-only unsaved correction, close/discard, and removed-input cleanup passed.'

  [IO.File]::WriteAllText((Join-Path $projectDirectory 'Invalid.almx'), '<Alarmlist><Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>')
  Wait-For { [AlarmlistRunningObjectTable]::FindItem($dte, $null, 'Invalid.almx', '') } 'globbed input discovery' | Out-Null
  $dte.Solution.SolutionBuild.Build($true)
  if ($dte.Solution.SolutionBuild.LastBuildInfo -eq 0) { throw 'An invalid reference did not fail the IDE build.' }
  Wait-For { [AlarmlistRunningObjectTable]::HasCompilerError($dte) } 'compiler error in Error List' | Out-Null
  if ([IO.File]::ReadAllText($outputFile) -ne $before) { throw 'A failed build replaced the previous output.' }
  Wait-For { [AlarmlistRunningObjectTable]::ReplaceXmlBuffer($dte, (Join-Path $projectDirectory 'Invalid.almx'), '<Alarmlist><Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><ReferenceName>NewMissing</ReferenceName></Alarm></Alarmlist>') } 'XML-only reference edit' | Out-Null
  Wait-For { [AlarmlistRunningObjectTable]::HasErrorDescription($dte, "Alarm 'Invalid' references missing alarm 'NewMissing'.") } 'updated live diagnostic beside Build error' | Out-Null
  if (![AlarmlistRunningObjectTable]::HasCompilerError($dte)) { throw 'Live diagnostics cleared the existing Build error.' }
  $dte.Documents.Item((Join-Path $projectDirectory 'Invalid.almx')).Close(2)
  Write-Output 'Updating live diagnostics preserves the existing Build Error List entry.'
  [AlarmlistRunningObjectTable]::FindItem($dte, $null, 'Invalid.almx', 'delete') | Out-Null
  $dte.Solution.SolutionBuild.Clean($true)
  if (Test-Path $outputFile) { throw 'Clean did not delete the output.' }
  $dte.ExecuteCommand('Build.RebuildSolution')
  Wait-For { Test-Path $outputFile } 'Rebuild output' | Out-Null
  Assert-AlarmCount 1
  Write-Output 'Compiler errors appear in Error List; failed output preservation, Clean, and Rebuild passed.'
  if ($KeepOpen) {
    $dte.ItemOperations.OpenFile((Join-Path $projectDirectory 'Alarms.almx')) | Out-Null
    $dte.SuppressUI = $false
    $dte.MainWindow.Visible = $true
    $retainInstance = $true
    Write-Output "Experimental instance retained for UI inspection. Process: $($ide.Id)"
  }
} catch {
  if ($dte -and $KeepOpen) {
    $dte.SuppressUI = $false
    $dte.MainWindow.Visible = $true
    $retainInstance = $true
    Write-Output "Failed experimental instance retained for inspection. Process: $($ide.Id)"
  }
  if ($dte) { try { [AlarmlistRunningObjectTable]::DumpBuildOutput($dte) } catch { Write-Warning $_ } }
  Write-Output $_.Exception.ToString()
  throw
} finally {
  if ($dte -and !$retainInstance) {
    try { $dte.Solution.Close($false); $dte.Quit() } catch { Write-Warning $_ }
  }
  if ($ide -and !$retainInstance -and !$ide.WaitForExit(10000)) { Stop-Process -Id $ide.Id }
  [AlarmlistRunningObjectTable]::RestoreMessageFilter()
}
