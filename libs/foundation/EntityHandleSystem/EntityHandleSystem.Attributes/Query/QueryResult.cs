using System.Collections.Generic;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// クエリの実行結果。
/// </summary>
public sealed class QueryResult
{
    private readonly List<VoidHandle> _handles;

    public QueryResult(List<VoidHandle> handles)
    {
        _handles = handles;
    }

    /// <summary>結果のハンドル一覧</summary>
    public IReadOnlyList<VoidHandle> Handles => _handles;

    /// <summary>結果の件数</summary>
    public int Count => _handles.Count;

    /// <summary>結果が空か</summary>
    public bool IsEmpty => _handles.Count == 0;

    /// <summary>最初の要素を取得（存在しない場合null）</summary>
    public VoidHandle? FirstOrNull()
    {
        return _handles.Count > 0 ? _handles[0] : (VoidHandle?)null;
    }
}
