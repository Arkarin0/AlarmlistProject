using Xunit;
using Alarmlist.Syntax;
// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alarmlist.Text;
using Moq;

namespace Alarmlist.Syntax.Tests
{
    public class SyntaxTreeGeneratorTests
    {
        public static Core.UnitTests.Fakes.SyntaxTreeGeneratorMock CreateInstance()
        {
            return new Core.UnitTests.Fakes.SyntaxTreeGeneratorMock();
        }

        public static SourceText CreateSourceText()
        {
            var mock = new Mock<SourceText>();
            return mock.Object;
        }

        [Fact()]
        public void CtorTest()
        {

        }

        [Fact()]
        public void UpdateDoesNotReturnNullAfterInstanceCreation()
        {
            var generator = new SyntaxTreeGenerator();
            var result = generator.Update();
            Assert.NotNull(result);
        }


        [Fact()]
        public void AddSourceTextTest()
        {
            var obj = CreateInstance();
            var expected = CreateSourceText();

            obj.AddSourceText(expected);
            var actual = obj.GetSourceTexts();

            Assert.Contains(expected, actual);
        }

        [Fact()]
        public void AddSourceTextsTest()
        {
            var obj = CreateInstance();
            int count = 5;

            var expected = new List<SourceText>();

            for (int i = 0; i < count; i++)
            {
                expected.Add(CreateSourceText());
            }

            obj.AddSourceTexts(expected);
            var actual = obj.GetSourceTexts();

            Assert.All(expected, item => Assert.Contains(item, actual));
        }

        [Fact()]
        public void ClearSourceTextsTest()
        {
            var obj = CreateInstance();
            var expected = CreateSourceText();
            obj.AddSourceText(expected);

            obj.ClearSourceTexts();
            var actual = obj.GetSourceTexts();

            Assert.Empty(actual);
        }

        [Fact()]
        public void RemoveSourceTextTest()
        {
            var obj = CreateInstance();
            var expected = CreateSourceText();
            obj.AddSourceText(expected);

            var actual = obj.GetSourceTexts();
            var isInCollection = actual.Contains(expected);
            obj.RemoveSourceText(expected);
            var isnotinCollection = actual.Contains(expected);

            Assert.True(isInCollection, "The expected source text is not in the collection.");
            Assert.False(isnotinCollection, "The expected source text is still in the collection after removal.");
        }
    }
}
