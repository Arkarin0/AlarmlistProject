// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Text;
using System.Xml;

namespace Alarmlist.MSBuild
{
    internal static class CapturedXmlFile
    {
        internal static string Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding encoding;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new XmlTextReader(stream) { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })
            {
                // Detect XML encoding from the captured bytes. Full syntax validation
                // belongs to the compiler, including located RESX diagnostics.
                try { reader.Read(); }
                catch (XmlException) { }
                encoding = reader.Encoding ?? Encoding.UTF8;
            }
            Encoding strict = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            string text = strict.GetString(bytes);
            return text.Length != 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }
    }
}
