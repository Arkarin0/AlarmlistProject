// Created/modified by Arkarin0 under one ore more license(s).

using System.Linq;
using Alarmlist.Evaluation;
using Xunit;

namespace Alarmlist.Core.UnitTests.Text
{
    public class ExpressionSourceSpanTests
    {
        [Fact]
        public void ParserOwnsExactNestedAndArgumentSpansIgnoringEscapesAndLiteralArguments()
        {
            const string raw = "$$(Ignored) $(loc:Key, Position=$(P), Literal=\"$(NotAReference)\") $(path:Plant.Pump)";
            var expression = PropertyExpression.Parse(raw); var tokens = expression.Tokens.ToArray();
            Assert.Equal(2, tokens.Length);
            foreach (var token in tokens)
            {
                Assert.Equal(token.Raw, raw.Substring(token.Start, token.Raw.Length));
                Assert.Equal(token.Key, raw.Substring(token.KeyStart, token.KeyLength));
            }
            var parameter = tokens[0].Arguments["Position"].Token;
            Assert.Equal("P", raw.Substring(parameter.KeyStart, parameter.KeyLength));
            Assert.Equal("Position", raw.Substring(tokens[0].Arguments["Position"].NameStart, tokens[0].Arguments["Position"].NameLength));
            Assert.Null(tokens[0].Arguments["Literal"].Token);
        }
    }
}
