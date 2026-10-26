// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Resources;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Editing
{
    public sealed class CustomDefinitionResourceTests
    {
        [Theory]
        [InlineData("en", "High priority", 2)]
        [InlineData("de", "Hohe Priorität", 1)]
        public void PlaygroundResourcesOpenWithTheNativeResxReader(string culture, string high, int count)
        {
            using (var reader = new ResXResourceReader(Path.Combine(AppContext.BaseDirectory,
                "testassets", "CustomDefinition", "Translations." + culture + ".resx")))
            {
                var values = reader.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value);
                Assert.Equal(count, values.Count);
                Assert.Equal(high, values["Priority.High"]);
                if (culture == "de") Assert.False(values.ContainsKey("Priority.Low"));
                else Assert.Equal("Low priority", values["Priority.Low"]);
            }
        }
    }
}
