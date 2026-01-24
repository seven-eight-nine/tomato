using System;
using System.Collections.Generic;

namespace Tomato.DependencySortSystem;

/// <summary>
/// 依存グラフをトポロジカルソートするソーター。
/// 依存先が先に処理される順序を返す。
/// </summary>
/// <typeparam name="TNode">ノードの型</typeparam>
public sealed class TopologicalSorter<TNode> where TNode : notnull
{
    private readonly IEqualityComparer<TNode> _comparer;
    private List<TNode>? _sortedOrder;
    private HashSet<TNode>? _visited;
    private HashSet<TNode>? _inStack;
    private List<TNode>? _currentPath;
    private List<TNode>? _cyclePath;

    /// <summary>
    /// デフォルトの等値比較器でソーターを作成する。
    /// </summary>
    public TopologicalSorter() : this(EqualityComparer<TNode>.Default)
    {
    }

    /// <summary>
    /// 指定した等値比較器でソーターを作成する。
    /// </summary>
    public TopologicalSorter(IEqualityComparer<TNode> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
    }

    /// <summary>
    /// 指定されたノード群をトポロジカルソートする。
    /// </summary>
    /// <param name="nodes">ソート対象のノード</param>
    /// <param name="graph">依存関係グラフ</param>
    /// <returns>ソート結果</returns>
    public SortResult<TNode> Sort(IEnumerable<TNode> nodes, DependencyGraph<TNode> graph)
    {
        if (nodes == null) throw new ArgumentNullException(nameof(nodes));
        if (graph == null) throw new ArgumentNullException(nameof(graph));

        // 作業領域を初期化（再利用のため）
        _sortedOrder ??= new List<TNode>();
        _visited ??= new HashSet<TNode>(_comparer);
        _inStack ??= new HashSet<TNode>(_comparer);
        _currentPath ??= new List<TNode>();

        _sortedOrder.Clear();
        _visited.Clear();
        _inStack.Clear();
        _currentPath.Clear();
        _cyclePath = null;

        foreach (var node in nodes)
        {
            if (!_visited.Contains(node))
            {
                if (!Visit(node, graph))
                {
                    // 循環検出
                    return SortResult<TNode>.CycleDetected(_cyclePath!);
                }
            }
        }

        // 結果をコピーして返す（内部リストを直接返さない）
        var result = new List<TNode>(_sortedOrder);
        return SortResult<TNode>.Succeeded(result);
    }

    private bool Visit(TNode node, DependencyGraph<TNode> graph)
    {
        if (_inStack!.Contains(node))
        {
            // 循環検出 - パスを抽出
            ExtractCyclePath(node);
            return false;
        }

        if (_visited!.Contains(node))
            return true;

        _inStack.Add(node);
        _currentPath!.Add(node);

        foreach (var dependency in graph.GetDependencies(node))
        {
            if (!Visit(dependency, graph))
                return false;
        }

        _inStack.Remove(node);
        _currentPath.RemoveAt(_currentPath.Count - 1);
        _visited.Add(node);
        _sortedOrder!.Add(node);

        return true;
    }

    private void ExtractCyclePath(TNode cycleStart)
    {
        _cyclePath = new List<TNode>();

        // cycleStartからcurrentPath内で見つけて、そこから最後までがサイクル
        int startIndex = -1;
        for (int i = 0; i < _currentPath!.Count; i++)
        {
            if (_comparer.Equals(_currentPath[i], cycleStart))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex >= 0)
        {
            for (int i = startIndex; i < _currentPath.Count; i++)
            {
                _cyclePath.Add(_currentPath[i]);
            }
            // サイクルを閉じる（開始ノードを末尾にも追加）
            _cyclePath.Add(cycleStart);
        }
        else
        {
            // フォールバック：cycleStartのみ
            _cyclePath.Add(cycleStart);
        }
    }
}
