using System.Collections.Generic;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// Entity位置の調停を行う。
/// LateUpdateで依存順に従った位置調停と押し出し処理を実行する。
/// </summary>
public sealed class PositionReconciler
{
    private readonly DependencyGraph _dependencyGraph;
    private readonly DependencyResolver _dependencyResolver;
    private readonly ReconciliationRule _rule;
    private readonly IEntityTransformAccessor _transforms;
    private readonly IEntityTypeAccessor _entityTypes;

    private readonly List<(VoidHandle, VoidHandle, CollisionContact)> _pushboxCollisions;
    private readonly Dictionary<VoidHandle, Vector3> _pushouts;

    public PositionReconciler(
        DependencyGraph dependencyGraph,
        ReconciliationRule rule,
        IEntityTransformAccessor transforms,
        IEntityTypeAccessor entityTypes)
    {
        _dependencyGraph = dependencyGraph;
        _dependencyResolver = new DependencyResolver(dependencyGraph);
        _rule = rule;
        _transforms = transforms;
        _entityTypes = entityTypes;
        _pushboxCollisions = new List<(VoidHandle, VoidHandle, CollisionContact)>();
        _pushouts = new Dictionary<VoidHandle, Vector3>();
    }

    /// <summary>
    /// LateUpdate処理を実行する。
    /// </summary>
    public void Process(IEnumerable<VoidHandle> entities, IReadOnlyList<CollisionResult> pushboxCollisions)
    {
        // 1. 押し出し衝突を収集
        CollectPushboxCollisions(pushboxCollisions);

        // 2. 依存順を計算
        var order = _dependencyResolver.ComputeOrder(entities);
        if (order == null)
        {
            // 循環依存検出時はスキップ
            return;
        }

        // 3. 依存順に従って位置調停
        foreach (var handle in order)
        {
            ReconcileEntity(handle);
        }

        // 4. 押し出し処理
        ProcessPushouts();
    }

    private void CollectPushboxCollisions(IReadOnlyList<CollisionResult> collisions)
    {
        _pushboxCollisions.Clear();
        foreach (var collision in collisions)
        {
            if (collision.Volume1.VolumeType == VolumeType.Pushbox &&
                collision.Volume2.VolumeType == VolumeType.Pushbox)
            {
                _pushboxCollisions.Add((
                    collision.Volume1.Owner,
                    collision.Volume2.Owner,
                    collision.Contact));
            }
        }
    }

    private void ReconcileEntity(VoidHandle handle)
    {
        // 依存先に追従（騎乗等の実装）
        // 現在は基本実装のみ
        var dependencies = _dependencyGraph.GetDependencies(handle);
        if (dependencies.Count == 0)
            return;

        // 依存先との相対位置を維持する処理
        // （実際の実装はゲームデザイン依存）
    }

    private void ProcessPushouts()
    {
        _pushouts.Clear();

        // 押し出し量を計算
        foreach (var (entityA, entityB, contact) in _pushboxCollisions)
        {
            var typeA = _entityTypes.GetEntityType(entityA);
            var typeB = _entityTypes.GetEntityType(entityB);

            _rule.ComputePushout(entityA, typeA, entityB, typeB, contact, out var pushA, out var pushB);

            AccumulatePushout(entityA, pushA);
            AccumulatePushout(entityB, pushB);
        }

        // 押し出しを適用
        foreach (var (handle, pushout) in _pushouts)
        {
            if (pushout == Vector3.Zero)
                continue;

            var position = _transforms.GetPosition(handle);
            position += pushout;
            _transforms.SetPosition(handle, position);
        }
    }

    private void AccumulatePushout(VoidHandle handle, Vector3 pushout)
    {
        if (!_pushouts.TryGetValue(handle, out var current))
            current = Vector3.Zero;
        _pushouts[handle] = current + pushout;
    }
}
