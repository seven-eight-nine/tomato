using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Query
{
    /// <summary>
    /// QueryExecutor テスト
    /// </summary>
    public class QueryExecutorTests
    {
        [Fact]
        public void Query_NoArenas_ShouldReturnEmpty()
        {
            var executor = new QueryExecutor();
            var result = executor.Query().Execute();
            Assert.Empty(result.Handles);
        }

        [Fact]
        public void Register_ShouldAddArena()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();

            executor.Register(arena);

            Assert.Equal(1, executor.ArenaCount);
        }

        [Fact]
        public void Unregister_ShouldRemoveArena()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();

            executor.Register(arena);
            var removed = executor.Unregister(arena);

            Assert.True(removed);
            Assert.Equal(0, executor.ArenaCount);
        }

        [Fact]
        public void Clear_ShouldRemoveAllArenas()
        {
            var executor = new QueryExecutor();
            executor.Register(new MockQueryableArena());
            executor.Register(new MockQueryableArena());

            executor.Clear();

            Assert.Equal(0, executor.ArenaCount);
        }

        [Fact]
        public void Query_AllEntities_ShouldReturnAll()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));
            arena.AddEntity(new MockAnyHandle(2, true));
            arena.AddEntity(new MockAnyHandle(3, true));

            executor.Register(arena);

            var result = executor.Query().Execute();
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Query_WhereAlive_ShouldFilterInvalid()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));
            arena.AddEntity(new MockAnyHandle(2, false)); // invalid
            arena.AddEntity(new MockAnyHandle(3, true));

            executor.Register(arena);

            var result = executor.Query().WhereAlive().Execute();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Query_OfType_ShouldFilterByArenaType()
        {
            var executor = new QueryExecutor();
            var arena1 = new MockQueryableArena { Name = "Arena1" };
            var arena2 = new MockQueryableArena2 { Name = "Arena2" };

            arena1.AddEntity(new MockAnyHandle(1, true));
            arena2.AddEntity(new MockAnyHandle(2, true));

            executor.Register(arena1);
            executor.Register(arena2);

            var result = executor.Query().OfType<MockQueryableArena>().Execute();
            Assert.Single(result.Handles);
        }

        [Fact]
        public void Query_WithPredicate_ShouldFilter()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));
            arena.AddEntity(new MockAnyHandle(2, true));
            arena.AddEntity(new MockAnyHandle(3, true));

            executor.Register(arena);

            // Only entities with even index
            var result = executor.Query()
                .Where((handle, a, idx) => idx % 2 == 1)
                .Execute();

            Assert.Single(result.Handles);
        }

        [Fact]
        public void Query_MultipleFilters_ShouldApplyAll()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));
            arena.AddEntity(new MockAnyHandle(2, false));
            arena.AddEntity(new MockAnyHandle(3, true));

            executor.Register(arena);

            var result = executor.Query()
                .WhereAlive()
                .Where((h, a, idx) => idx > 0)
                .Execute();

            Assert.Single(result.Handles);
        }

        [Fact]
        public void Query_Enumerate_ShouldReturnHandles()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));
            arena.AddEntity(new MockAnyHandle(2, true));

            executor.Register(arena);

            var handles = executor.Query().Enumerate().ToList();
            Assert.Equal(2, handles.Count);
        }

        [Fact]
        public void Query_First_ShouldReturnFirstOrNull()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));

            executor.Register(arena);

            var first = executor.Query().First();
            Assert.NotNull(first);
        }

        [Fact]
        public void Query_First_WhenEmpty_ShouldReturnNull()
        {
            var executor = new QueryExecutor();
            var first = executor.Query().First();
            Assert.Null(first);
        }

        [Fact]
        public void Query_Count_ShouldReturnCount()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));
            arena.AddEntity(new MockAnyHandle(2, true));
            arena.AddEntity(new MockAnyHandle(3, true));

            executor.Register(arena);

            Assert.Equal(3, executor.Query().Count());
        }

        [Fact]
        public void Query_Any_ShouldReturnTrueIfExists()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));

            executor.Register(arena);

            Assert.True(executor.Query().Any());
        }

        [Fact]
        public void Query_Any_WhenEmpty_ShouldReturnFalse()
        {
            var executor = new QueryExecutor();
            Assert.False(executor.Query().Any());
        }

        [Fact]
        public void Query_MultipleArenas_ShouldQueryAll()
        {
            var executor = new QueryExecutor();
            var arena1 = new MockQueryableArena();
            var arena2 = new MockQueryableArena();

            arena1.AddEntity(new MockAnyHandle(1, true));
            arena1.AddEntity(new MockAnyHandle(2, true));
            arena2.AddEntity(new MockAnyHandle(3, true));

            executor.Register(arena1);
            executor.Register(arena2);

            var result = executor.Query().Execute();
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void QueryResult_IsEmpty_ShouldReturnTrueWhenEmpty()
        {
            var executor = new QueryExecutor();
            var result = executor.Query().Execute();
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void QueryResult_IsEmpty_ShouldReturnFalseWhenNotEmpty()
        {
            var executor = new QueryExecutor();
            var arena = new MockQueryableArena();
            arena.AddEntity(new MockAnyHandle(1, true));
            executor.Register(arena);

            var result = executor.Query().Execute();
            Assert.False(result.IsEmpty);
        }

        #region Mock Classes

        private class MockAnyHandle
        {
            public int Id { get; }
            public bool Valid { get; }

            public MockAnyHandle(int id, bool valid)
            {
                Id = id;
                Valid = valid;
            }
        }

        private class MockQueryableArena : IQueryableArena
        {
            private readonly List<MockAnyHandle> _entities = new List<MockAnyHandle>();

            public string Name { get; set; } = "MockArena";
            public Type ArenaType => typeof(MockQueryableArena);
            public object Arena => this;
            public int ActiveCount => _entities.Count(e => e.Valid);

            public void AddEntity(MockAnyHandle entity)
            {
                _entities.Add(entity);
            }

            public IEnumerable<(AnyHandle Handle, int Index)> EnumerateActive()
            {
                for (int i = 0; i < _entities.Count; i++)
                {
                    var mockHandle = new TestAnyHandle(_entities[i].Valid);
                    yield return (mockHandle.ToAnyHandle(), i);
                }
            }

            public bool IsActive(int index)
            {
                return index >= 0 && index < _entities.Count && _entities[index].Valid;
            }
        }

        private class MockQueryableArena2 : IQueryableArena
        {
            private readonly List<MockAnyHandle> _entities = new List<MockAnyHandle>();

            public string Name { get; set; } = "MockArena2";
            public Type ArenaType => typeof(MockQueryableArena2);
            public object Arena => this;
            public int ActiveCount => _entities.Count(e => e.Valid);

            public void AddEntity(MockAnyHandle entity)
            {
                _entities.Add(entity);
            }

            public IEnumerable<(AnyHandle Handle, int Index)> EnumerateActive()
            {
                for (int i = 0; i < _entities.Count; i++)
                {
                    var mockHandle = new TestAnyHandle(_entities[i].Valid);
                    yield return (mockHandle.ToAnyHandle(), i);
                }
            }

            public bool IsActive(int index)
            {
                return index >= 0 && index < _entities.Count && _entities[index].Valid;
            }
        }

        private class TestAnyHandle : IEntityArena
        {
            private readonly bool _valid;

            public TestAnyHandle(bool valid)
            {
                _valid = valid;
            }

            public bool IsValid(int index, int generation) => _valid;

            public AnyHandle ToAnyHandle()
            {
                return new AnyHandle(this, 0, 1);
            }
        }

        #endregion
    }
}
