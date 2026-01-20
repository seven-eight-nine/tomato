using System.Collections.Generic;

namespace Tomato.SystemPipeline;

/// <summary>
/// システムグループのインターフェース。
/// 複数の IExecutable（システムまたはグループ）を管理・実行します。
/// </summary>
public interface ISystemGroup : IExecutable
{
    /// <summary>
    /// グループ内の要素数。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 要素を末尾に追加します。
    /// </summary>
    void Add(IExecutable item);

    /// <summary>
    /// 指定した位置に要素を挿入します。
    /// </summary>
    void Insert(int index, IExecutable item);

    /// <summary>
    /// 要素を削除します。
    /// </summary>
    bool Remove(IExecutable item);

    /// <summary>
    /// 指定したインデックスの要素を取得します。
    /// </summary>
    IExecutable this[int index] { get; }

    /// <summary>
    /// 全要素を列挙します。
    /// </summary>
    IEnumerable<IExecutable> Items { get; }
}
