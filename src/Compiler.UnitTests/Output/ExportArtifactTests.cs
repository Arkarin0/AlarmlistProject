// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Text;
using Alarmlist.Output;
using Xunit;

namespace Alarmlist.Output.Tests
{
    public class ExportArtifactTests
    {
        [Fact]
        public void BinaryContentIsIndependentOfProviderAndConsumerMutations()
        {
            byte[] bytes = { 0, 255, 128 };
            var artifact = new ExportArtifact("bundle/data.bin", bytes);
            bytes[0] = 1;
            byte[] first = artifact.GetContent();
            first[1] = 0;
            Assert.Equal(new byte[] { 0, 255, 128 }, artifact.GetContent());
            Assert.Equal("bundle/data.bin", artifact.RelativePath);
            Assert.Empty(new ExportArtifact("empty", Array.Empty<byte>()).GetContent());
        }

        [Fact]
        public void TextUsesStrictUtf8WithoutBomAndPreservesLineEndings()
        {
            const string text = "Ü 😀\r\nnext\n";
            byte[] bytes = ExportArtifact.FromText("data.txt", text).GetContent();
            Assert.Equal(new UTF8Encoding(false, true).GetBytes(text), bytes);
            Assert.Throws<EncoderFallbackException>(() => ExportArtifact.FromText("bad", "\ud800"));
            Assert.Throws<ArgumentNullException>(() => ExportArtifact.FromText("bad", null));
            Assert.Throws<ArgumentNullException>(() => new ExportArtifact("bad", null));
            Assert.Throws<ArgumentException>(() => new ExportArtifact(" ", Array.Empty<byte>()));
        }

        [Fact]
        public void TextSegmentsPreserveKeyBoundariesAndLiteralTokenLookingText()
        {
            var segments = new List<ExportTextSegment>
            {
                ExportTextSegment.LocalizationKey("YouPlacedAtPositionKeyword"),
                ExportTextSegment.Literal(" 1 "),
                ExportTextSegment.LocalizationKey("atLocationkeyword"),
                ExportTextSegment.Literal(" $(loc:literal) @literal@")
            };
            var text = new ExportText("You placed at position 1 at location $(loc:literal) @literal@", segments);
            segments.Clear();
            Assert.Equal(4, text.Segments.Count);
            Assert.Equal(ExportTextSegmentKind.LocalizationKey, text.Segments[0].Kind);
            Assert.Equal(" 1 ", text.Segments[1].Text);
            Assert.Equal(ExportTextSegmentKind.Literal, text.Segments[3].Kind);
            Assert.Equal("You placed at position 1 at location $(loc:literal) @literal@", text.ResolvedText);
            Assert.Throws<ArgumentException>(() => new ExportText("", new ExportTextSegment[] { null }));
            Assert.Throws<ArgumentException>(() => ExportTextSegment.LocalizationKey(""));
            Assert.Throws<ArgumentNullException>(() => ExportTextSegment.Literal(null));
        }
    }
}
