using System;
using System.Collections.Generic;
using System.Linq;

namespace Tomato.DependencySortSystem;

/// <summary>
/// ノード間の依存関係を管理する汎用グラフ。
/// 「fromがtoに依存する」という関係を表現し、依存先が先に処理される順序を決定するために使用する。
/// </summary>
/// <typeparam name="TNode">ノードの型</typeparam>
public sealed class DependencyGraph<TNode> where TNode : notnull
{
    private readonly Dictionary<TNode, List<TNode>> _dependencies;  // Node -> 依存先リスト
    private readonly Dictionary<TNode, List<TNode>> _dependents;    // Node -> 依存元リスト
    private readonly IEqualityComparer<TNode> _comparer;

    /// <summary>
    /// グラフに登録されているノード数。
    /// </summary>
    public int NodeCount => _dependencies.Count;

    /// <summary>
    /// デフォルトの等値比較器でグラフを作成する。
    /// </summary>
    public DependencyGraph() : this(EqualityComparer<TNode>.Default)
    {
    }

    /// <summary>
    /// 指定した等値比較器でグラフを作成する。
    /// </summary>
    public DependencyGraph(IEqualityComparer<TNode> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _dependencies = new Dictionary<TNode, List<TNode>>(_comparer);
        _dependents = new Dictionary<TNode, List<TNode>>(_comparer);
    }

    /// <summary>
    /// 依存関係を追加する（fromがtoに依存）。
    /// toが先に処理される必要がある、という意味。
    /// </summary>
    /// <param name="from">依存元ノード</param>
    /// <param name="to">依存先ノード（先に処理される）</param>
    public void AddDependency(TNode from, TNode to)
    {
        if (!_dependencies.TryGetValue(from, out var deps))
        {
            deps = new List<TNode>();
            _dependencies[from] = deps;
        }
        if (!deps.Contains(to, _comparer))
            deps.Add(to);

        if (!_dependents.TryGetValue(to, out var depts))
        {
            depts = new List<TNode>();
            _dependents[to] = depts;
        }
        if (!depts.Contains(from, _comparer))
            depts.Add(from);

        // toがdependenciesに存在しない場合も登録（ノードとして認識するため）
        if (!_dependencies.ContainsKey(to))
            _dependencies[to] = new List<TNode>();
    }

    /// <summary>
    /// 依存関係を削除する。
    /// </summary>
    public void RemoveDependency(TNode from, TNode to)
    {
        if (_dependencies.TryGetValue(from, out var deps))
            deps.Remove(to);

        if (_dependents.TryGetValue(to, out var depts))
            depts.Remove(from);
    }

    /// <summary>
    /// ノードの依存先を取得する（このノードが依存しているノード）。
    /// </summary>
    public IReadOnlyList<TNode> GetDependencies(TNode node)
    {
        return _dependencies.TryGetValue(node, out var deps) ? deps : Array.Empty<TNode>();
    }

    /// <summary>
    /// ノードの依存元を取得する（このノードに依存しているノード）。
    /// </summary>
    public IReadOnlyList<TNode> GetDependents(TNode node)
    {
        return _dependents.TryGetValue(node, out var depts) ? depts : Array.Empty<TNode>();
    }

    /// <summary>
    /// 指定ノードが依存関係を持っているか（依存先があるか）。
    /// </summary>
    public bool HasDependencies(TNode node)
    {
        return _dependencies.TryGetValue(node, out var deps) && deps.Count > 0;
    }

    /// <summary>
    /// 指定ノードが依存元を持っているか（このノードに依存しているノードがあるか）。
    /// </summary>
    public bool HasDependents(TNode node)
    {
        return _dependents.TryGetValue(node, out var depts) && depts.Count > 0;
    }

    /// <summary>
    /// ノードを削除する（関連する依存関係も削除）。
    /// </summary>
    public void RemoveNode(TNode node)
    {
        // 自分への依存を持つノードから削除
        if (_dependents.TryGetValue(node, out var dependents))
        {
            foreach (var dependent in dependents.ToArray())
            {
                if (_dependencies.TryGetValue(dependent, out var deps))
                    deps.Remove(node);
            }
            _dependents.Remove(node);
        }

        // 自分が依存しているノードの依存元から削除
        if (_dependencies.TryGetValue(node, out var dependencies))
        {
            foreach (var dependency in dependencies.ToArray())
            {
                if (_dependents.TryGetValue(dependency, out var depts))
                    depts.Remove(node);
            }
            _dependencies.Remove(node);
        }
    }

    /// <summary>
    /// グラフに登録されている全ノードを取得する。
    /// </summary>
    public IEnumerable<TNode> GetAllNodes()
    {
        return _dependencies.Keys;
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

/// <summary>
/// List.Contains の拡張（IEqualityComparer対応）。
/// </summary>
internal static class ListExtensions
{
    public static bool Contains<T>(this List<T> list, T item, IEqualityComparer<T> comparer)
    {
        foreach (var element in list)
        {
            if (comparer.Equals(element, item))
                return true;
        }
        return false;
    }
}
