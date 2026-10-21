// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.UI;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public partial class EditorTests
    {
        [Theory]
        [InlineData("Conta")]
        [InlineData("conta")]
        [InlineData("CONTA")]
        public void LocalizationSearchRanksKeyPrefixesBeforeSubstringMatches(string query)
        {
            var middle = LocalizationChoice("Electrical.ContactorTerminal");
            var prefix = LocalizationChoice("Contactor.Status");
            var template = new PropertyCompletion("Electrical.ContactorTerminal", new[] { "wire" });
            var unrelated = LocalizationChoice("Motor.Status");
            var choices = new[] { middle, unrelated, template, prefix };

            Assert.Equal(new[] { prefix, middle, template }, ExpressionTextBox.FindMatches(choices, "loc:" + query));
            Assert.Equal(choices, ExpressionTextBox.FindMatches(choices, "loc:"));
            Assert.Equal(new[] { middle, template }, ExpressionTextBox.FindMatches(choices, "loc:terminal"));
            Assert.Equal("$(loc:Electrical.ContactorTerminal)", middle.Insertion);
            Assert.Equal("$(loc:Electrical.ContactorTerminal, wire=\"\")", template.Insertion);
            Assert.Equal("\"\"", template.Insertion.Substring(template.ArgumentOffset, 2));
        }

        [Theory]
        [InlineData("loc:wire")]
        [InlineData("loc:translation")]
        [InlineData("loc:comment")]
        [InlineData("loc:unmatched")]
        [InlineData("LOC:Conta")]
        public void LocalizationSearchDoesNotMatchArgumentNamesDisplayDetailsOrInvalidNamespace(string query)
        {
            var choices = new[] { LocalizationChoice("Electrical.ContactorTerminal"),
                new PropertyCompletion("Electrical.ContactorTerminal", new[] { "wire" }) };
            Assert.Empty(ExpressionTextBox.FindMatches(choices, query));
        }

        [Fact]
        public void LocalizationSearchKeepsOtherNamespacesCaseSensitiveAndPrefixOnly()
        {
            var lookup = new PropertyCompletion("$(path:Electrical.ContactorTerminal)", "value", "lookup");
            var choices = new[] { lookup, LocalizationChoice("Electrical.ContactorTerminal") };
            Assert.Same(lookup, Assert.Single(ExpressionTextBox.FindMatches(choices, "path:Electrical.")));
            Assert.Empty(ExpressionTextBox.FindMatches(choices, "path:Conta"));
            Assert.Empty(ExpressionTextBox.FindMatches(choices, "path:electrical."));
            Assert.Equal(choices, ExpressionTextBox.FindMatches(choices, ""));
        }

        private static PropertyCompletion LocalizationChoice(string key)
            => new PropertyCompletion(new AnalysisTranslation(key, "en", "en", false, true,
                "translation", "comment", null, null), null, false);
    }
}
