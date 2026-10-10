// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Testing
{
    // Called under the live-test mutex, before launching the owned IDE or timing.
    internal static class VisualStudioTestDeployment
    {
        internal static void Ensure(string version, string installation, string suffix, string logDirectory, Action<string> log)
        {
            Require(Regex.IsMatch(suffix ?? "", "^[A-Za-z][A-Za-z0-9_]*$"), "An explicit experimental profile is required.");
            string repository = Metadata("RepositoryRoot");
            string configuration = Metadata("BuildConfiguration");
            string vsix = Path.Combine(AppContext.BaseDirectory, "testassets", "Alarmlist.VisualStudio.vsix");
            Require(File.Exists(vsix), "Build the xUnit project to prepare its VSIX test asset.");
            string executable = Path.Combine(installation, "Common7", "IDE", "devenv.exe");
            Require(File.Exists(executable), "The configured Visual Studio installation does not exist.");
            int major = FileVersionInfo.GetVersionInfo(executable).FileMajorPart;
            Require((version == "2022" && major == 17) || (version == "2026" && major == 18), "The configured installation does not match the requested IDE host.");
            string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            XElement instances = Parse(Run(vswhere, "-all -products * -format xml -utf8", repository, logDirectory, "instances"));
            XElement instance = instances.Elements("instance").SingleOrDefault(item =>
                SamePath((string)item.Element("installationPath"), installation));
            Require(instance != null, "The configured Visual Studio installation was not found by vswhere.");
            string id = (string)instance.Element("instanceId");
            Require(Regex.IsMatch(id ?? "", "^[A-Za-z0-9]+$"), "Invalid Visual Studio instance identity.");

            string system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.Is64BitProcess ? "System32" : "Sysnative");
            string powershell = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
            XElement processes = Parse(Run(powershell, "-NoProfile -NonInteractive -Command " + Quote(
                "[Console]::OutputEncoding = [Text.Encoding]::UTF8; Get-CimInstance Win32_Process -Filter \"name = 'devenv.exe'\" -ErrorAction Stop | Select-Object ProcessId,ExecutablePath,CommandLine | ConvertTo-Xml -As String"),
                repository, logDirectory, "running-ides"));
            foreach (XElement process in processes.Elements("Object"))
            {
                string path = Property(process, "ExecutablePath");
                string command = Property(process, "CommandLine");
                Require(!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(command), "Cannot identify a running IDE; deployment refused.");
                if (SamePath(path, executable) && UsesProfile(SplitCommandLine(command), suffix))
                    throw new InvalidOperationException("Close the existing " + suffix + " IDE before running tests. No existing IDE was changed or stopped.");
            }

            string extensions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "VisualStudio",
                major + ".0_" + id + suffix, "Extensions");
            Dictionary<string, byte[]> payload = ReadPayload(vsix);
            string identity = ManifestIdentity(payload["extension.vsixmanifest"]);
            Require(!string.IsNullOrEmpty(identity), "The VSIX test asset has no extension identity.");
            string installed = FindExtension(extensions, identity);
            if (Matches(installed, payload))
            {
                log("VS" + major + " " + suffix + ": deployed extension matches the test asset; no deployment needed.");
                return;
            }

            log("VS" + major + " " + suffix + ": deploying missing or outdated test extension.");
            string manifest = Path.Combine(logDirectory, "extension.vsixmanifest");
            File.WriteAllBytes(manifest, payload["extension.vsixmanifest"]);
            string msbuild = Path.Combine(installation, "MSBuild", "Current", "Bin", "MSBuild.exe");
            string arguments = Quote(Path.Combine(repository, "src", "VisualStudio", "Alarmlist.VisualStudio", "Alarmlist.VisualStudio.csproj"))
                + " /t:DeployVsixExtensionFiles /nologo /v:minimal /nr:false"
                + " " + Quote("/p:Configuration=" + configuration)
                + " " + Quote("/p:DeployTargetInstanceId=" + id)
                + " " + Quote("/p:VSSDKTargetPlatformRegRootSuffix=" + suffix)
                + " " + Quote("/p:AlarmlistTestVsixPath=" + vsix)
                + " " + Quote("/p:AlarmlistTestVsixManifest=" + manifest);
            Run(msbuild, arguments, repository, logDirectory, "deployment", timeoutSeconds: 300);
            Require(Matches(FindExtension(extensions, identity), payload), "Deployment finished without installing the matching test payload. Inspect " + logDirectory);
            log("Verified the deployed test extension, templates and dependencies.");
        }

        internal static Dictionary<string, byte[]> ReadPayload(string vsix)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            using (var archive = ZipFile.OpenRead(vsix))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.Name.Length == 0) continue;
                    string name = Uri.UnescapeDataString(entry.FullName).Replace('\\', '/');
                    // Packaging metadata is not loaded by the IDE.
                    if (name == "[Content_Types].xml" || name.StartsWith("_rels/", StringComparison.Ordinal)
                        || name.StartsWith("package/", StringComparison.Ordinal)) continue;
                    Require(!Path.IsPathRooted(name) && !name.Split('/').Any(part => part == ".." || part == "."), "Unexpected VSIX payload path.");
                    using (var memory = new MemoryStream())
                    using (Stream stream = entry.Open())
                    { stream.CopyTo(memory); files.Add(name, memory.ToArray()); }
                }
            }
            Require(files.ContainsKey("extension.vsixmanifest") && files.ContainsKey("Alarmlist.VisualStudio.dll"), "Incomplete VSIX test asset.");
            return files;
        }

        internal static bool Matches(string directory, IReadOnlyDictionary<string, byte[]> payload)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return false;
            using (var hash = SHA256.Create())
            {
                foreach (KeyValuePair<string, byte[]> file in payload)
                {
                    string path = Path.Combine(directory, file.Key.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(path)) return false;
                    using (Stream stream = File.OpenRead(path))
                        if (!hash.ComputeHash(stream).SequenceEqual(hash.ComputeHash(file.Value))) return false;
                }
            }
            return true;
        }

        internal static bool UsesProfile(IReadOnlyList<string> arguments, string suffix)
        {
            for (int index = 1; index < arguments.Count; index++)
            {
                string argument = arguments[index];
                if ((argument.Equals("/RootSuffix", StringComparison.OrdinalIgnoreCase) || argument.Equals("-RootSuffix", StringComparison.OrdinalIgnoreCase))
                    && index + 1 < arguments.Count && arguments[index + 1].Equals(suffix, StringComparison.OrdinalIgnoreCase)) return true;
                if (argument.Equals("/RootSuffix:" + suffix, StringComparison.OrdinalIgnoreCase) || argument.Equals("-RootSuffix:" + suffix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string FindExtension(string root, string identity)
        {
            if (!Directory.Exists(root)) return null;
            string[] matches = Directory.GetFiles(root, "extension.vsixmanifest", SearchOption.AllDirectories)
                .Where(file => ManifestIdentity(File.ReadAllBytes(file)) == identity).Select(Path.GetDirectoryName).ToArray();
            Require(matches.Length <= 1, "Multiple installed copies of the test extension were found; deployment is ambiguous.");
            return matches.SingleOrDefault();
        }

        private static string ManifestIdentity(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                return (string)XDocument.Load(reader).Descendants().FirstOrDefault(element => element.Name.LocalName == "Identity")?.Attribute("Id");
        }

        private static string Run(string executable, string arguments, string directory, string logs, string name, int timeoutSeconds = 60)
        {
            var start = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            start.EnvironmentVariables.Remove("PSModulePath");
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            using (var process = new Process { StartInfo = start })
            {
                process.OutputDataReceived += (_, item) => { if (item.Data != null) stdout.AppendLine(item.Data); };
                process.ErrorDataReceived += (_, item) => { if (item.Data != null) stderr.AppendLine(item.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                bool finished = process.WaitForExit(timeoutSeconds * 1000);
                if (!finished) process.Kill();
                process.WaitForExit();
                File.WriteAllText(Path.Combine(logs, name + ".log"), stdout + Environment.NewLine + stderr);
                Require(finished && process.ExitCode == 0, "Test deployment preparation failed: " + name + ". Inspect " + logs);
                return stdout.ToString();
            }
        }

        private static string[] SplitCommandLine(string command)
        {
            IntPtr pointer = CommandLineToArgvW(command, out int count);
            Require(pointer != IntPtr.Zero, "Cannot read a running IDE command line.");
            try { return Enumerable.Range(0, count).Select(index => Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size))).ToArray(); }
            finally { LocalFree(pointer); }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW(string command, out int count);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr pointer);
        private static string Property(XElement item, string name) => (string)item.Elements("Property").SingleOrDefault(property => (string)property.Attribute("Name") == name);
        private static XElement Parse(string xml)
        {
            using (var text = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit })) return XDocument.Load(reader).Root;
        }
        private static bool SamePath(string left, string right) => !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right)
            && Path.GetFullPath(left).TrimEnd('\\', '/').Equals(Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        private static string Metadata(string name) => typeof(VisualStudioTestDeployment).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == name).Value;
        private static string Quote(string value)
        {
            Require(!string.IsNullOrEmpty(value) && value.IndexOfAny(new[] { '\r', '\n' }) < 0, "Invalid process argument.");
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
