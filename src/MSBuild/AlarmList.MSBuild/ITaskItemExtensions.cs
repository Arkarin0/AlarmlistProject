using Microsoft.Build.Framework;
using System.Linq;

namespace Alarmlist.MSBuild
{
    internal static class ITaskItemExtensions
    {
        public static string[] ToIncludeList(this ITaskItem[] items)
        {
            return items.Select(item=> Factory.GetFullFilePath(item)).ToArray();
        }
    }
}
