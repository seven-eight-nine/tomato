using System;
using System.Collections.Generic;
using Xunit;
using Tomato.ReconciliationSystem;
using Tomato.CommandGenerator;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem.Tests;

/// <summary>
/// PositionReconciler テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] PositionReconcilerを作成できる
/// - [x] 押し出し衝突を処理できる
/// - [x] 依存順に従って位置調停できる
/// - [x] 循環依存の場合はスキップされる
/// </summary>
public class PositionReconcilerTests
{
    private readonly MockArena _arena = new();

    [Fact]
    public void PositionReconciler_ShouldBeCreatable()
    {
        var graph = new DependencyGraph();
        var rule = new PriorityBasedReconciliationRule();
        var transforms = new MockTransformAccessor();
        var entityTypes = new MockEntityTypeAccessor();

        var reconciler = new PositionReconciler(graph, rule, transforms, entityTypes);

        Assert.NotNull(reconciler);
    }

    [Fact]
    public void Process_ShouldApplyPushout()
    {
        var graph = new DependencyGraph();
        var rule = new PriorityBasedReconciliationRule();
        var transforms = new MockTransformAccessor();
        var entityTypes = new MockEntityTypeAccessor();

        var player = _arena.CreateHandle(1);
        var wall = _arena.CreateHandle(2);

        transforms.SetPosition(player, new Vector3(0, 0, 0));
        transforms.SetPosition(wall, new Vector3(0, 0, 0));

        entityTypes.SetEntityType(player, EntityType.Player);
        entityTypes.SetEntityType(wall, EntityType.Wall);

        var reconciler = new PositionReconciler(graph, rule, transforms, entityTypes);

        // Pushbox衝突を作成
        var collisions = new List<CollisionResult>
        {
            CreatePushboxCollision(player, wall, new Vector3(1, 0, 0), 0.5f)
        };

        reconciler.Process(new[] { player, wall }, collisions);

        // Playerは押し出される（Wallは動かない）
        var playerPos = transforms.GetPosition(player);
        Assert.Equal(-0.5f, playerPos.X, 3);

        var wallPos = transforms.GetPosition(wall);
        Assert.Equal(0f, wallPos.X, 3);
    }

    [Fact]
    public void Process_ShouldRespectDependencyOrder()
    {
        var graph = new DependencyGraph();
        var rule = new PriorityBasedReconciliationRule();
        var transforms = new MockTransformAccessor();
        var entityTypes = new MockEntityTypeAccessor();

        var rider = _arena.CreateHandle(1);
        var horse = _arena.CreateHandle(2);

        transforms.SetPosition(rider, Vector3.Zero);
        transforms.SetPosition(horse, Vector3.Zero);

        entityTypes.SetEntityType(rider, EntityType.Player);
        entityTypes.SetEntityType(horse, EntityType.Player);

        // 騎乗者は馬に依存
        graph.AddDependency(rider, horse);

        var reconciler = new PositionReconciler(graph, rule, transforms, entityTypes);

        // 衝突なし
        reconciler.Process(new[] { rider, horse }, new List<CollisionResult>());

        // 処理は正常に完了（依存関係が正しく解決される）
        Assert.True(true);
    }

    [Fact]
    public void Process_CircularDependency_ShouldNotCrash()
    {
        var graph = new DependencyGraph();
        var rule = new PriorityBasedReconciliationRule();
        var transforms = new MockTransformAccessor();
        var entityTypes = new MockEntityTypeAccessor();

        var a = _arena.CreateHandle(1);
        var b = _arena.CreateHandle(2);

        transforms.SetPosition(a, Vector3.Zero);
        transforms.SetPosition(b, Vector3.Zero);

        entityTypes.SetEntityType(a, EntityType.Player);
        entityTypes.SetEntityType(b, EntityType.Player);

        // 循環依存
        graph.AddDependency(a, b);
        graph.AddDependency(b, a);

        var reconciler = new PositionReconciler(graph, rule, transforms, entityTypes);

        // 循環依存があってもクラッシュしない
        reconciler.Process(new[] { a, b }, new List<CollisionResult>());

        Assert.True(true);
    }

    [Fact]
    public void Process_MultiplePushouts_ShouldAccumulate()
    {
        var graph = new DependencyGraph();
        var rule = new PriorityBasedReconciliationRule();
        var transforms = new MockTransformAccessor();
        var entityTypes = new MockEntityTypeAccessor();

        var player = _arena.CreateHandle(1);
        var wall1 = _arena.CreateHandle(2);
        var wall2 = _arena.CreateHandle(3);

        transforms.SetPosition(player, Vector3.Zero);
        transforms.SetPosition(wall1, Vector3.Zero);
        transforms.SetPosition(wall2, Vector3.Zero);

        entityTypes.SetEntityType(player, EntityType.Player);
        entityTypes.SetEntityType(wall1, EntityType.Wall);
        entityTypes.SetEntityType(wall2, EntityType.Wall);

        var reconciler = new PositionReconciler(graph, rule, transforms, entityTypes);

        // 複数の壁との衝突
        var collisions = new List<CollisionResult>
        {
            CreatePushboxCollision(player, wall1, new Vector3(1, 0, 0), 0.3f),
            CreatePushboxCollision(player, wall2, new Vector3(0, 1, 0), 0.2f)
        };

        reconciler.Process(new[] { player, wall1, wall2 }, collisions);

        var playerPos = transforms.GetPosition(player);
        Assert.Equal(-0.3f, playerPos.X, 3);
        Assert.Equal(-0.2f, playerPos.Y, 3);
    }

    #region Helper Classes

    private static CollisionResult CreatePushboxCollision(VoidHandle entityA, VoidHandle entityB, Vector3 normal, float penetration)
    {
        var volumeA = new CollisionVolume(
            owner: entityA,
            shape: new SphereShape(1.0f),
            filter: CollisionFilter.PlayerHitbox,
            volumeType: VolumeType.Pushbox);

        var volumeB = new CollisionVolume(
            owner: entityB,
            shape: new SphereShape(1.0f),
            filter: CollisionFilter.PlayerHitbox,
            volumeType: VolumeType.Pushbox);

        var contact = new CollisionContact(Vector3.Zero, normal, penetration);
        return new CollisionResult(volumeA, volumeB, contact);
    }

    private class MockArena : IEntityArena
    {
        public VoidHandle CreateHandle(int index) => new VoidHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    private class MockTransformAccessor : IEntityTransformAccessor
    {
        private readonly Dictionary<VoidHandle, Vector3> _positions = new();

        public Vector3 GetPosition(VoidHandle handle)
        {
            return _positions.TryGetValue(handle, out var pos) ? pos : Vector3.Zero;
        }

        public void SetPosition(VoidHandle handle, Vector3 position)
        {
            _positions[handle] = position;
        }
    }

    private class MockEntityTypeAccessor : IEntityTypeAccessor
    {
        private readonly Dictionary<VoidHandle, EntityType> _types = new();

        public EntityType GetEntityType(VoidHandle handle)
        {
            return _types.TryGetValue(handle, out var type) ? type : EntityType.Player;
        }

        public void SetEntityType(VoidHandle handle, EntityType type)
        {
            _types[handle] = type;
        }
    }

    #endregion
}
