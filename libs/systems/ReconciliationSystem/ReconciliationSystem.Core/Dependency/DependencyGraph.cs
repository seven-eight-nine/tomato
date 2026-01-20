using System;
using System.Collections.Generic;
using System.Linq;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// Entity間の依存関係を管理するグラフ。
/// 騎乗者→馬のような「依存先が先に処理される」関係を表現する。
/// </summary>
public sealed class DependencyGraph
{
    private readonly Dictionary<VoidHandle, List<VoidHandle>> _dependencies;  // Entity -> 依存先リスト
    private readonly Dictionary<VoidHandle, List<VoidHandle>> _dependents;    // Entity -> 依存元リスト

    public DependencyGraph()
    {
        _dependencies = new Dictionary<VoidHandle, List<VoidHandle>>();
        _dependents = new Dictionary<VoidHandle, List<VoidHandle>>();
    }

    /// <summary>
    /// 依存関係を追加する（fromがtoに依存）。
    /// </summary>
    public void AddDependency(VoidHandle from, VoidHandle to)
    {
        if (!_dependencies.TryGetValue(from, out var deps))
        {
            deps = new List<VoidHandle>();
            _dependencies[from] = deps;
        }
        if (!deps.Contains(to))
            deps.Add(to);

        if (!_dependents.TryGetValue(to, out var depts))
        {
            depts = new List<VoidHandle>();
            _dependents[to] = depts;
        }
        if (!depts.Contains(from))
            depts.Add(from);
    }

    /// <summary>
    /// 依存関係を削除する。
    /// </summary>
    public void RemoveDependency(VoidHandle from, VoidHandle to)
    {
        if (_dependencies.TryGetValue(from, out var deps))
            deps.Remove(to);

        if (_dependents.TryGetValue(to, out var depts))
            depts.Remove(from);
    }

    /// <summary>
    /// Entityの依存先を取得する。
    /// </summary>
    public IReadOnlyList<VoidHandle> GetDependencies(VoidHandle entity)
    {
        return _dependencies.TryGetValue(entity, out var deps) ? deps : Array.Empty<VoidHandle>();
    }

    /// <summary>
    /// Entityの依存元を取得する。
    /// </summary>
    public IReadOnlyList<VoidHandle> GetDependents(VoidHandle entity)
    {
        return _dependents.TryGetValue(entity, out var depts) ? depts : Array.Empty<VoidHandle>();
    }

    /// <summary>
    /// Entityを削除する（関連する依存関係も削除）。
    /// </summary>
    public void RemoveEntity(VoidHandle entity)
    {
        // 自分への依存を持つEntityから削除
        if (_dependents.TryGetValue(entity, out var dependents))
        {
            foreach (var dependent in dependents.ToArray())
            {
                if (_dependencies.TryGetValue(dependent, out var deps))
                    deps.Remove(entity);
            }
            _dependents.Remove(entity);
        }

        // 自分が依存しているEntityの依存元から削除
        if (_dependencies.TryGetValue(entity, out var dependencies))
        {
            foreach (var dependency in dependencies.ToArray())
            {
                if (_dependents.TryGetValue(dependency, out var depts))
                    depts.Remove(entity);
            }
            _dependencies.Remove(entity);
        }
    }

    /// <summary>
    /// グラフをクリアする。
    /// </summary>
    public void Clear()
    {
        _dependencies.Clear();
        _dependents.Clear();
    }
}
