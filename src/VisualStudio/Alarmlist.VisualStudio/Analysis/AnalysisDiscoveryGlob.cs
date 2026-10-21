// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Globbing;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Detached evaluated item patterns; membership remains owned by MSBuild evaluation.</summary>
    internal sealed class AnalysisDiscoveryGlob
    {
        private readonly MSBuildGlob[] _includes;
        private readonly MSBuildGlob[] _excludes;
        public IReadOnlyList<string> Directories { get; }
        public bool Recursive { get; }
        public string Identity { get; }

        public AnalysisDiscoveryGlob(string root, IEnumerable<string> includes, IEnumerable<string> excludes)
        {
            string[] includePatterns = includes.ToArray(), excludePatterns = excludes.ToArray();
            Identity = root + "\0" + string.Join("\0", includePatterns) + "\0\0" + string.Join("\0", excludePatterns);
            _includes = includePatterns.Select(pattern => MSBuildGlob.Parse(root, pattern)).ToArray();
            _excludes = excludePatterns.Select(pattern => MSBuildGlob.Parse(root, pattern)).ToArray();
            Recursive = _includes.Any(glob => !string.IsNullOrEmpty(glob.WildcardDirectoryPart));
            Directories = Array.AsReadOnly(_includes.Where(glob => glob.IsLegal).Select(glob => glob.FixedDirectoryPart)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }

        public bool Matches(string path) => _includes.Any(glob => glob.IsMatch(path)) && !_excludes.Any(glob => glob.IsMatch(path));

        public bool IntersectsDirectory(string path)
        {
            string directory = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Directories.Any(root => root.StartsWith(directory, StringComparison.OrdinalIgnoreCase)
                || (Recursive && directory.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
