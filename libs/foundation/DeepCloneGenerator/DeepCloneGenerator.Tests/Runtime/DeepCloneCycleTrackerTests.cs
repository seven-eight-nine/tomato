using Xunit;

namespace Tomato.DeepCloneGenerator.Tests.Runtime
{
    public class DeepCloneCycleTrackerTests
    {
        [Fact]
        public void TryGetClone_ReturnsNull_WhenOriginalIsNull()
        {
            DeepCloneCycleTracker.Clear();

            var result = DeepCloneCycleTracker.TryGetClone<string>(null!, out var clone);

            Assert.False(result);
            Assert.Null(clone);
        }

        [Fact]
        public void TryGetClone_ReturnsFalse_WhenNotRegistered()
        {
            DeepCloneCycleTracker.Clear();
            var original = new object();

            var result = DeepCloneCycleTracker.TryGetClone(original, out object? clone);

            Assert.False(result);
            Assert.Null(clone);
        }

        [Fact]
        public void TryGetClone_ReturnsTrue_WhenRegistered()
        {
            DeepCloneCycleTracker.Clear();
            var original = new TestClass { Value = 42 };
            var cloned = new TestClass { Value = 42 };

            DeepCloneCycleTracker.Register(original, cloned);
            var result = DeepCloneCycleTracker.TryGetClone(original, out TestClass? retrieved);

            Assert.True(result);
            Assert.Same(cloned, retrieved);
        }

        [Fact]
        public void Clear_RemovesAllRegisteredClones()
        {
            var original = new TestClass { Value = 1 };
            var cloned = new TestClass { Value = 1 };

            DeepCloneCycleTracker.Register(original, cloned);
            DeepCloneCycleTracker.Clear();

            var result = DeepCloneCycleTracker.TryGetClone(original, out TestClass? retrieved);

            Assert.False(result);
            Assert.Null(retrieved);
        }

        [Fact]
        public void Register_DoesNotThrow_WhenOriginalIsNull()
        {
            DeepCloneCycleTracker.Clear();
            var exception = Record.Exception(() => DeepCloneCycleTracker.Register<TestClass>(null!, new TestClass()));
            Assert.Null(exception);
        }

        [Fact]
        public void Register_DoesNotThrow_WhenCloneIsNull()
        {
            DeepCloneCycleTracker.Clear();
            var exception = Record.Exception(() => DeepCloneCycleTracker.Register(new TestClass(), null!));
            Assert.Null(exception);
        }

        [Fact]
        public void UsesReferenceEquality_NotValueEquality()
        {
            DeepCloneCycleTracker.Clear();
            var original1 = new TestClass { Value = 42 };
            var original2 = new TestClass { Value = 42 };
            var cloned1 = new TestClass { Value = 42 };

            DeepCloneCycleTracker.Register(original1, cloned1);

            var result1 = DeepCloneCycleTracker.TryGetClone(original1, out TestClass? retrieved1);
            var result2 = DeepCloneCycleTracker.TryGetClone(original2, out TestClass? retrieved2);

            Assert.True(result1);
            Assert.Same(cloned1, retrieved1);
            Assert.False(result2);
            Assert.Null(retrieved2);
        }

        private class TestClass
        {
            public int Value { get; set; }
        }
    }
}
