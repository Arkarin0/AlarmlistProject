using Microsoft.Build.Framework;
using System;
using System.IO;

namespace Alarmlist.MSBuild
{
    internal partial class Factory
    {
        public static string GetFullFilePath(ITaskItem item)
        {
            _ = item ?? throw new ArgumentNullException(nameof(item));

            var fullfilepath = item.GetMetadata("FullPath");
            if (string.IsNullOrWhiteSpace(fullfilepath))
                fullfilepath = item.ItemSpec;

            return Path.GetFullPath(fullfilepath);
        }
    }
}
