// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Evaluation;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Evaluation
{
    public sealed class ExpressionSyntaxTests
    {
        [Theory]
        [InlineData("$(P)", (int)ExpressionSyntaxKind.ParameterReference)]
        [InlineData("$(loc:Caption.Key-1)", (int)ExpressionSyntaxKind.LocalizationReference)]
        [InlineData("$(item:Count)", (int)ExpressionSyntaxKind.ItemReference)]
        [InlineData("$(path:Ärea.Pump)", (int)ExpressionSyntaxKind.PathReference)]
        [InlineData("$(property:Ärea.Pump#État)", (int)ExpressionSyntaxKind.PropertyReference)]
        [InlineData("$(button:Ärea.Pump#Start)", (int)ExpressionSyntaxKind.ButtonReference)]
        [InlineData("$(meta:Ärea.Pump#État#Unité)", (int)ExpressionSyntaxKind.MetadataReference)]
        [InlineData("$(list:Modes#Auto)", (int)ExpressionSyntaxKind.ListReference)]
        public void EveryReferenceKindOwnsItsAuthoredTokensAndExactKeySpan(string raw, int kind)
        {
            var syntax = ExpressionSyntax.Parse(raw, allowItemReferences: true);
            Assert.False(syntax.HasErrors);
            var reference = Assert.IsType<ExpressionReferenceSyntax>(Assert.Single(syntax.Parts));
            Assert.Equal((ExpressionSyntaxKind)kind, reference.Kind); Assert.Equal(raw, reference.Raw);
            Assert.Equal(reference.Key, raw.Substring(reference.KeyStart, reference.KeyLength));
            Assert.Equal(ExpressionSyntaxKind.DollarToken, syntax.Tokens.First().Kind);
            Assert.Equal(ExpressionSyntaxKind.CloseParenToken, syntax.Tokens.Last().Kind);
            AssertLossless(syntax);
        }

        [Fact]
        public void NamedArgumentsRetainNestedReferenceAndQuotedLiteralBoundaries()
        {
            const string raw = "prefix 🙂 $(loc:Key,\r\n Position = $(P), Literal=\"$(NotAReference)\\n\\\"\\\\\") tail";
            var syntax = ExpressionSyntax.Parse(raw); Assert.False(syntax.HasErrors);
            var reference = Assert.Single(syntax.Parts.OfType<ExpressionReferenceSyntax>());
            Assert.Equal(ExpressionSyntaxKind.LocalizationReference, reference.Kind);
            Assert.Contains(reference.Tokens, token => token.Kind == ExpressionSyntaxKind.LocalizationKeyword && token.Text == "loc");
            Assert.Contains(reference.Tokens, token => token.Kind == ExpressionSyntaxKind.WhitespaceToken && token.Text == "\r\n ");
            var position = reference.Arguments["Position"];
            Assert.Equal("Position", raw.Substring(position.NameStart, position.NameLength));
            Assert.Equal(ExpressionSyntaxKind.ParameterReference, position.Reference.Kind);
            Assert.Equal("P", raw.Substring(position.Reference.KeyStart, position.Reference.KeyLength));
            Assert.Null(reference.Arguments["Literal"].Reference);
            Assert.Equal("$(NotAReference)\n\"\\", reference.Arguments["Literal"].Literal);
            Assert.Single(reference.Arguments["Literal"].Tokens.Where(token => token.Kind == ExpressionSyntaxKind.StringLiteralToken));
            AssertLossless(syntax);
        }

        [Theory]
        [InlineData("$(")]
        [InlineData("$()")]
        [InlineData("$(loc:)")]
        [InlineData("$(loc:K, X=)")]
        [InlineData("$(loc:K, X=\"unfinished")]
        public void IncompleteInputHasExplicitMissingTokensAndNeverEntersEvaluation(string raw)
        {
            var syntax = ExpressionSyntax.Parse(raw);
            Assert.True(syntax.HasErrors); Assert.NotEmpty(syntax.Diagnostics);
            Assert.Contains(syntax.Tokens, token => token.IsMissing && token.Length == 0);
            Assert.Null(PropertyExpression.Parse(raw)); AssertLossless(syntax);
        }

        [Theory]
        [InlineData("$(P )")]
        [InlineData("$(P, X=\"a\")")]
        [InlineData("$(parameter:P)")]
        [InlineData("$(LOC:Key)")]
        [InlineData("$(loc:K, X=\"\\q\")")]
        [InlineData("$(loc:K, X=\"one\", X=\"two\")")]
        [InlineData("$(loc:K, X=$(loc:Nested))")]
        [InlineData("$(loc:K, X=$(P, Y=$(Deep)))")]
        [InlineData("$(loc:K, X=unquoted)")]
        [InlineData("$(path:one#two)")]
        [InlineData("$(meta:one#two)")]
        [InlineData("$(item:Count)")]
        [InlineData("$(Résumé)")]
        [InlineData("$(loc:Résumé)")]
        public void RecoveryPreservesMalformedTextWithoutExpandingTheGrammar(string raw)
        {
            var syntax = ExpressionSyntax.Parse(raw);
            Assert.True(syntax.HasErrors); Assert.NotEmpty(syntax.Diagnostics);
            Assert.Null(PropertyExpression.Parse(raw)); AssertLossless(syntax);
        }

        [Fact]
        public void PlaceholdersAndExactlyOnceEscapesKeepTheirSourceAndEvaluationFormsSeparate()
        {
            const string raw = "{{Literal}} {Position} $$(loc:Ignored, X=$(P)) $$ $$$$(Q)";
            var syntax = ExpressionSyntax.Parse(raw, placeholders: true);
            Assert.False(syntax.HasErrors); Assert.Empty(syntax.Parts.OfType<ExpressionReferenceSyntax>());
            var placeholder = Assert.Single(syntax.Parts.OfType<ExpressionPlaceholderSyntax>());
            Assert.Equal("Position", placeholder.Name);
            Assert.Equal(placeholder.Name, raw.Substring(placeholder.NameStart, placeholder.Name.Length));
            Assert.Contains(syntax.Tokens, token => token.Kind == ExpressionSyntaxKind.EscapedTextToken && token.Text == "$$(loc:Ignored, X=$(P))");
            Assert.Equal("{Literal} 1 $(loc:Ignored, X=$(P)) $ $$(Q)", PropertyExpression.Parse(raw, placeholders: true).Evaluate(_ => throw new Exception("Escaped reference executed."), _ => "1"));
            AssertLossless(syntax);
            Assert.True(ExpressionSyntax.Parse("{Position", placeholders: true).HasErrors);
            Assert.True(ExpressionSyntax.Parse("}", placeholders: true).HasErrors);
            Assert.False(ExpressionSyntax.Parse("{Position").HasErrors);
        }

        [Fact]
        public void EveryTypingPrefixAndMalformedDelimiterCombinationIsLossless()
        {
            const string complete = "🙂 $(loc:Caption, Value=$(item:Count), Literal=\"a\\n$$(P)\") {Value} {{x}}";
            for (int length = 0; length <= complete.Length; length++)
                AssertLossless(ExpressionSyntax.Parse(complete.Substring(0, length), placeholders: true, allowItemReferences: true));
            var random = new Random(14);
            const string alphabet = "$(){}#,:=\\\" P\t\nÄ";
            for (int sample = 0; sample < 300; sample++)
            {
                string raw = new string(Enumerable.Range(0, 60).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
                AssertLossless(ExpressionSyntax.Parse(raw, placeholders: sample % 2 == 0, allowItemReferences: sample % 3 == 0));
            }
        }

        [Fact]
        public void FrozenSyntaxDoesNotExposeMutablePartsTokensArgumentsOrDiagnostics()
        {
            var syntax = ExpressionSyntax.Parse("$(loc:K, X=$(P))");
            var reference = Assert.IsType<ExpressionReferenceSyntax>(Assert.Single(syntax.Parts));
            Assert.Throws<NotSupportedException>(() => ((IList<ExpressionPartSyntax>)syntax.Parts).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ExpressionSyntaxToken>)reference.Tokens).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ExpressionArgumentSyntax>)reference.ArgumentList).Clear());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ExpressionArgumentSyntax>)reference.Arguments).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ExpressionSyntaxDiagnostic>)syntax.Diagnostics).Clear());
            Assert.Equal(string.Empty, default(ExpressionSyntaxToken).Text);
        }

        [Fact]
        public void LargeArgumentSetsPreserveEveryNameAndKeepDuplicatesNonExecutable()
        {
            string arguments = string.Join(", ", Enumerable.Range(0, 1024).Select(index => "A" + index + "=\"" + index + "\""));
            string raw = "$(loc:K, " + arguments + ")";
            var syntax = ExpressionSyntax.Parse(raw); Assert.False(syntax.HasErrors); AssertLossless(syntax);
            var reference = Assert.IsType<ExpressionReferenceSyntax>(Assert.Single(syntax.Parts));
            Assert.Equal(1024, reference.Arguments.Count);
            Assert.Equal("1023", reference.Arguments["A1023"].Literal);
            var duplicate = ExpressionSyntax.Parse("$(loc:K, " + arguments + ", A1023=\"duplicate\")");
            Assert.True(duplicate.HasErrors); AssertLossless(duplicate);
            Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Message == "Duplicate argument name.");
            Assert.Null(PropertyExpression.Parse(duplicate.Raw));
        }

        [Fact]
        public void TypedSpansMapThroughEntitiesCdataAndUnicodeWithoutDiscardingMarkup()
        {
            const string xml = "<r>&#x1F600; $(loc:Al&#97;rm) <![CDATA[$(meta:Ärea#État#Unité)]]> $(P<!--keep-->aram)</r>";
            var map = XmlValueSourceMap.Parse(xml); var value = map.Values[map.Document.Root];
            var syntax = ExpressionSyntax.Parse(value.Value); Assert.False(syntax.HasErrors); AssertLossless(syntax);
            var references = syntax.Parts.OfType<ExpressionReferenceSyntax>().ToArray();
            Assert.Equal(3, references.Length);
            Assert.Equal(3, references[0].Start); // Two UTF-16 units for the emoji, then a space.
            Assert.True(value.TryReplace(references[0].KeyStart, references[0].KeyLength, "Next", out var resource));
            Assert.Contains("$(loc:Next)", resource.Apply(xml));
            Assert.True(value.TryReplace(references[1].KeyStart, references[1].KeyLength, "Other#État#Unité", out var lookup));
            Assert.Contains("<![CDATA[$(meta:Other#État#Unité)]]>", lookup.Apply(xml));
            Assert.Equal(2, value.SourceRanges(references[2].KeyStart, references[2].KeyLength).Count);
            Assert.False(value.TryReplace(references[2].KeyStart, references[2].KeyLength, "New", out _));
        }

        [Fact]
        public void RecoveredNestedReferencesAreNotPublishedAsProvenSemanticUses()
        {
            const string raw = "$(loc:K, X=$(P),)";
            var recovered = ExpressionSyntax.Parse(raw);
            Assert.Equal("P", Assert.Single(recovered.Parts.OfType<ExpressionReferenceSyntax>()).Arguments["X"].Reference.Key);
            Assert.True(recovered.HasErrors); Assert.Null(PropertyExpression.Parse(raw));
            var input = new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName>"
                + "<Properties><Property Name='P' Value='1'/></Properties><Code>$(P)</Code><Name>" + raw + "</Name></Alarm></Alarmlist>", "input.almx", 1);
            var result = ReferenceCapture.Capture(new[] { input }, CompilationContext.Empty);
            var parameter = result.Symbols.Single(symbol => symbol.Kind == "Parameter");
            Assert.Single(result.Occurrences.Where(use => use.Family == parameter.Family && use.Role == "Expression"));
            Assert.Contains(result.Coverage, finding => finding.Contains("Invalid expression"));
            var compilation = new AlarmCompiler().CompileSnapshots(new[] { input });
            Assert.False(compilation.Success); Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Id == "ALM0006");
        }

        private static void AssertLossless(ExpressionSyntax syntax)
        {
            int position = 0;
            foreach (var token in syntax.Tokens)
            {
                Assert.Equal(position, token.Start);
                Assert.Equal(syntax.Raw.Substring(token.Start, token.Length), token.Text);
                if (token.IsMissing) Assert.Equal(0, token.Length);
                position += token.Length;
            }
            Assert.Equal(syntax.Raw.Length, position);
            Assert.Equal(syntax.Raw, string.Concat(syntax.Tokens.Select(token => token.Text)));
        }
    }
}
