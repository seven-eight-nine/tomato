using System.Collections.Generic;

namespace Tomato.DependencySortSystem;

/// <summary>
/// トポロジカルソートの結果。
/// </summary>
/// <typeparam name="TNode">ノードの型</typeparam>
public readonly struct SortResult<TNode> where TNode : notnull
{
    /// <summary>
    /// ソートが成功したかどうか。
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// ソート結果（成功時のみ有効）。
    /// 依存先が先に来る順序。
    /// </summary>
    public IReadOnlyList<TNode>? SortedOrder { get; }

    /// <summary>
    /// 循環パス（循環検出時のみ有効）。
    /// 循環を構成するノードのリスト。
    /// </summary>
    public IReadOnlyList<TNode>? CyclePath { get; }

    private SortResult(bool success, IReadOnlyList<TNode>? sortedOrder, IReadOnlyList<TNode>? cyclePath)
    {
        Success = success;
        SortedOrder = sortedOrder;
        CyclePath = cyclePath;
    }

    /// <summary>
    /// 成功結果を作成する。
    /// </summary>
    public static SortResult<TNode> Succeeded(IReadOnlyList<TNode> sortedOrder)
    {
        return new SortResult<TNode>(true, sortedOrder, null);
    }

    /// <summary>
    /// 循環検出による失敗結果を作成する。
    /// </summary>
    public static SortResult<TNode> CycleDetected(IReadOnlyList<TNode> cyclePath)
    {
        return new SortResult<TNode>(false, null, cyclePath);
    }
}
