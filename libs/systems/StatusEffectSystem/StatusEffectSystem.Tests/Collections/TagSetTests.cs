using Xunit;

namespace Tomato.StatusEffectSystem.Tests
{
    public class TagSetTests
    {
        [Fact]
        public void Empty_ShouldContainNoTags()
        {
            var set = TagSet.Empty;

            Assert.True(set.IsEmpty);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void With_ShouldAddTag()
        {
            var tag = new TagId(5);
            var set = TagSet.Empty.With(tag);

            Assert.True(set.Contains(tag));
            Assert.Equal(1, set.Count);
        }

        [Fact]
        public void Without_ShouldRemoveTag()
        {
            var tag = new TagId(5);
            var set = TagSet.Empty.With(tag).Without(tag);

            Assert.False(set.Contains(tag));
            Assert.True(set.IsEmpty);
        }

        [Fact]
        public void ContainsAny_WithOverlap_ShouldReturnTrue()
        {
            var tag1 = new TagId(1);
            var tag2 = new TagId(2);
            var tag3 = new TagId(3);

            var set1 = TagSet.Empty.With(tag1).With(tag2);
            var set2 = TagSet.Empty.With(tag2).With(tag3);

            Assert.True(set1.ContainsAny(set2));
        }

        [Fact]
        public void ContainsAny_NoOverlap_ShouldReturnFalse()
        {
            var tag1 = new TagId(1);
            var tag2 = new TagId(2);

            var set1 = TagSet.Empty.With(tag1);
            var set2 = TagSet.Empty.With(tag2);

            Assert.False(set1.ContainsAny(set2));
        }

        [Fact]
        public void ContainsAll_WithSubset_ShouldReturnTrue()
        {
            var tag1 = new TagId(1);
            var tag2 = new TagId(2);
            var tag3 = new TagId(3);

            var set1 = TagSet.Empty.With(tag1).With(tag2).With(tag3);
            var set2 = TagSet.Empty.With(tag1).With(tag2);

            Assert.True(set1.ContainsAll(set2));
        }

        [Fact]
        public void Union_ShouldCombineTags()
        {
            var tag1 = new TagId(1);
            var tag2 = new TagId(2);

            var set1 = TagSet.Empty.With(tag1);
            var set2 = TagSet.Empty.With(tag2);
            var union = set1 | set2;

            Assert.True(union.Contains(tag1));
            Assert.True(union.Contains(tag2));
            Assert.Equal(2, union.Count);
        }

        [Fact]
        public void HighTagId_ShouldWork()
        {
            // Test tag in second ulong (64-127)
            var tag = new TagId(100);
            var set = TagSet.Empty.With(tag);

            Assert.True(set.Contains(tag));
            Assert.Equal(1, set.Count);
        }
    }
}
