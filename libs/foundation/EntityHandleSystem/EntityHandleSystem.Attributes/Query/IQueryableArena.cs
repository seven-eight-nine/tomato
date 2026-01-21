using System;
using System.Collections.Generic;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// クエリ可能なArenaのインターフェース。
/// </summary>
public interface IQueryableArena
{
    /// <summary>Arena の型</summary>
    Type ArenaType { get; }

    /// <summary>Arena インスタンス</summary>
    object Arena { get; }

    /// <summary>有効なEntityを列挙</summary>
    IEnumerable<(AnyHandle Handle, int Index)> EnumerateActive();

    /// <summary>指定インデックスのEntityが有効か確認</summary>
    bool IsActive(int index);

    /// <summary>有効なEntityの数</summary>
    int ActiveCount { get; }
}
