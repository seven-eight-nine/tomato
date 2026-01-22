using Xunit;

namespace Tomato.StatusEffectSystem.Tests
{
    public class EffectIdTests
    {
        [Fact]
        public void Invalid_ShouldHaveNegativeValue()
        {
            Assert.Equal(-1, EffectId.Invalid.Value);
            Assert.False(EffectId.Invalid.IsValid);
        }

        [Fact]
        public void Equals_SameValue_ShouldBeTrue()
        {
            var id1 = new EffectId(1);
            var id2 = new EffectId(1);

            Assert.True(id1.Equals(id2));
            Assert.True(id1 == id2);
        }

        [Fact]
        public void Equals_DifferentValue_ShouldBeFalse()
        {
            var id1 = new EffectId(1);
            var id2 = new EffectId(2);

            Assert.False(id1.Equals(id2));
            Assert.True(id1 != id2);
        }
    }
}
