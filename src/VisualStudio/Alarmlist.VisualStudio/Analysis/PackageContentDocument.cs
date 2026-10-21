// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    internal static class PackageContentDocument
    {
        internal static AnalysisNavigationTarget Create(ProjectAnalysisState state, AnalysisPackageNode package, AnalysisPackageContent content)
        {
            byte[] bytes = content.ReadCurrentBytes();
            string text;
            if (content.Kind == "Analyzer")
            {
                var analyzer = state.Response?.Analyzers.FirstOrDefault(item => string.Equals(item.FilePath, content.FilePath, StringComparison.OrdinalIgnoreCase));
                text = "Assembly: " + (analyzer?.AssemblyIdentity ?? "Unavailable") + Environment.NewLine
                    + "Path: " + content.FilePath + Environment.NewLine
                    + "Package: " + package.Id + " " + package.Version + Environment.NewLine;
                if (analyzer?.Failure != null) text += analyzer.Failure + Environment.NewLine;
            }
            else
            {
                using (var stream = new MemoryStream(bytes))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true)) text = reader.ReadToEnd();
                if (content.Kind == "Library")
                    using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                        text = XDocument.Load(reader).ToString();
            }
            return AnalysisNavigationTarget.ReadOnlyDocument(text, package.Id + " " + package.Version + " — " + content.RelativePath,
                content.Kind == "Library");
        }
    }
}
