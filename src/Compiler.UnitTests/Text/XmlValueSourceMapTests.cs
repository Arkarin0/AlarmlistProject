// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Text
{
    public class XmlValueSourceMapTests
    {
        [Theory]
        [InlineData("<r><Name>$(Old)</Name></r>", "$(Old)", "Old", "New", "<r><Name>$(New)</Name></r>")]
        [InlineData("<r><!--Old--><Name a='x'>$(O&#108;d)</Name></r>", "$(Old)", "Old", "New", "<r><!--Old--><Name a='x'>$(New)</Name></r>")]
        [InlineData("<r>\r\n<Name><![CDATA[$(Old) & literal]]></Name></r>", "$(Old) & literal", "Old", "New", "<r>\r\n<Name><![CDATA[$(New) & literal]]></Name></r>")]
        [InlineData("<r><Name a=\"Old\" b='same'/></r>", "Old", "Old", "A&B\"", "<r><Name a=\"A&amp;B&quot;\" b='same'/></r>")]
        [InlineData("<r xmlns='urn:r' xmlns:p='urn:p'><p:Name p:a='Old'/></r>", "Old", "Old", "New", "<r xmlns='urn:r' xmlns:p='urn:p'><p:Name p:a='New'/></r>")]
        [InlineData("<?xml version='1.0'?><r><Name>line\r\n$(Old)</Name></r>", "line\n$(Old)", "Old", "New", "<?xml version='1.0'?><r><Name>line\r\n$(New)</Name></r>")]
        [InlineData("<r>Old</r>", "Old", "Old", "A\rB", "<r>A&#xD;B</r>")]
        [InlineData("<r><![CDATA[Old]]></r>", "Old", "Old", "A\rB", "<r><![CDATA[A]]>&#xD;<![CDATA[B]]></r>")]
        public void MapsOnlyRequestedDecodedSpanAndPreservesOtherSource(string source, string decoded, string before, string after, string expected)
        {
            var map = XmlValueSourceMap.Parse(source);
            var value = map.Values.Values.Single(item => item.Value == decoded);
            Assert.True(value.TryReplace(decoded.IndexOf(before, StringComparison.Ordinal), before.Length, after, out var edit));
            Assert.Equal(expected, edit.Apply(source));
            var parsed = XDocument.Parse(expected, LoadOptions.PreserveWhitespace);
            Assert.Contains(parsed.Descendants().SelectMany(element => element.Attributes()).Select(attribute => attribute.Value)
                .Concat(parsed.Descendants().Where(element => !element.HasElements).Select(element => element.Value)), item => item.Contains(after));
        }

        [Theory]
        [InlineData("<r>O<!--keep-->ld</r>")]
        [InlineData("<r>O<![CDATA[ld]]></r>")]
        public void FragmentedTokensAreNotMappedAcrossMarkup(string source)
        {
            var map = XmlValueSourceMap.Parse(source); var value = map.Values[map.Document.Root];
            Assert.Equal("Old", value.Value); Assert.False(value.TryReplace(0, 3, "New", out _));
            Assert.True(value.TryReplace(1, 2, "tail", out _));
        }

        [Fact]
        public void EntityAndSurrogateBoundariesAndCdataTerminatorAreSafe()
        {
            var map = XmlValueSourceMap.Parse("<r>&#x1F600;</r>"); var value = map.Values[map.Document.Root];
            Assert.False(value.TryReplace(0, 1, "x", out _)); Assert.False(value.TryReplace(1, 1, "x", out _));
            Assert.True(value.TryReplace(0, 2, "x", out _));
            map = XmlValueSourceMap.Parse("<r><![CDATA[Old]]></r>");
            Assert.True(map.Values[map.Document.Root].TryReplace(0, 3, "]]>", out var edit));
            Assert.Equal("]]>", XDocument.Parse(edit.Apply("<r><![CDATA[Old]]></r>")).Root.Value);
        }

        [Fact]
        public void AttributeWhitespaceNormalizationStillMapsOriginalSpan()
        {
            const string source = "<r a='start\r\nOld&#xA;end'/>";
            var map = XmlValueSourceMap.Parse(source); var value = map.Values[map.Document.Root.Attribute("a")];
            Assert.Equal("start Old\nend", value.Value); Assert.True(value.TryReplace(6, 3, "New", out var edit));
            Assert.Equal(source.Replace("Old", "New"), edit.Apply(source));
        }

        [Theory]
        [InlineData("<!DOCTYPE r [<!ENTITY x 'Old'>]><r>&x;</r>")]
        [InlineData("<r><x></r>")]
        public void RejectsInvalidOrExternalXml(string source) => Assert.Throws<XmlException>(() => XmlValueSourceMap.Parse(source));

        [Fact]
        public void CancellationDoesNotPublishMapping() => Assert.Throws<OperationCanceledException>(() => XmlValueSourceMap.Parse("<r/>", new CancellationToken(true)));
    }
}
