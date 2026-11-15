// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using Xunit;

namespace Alarmlist.VisualStudio.InfrastructureTests
{
    [Trait("Category", "Infrastructure"), Trait("Area", "ProcessCleanupPreparation")]
    public sealed class ProcessCleanupPreparationTests
    {
        [Fact]
        public async Task RealExamplePreparationStaysWindowlessAndPreservesEditedSourcesAsync()
        {
            string repository = GetType().Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "RepositoryRoot").Value;
            string run = Path.Combine(repository, "artifacts", "log", "Debug", "Milestone14", "Iteration22", "ConsolePreparation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(run);
            var hosts = new List<Tuple<string, string>> { Tuple.Create("dotnet", (string)null) };
            string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (File.Exists(vswhere))
            {
                var query = Start(vswhere, run, "-all", "-products", "*", "-format", "xml", "-utf8");
                string xml = await RunAsync(query, Path.Combine(run, "instances.log"));
                foreach (var instance in XDocument.Parse(xml).Root.Elements("instance"))
                {
                    string version = (string)instance.Element("installationVersion");
                    if (version == null || !(version.StartsWith("17.", StringComparison.Ordinal) || version.StartsWith("18.", StringComparison.Ordinal))) continue;
                    string msbuild = Path.Combine((string)instance.Element("installationPath"), "MSBuild", "Current", "Bin", "MSBuild.exe");
                    if (File.Exists(msbuild)) hosts.Add(Tuple.Create("MSBuild-" + version.Split('.')[0], msbuild));
                }
            }
            File.WriteAllLines(Path.Combine(run, "hosts.txt"), hosts.Select(host => host.Item1 + ": " + host.Item2));
            foreach (var host in hosts)
            {
                string root = Path.Combine(run, host.Item1, "edited example Δ"); Directory.CreateDirectory(root);
                CopyExample(Path.Combine(repository, "example"), root);
                File.AppendAllText(Path.Combine(root, "North", "Alarms.almx"), "\r\n<!-- Keep this source edit. -->\r\n");
                File.AppendAllText(Path.Combine(root, "North", "North.almproj"), "\r\n<!-- Keep this project edit. -->\r\n");
                File.AppendAllText(Path.Combine(root, "Shared", "Shared.projitems"), "\r\n<!-- Keep shared membership. -->\r\n");
                File.AppendAllText(Path.Combine(root, "Example.sln"), "\r\n# Keep this solution edit.\r\n");
                var expected = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
                string script = Path.Combine(root, "prepare-twice.ps1");
                string option = host.Item2 == null ? "" : " -MSBuildPath '" + Ps(host.Item2) + "'";
                File.WriteAllText(script,
                    "$ErrorActionPreference = 'Stop'\r\n[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)\r\n"
                    + "Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class PreparationConsoleProbe { [DllImport(\"kernel32.dll\")] public static extern IntPtr GetConsoleWindow(); }'\r\n"
                    + "[Console]::Out.WriteLine('BEFORE:' + [PreparationConsoleProbe]::GetConsoleWindow().ToInt64())\r\n"
                    + "1..2 | ForEach-Object { & '" + Ps(Path.Combine(repository, "eng", "prepare-example.ps1")) + "' -ExampleRoot '" + Ps(root) + "'" + option + " }\r\n"
                    + "[Console]::Out.WriteLine('AFTER:' + [PreparationConsoleProbe]::GetConsoleWindow().ToInt64())\r\n", new UTF8Encoding(true));
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                string output = await RunAsync(Start(powershell, root, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script), Path.Combine(root, "preparation.log"));
                Assert.Contains("BEFORE:0", output); Assert.Contains("AFTER:0", output);
                foreach (var source in expected) Assert.Equal(source.Value, File.ReadAllBytes(source.Key));
                foreach (string project in new[] { "North", "South", "Zenon" })
                    Assert.True(File.Exists(Path.Combine(root, project, "obj", project + ".almproj.nuget.g.props")), "Restore output for " + project);
                Assert.False(Directory.Exists(Path.Combine(root, "Shared", "obj")));
            }
        }

        private static ProcessStartInfo Start(string file, string directory, params string[] arguments)
        {
            var start = new ProcessStartInfo(file) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            return start;
        }
        private static async Task<string> RunAsync(ProcessStartInfo start, string log)
        {
            using (var process = OwnedTestProcess.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                Assert.True(process.WaitForExit(180000), "Headless preparation timed out.");
                string text = await output, errors = await error; File.WriteAllText(log, text + errors);
                Assert.True(process.ExitCode == 0, text + errors); return text;
            }
        }
        private static string Ps(string value) => value.Replace("'", "''");
        private static void CopyExample(string source, string destination)
        {
            foreach (string folder in new[] { "North", "South", "Zenon", "Shared" })
            foreach (string file in Directory.GetFiles(Path.Combine(source, folder), "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).SkipLast(1)
                    .Any(part => part == "bin" || part == "obj" || part.StartsWith(".", StringComparison.Ordinal))) continue;
                if (!new[] { ".almx", ".resx", ".almlookup", ".almproj", ".shproj", ".projitems" }.Contains(Path.GetExtension(file)) && Path.GetFileName(file) != ".editorconfig") continue;
                string copy = Path.Combine(destination, relative); Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(file, copy);
            }
            foreach (string file in new[] { ".editorconfig", "Directory.Build.props", "Directory.Build.targets", "Example.sln" }) File.Copy(Path.Combine(source, file), Path.Combine(destination, file));
        }
    }
}
