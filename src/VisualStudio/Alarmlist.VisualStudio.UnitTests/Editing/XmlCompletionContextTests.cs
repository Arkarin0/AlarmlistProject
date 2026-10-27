// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.Editor;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class XmlCompletionContextTests
    {
        [Theory]
        [InlineData("<", 1, 1, 0)]
        [InlineData("<Al", 3, 1, 2)]
        [InlineData("  <Alarm>", 5, 3, 5)]
        public void RootCompletionReplacesTheWholeNameWithoutReplacingMarkup(string text, int caret, int expectedStart, int expectedLength)
        {
            XmlCompletionEdit edit = XmlCompletionContext.Find(text, caret);
            Assert.NotNull(edit);
            Assert.Equal(XmlCompletionKind.Element, edit.Kind);
            Assert.Empty(edit.Ancestors);
            Assert.Equal(expectedStart, edit.Start);
            Assert.Equal(expectedLength, edit.Length);
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("<Al", -1)]
        [InlineData("<Al", 4)]
        [InlineData("<!-- <Al", 8)]
        [InlineData("<Root><Al", 9)]
        [InlineData("</Al", 4)]
        [InlineData("<!Al", 4)]
        [InlineData("<Root a=\"<Al", 12)]
        public void RootCompletionDoesNotParticipateOutsideTheRootName(string text, int caret)
        {
            XmlCompletionEdit edit = XmlCompletionContext.Find(text, caret);
            Assert.False(edit != null && edit.Kind == XmlCompletionKind.Element && edit.Ancestors.Length == 0);
        }

        [Theory]
        [InlineData("<?xml version=\"1.0\"?>\r\n<¦", "Alarmlist")]
        [InlineData("<!-- comment -->\n<¦", "Alarmlist")]
        [InlineData("<Alarmlist><¦", "Alarm")]
        [InlineData("<Alarmlist><Properties><¦", "Property")]
        [InlineData("<Alarmlist><Alarm><¦", "FullyQualifiedName")]
        [InlineData("<Alarmlist><Alarm><Properties><¦", "Property")]
        [InlineData("<Alarmlist><Alarm><TestProcedure><¦", "Group")]
        [InlineData("<Alarmlist><Alarm><TestProcedure><Group Name='G'><¦", "Step")]
        [InlineData("<Alarmlist><Alarm><TestProcedure><Group Name='G'><¦", "Title")]
        [InlineData("<Alarmlist><Alarm><TestProcedure><Group Name='G'><¦", "Clear")]
        [InlineData("<Alarmlist><Alarm ¦", "abstract")]
        [InlineData("<Alarmlist><Alarm abstract='¦'", "true")]
        [InlineData("<Alarmlist><Alarm abstract=\"¦\"", "false")]
        [InlineData("<Alarmlist><Properties><Property ¦", "Name")]
        [InlineData("<Alarmlist><Properties><Property Name='X' ¦", "Value")]
        [InlineData("<Alarmlist><Alarm><TestProcedure><Group ¦", "Name")]
        [InlineData("<Alarmlist><Alarm></¦", "Alarm")]
        public void StructuralCandidatesFollowXmlScope(string source, string candidate)
            => Assert.Contains(candidate, Candidates(source));

        [Theory]
        [InlineData("<Alarmlist><!-- <¦ -->")]
        [InlineData("<Alarmlist><![CDATA[<¦]]>")]
        [InlineData("<?xml ¦ ?>")]
        [InlineData("<!DOCTYPE Alarmlist [ <!ELEMENT Alarmlist ANY> ]><¦")]
        [InlineData("<Alarmlist><Unknown><¦")]
        [InlineData("<Alarmlist><Alarm abstract='$(¦)' />")]
        [InlineData("<Alarmlist><Alarm abstract='true' ¦/>")]
        [InlineData("<Alarmlist xmlns='urn:other'><¦")]
        [InlineData("<x:Alarmlist><¦")]
        [InlineData("<x:¦")]
        [InlineData("<Alarmlist/><¦")]
        [InlineData("<Alarmlist><Alarm></Wrong><¦")]
        public void UnsupportedAndLiteralContextsDoNotOfferStructure(string source)
            => Assert.Empty(Candidates(source));

        [Theory]
        [InlineData("<Alarmlist><Alarm><Name/><¦", "Name")]
        [InlineData("<Alarmlist><Alarm><TestProcedure><Group Name='G'><Title>T</Title><¦", "Title")]
        [InlineData("<Alarmlist><Properties><Property ¦ Value='a Name=\"b\"'/>", "Value")]
        public void ExistingSingletonsAndAttributesAreNotSuggestedAgain(string source, string candidate)
            => Assert.DoesNotContain(candidate, Candidates(source));

        [Fact]
        public void EditingAnExistingAttributeKeepsItsCandidateAndQuotesOutsideTheSpan()
        {
            const string source = "<Alarmlist><Alarm abs¦tract='false'/>";
            string text = source.Replace("¦", "");
            XmlCompletionEdit edit = XmlCompletionContext.Find(text, source.IndexOf('¦'));
            Assert.Equal("abstract", text.Substring(edit.Start, edit.Length));
            Assert.Contains("abstract", Candidates(source));
            Assert.Equal("<Alarmlist><Alarm abstract='false'/>", text.Remove(edit.Start, edit.Length).Insert(edit.Start, "abstract"));
        }

        [Theory]
        [InlineData("<Alarmlist><Alarm abstract='fa¦></Alarm></Alarmlist>")]
        [InlineData("<Alarmlist><Alarm abstract='fa¦\n  <Alarm/>")]
        public void MissingAttributeQuoteNeverMakesLaterMarkupPartOfTheReplacement(string source)
        {
            string text = source.Replace("¦", "");
            XmlCompletionEdit edit = XmlCompletionContext.Find(text, source.IndexOf('¦'));
            Assert.Equal("fa", text.Substring(edit.Start, edit.Length));
            Assert.Equal(source.Replace("fa¦", "false"), text.Remove(edit.Start, edit.Length).Insert(edit.Start, "false"));
        }

        [Theory]
        [InlineData("<Alarmlist><Alarm abstract='  tr¦  '/></Alarmlist>")]
        [InlineData("<Alarmlist><Alarm><Enabled>\r\n  tr¦\r\n</Enabled></Alarm></Alarmlist>")]
        public void LiteralCompletionPreservesSurroundingValueWhitespace(string source)
        {
            string text = source.Replace("¦", "");
            XmlCompletionEdit edit = XmlCompletionContext.Find(text, source.IndexOf('¦'));
            Assert.Equal("tr", text.Substring(edit.Start, edit.Length));
            Assert.Equal(source.Replace("tr¦", "true"), text.Remove(edit.Start, edit.Length).Insert(edit.Start, "true"));
        }

        [Fact]
        public void AlternateMetadataControlsAllFieldsCollectionsAndProcedureCapability()
        {
            var fields = new[] { Field("Enabled", "Boolean"), Field("Priority", "Enum", "Low", "High&\"<") };
            var collections = new[]
            {
                new AlarmCollectionDescriptor("Flags", "Simple", "Flags", "", 0, null, new[] { Field("Item", "Boolean") }),
                new AlarmCollectionDescriptor("Checks", "Complex", "Checks", "", 0, null, new[] { Field("Count", "Int64"), Field("Enabled", "Boolean") })
            };
            string[] Complete(string source) => Candidates(source, fields, collections, false);
            Assert.Contains("Enabled", Complete("<Alarmlist><Alarm><¦"));
            Assert.DoesNotContain("Name", Complete("<Alarmlist><Alarm><¦"));
            Assert.DoesNotContain("TestProcedure", Complete("<Alarmlist><Alarm><¦"));
            Assert.Empty(Complete("<Alarmlist><Alarm><TestProcedure><¦"));
            Assert.Contains("Flags", Complete("<Alarmlist><Alarm><¦"));
            Assert.Equal(new[] { "Clear", "Item" }, Complete("<Alarmlist><Alarm><Flags><¦"));
            Assert.Equal(new[] { "Count", "Enabled" }, Complete("<Alarmlist><Alarm><Checks><Item><¦"));
            Assert.Equal(new[] { "false", "true" }, Complete("<Alarmlist><Alarm><Enabled>¦"));
            Assert.Equal(new[] { "false", "true" }, Complete("<Alarmlist><Alarm><Flags><Item>¦"));
            Assert.Equal(new[] { "false", "true" }, Complete("<Alarmlist><Alarm><Checks><Item><Enabled>¦"));
            Assert.Contains("High&amp;&quot;&lt;", Complete("<Alarmlist><Alarm><Priority>¦"));
            Assert.Empty(Complete("<Alarmlist><Alarm><Checks><Item><Count>¦"));
            Assert.Empty(Complete("<Alarmlist><Alarm><Enabled>$(¦)"));
            Assert.DoesNotContain("Enabled", Complete("<Alarmlist><Alarm><Checks><Item><Enabled>true</Enabled><¦"));
        }

        private static AlarmFieldDescriptor Field(string name, string type, params string[] values)
            => new AlarmFieldDescriptor(name, type, false, "literal default", null, null, name + " caption", "help", false, true, 100,
                values, values.Select(value => "caption " + value).ToArray());

        private static string[] Candidates(string source, AlarmFieldDescriptor[] fields = null,
            AlarmCollectionDescriptor[] collections = null, bool? procedure = null)
        {
            string text = source.Replace("¦", "");
            return AlmxXmlGrammar.Complete(text, XmlCompletionContext.Find(text, source.IndexOf('¦')), fields, collections, procedure)
                .Select(item => item.Insertion).ToArray();
        }
    }
}
