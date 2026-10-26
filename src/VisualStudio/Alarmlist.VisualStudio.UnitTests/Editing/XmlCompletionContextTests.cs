// Created/modified by Arkarin0 under one ore more license(s).

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
            Assert.True(XmlCompletionContext.TryGetRootSpan(text, caret, out int start, out int length));
            Assert.Equal(expectedStart, start);
            Assert.Equal(expectedLength, length);
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
            Assert.False(XmlCompletionContext.TryGetRootSpan(text, caret, out _, out _));
        }
    }
}
