using System.Collections.Generic;
using System.Linq;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline.Query;
using Xunit;

namespace Tomato.SystemPipeline.Tests
{
    public class QueryTests
    {
        private class MockArena : IEntityArena
        {
            public bool IsValid(int index, int generation) => true;
        }

        private class TestEntityRegistry : IEntityRegistry
        {
            public IReadOnlyList<VoidHandle> GetAllEntities()
            {
                return new List<VoidHandle>();
            }

            public IReadOnlyList<VoidHandle> GetEntitiesOfType<TArena>() where TArena : class
            {
                return new List<VoidHandle>();
            }
        }

        [Fact]
        public void ActiveEntityQuery_FiltersInvalidHandles()
        {
            // Arrange
            var registry = new TestEntityRegistry();
            var arena = new MockArena();
            var validHandle = new VoidHandle(arena, 0, 0);
            var invalidHandle = VoidHandle.Invalid;
            var entities = new List<VoidHandle> { validHandle, invalidHandle, validHandle };

            // Act
            var result = ActiveEntityQuery.Instance.Filter(registry, entities).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, h => Assert.True(h.IsValid));
        }

        [Fact]
        public void ActiveEntityQuery_IsSingleton()
        {
            // Assert
            Assert.Same(ActiveEntityQuery.Instance, ActiveEntityQuery.Instance);
        }

        [Fact]
        public void HasComponentQuery_FiltersEntitiesWithComponent()
        {
            // Arrange
            var registry = new TestEntityRegistry();
            var arena = new MockArena();
            var handle1 = new VoidHandle(arena, 0, 0);
            var handle2 = new VoidHandle(arena, 1, 0);
            var handle3 = new VoidHandle(arena, 2, 0);
            var entities = new List<VoidHandle> { handle1, handle2, handle3 };

            // Only handle1 and handle3 have the component (index 0 and 2)
            var query = new HasComponentQuery<int>(h => h.Index == 0 || h.Index == 2);

            // Act
            var result = query.Filter(registry, entities).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(0, result[0].Index);
            Assert.Equal(2, result[1].Index);
        }

        [Fact]
        public void CompositeQuery_ChainsQueries()
        {
            // Arrange
            var registry = new TestEntityRegistry();
            var arena = new MockArena();
            var validHandle = new VoidHandle(arena, 0, 0);
            var invalidHandle = VoidHandle.Invalid;
            var entities = new List<VoidHandle> { validHandle, invalidHandle };

            var compositeQuery = new CompositeQuery(
                ActiveEntityQuery.Instance,
                new HasComponentQuery<int>(_ => true)
            );

            // Act
            var result = compositeQuery.Filter(registry, entities).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(0, result[0].Index);
        }

        [Fact]
        public void CompositeQuery_EmptyQueries_ReturnsAll()
        {
            // Arrange
            var registry = new TestEntityRegistry();
            var arena = new MockArena();
            var handle1 = new VoidHandle(arena, 0, 0);
            var handle2 = new VoidHandle(arena, 1, 0);
            var entities = new List<VoidHandle> { handle1, handle2 };

            var compositeQuery = new CompositeQuery();

            // Act
            var result = compositeQuery.Filter(registry, entities).ToList();

            // Assert
            Assert.Equal(2, result.Count);
        }
    }
}
