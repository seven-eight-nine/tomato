using System;
using System.Collections.Generic;
using Xunit;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.CollisionSystem.Tests;

/// <summary>
/// 空間分割テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] CollisionVolumeを作成できる
/// - [x] UniformGridを作成できる
/// - [x] UniformGridにボリュームを挿入できる
/// - [x] UniformGridからAABBでクエリできる
/// - [x] UniformGridで離れたボリュームは返さない
/// - [x] UniformGridから全ペアを取得できる
/// - [x] UniformGridをクリアできる
/// - [x] CollisionVolumeのライフタイムが機能する
/// </summary>
public class SpatialPartitionTests
{
    private readonly MockArena _arena = new();

    #region CollisionVolume Tests

    [Fact]
    public void CollisionVolume_ShouldBeCreatable()
    {
        var owner = _arena.CreateHandle(1);
        var volume = new CollisionVolume(
            owner: owner,
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);

        Assert.Equal(owner, volume.Owner);
        Assert.Equal(VolumeType.Hurtbox, volume.VolumeType);
        Assert.False(volume.IsExpired);
    }

    [Fact]
    public void CollisionVolume_WithLifetime_ShouldExpireAfterTicks()
    {
        var volume = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerAttack,
            volumeType: VolumeType.Hitbox,
            lifetime: 3);

        Assert.False(volume.IsExpired);
        Assert.Equal(3, volume.RemainingLifetime);

        volume.Tick();
        Assert.Equal(2, volume.RemainingLifetime);
        Assert.False(volume.IsExpired);

        volume.Tick();
        volume.Tick();
        Assert.Equal(0, volume.RemainingLifetime);
        Assert.True(volume.IsExpired);
    }

    [Fact]
    public void CollisionVolume_WithZeroLifetime_ShouldNeverExpire()
    {
        var volume = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox,
            lifetime: 0);

        for (int i = 0; i < 100; i++)
        {
            volume.Tick();
        }

        Assert.False(volume.IsExpired);
    }

    #endregion

    #region UniformGrid Tests

    [Fact]
    public void UniformGrid_ShouldBeCreatable()
    {
        var grid = new UniformGrid(cellSize: 10f);

        Assert.NotNull(grid);
    }

    [Fact]
    public void UniformGrid_Insert_ShouldAddVolume()
    {
        var grid = new UniformGrid(cellSize: 10f);
        var volume = CreateTestVolume(_arena.CreateHandle(1), Vector3.Zero);

        grid.Insert(volume, Vector3.Zero);

        var results = new List<CollisionVolume>();
        grid.Query(new AABB(new Vector3(-5f, -5f, -5f), new Vector3(5f, 5f, 5f)), results);

        Assert.Single(results);
        Assert.Same(volume, results[0]);
    }

    [Fact]
    public void UniformGrid_Query_ShouldReturnVolumesInBounds()
    {
        var grid = new UniformGrid(cellSize: 10f);

        var v1 = CreateTestVolume(_arena.CreateHandle(1), Vector3.Zero);
        var v2 = CreateTestVolume(_arena.CreateHandle(2), new Vector3(5f, 0f, 0f));
        var v3 = CreateTestVolume(_arena.CreateHandle(3), new Vector3(100f, 0f, 0f)); // 遠い

        grid.Insert(v1, Vector3.Zero);
        grid.Insert(v2, new Vector3(5f, 0f, 0f));
        grid.Insert(v3, new Vector3(100f, 0f, 0f));

        var results = new List<CollisionVolume>();
        grid.Query(new AABB(new Vector3(-10f, -10f, -10f), new Vector3(10f, 10f, 10f)), results);

        Assert.Equal(2, results.Count);
        Assert.Contains(v1, results);
        Assert.Contains(v2, results);
        Assert.DoesNotContain(v3, results);
    }

    [Fact]
    public void UniformGrid_Query_ShouldNotReturnDuplicates()
    {
        var grid = new UniformGrid(cellSize: 5f);

        // 複数セルにまたがる大きなボリューム
        var volume = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(10f), // 大きな球
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);

        grid.Insert(volume, Vector3.Zero);

        var results = new List<CollisionVolume>();
        grid.Query(new AABB(new Vector3(-20f, -20f, -20f), new Vector3(20f, 20f, 20f)), results);

        Assert.Single(results);
    }

    [Fact]
    public void UniformGrid_QueryAllPairs_ShouldReturnPotentialCollisions()
    {
        var grid = new UniformGrid(cellSize: 10f);

        var v1 = CreateTestVolume(_arena.CreateHandle(1), Vector3.Zero);
        var v2 = CreateTestVolume(_arena.CreateHandle(2), new Vector3(1f, 0f, 0f)); // 近い
        var v3 = CreateTestVolume(_arena.CreateHandle(3), new Vector3(100f, 0f, 0f)); // 遠い

        grid.Insert(v1, Vector3.Zero);
        grid.Insert(v2, new Vector3(1f, 0f, 0f));
        grid.Insert(v3, new Vector3(100f, 0f, 0f));

        var pairs = new List<(CollisionVolume, CollisionVolume)>();
        grid.QueryAllPairs(pairs);

        // v1-v2のペアのみ（v3は別セル）
        Assert.Single(pairs);
        Assert.True(
            (pairs[0].Item1 == v1 && pairs[0].Item2 == v2) ||
            (pairs[0].Item1 == v2 && pairs[0].Item2 == v1));
    }

    [Fact]
    public void UniformGrid_Clear_ShouldRemoveAllVolumes()
    {
        var grid = new UniformGrid(cellSize: 10f);

        grid.Insert(CreateTestVolume(_arena.CreateHandle(1), Vector3.Zero), Vector3.Zero);
        grid.Insert(CreateTestVolume(_arena.CreateHandle(2), new Vector3(5f, 0f, 0f)), new Vector3(5f, 0f, 0f));

        grid.Clear();

        var results = new List<CollisionVolume>();
        grid.Query(new AABB(new Vector3(-100f, -100f, -100f), new Vector3(100f, 100f, 100f)), results);

        Assert.Empty(results);
    }

    [Fact]
    public void UniformGrid_Remove_ShouldRemoveSpecificVolume()
    {
        var grid = new UniformGrid(cellSize: 10f);

        var v1 = CreateTestVolume(_arena.CreateHandle(1), Vector3.Zero);
        var v2 = CreateTestVolume(_arena.CreateHandle(2), new Vector3(5f, 0f, 0f));

        grid.Insert(v1, Vector3.Zero);
        grid.Insert(v2, new Vector3(5f, 0f, 0f));

        grid.Remove(v1);

        var results = new List<CollisionVolume>();
        grid.Query(new AABB(new Vector3(-10f, -10f, -10f), new Vector3(10f, 10f, 10f)), results);

        Assert.Single(results);
        Assert.Same(v2, results[0]);
    }

    #endregion

    #region Helper Methods

    private static CollisionVolume CreateTestVolume(AnyHandle owner, Vector3 position)
    {
        return new CollisionVolume(
            owner: owner,
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);
    }

    #endregion

    #region Helper Classes

    private class MockArena : IEntityArena
    {
        public AnyHandle CreateHandle(int index) => new AnyHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    #endregion
}
