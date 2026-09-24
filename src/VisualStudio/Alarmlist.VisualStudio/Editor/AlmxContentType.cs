// Created/modified by Arkarin0 under one ore more license(s).

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace Alarmlist.VisualStudio.Editor
{
    internal static class AlmxContentType
    {
        [Export]
        [FileExtension(".almx")]
        [ContentType("XML")]
        internal static FileExtensionToContentTypeDefinition s_almxFileExtension;
    }
}
