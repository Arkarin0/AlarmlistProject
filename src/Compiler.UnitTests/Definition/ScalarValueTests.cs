using System;
using Alarmlist.Definition;
using Xunit;

namespace Alarmlist.Tests
{
    public class ScalarValueTests
    {
        [Theory]
        [InlineData("Int64", "-9223372036854775808", "-9223372036854775808")]
        [InlineData("Int64", " +00042 ", "42")]
        [InlineData("Decimal", "79228162514264337593543950335", "79228162514264337593543950335")]
        [InlineData("Decimal", "1.23000", "1.23")]
        [InlineData("Double", "1e+25", "1.0000000000000001E25")]
        [InlineData("Double", "-0", "0")]
        [InlineData("Double", "1.7976931348623157E308", "1.7976931348623157E308")]
        [InlineData("Double", "4.9406564584124654E-324", "4.9406564584124654E-324")]
        [InlineData("Boolean", " 0 ", "false")]
        [InlineData("Boolean", "1", "true")]
        [InlineData("Enum", "High", "High")]
        [InlineData("Date", "2024-02-29", "2024-02-29")]
        [InlineData("Time", "12:00:00.0000001", "12:00:00.0000001")]
        [InlineData("Timestamp", "2024-02-29T00:00:00.0000000+00:00", "2024-02-29T00:00:00Z")]
        [InlineData("Timestamp", "2024-02-29T00:00:00-05:30", "2024-02-29T00:00:00-05:30")]
        [InlineData("String", "  $(Literal)  ", "  $(Literal)  ")]
        public void CanonicalValuesAreInvariant(string type, string raw, string expected)
        {
            Assert.True(ScalarCodec.TryParse(type, new[] { "High" }, raw, out _, out string actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Int64", "9223372036854775808")]
        [InlineData("Int64", "1.0")]
        [InlineData("Decimal", "1e2")]
        [InlineData("Decimal", "1,000")]
        [InlineData("Double", "1e9999")]
        [InlineData("Double", "NaN")]
        [InlineData("Boolean", "TRUE")]
        [InlineData("Enum", "high")]
        [InlineData("Date", "2023-02-29")]
        [InlineData("Time", "24:00:00")]
        [InlineData("Time", "23:59:60")]
        [InlineData("Time", "12:00:00.")]
        [InlineData("Timestamp", "2024-01-01T00:00:00")]
        [InlineData("Timestamp", "2024-01-01T00:00:00+15:00")]
        [InlineData("Int64", " ")]
        public void InvalidValuesAreRejected(string type, string raw)
            => Assert.False(ScalarCodec.TryParse(type, new[] { "High" }, raw, out _, out _));

        private static AlarmFieldDescriptor Field(string type, bool required = false, string minimum = null, string maximum = null)
            => new AlarmFieldDescriptor("Field", type, required, null, minimum, maximum, "Field", "", false, true, 180, new[] { "High" }, new[] { "High" });

        [Fact]
        public void RequiredBoundsAndStatesAreDistinct()
        {
            Assert.Equal(AlarmFieldValueState.Absent, AlarmFieldValue.Resolve(Field("Int64"), null, null, false, false).State);
            Assert.Equal(AlarmFieldValueState.Invalid, AlarmFieldValue.Resolve(Field("Int64", true), null, null, false, false).State);
            Assert.Equal(AlarmFieldValueState.Invalid, AlarmFieldValue.Resolve(Field("String", true), "$(Empty)", "", false, false).State);
            Assert.Equal(AlarmFieldValueState.Valid, AlarmFieldValue.Resolve(Field("String", true), " ", " ", false, false).State);
            var numeric = Field("Int64", minimum: "10", maximum: "20");
            foreach (string value in new[] { "10", "20" }) Assert.Equal(AlarmFieldValueState.Valid, AlarmFieldValue.Resolve(numeric, value, value, false, false).State);
            foreach (string value in new[] { "9", "21" }) Assert.Equal(AlarmFieldValueState.Invalid, AlarmFieldValue.Resolve(numeric, value, value, false, false).State);
            var deferred = AlarmFieldValue.Resolve(numeric, "$(loc:X)", "$(loc:X)", true, false);
            Assert.Equal(AlarmFieldValueState.Deferred, deferred.State);
            Assert.Null(deferred.Value);
            Assert.Equal("$(loc:X)", deferred.CanonicalText);
        }
    }
}
