// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "XmlCatalogCompletion")]
    public sealed class XmlCatalogCompletionTests
    {
        private readonly ITestOutputHelper _output;
        public XmlCatalogCompletionTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task InstalledCatalogCompletion2022() => UiTestRun.ExecuteAsync("2022", "XmlCatalogCompletion", _output, Check);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task InstalledCatalogCompletion2026() => UiTestRun.ExecuteAsync("2026", "XmlCatalogCompletion", _output, Check);

        private static void Check(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            // No schema is opened or registered in a global catalogue. The
            // fixture verifies every deployed VSIX asset against its package.
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            int? nativeUndoSteps = null;
            foreach (var probe in new[]
            {
                (File: "Catalogue.almlookup", Marked: "<Lookup xmlns='urn:alarmlist:lookup:1'><Pa¦", Candidate: "Path"),
                (File: "Catalogue.almlookup", Marked: "<Lookup xmlns='urn:alarmlist:lookup:1'><List><Entry Des¦", Candidate: "Description"),
                (File: "Catalogue.almlookup", Marked: "<l:Lookup xmlns:l='urn:alarmlist:lookup:1'><l:Te¦", Candidate: "l:Template"),
                (File: "Catalogue.almlookup", Marked: "<Lookup xmlns='urn:¦'", Candidate: "urn:alarmlist:lookup:1"),
                (File: "Catalogue.almlookup", Marked: "<l:Lookup xmlns:l='urn:¦'", Candidate: "urn:alarmlist:lookup:1"),
                (File: "Consumer.almproj", Marked: "<Project><Prop¦", Candidate: "PropertyGroup"),
                (File: "Consumer.almproj", Marked: "<Project><PropertyGroup><AlarmlistCu¦", Candidate: "AlarmlistCulture"),
                (File: "Consumer.almproj", Marked: "<Project><ItemGroup><AlarmlistLo¦", Candidate: "AlarmlistLookup"),
                (File: "Consumer.almproj", Marked: "<Project><ItemGroup><AlarmlistResource Cu¦", Candidate: "Culture"),
                (File: "Consumer.almproj", Marked: "<Project><PropertyGroup><OutputType>Lib¦</OutputType></PropertyGroup></Project>", Candidate: "Library"),
                (File: "Consumer.almproj", Marked: "<Project><Import Label='Sha¦'", Candidate: "Shared"),
                (File: "Consumer.almproj", Marked: "<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'><ItemGroup><AlarmlistLo¦", Candidate: "AlarmlistLookup"),
                (File: "Consumer.almproj", Marked: "<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'><Prop¦", Candidate: "PropertyGroup"),
                (File: "Ordinary.csproj", Marked: "<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'><Prop¦", Candidate: "PropertyGroup")
            })
            {
                string path = Path.Combine(workspace.DirectoryPath, probe.File);
                string original = probe.Marked.Replace("¦", "");
                File.WriteAllText(path, original);
                ide.Automation.ItemOperations.OpenFile(path, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
                var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
                int caret = probe.Marked.IndexOf('¦');
                selection.MoveToAbsoluteOffset(caret + 1);
                window.SetForeground(); window.FindFirstDescendant(cf => cf.ByAutomationId("WpfTextView")).Focus();
                ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(probe.Candidate)) != null, "installed catalogue candidate " + probe.Candidate);
                XmlCompletionTests.RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.ESCAPE);
                Assert.Equal(original, ide.ReadText(path));
                ide.Automation.ExecuteCommand("Edit.ListMembers", "");
                VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(probe.Candidate)) != null, "catalogue candidate after Escape");
                XmlCompletionTests.RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.TAB);
                int start = caret;
                while (start > 0 && (char.IsLetterOrDigit(original[start - 1]) || original[start - 1] == ':' || original[start - 1] == '-')) start--;
                string expected = original.Remove(start, caret - start).Insert(start, probe.Candidate);
                VisualStudioSession.Wait(() => ide.ReadText(path) == expected, "catalogue insertion " + probe.Candidate);
                bool native = probe.Candidate == "PropertyGroup" && probe.Marked.Contains("http://schemas.microsoft.com/developer/msbuild/2003");
                if (native)
                {
                    // Native XML owns its existing Undo units. Compare .almproj
                    // with ordinary .csproj instead of imposing our transaction.
                    int steps = 0;
                    do
                    {
                        ide.Automation.ExecuteCommand("Edit.Undo", "");
                        Wait.UntilInputIsProcessed(); steps++;
                    } while (ide.ReadText(path) != original && steps < 4);
                    Assert.Equal(original, ide.ReadText(path));
                    if (nativeUndoSteps.HasValue) Assert.Equal(nativeUndoSteps.Value, steps);
                    else nativeUndoSteps = steps;
                    File.AppendAllText(Path.Combine(workspace.DirectoryPath, "native-undo-steps.txt"), probe.File + ": " + steps + Environment.NewLine);
                }
                else
                {
                    ide.Automation.ExecuteCommand("Edit.Undo", "");
                    VisualStudioSession.Wait(() => ide.ReadText(path) == original, "catalogue insertion Undo");
                }
                ide.Automation.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
                Assert.Equal(original, File.ReadAllText(path));
            }
        }
    }
}
