// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Text;

namespace Alarmlist.Libraries
{
    internal static class LibraryResourceProjection
    {
        /// <summary>Projects validated string data, retaining original spans while dropping editor/runtime metadata.</summary>
        internal static ResxTextSnapshot Capture(ResxTextSnapshot snapshot)
        {
            using (var input = new StringReader(snapshot.Text))
            using (XmlReader reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true, IgnoreProcessingInstructions = true
            }))
            {
                XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                XmlOriginMap original = snapshot.Origins ?? XmlOriginMap.Capture(document, snapshot.FilePath);
                var positions = XmlOriginMap.Nodes(document).ToDictionary(node => node, node => original.Resolve(node, snapshot.FilePath));
                document.Root.Elements().Where(element => element.Name != "data").Remove();
                foreach (XElement element in document.Descendants())
                    element.Attributes().Where(attribute => attribute.IsNamespaceDeclaration || attribute.Name == "type").Remove();
                XmlOriginMap origins = XmlOriginMap.Capture(document, snapshot.FilePath, node => positions[node]);
                return new ResxTextSnapshot(document.ToString(SaveOptions.DisableFormatting), snapshot.FilePath, 0, snapshot.Culture, origins);
            }
        }
    }
}
