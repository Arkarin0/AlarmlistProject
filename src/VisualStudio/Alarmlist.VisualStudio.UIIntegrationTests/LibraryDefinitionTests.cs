// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "LibraryDefinitions")]
        public Task PackageDefinition2022() => UiTestRun.ExecuteAsync("2022", "PackageDefinition", _output, CheckPackageDefinition);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "LibraryDefinitions")]
        public Task PackageDefinition2026() => UiTestRun.ExecuteAsync("2026", "PackageDefinition", _output, CheckPackageDefinition);

        private static void CheckPackageDefinition(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            ide.Automation.Solution.Close(false);
            string consumerPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            string directory = Path.Combine(workspace.DirectoryPath, "PackageSource");
            Directory.CreateDirectory(directory);
            var library = XDocument.Load(consumerPath);
            library.Descendants("ProjectGuid").Single().Value = Guid.NewGuid().ToString();
            library.Root.Add(new XElement("PropertyGroup", new XElement("OutputType", "Library"),
                new XElement("PackageId", "Native.Definition"), new XElement("PackageVersion", "1.0.0")));
            string libraryPath = Path.Combine(directory, "Library.almproj");
            library.Save(libraryPath);
            File.WriteAllText(Path.Combine(directory, "Template.almx"), "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>Library.Template</FullyQualifiedName>"
                + "<Name>Packaged definition</Name></Alarm></Alarmlist>");
            Run("pack", libraryPath, "/t:Restore;Pack", "/p:PackageOutputPath=" + Path.Combine(workspace.DirectoryPath, "feed"));
            string configPath = Path.Combine(workspace.DirectoryPath, "NuGet.Config");
            var config = XDocument.Load(configPath);
            config.Descendants("packageSource").Single().Add(new XElement("package", new XAttribute("pattern", "Native.Definition")));
            config.Save(configPath);
            var consumer = XDocument.Load(consumerPath);
            consumer.Root.Add(new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Native.Definition"), new XAttribute("Version", "1.0.0"))));
            consumer.Save(consumerPath);
            File.WriteAllText(workspace.AlarmPath, "<Alarmlist><Alarm><FullyQualifiedName>Consumer</FullyQualifiedName>"
                + "<ReferenceName>Library.Template</ReferenceName></Alarm></Alarmlist>");
            Run("restore", consumerPath, "/t:Restore");
            string artifact = Directory.GetFiles(Path.Combine(workspace.DirectoryPath, ".packages"), "*.almlib", SearchOption.AllDirectories).Single();
            byte[] original = File.ReadAllBytes(artifact);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            using var automation = new UIA3Automation();
            ide.OpenDesigner(workspace.AlarmPath);
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            window.SetForeground();
            AutomationElement editor = OpenDefinition();
            string generated = editor.Patterns.Text.Pattern.DocumentRange.GetText(-1);
            Assert.Contains("Packaged definition", generated);
            editor.Focus();
            ide.RequireForeground();
            Assert.True(editor.Properties.HasKeyboardFocus.Value);
            Keyboard.Type("MUST_NOT_EDIT");
            foreach (var key in new[] { VirtualKeyShort.BACK, VirtualKeyShort.DELETE, VirtualKeyShort.RETURN })
            {
                ide.RequireForeground();
                Assert.True(editor.Properties.HasKeyboardFocus.Value);
                Keyboard.Type(key);
            }
            Assert.Equal(generated, editor.Patterns.Text.Pattern.DocumentRange.GetText(-1));
            Assert.Equal(original, File.ReadAllBytes(artifact));
            Assert.False(File.Exists(workspace.OutputPath));
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "read-only-definition.png"));
            Assert.Equal(generated, OpenDefinition().Patterns.Text.Pattern.DocumentRange.GetText(-1));
            Assert.Single(DefinitionTabs());
            ide.Automation.ActiveWindow.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => DefinitionTabs().Length == 0, "closed generated definition tab");
            Assert.Equal(generated, OpenDefinition().Patterns.Text.Pattern.DocumentRange.GetText(-1));
            Assert.Single(DefinitionTabs());
            ide.Automation.ActiveWindow.Close(EnvDTE.vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => DefinitionTabs().Length == 0, "reopened generated definition closes");

            AutomationElement[] DefinitionTabs() => window.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
                .Where(item => item.Name.Contains("Native.Definition 1.0.0")).ToArray();

            AutomationElement OpenDefinition()
            {
                ide.OpenDesigner(workspace.AlarmPath);
                SelectDesigner(window);
                ide.Automation.ExecuteCommand("View.AlarmlistPreview");
                SelectPreviewTab(window, "Selected alarm summary");
                VisualStudioSession.Wait(() => PreviewValue(window, "Resolved Name")?.Text == "Packaged definition", "restored package analysis");
                ValueExplanationUi.Explain(window, "Explain this value for resolved Name", "Packaged definition", "Inheritance:");
                ValueExplanationUi.Navigate(window, "Field: Name");
                AutomationElement definition = null;
                VisualStudioSession.Wait(() =>
                {
                    try
                    {
                        var error = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                            .FirstOrDefault(item => !item.IsOffscreen && item.Name.StartsWith("Cannot ", StringComparison.Ordinal));
                        Assert.True(error == null, error?.Name);
                        definition = window.FindAllDescendants(cf => cf.ByAutomationId("WpfTextView")).FirstOrDefault(item => !item.IsOffscreen
                            && item.Patterns.Text.IsSupported && item.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Library.Template"));
                        return definition != null;
                    }
                    catch (System.Runtime.InteropServices.COMException exception) when (exception.HResult == unchecked((int)0x80040201))
                    { return false; }
                }, "generated package definition editor");
                return definition;
            }

            void Run(string label, string project, params string[] arguments)
            {
                var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workspace.DirectoryPath, UseShellExecute = false,
                    CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string argument in new[] { "msbuild", project, "/nologo", "/nr:false", "/v:minimal" }.Concat(arguments)) start.ArgumentList.Add(argument);
                using var process = Process.Start(start);
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000)) { process.Kill(true); throw new TimeoutException("The owned package fixture process did not finish."); }
                string log = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, label + ".log"), log);
                Assert.True(process.ExitCode == 0, log);
            }
        }
    }
}
