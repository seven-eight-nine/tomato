using Xunit;

namespace Tomato.StatusEffectSystem.Tests
{
    public class TickDurationTests
    {
        [Fact]
        public void Zero_ShouldHaveZeroValue()
        {
            Assert.Equal(0, TickDuration.Zero.Value);
            Assert.True(TickDuration.Zero.IsZero);
            Assert.False(TickDuration.Zero.IsInfinite);
        }

        [Fact]
        public void Infinite_ShouldBeMaxValue()
        {
            Assert.Equal(int.MaxValue, TickDuration.Infinite.Value);
            Assert.True(TickDuration.Infinite.IsInfinite);
            Assert.False(TickDuration.Infinite.IsZero);
        }

        [Fact]
        public void Addition_ShouldAddValues()
        {
            var a = new TickDuration(100);
            var b = new TickDuration(50);
            var result = a + b;

            Assert.Equal(150, result.Value);
        }

        [Fact]
        public void Addition_WithInfinite_ShouldReturnInfinite()
        {
            var a = new TickDuration(100);
            var result = a + TickDuration.Infinite;

            Assert.True(result.IsInfinite);
        }

        [Fact]
        public void Subtraction_ShouldSubtractValues()
        {
            var a = new TickDuration(100);
            var b = new TickDuration(30);
            var result = a - b;

            Assert.Equal(70, result.Value);
        }

        [Fact]
        public void Subtraction_BelowZero_ShouldClampToZero()
        {
            var a = new TickDuration(30);
            var b = new TickDuration(100);
            var result = a - b;

            Assert.Equal(0, result.Value);
            Assert.True(result.IsZero);
        }

        [Fact]
        public void NegativeInput_ShouldClampToZero()
        {
            var duration = new TickDuration(-100);

            Assert.Equal(0, duration.Value);
        }
    }
}
