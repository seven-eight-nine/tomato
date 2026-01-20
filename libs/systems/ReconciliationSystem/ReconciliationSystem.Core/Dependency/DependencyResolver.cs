using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem;

/// <summary>
/// 依存グラフからトポロジカルソート順を計算する。
/// 依存先が先に処理される順序を返す。
/// </summary>
public sealed class DependencyResolver
{
    private readonly DependencyGraph _graph;
    private readonly List<VoidHandle> _sortedOrder;
    private readonly HashSet<VoidHandle> _visited;
    private readonly HashSet<VoidHandle> _inStack;

    public DependencyResolver(DependencyGraph graph)
    {
        _graph = graph;
        _sortedOrder = new List<VoidHandle>();
        _visited = new HashSet<VoidHandle>();
        _inStack = new HashSet<VoidHandle>();
    }

    /// <summary>
    /// トポロジカルソート順を計算する。
    /// </summary>
    /// <returns>処理順序。循環検出時はnull。</returns>
    public IReadOnlyList<VoidHandle>? ComputeOrder(IEnumerable<VoidHandle> entities)
    {
        _sortedOrder.Clear();
        _visited.Clear();
        _inStack.Clear();

        foreach (var entity in entities)
        {
            if (!_visited.Contains(entity))
            {
                if (!Visit(entity))
                {
                    // 循環検出
                    return null;
                }
            }
        }

        // Post-order DFSでは依存先が先に追加されるので、そのまま返す
        return _sortedOrder;
    }

    private bool Visit(VoidHandle entity)
    {
        if (_inStack.Contains(entity))
        {
            // 循環検出
            return false;
        }

        if (_visited.Contains(entity))
            return true;

        _inStack.Add(entity);

        foreach (var dependency in _graph.GetDependencies(entity))
        {
            if (!Visit(dependency))
                return false;
        }

        _inStack.Remove(entity);
        _visited.Add(entity);
        _sortedOrder.Add(entity);

        return true;
    }
}
