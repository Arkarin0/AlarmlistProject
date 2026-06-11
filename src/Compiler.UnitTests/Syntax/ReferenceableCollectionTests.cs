using System.Collections.Generic;
using Alarmlist.Syntax;
using Xunit;

namespace Alarmlist.Syntax.Tests
{
    public class ReferenceableCollectionTests
    {
        [Fact]
        public void ResolveFromReturnsLocalItemsWhenInheritedItemsAreNull()
        {
            var first = new TestItem("A");
            var second = new TestItem("B");
            var collection = new ReferenceableCollection<IReferenceableCollectionItem>
            {
                first,
                second
            };

            var actual = collection.ResolveFrom(null);

            Assert.Equal(new IReferenceableCollectionItem[] { first, second }, actual);
        }

        [Fact]
        public void ResolveFromReturnsInheritedItemsBeforeLocalItems()
        {
            var inherited = new TestItem("Inherited");
            var local = new TestItem("Local");
            var collection = new ReferenceableCollection<IReferenceableCollectionItem>
            {
                local
            };

            var actual = collection.ResolveFrom(new[] { inherited });

            Assert.Equal(new IReferenceableCollectionItem[] { inherited, local }, actual);
        }

        [Fact]
        public void ResolveFromDoesNotMutateInheritedItems()
        {
            var inherited = new List<IReferenceableCollectionItem>
            {
                new TestItem("Inherited")
            };
            var collection = new ReferenceableCollection<IReferenceableCollectionItem>
            {
                new TestItem("Local")
            };

            collection.ResolveFrom(inherited);

            Assert.Single(inherited);
        }

        [Fact]
        public void ResolveFromClearsInheritedItemsWhenClearItemIsFound()
        {
            var inherited = new TestItem("Inherited");
            var local = new TestItem("Local");
            var collection = new ReferenceableCollection<IReferenceableCollectionItem>
            {
                new Clear(),
                local
            };

            var actual = collection.ResolveFrom(new[] { inherited });

            Assert.Equal(new IReferenceableCollectionItem[] { local }, actual);
        }

        [Fact]
        public void ResolveFromClearsItemsAddedBeforeClearItem()
        {
            var inherited = new TestItem("Inherited");
            var beforeClear = new TestItem("BeforeClear");
            var afterClear = new TestItem("AfterClear");
            var collection = new ReferenceableCollection<IReferenceableCollectionItem>
            {
                beforeClear,
                new Clear(),
                afterClear
            };

            var actual = collection.ResolveFrom(new[] { inherited });

            Assert.Equal(new IReferenceableCollectionItem[] { afterClear }, actual);
        }

        [Fact]
        public void ResolveFromSupportsMultipleClearItems()
        {
            var first = new TestItem("First");
            var second = new TestItem("Second");
            var collection = new ReferenceableCollection<IReferenceableCollectionItem>
            {
                first,
                new Clear(),
                second,
                new Clear()
            };

            var actual = collection.ResolveFrom(null);

            Assert.Empty(actual);
        }

        [Fact]
        public void ResolveFromExcludesClearItemsFromResult()
        {
            var collection = new ReferenceableCollection<IReferenceableCollectionItem>
            {
                new Clear()
            };

            var actual = collection.ResolveFrom(null);

            Assert.DoesNotContain(actual, item => item is Clear);
        }

        private sealed class TestItem : IReferenceableCollectionItem
        {
            public TestItem(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }
    }
}
