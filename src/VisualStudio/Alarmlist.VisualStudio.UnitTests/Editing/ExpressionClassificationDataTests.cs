// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Reflection;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class ExpressionClassificationDataTests
    {
        private static AnalysisClassificationTarget Target(string text = "$(P)", int version = 4, string id = "view")
            => new AnalysisClassificationTarget(id, "Almx", new AnalysisInput(text, "input.almx", version));
        private static AnalysisClassificationSpan Span(int start = 2, int length = 1, int expressionStart = 0, int expressionLength = 4, string kind = "Parameter")
            => new AnalysisClassificationSpan(kind, "IdentifierToken", start, length, expressionStart, expressionLength, null, Array.Empty<AnalysisClassificationRange>());
        private static AnalysisClassificationResult Result(AnalysisClassificationTarget target, AnalysisClassificationSpan[] spans = null,
            int contract = 10, bool lexical = true, string fingerprint = null, bool available = true)
            => new AnalysisClassificationResult(contract, fingerprint ?? AlarmDefinition.ToolingFingerprint, target,
                "snapshot", "Plant", "Debug", "AnyCPU", "en", spans ?? new[] { Span() }, Array.Empty<AnalysisClassificationIssue>(), available, lexical, null);

        [Fact]
        public void PresentationAcceptsOnlyAnExactTargetAndCompatibleLexicalResult()
        {
            var target = Target(); var result = Result(target);
            Assert.NotNull(ExpressionClassificationData.Create(target, result));
            Assert.Null(ExpressionClassificationData.Create(Target(version: 5), result));
            Assert.Null(ExpressionClassificationData.Create(Target(text: "$(Q)"), result));
            Assert.Null(ExpressionClassificationData.Create(Target(id: "other"), result));
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, contract: 9)));
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, lexical: false)));
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, fingerprint: "different")));
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, available: false)));
            Assert.Null(ExpressionClassificationData.Create(target, null));
        }

        [Theory]
        [InlineData(-1, 1, 0, 4)]
        [InlineData(2, 0, 0, 4)]
        [InlineData(2, 3, 0, 4)]
        [InlineData(2, 1, -1, 4)]
        [InlineData(2, 1, 3, 1)]
        [InlineData(2, 1, 0, 2)]
        [InlineData(2, 1, 0, int.MaxValue)]
        public void InvalidOrOverflowingCompilerRangesDoNotEnterTheEditor(int start, int length, int expressionStart, int expressionLength)
        {
            var target = Target();
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, new[] { Span(start, length, expressionStart, expressionLength) })));
        }

        [Fact]
        public void UnknownOverlappingAndUnorderedSpansAreRejected()
        {
            var target = Target();
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, new[] { Span(kind: "Unknown") })));
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, new[] { Span(), Span() })));
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, new[] { Span(), Span(start: 0) })));
            Assert.Null(ExpressionClassificationData.Create(target, Result(target, new AnalysisClassificationSpan[] { null })));
        }

        [Fact]
        public void ProjectionUsesOnlyCompilerRangesAndBoundsQueriesToTheViewport()
        {
            // Deliberately contains no expression syntax: the host must consume
            // compiler ranges rather than discover or reinterpret tokens itself.
            var target = Target(new string('x', 20000));
            var spans = Enumerable.Range(0, 10000).Select(index => Span(index * 2, 1, index * 2, 1)).ToArray();
            var data = ExpressionClassificationData.Create(target, Result(target, spans)); Assert.NotNull(data);
            Assert.Equal(new[] { 100, 102, 104 }, data.Intersecting(100, 5).Select(span => span.Start));
            Assert.Equal(19998, Assert.Single(data.Intersecting(19998, 2)).Start);
            Assert.Empty(data.Intersecting(19999, 1)); Assert.Empty(data.Intersecting(-1, 1));
            Assert.Empty(data.Intersecting(int.MaxValue, 2)); Assert.Empty(data.Intersecting(0, 0));
        }

        [Fact]
        public void FormattingRolesAreDistinctUserVisibleAndInheritNativeColors()
        {
            var definitions = typeof(ExpressionClassifications).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(ClassificationTypeDefinition)).ToArray();
            Assert.Equal(9, definitions.Length);
            var names = definitions.Select(field => field.GetCustomAttribute<NameAttribute>().Name).ToArray();
            Assert.Equal(9, names.Distinct().Count());
            var formats = new[] { typeof(ParameterExpressionFormat), typeof(LocalizationExpressionFormat), typeof(ItemExpressionFormat),
                typeof(LookupExpressionFormat), typeof(KeywordExpressionFormat), typeof(PunctuationExpressionFormat),
                typeof(ArgumentExpressionFormat), typeof(LiteralExpressionFormat), typeof(InvalidExpressionFormat) };
            Assert.Equal(9, formats.Length);
            foreach (var format in formats)
            {
                Assert.Contains(format.GetCustomAttribute<NameAttribute>().Name, names);
                Assert.True(format.GetCustomAttribute<UserVisibleAttribute>().UserVisible);
                var instance = (ExpressionFormat)Activator.CreateInstance(format);
                Assert.Null(instance.ForegroundColor); Assert.Null(instance.BackgroundColor);
                Assert.Null(instance.ForegroundBrush); Assert.Null(instance.BackgroundBrush);
            }
            Assert.True(XmlExpressionClassification.Supports("input.almx")); Assert.True(XmlExpressionClassification.Supports("Labels.EN.RESX"));
            Assert.False(XmlExpressionClassification.Supports("Input.almproj")); Assert.False(XmlExpressionClassification.Supports("Hmi.almlookup"));
            Assert.True(XmlExpressionClassificationProvider.SupportsTagType(typeof(ClassificationTag)));
            Assert.True(XmlExpressionClassificationProvider.SupportsTagType(typeof(IClassificationTag)));
            Assert.True(typeof(ITagger<IClassificationTag>).IsAssignableFrom(typeof(XmlExpressionClassification)));
            Assert.False(XmlExpressionClassificationProvider.SupportsTagType(typeof(ErrorTag)));
        }
    }
}
