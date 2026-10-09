// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Alarmlist.ConfigurationInput
{
    /// <summary>Host-owned saved configuration discovery, shared by MSBuild and Visual Studio.</summary>
    internal static class SavedEditorConfigCapture
    {
        internal static IReadOnlyList<CapturedEditorConfig> Capture(IEnumerable<string> sourcePaths, CancellationToken token,
            Func<string, string> readText = null)
        {
            readText = readText ?? ReadSavedText;
            var comparer = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var files = new Dictionary<string, CapturedEditorConfig>(comparer);
            var ordered = new List<CapturedEditorConfig>();
            foreach (string source in sourcePaths)
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(source));
                while (directory != null)
                {
                    token.ThrowIfCancellationRequested();
                    string path = Path.Combine(directory, ".editorconfig");
                    if (!files.TryGetValue(path, out CapturedEditorConfig input))
                    {
                        string text;
                        try { text = readText(path); }
                        catch (FileNotFoundException) { text = null; }
                        catch (DirectoryNotFoundException) { text = null; }
                        input = new CapturedEditorConfig(path, text);
                        files.Add(path, input);
                        ordered.Add(input);
                    }
                    if (input.Text != null && EditorConfigPreamble.IsRoot(input.Text)) break;
                    directory = Path.GetDirectoryName(directory);
                }
            }
            return ordered.AsReadOnly();
        }

        private static string ReadSavedText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var bytes = new MemoryStream())
            {
                stream.CopyTo(bytes);
                return new UTF8Encoding(false, true).GetString(bytes.ToArray());
            }
        }
    }

    [Serializable]
    internal sealed class CapturedEditorConfig
    {
        internal CapturedEditorConfig(string path, string text) { FilePath = path; Text = text; }
        public string FilePath { get; }
        // Null records an absent candidate so later creation invalidates a captured result.
        public string Text { get; }
        internal bool Matches(CapturedEditorConfig other) => other != null && FilePath == other.FilePath && Text == other.Text;
    }
}
