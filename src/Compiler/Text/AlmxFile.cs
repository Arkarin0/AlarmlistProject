// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Alarmlist.Syntax;

namespace Alarmlist.Text
{
    public partial class AlmxFile : SourceText, IXmlSerializable
    {
        public AlmxFile() : base()
        {

        }

        public AlmxFile(string filePath) : this()
        {
            FilePath = filePath;
        }

        public AlmxFile(AlarmSyntaxTree tree) : this()
        {
            SetSyntaxTree(tree);
        }

        public string FilePath { get; set; }

        public static AlmxFile FromFile(string filePath)
        {
            var file = new AlmxFile(filePath);
            file.Read();
            return file;
        }

        protected override AlarmSyntaxTree OnRead()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                return base.OnRead();

            using (var reader = XmlReader.Create(FilePath, CreateReaderSettings()))
            {
                if (!ReadAlarmListNode(reader, out var tree))
                    return new AlarmSyntaxTree();

                return tree;
            }
        }

        protected override void OnWrite(AlarmSyntaxTree tree)
        {
            if (tree == null)
                throw new ArgumentNullException(nameof(tree));

            if (string.IsNullOrWhiteSpace(FilePath))
            {
                base.OnWrite(tree);
                return;
            }

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (var writer = XmlWriter.Create(FilePath, CreateWriterSettings()))
            {
                WriteAlarmlistNode(tree, writer);
            }
        }

        XmlSchema IXmlSerializable.GetSchema() => null;

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (!ReadAlarmListNode(reader, out var tree))
                tree = new AlarmSyntaxTree();

            SetSyntaxTree(tree);
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            var tree = new AlarmSyntaxTree();
            foreach (var alarm in Alarms)
                tree.Alarms.Add(alarm);

            foreach (var alarm in tree.Alarms)
                WriteAlarmSyntaxNode(alarm, writer);
        }

        private static XmlReaderSettings CreateReaderSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit
            };
        }

        private static XmlWriterSettings CreateWriterSettings()
        {
            return new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                OmitXmlDeclaration = false
            };
        }
    }
}
