using System;
using System.Collections.Generic;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Attributes
{
    /// <summary>
    /// EntityManager テスト
    /// </summary>
    public class EntityManagerTests
    {
        #region Register Tests

        [Fact]
        public void Register_ShouldAddArena()
        {
            var manager = new EntityManager();
            var arena = new MockArena();

            manager.Register<MockArena, MockArenaSnapshot>(arena);

            Assert.Equal(1, manager.ArenaCount);
        }

        [Fact]
        public void Register_NullArena_ShouldThrow()
        {
            var manager = new EntityManager();

            Assert.Throws<ArgumentNullException>(() =>
                manager.Register<MockArena, MockArenaSnapshot>(null));
        }

        [Fact]
        public void Register_DuplicateArena_ShouldThrow()
        {
            var manager = new EntityManager();
            var arena = new MockArena();

            manager.Register<MockArena, MockArenaSnapshot>(arena);

            Assert.Throws<InvalidOperationException>(() =>
                manager.Register<MockArena, MockArenaSnapshot>(arena));
        }

        [Fact]
        public void Register_MultipleArenas_ShouldAddAll()
        {
            var manager = new EntityManager();

            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());
            manager.Register<MockArena2, MockArena2Snapshot>(new MockArena2());

            Assert.Equal(2, manager.ArenaCount);
        }

        #endregion

        #region Unregister Tests

        [Fact]
        public void Unregister_ExistingArena_ShouldRemove()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());

            var removed = manager.Unregister<MockArena>();

            Assert.True(removed);
            Assert.Equal(0, manager.ArenaCount);
        }

        [Fact]
        public void Unregister_NonExistingArena_ShouldReturnFalse()
        {
            var manager = new EntityManager();

            var removed = manager.Unregister<MockArena>();

            Assert.False(removed);
        }

        [Fact]
        public void Unregister_MiddleArena_ShouldMaintainOthers()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());
            manager.Register<MockArena2, MockArena2Snapshot>(new MockArena2());
            manager.Register<MockArena3, MockArena3Snapshot>(new MockArena3());

            manager.Unregister<MockArena2>();

            Assert.Equal(2, manager.ArenaCount);
            Assert.True(manager.IsRegistered<MockArena>());
            Assert.False(manager.IsRegistered<MockArena2>());
            Assert.True(manager.IsRegistered<MockArena3>());
        }

        #endregion

        #region IsRegistered Tests

        [Fact]
        public void IsRegistered_RegisteredArena_ShouldReturnTrue()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());

            Assert.True(manager.IsRegistered<MockArena>());
        }

        [Fact]
        public void IsRegistered_NotRegisteredArena_ShouldReturnFalse()
        {
            var manager = new EntityManager();

            Assert.False(manager.IsRegistered<MockArena>());
        }

        #endregion

        #region CaptureSnapshot Tests

        [Fact]
        public void CaptureSnapshot_EmptyManager_ShouldReturnEmptySnapshot()
        {
            var manager = new EntityManager();

            var snapshot = manager.CaptureSnapshot(0);

            Assert.NotNull(snapshot);
            Assert.Equal(0, snapshot.FrameNumber);
            Assert.Equal(0, snapshot.ArenaCount);
        }

        [Fact]
        public void CaptureSnapshot_WithArenas_ShouldCaptureAll()
        {
            var manager = new EntityManager();
            var arena1 = new MockArena { Value = 42 };
            var arena2 = new MockArena2 { Name = "Test" };

            manager.Register<MockArena, MockArenaSnapshot>(arena1);
            manager.Register<MockArena2, MockArena2Snapshot>(arena2);

            var snapshot = manager.CaptureSnapshot(100);

            Assert.Equal(100, snapshot.FrameNumber);
            Assert.Equal(2, snapshot.ArenaCount);

            var snap1 = snapshot.GetSnapshot<MockArena, MockArenaSnapshot>();
            Assert.Equal(42, snap1.Value);

            var snap2 = snapshot.GetSnapshot<MockArena2, MockArena2Snapshot>();
            Assert.Equal("Test", snap2.Name);
        }

        [Fact]
        public void CaptureSnapshot_FrameNumberShouldBeStored()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());

            var snapshot = manager.CaptureSnapshot(12345);

            Assert.Equal(12345, snapshot.FrameNumber);
        }

        [Fact]
        public void CaptureSnapshot_ShouldStoreCapturedAt()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());

            var before = DateTime.UtcNow;
            var snapshot = manager.CaptureSnapshot(0);
            var after = DateTime.UtcNow;

            Assert.True(snapshot.CapturedAt >= before);
            Assert.True(snapshot.CapturedAt <= after);
        }

        #endregion

        #region RestoreSnapshot Tests

        [Fact]
        public void RestoreSnapshot_ShouldRestoreAllArenas()
        {
            var manager = new EntityManager();
            var arena1 = new MockArena { Value = 10 };
            var arena2 = new MockArena2 { Name = "Initial" };

            manager.Register<MockArena, MockArenaSnapshot>(arena1);
            manager.Register<MockArena2, MockArena2Snapshot>(arena2);

            var snapshot = manager.CaptureSnapshot(0);

            // 状態変更
            arena1.Value = 999;
            arena2.Name = "Changed";

            // 復元
            manager.RestoreSnapshot(snapshot);

            Assert.Equal(10, arena1.Value);
            Assert.Equal("Initial", arena2.Name);
        }

        [Fact]
        public void RestoreSnapshot_NullSnapshot_ShouldThrow()
        {
            var manager = new EntityManager();

            Assert.Throws<ArgumentNullException>(() => manager.RestoreSnapshot(null));
        }

        [Fact]
        public void RestoreSnapshot_PartialArenas_ShouldRestoreOnlyMatching()
        {
            var manager = new EntityManager();
            var arena1 = new MockArena { Value = 10 };

            manager.Register<MockArena, MockArenaSnapshot>(arena1);

            var snapshot = manager.CaptureSnapshot(0);

            // 新しいArenaを追加
            var arena2 = new MockArena2 { Name = "New" };
            manager.Register<MockArena2, MockArena2Snapshot>(arena2);

            arena1.Value = 999;

            // 復元（arena2は古いスナップショットに含まれない）
            manager.RestoreSnapshot(snapshot);

            Assert.Equal(10, arena1.Value);
            Assert.Equal("New", arena2.Name); // 変更されていない
        }

        #endregion

        #region CaptureArena/RestoreArena Tests

        [Fact]
        public void CaptureArena_ShouldCaptureSpecificArena()
        {
            var manager = new EntityManager();
            var arena = new MockArena { Value = 42 };

            manager.Register<MockArena, MockArenaSnapshot>(arena);

            var snapshot = manager.CaptureArena<MockArena, MockArenaSnapshot>();

            Assert.Equal(42, snapshot.Value);
        }

        [Fact]
        public void CaptureArena_NonRegisteredArena_ShouldThrow()
        {
            var manager = new EntityManager();

            Assert.Throws<KeyNotFoundException>(() =>
                manager.CaptureArena<MockArena, MockArenaSnapshot>());
        }

        [Fact]
        public void RestoreArena_ShouldRestoreSpecificArena()
        {
            var manager = new EntityManager();
            var arena = new MockArena { Value = 10 };

            manager.Register<MockArena, MockArenaSnapshot>(arena);

            var snapshot = manager.CaptureArena<MockArena, MockArenaSnapshot>();

            arena.Value = 999;

            manager.RestoreArena<MockArena, MockArenaSnapshot>(snapshot);

            Assert.Equal(10, arena.Value);
        }

        [Fact]
        public void RestoreArena_ShouldNotAffectOtherArenas()
        {
            var manager = new EntityManager();
            var arena1 = new MockArena { Value = 10 };
            var arena2 = new MockArena2 { Name = "Original" };

            manager.Register<MockArena, MockArenaSnapshot>(arena1);
            manager.Register<MockArena2, MockArena2Snapshot>(arena2);

            var snapshot = manager.CaptureArena<MockArena, MockArenaSnapshot>();

            arena1.Value = 999;
            arena2.Name = "Changed";

            manager.RestoreArena<MockArena, MockArenaSnapshot>(snapshot);

            Assert.Equal(10, arena1.Value);
            Assert.Equal("Changed", arena2.Name); // 変更されていない
        }

        #endregion

        #region Clear Tests

        [Fact]
        public void Clear_ShouldRemoveAllArenas()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());
            manager.Register<MockArena2, MockArena2Snapshot>(new MockArena2());

            manager.Clear();

            Assert.Equal(0, manager.ArenaCount);
        }

        #endregion

        #region GetRegisteredArenaTypes Tests

        [Fact]
        public void GetRegisteredArenaTypes_ShouldReturnAllTypes()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());
            manager.Register<MockArena2, MockArena2Snapshot>(new MockArena2());

            var types = manager.GetRegisteredArenaTypes();

            Assert.Equal(2, types.Count);
            Assert.Contains(typeof(MockArena), types);
            Assert.Contains(typeof(MockArena2), types);
        }

        #endregion

        #region Multiple EntityManager Tests

        [Fact]
        public void MultipleManagers_ShouldBeIndependent()
        {
            var manager1 = new EntityManager();
            var manager2 = new EntityManager();

            var arena1 = new MockArena { Value = 10 };
            var arena2 = new MockArena { Value = 20 };

            manager1.Register<MockArena, MockArenaSnapshot>(arena1);
            manager2.Register<MockArena, MockArenaSnapshot>(arena2);

            Assert.Equal(1, manager1.ArenaCount);
            Assert.Equal(1, manager2.ArenaCount);

            var snap1 = manager1.CaptureArena<MockArena, MockArenaSnapshot>();
            var snap2 = manager2.CaptureArena<MockArena, MockArenaSnapshot>();

            Assert.Equal(10, snap1.Value);
            Assert.Equal(20, snap2.Value);
        }

        #endregion

        #region EntityManagerSnapshot Tests

        [Fact]
        public void Snapshot_TryGetSnapshot_ExistingArena_ShouldReturnValue()
        {
            var manager = new EntityManager();
            var arena = new MockArena { Value = 42 };
            manager.Register<MockArena, MockArenaSnapshot>(arena);

            var snapshot = manager.CaptureSnapshot(0);
            var result = snapshot.TryGetSnapshot<MockArena, MockArenaSnapshot>();

            Assert.NotNull(result);
            Assert.Equal(42, result.Value.Value);
        }

        [Fact]
        public void Snapshot_TryGetSnapshot_NonExistingArena_ShouldReturnNull()
        {
            var manager = new EntityManager();
            var snapshot = manager.CaptureSnapshot(0);

            var result = snapshot.TryGetSnapshot<MockArena, MockArenaSnapshot>();

            Assert.Null(result);
        }

        [Fact]
        public void Snapshot_HasSnapshot_ExistingArena_ShouldReturnTrue()
        {
            var manager = new EntityManager();
            manager.Register<MockArena, MockArenaSnapshot>(new MockArena());

            var snapshot = manager.CaptureSnapshot(0);

            Assert.True(snapshot.HasSnapshot<MockArena>());
        }

        [Fact]
        public void Snapshot_HasSnapshot_NonExistingArena_ShouldReturnFalse()
        {
            var manager = new EntityManager();
            var snapshot = manager.CaptureSnapshot(0);

            Assert.False(snapshot.HasSnapshot<MockArena>());
        }

        [Fact]
        public void Snapshot_GetSnapshot_NonExistingArena_ShouldThrow()
        {
            var manager = new EntityManager();
            var snapshot = manager.CaptureSnapshot(0);

            Assert.Throws<KeyNotFoundException>(() =>
                snapshot.GetSnapshot<MockArena, MockArenaSnapshot>());
        }

        #endregion

        #region Mock Classes

        private struct MockArenaSnapshot
        {
            public int Value;

            public MockArenaSnapshot(int value) => Value = value;
        }

        private class MockArena : ISnapshotableArena<MockArenaSnapshot>
        {
            public int Value { get; set; }

            public MockArenaSnapshot CaptureSnapshot()
            {
                return new MockArenaSnapshot(Value);
            }

            public void RestoreSnapshot(in MockArenaSnapshot snapshot)
            {
                Value = snapshot.Value;
            }
        }

        private struct MockArena2Snapshot
        {
            public string Name;

            public MockArena2Snapshot(string name) => Name = name;
        }

        private class MockArena2 : ISnapshotableArena<MockArena2Snapshot>
        {
            public string Name { get; set; }

            public MockArena2Snapshot CaptureSnapshot()
            {
                return new MockArena2Snapshot(Name);
            }

            public void RestoreSnapshot(in MockArena2Snapshot snapshot)
            {
                Name = snapshot.Name;
            }
        }

        private struct MockArena3Snapshot
        {
            public float Position;
        }

        private class MockArena3 : ISnapshotableArena<MockArena3Snapshot>
        {
            public float Position { get; set; }

            public MockArena3Snapshot CaptureSnapshot()
            {
                return new MockArena3Snapshot { Position = Position };
            }

            public void RestoreSnapshot(in MockArena3Snapshot snapshot)
            {
                Position = snapshot.Position;
            }
        }

        #endregion
    }
}
