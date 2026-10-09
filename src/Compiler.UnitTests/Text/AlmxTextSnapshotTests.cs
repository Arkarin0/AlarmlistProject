// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Alarmlist.Compiler;
using Alarmlist.Syntax;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Text
{
    public class AlmxTextSnapshotTests
    {
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public void LocationsIdentifyLocalDeclarationsInTheCapturedVersion(string newline)
        {
            string text = string.Join(newline, new[]
            {
                "<a:Alarmlist xmlns:a='urn:alarms'>",
                "  <!-- <a:Alarm><a:Name>ignored</a:Name></a:Alarm> -->",
                "  <a:Alarm>",
                "    <a:FullyQualifiedName>Plant.A</a:FullyQualifiedName>",
                "    <a:Unknown><a:ReferenceName>Ignored</a:ReferenceName></a:Unknown>",
                "    <a:Name>First</a:Name>",
                "    <a:Name />",
                "    <a:Description>Text &amp; 😀</a:Description>",
                "    <a:ReferenceName>Base</a:ReferenceName>",
                "  </a:Alarm>",
                "</a:Alarmlist>"
            });
            var snapshot = new AlmxTextSnapshot(text, "Plant.almx", 17);

            AlarmSyntaxNode alarm = Assert.Single(snapshot.Parse().Alarms);

            Assert.Equal("Plant.almx", alarm.Location.FilePath);
            Assert.Equal(17, alarm.Location.SourceVersion);
            Assert.Equal(3, alarm.Location.LineNumber);
            Assert.Equal(4, alarm.Location.ColumnNumber);
            Assert.Equal(7, alarm.GetFieldLocation("Name").LineNumber);
            Assert.Null(alarm.Name);
            Assert.Equal("Text & 😀", alarm.Description);
            Assert.Null(alarm.GetFieldLocation("Code"));
            Assert.Null(alarm.GetFieldLocation("Unknown"));
            SourceLocation reference = alarm.GetFieldLocation("ReferenceName");
            Assert.Equal(9, reference.LineNumber);
            Assert.Equal(6, reference.ColumnNumber);
            Assert.Equal(reference.LineNumber, reference.EndLineNumber);
            Assert.Equal(6 + "a:ReferenceName".Length, reference.EndColumnNumber);
            Assert.Equal(17, reference.SourceVersion);
        }

        [Fact]
        public void ParsingAgainDoesNotReuseMutableSyntaxOrReferences()
        {
            const string text = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>"
                + "<Name>Original</Name><ReferenceName>Base</ReferenceName>"
                + "<TestProcedure><Instructions><Step>Test</Step></Instructions></TestProcedure>"
                + "</Alarm></Alarmlist>";
            var snapshot = new AlmxTextSnapshot(text, "A.almx", 1);
            AlarmSyntaxNode first = Assert.Single(snapshot.Parse().Alarms);
            first.Name = "Changed";
            first.ReferenceName = "Other";
            first.TestProcedure.Instructions.Clear();

            AlarmSyntaxNode second = Assert.Single(snapshot.Parse().Alarms);

            Assert.NotSame(first, second);
            Assert.Equal("Original", second.Name);
            Assert.Equal("Base", second.ReferenceName);
            Assert.Null(second.Reference);
            Assert.Single(second.TestProcedure.Instructions);
            Assert.Equal(text, snapshot.Text);
        }

        [Fact]
        public void SnapshotCompilationUsesUnsavedTextAndPreservesReferenceAndClearRules()
        {
            string path = Path.GetTempFileName();
            const string baseText = "<Alarmlist><Alarm><FullyQualifiedName>Base</FullyQualifiedName>"
                + "<Name>Überdruck</Name><Code>B</Code><TestProcedure>"
                + "<Instructions><Step>Inherited</Step></Instructions>"
                + "<Reset><Step>Reset</Step></Reset></TestProcedure></Alarm></Alarmlist>";
            const string childText = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName>"
                + "<ReferenceName>Base</ReferenceName><Name/><Code>Local</Code><TestProcedure>"
                + "<Instructions><Note>Excluded</Note><Clear/><Warning>Check</Warning></Instructions>"
                + "<Reset><Hint>Local reset</Hint></Reset></TestProcedure></Alarm></Alarmlist>";
            try
            {
                const string savedText = "<Alarmlist/>";
                File.WriteAllText(path, savedText);
                var compiler = new AlarmCompiler();
                var baseSnapshot = new AlmxTextSnapshot(baseText, path, 2);
                var childSnapshot = new AlmxTextSnapshot(childText, path + ".unsaved", 3);

                CompilationResult result = compiler.CompileSnapshots(new[] { baseSnapshot, childSnapshot });

                Assert.True(result.Success);
                Alarm child = result.AlarmList.Single(alarm => alarm.FullyQualifiedName == "Child");
                Assert.Equal("Überdruck", child.Name);
                Assert.Equal("Local", child.Code);
                Assert.Equal("Check", Assert.Single(child.TestProcedure.Instructions).Text);
                Assert.Equal(TestProcedureItemKind.Warning, child.TestProcedure.Instructions[0].Kind);
                Assert.Equal(new[] { "Reset", "Local reset" }, child.TestProcedure.Reset.Select(item => item.Text));
                Assert.Equal(savedText, File.ReadAllText(path));
                Assert.False(File.Exists(childSnapshot.FilePath));

                CompilationResult missing = compiler.CompileSnapshots(new[] { childSnapshot });
                Assert.False(missing.Success);
                Assert.Equal("ALM0002", Assert.Single(missing.Diagnostics).Id);
                Assert.Empty(missing.AlarmList);
                Assert.Equal("Überdruck", child.Name);

                File.WriteAllText(path, "<?xml version='1.0' encoding='utf-16'?>" + baseText, Encoding.Unicode);
                Alarm saved = Assert.Single(compiler.CompileFiles(new[] { path }).AlarmList);
                Assert.Equal(result.AlarmList[0].Name, saved.Name);
                Assert.Equal(result.AlarmList[0].Code, saved.Code);
                Assert.Equal(result.AlarmList[0].TestProcedure.Instructions, saved.TestProcedure.Instructions);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData("<!DOCTYPE Alarmlist [<!ENTITY name 'Injected'>]><Alarmlist><Alarm><Name>&name;</Name></Alarm></Alarmlist>")]
        [InlineData("<Alarmlist><Alarm>")]
        [InlineData("<Alarmlist/>\n\n<Alarmlist/>")]
        [InlineData("<Alarmlist/>\n\ntrailing text")]
        public void DiskAndSnapshotReadersRejectUnsafeOrMalformedXml(string text)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, text);
                var snapshot = new AlmxTextSnapshot(text, path, 0);
                Assert.Throws<XmlException>(() => snapshot.Parse());
                Assert.Throws<XmlException>(() => new AlmxFile(path).Read());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void EmptyDocumentsAndLocationFreeReaderCallersRemainSupported()
        {
            Assert.Empty(new AlmxTextSnapshot("<Alarmlist/>", "Empty.almx", 0).Parse().Alarms);
            using (XmlReader reader = XmlReader.Create(new StringReader("<Alarm><Name>Local</Name></Alarm>")))
            {
                Assert.True(AlmxFile.ReadAlarmSyntaxNode(reader, out AlarmSyntaxNode alarm));
                Assert.Equal("Local", alarm.Name);
                Assert.Null(alarm.Location);
                Assert.Null(alarm.GetFieldLocation("Name"));
            }
        }
    }
}
