// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.Definition;
using Alarmlist.Diagnostics;
using Alarmlist.Localization;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Core.UnitTests.Localization
{
    public class EnumCaptionTests
    {
        private static AlarmFieldDescriptor Field(string key) => new AlarmFieldDescriptor("Priority", "Enum", false, null, null, null,
            "Priority", "", false, true, 180, new[] { "High" }, new[] { "High priority" }, new[] { key });
        private static ResxTextSnapshot Resource(string culture, string value) => new ResxTextSnapshot(
            "<root><data name='Priority.High'><value>" + value + "</value></data></root>", "Captions." + culture + ".resx", 1, culture);

        [Fact]
        public void AllConfiguredCaptionKeysAreCheckedAndDeduplicatedWithoutSelectedValues()
        {
            var resources = new ResourceCatalogue(Array.Empty<ResxTextSnapshot>());
            var diagnostic = Assert.Single(EnumCaptionValidation.Validate(new[] { Field("Priority.High"), Field("Priority.High"), Field(null) }, resources, "de-DE"));
            Assert.Equal("ALM0031", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Null(DiagnosticPolicy.Apply(diagnostic, new DiagnosticOptions(noWarn: new[] { "ALM0031" })));
            Assert.Equal(DiagnosticSeverity.Error, DiagnosticPolicy.Apply(diagnostic, new DiagnosticOptions(treatWarningsAsErrors: true)).Severity);
        }

        [Theory]
        [InlineData("Hoch")]
        [InlineData("$(Parameter) {literal}")]
        [InlineData(" ")]
        public void CaptionsUseParentCultureAndRemainLiteral(string caption)
        {
            var resources = new ResourceCatalogue(new[] { Resource("de-DE", ""), Resource("de", caption) });
            Assert.Empty(EnumCaptionValidation.Validate(new[] { Field("Priority.High") }, resources, "de-DE"));
            Assert.True(resources.TryResolve("Priority.High", "de-DE", out var translation));
            Assert.Equal(caption, translation.Entry.Value);
            Assert.Equal("High", AlarmFieldValue.Resolve(Field("Priority.High"), "High", "High", false, false).Value);
        }

        [Fact]
        public void EmptyOrAmbiguousCaptionsWarnWithoutChoosingAnArbitraryTranslation()
        {
            foreach (var inputs in new[] { new[] { Resource("de", "") }, new[] { Resource("de", "Hoch"), Resource("de", "Anders") } })
            {
                var resources = new ResourceCatalogue(inputs);
                Assert.Single(EnumCaptionValidation.Validate(new[] { Field("Priority.High") }, resources, "de-DE"));
            }
        }
    }
}
