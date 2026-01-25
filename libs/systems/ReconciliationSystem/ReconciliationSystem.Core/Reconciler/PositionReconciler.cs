using System.Collections.Generic;
using Tomato.Math;
using Tomato.DependencySortSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// Entity位置の調停を行う。
/// LateUpdateで依存順に従った位置調停と押し出し処理を実行する。
/// </summary>
public sealed class PositionReconciler
{
    private readonly DependencyGraph<AnyHandle> _dependencyGraph;
    private readonly TopologicalSorter<AnyHandle> _sorter;
    private readonly ReconciliationRule _rule;
    private readonly IEntityTransformAccessor _transforms;
    private readonly IEntityTypeAccessor _entityTypes;

    private readonly Dictionary<AnyHandle, Vector3> _pushouts;

    public PositionReconciler(
        DependencyGraph<AnyHandle> dependencyGraph,
        ReconciliationRule rule,
        IEntityTransformAccessor transforms,
        IEntityTypeAccessor entityTypes)
    {
        _dependencyGraph = dependencyGraph;
        _sorter = new TopologicalSorter<AnyHandle>();
        _rule = rule;
        _transforms = transforms;
        _entityTypes = entityTypes;
        _pushouts = new Dictionary<AnyHandle, Vector3>();
    }

    /// <summary>
    /// 依存グラフを取得する。
    /// </summary>
    public DependencyGraph<AnyHandle> DependencyGraph => _dependencyGraph;

    /// <summary>
    /// LateUpdate処理を実行する。
    /// </summary>
    public void Process(IEnumerable<AnyHandle> entities, IReadOnlyList<PushCollision> pushCollisions)
    {
        // 1. 依存順を計算
        var result = _sorter.Sort(entities, _dependencyGraph);
        if (!result.Success)
        {
            // 循環依存検出時はスキップ
            return;
        }

        // 2. 依存順に従って位置調停
        foreach (var handle in result.SortedOrder!)
        {
            ReconcileEntity(handle);
        }

        // 3. 押し出し処理
        ProcessPushouts(pushCollisions);
    }

    private void ReconcileEntity(AnyHandle handle)
    {
        // 依存先に追従（騎乗等の実装）
        // 現在は基本実装のみ
        var dependencies = _dependencyGraph.GetDependencies(handle);
        if (dependencies.Count == 0)
            return;

        // 依存先との相対位置を維持する処理
        // （実際の実装はゲームデザイン依存）
    }

    private void ProcessPushouts(IReadOnlyList<PushCollision> pushCollisions)
    {
        _pushouts.Clear();

        // 押し出し量を計算
        foreach (var collision in pushCollisions)
        {
            var typeA = _entityTypes.GetEntityType(collision.EntityA);
            var typeB = _entityTypes.GetEntityType(collision.EntityB);

            _rule.ComputePushout(
                collision.EntityA, typeA,
                collision.EntityB, typeB,
                collision.Normal, collision.Penetration,
                out var pushA, out var pushB);

            AccumulatePushout(collision.EntityA, pushA);
            AccumulatePushout(collision.EntityB, pushB);
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

    private void AccumulatePushout(AnyHandle handle, Vector3 pushout)
    {
        if (!_pushouts.TryGetValue(handle, out var current))
            current = Vector3.Zero;
        _pushouts[handle] = current + pushout;
    }
}
