using System;

namespace Tomato.HandleSystem;

/// <summary>
/// 生成されるArena型の抽象基底クラス。
/// 共通のプーリングロジックと世代番号ベースのハンドル検証機能を提供します。
///
/// <para>主な機能:</para>
/// <list type="bullet">
///   <item><description>オブジェクトのプール管理（メモリ効率的な再利用）</description></item>
///   <item><description>世代番号によるハンドル無効化（削除済みオブジェクトへのアクセス防止）</description></item>
///   <item><description>スレッドセーフな操作（ロックによる同期）</description></item>
///   <item><description>自動的なプール拡張（容量不足時）</description></item>
/// </list>
/// </summary>
/// <typeparam name="T">管理対象のオブジェクト型（new()制約が必要）</typeparam>
/// <typeparam name="THandle">オブジェクトのハンドル型</typeparam>
public abstract class ArenaBase<T, THandle>
    where T : new()
{
    /// <summary>
    /// スレッドセーフな操作のためのロックオブジェクト。
    /// </summary>
    protected readonly object _lock = new object();

    /// <summary>
    /// オブジェクトがプールから取得された（spawn）際に呼び出されるコールバック。
    /// </summary>
    protected readonly RefAction<T> _onSpawn;

    /// <summary>
    /// オブジェクトがプールに返却された（despawn）際に呼び出されるコールバック。
    /// </summary>
    protected readonly RefAction<T> _onDespawn;

    /// <summary>
    /// プールされたオブジェクトインスタンスの配列。
    /// </summary>
    protected T[] _items;

    /// <summary>
    /// 各スロットの世代カウンター。
    /// </summary>
    protected int[] _generations;

    /// <summary>
    /// 割り当て可能な空きインデックスのスタック。
    /// </summary>
    protected int[] _freeIndices;

    /// <summary>
    /// 空きインデックススタックの要素数。
    /// </summary>
    protected int _freeCount;

    /// <summary>
    /// 現在割り当てられているオブジェクトの数。
    /// </summary>
    protected int _count;

    /// <summary>
    /// ArenaBaseクラスの新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="initialCapacity">オブジェクトプールの初期容量</param>
    /// <param name="onSpawn">オブジェクトがspawn時に呼び出されるコールバック</param>
    /// <param name="onDespawn">オブジェクトがdespawn時に呼び出されるコールバック</param>
    protected ArenaBase(
        int initialCapacity,
        RefAction<T> onSpawn,
        RefAction<T> onDespawn)
    {
        if (initialCapacity <= 0)
        {
            initialCapacity = 1;
        }

        _onSpawn = onSpawn;
        _onDespawn = onDespawn;

        _items = new T[initialCapacity];
        _generations = new int[initialCapacity];
        _freeIndices = new int[initialCapacity];
        _freeCount = 0;
        _count = 0;

        for (int i = 0; i < initialCapacity; i++)
        {
            _items[i] = new T();
            _generations[i] = 1;
        }
    }

    /// <summary>
    /// 指定インデックスのオブジェクトへの参照を検証なしで取得します。
    /// </summary>
    protected ref T GetItemRefUnchecked(int index)
    {
        return ref _items[index];
    }

    /// <summary>
    /// 検証付きでオブジェクト参照を取得します。
    /// </summary>
    protected ref T GetItemRef(int index, int generation, out bool valid)
    {
        if (index < 0 || index >= _items.Length || _generations[index] != generation)
        {
            valid = false;
            return ref _items[0];
        }
        valid = true;
        return ref _items[index];
    }

    /// <summary>
    /// オブジェクトスロットを割り当て、インデックスと世代番号を返します。
    /// </summary>
    protected int AllocateInternal(out int generation)
    {
        int index;

        if (_freeCount > 0)
        {
            _freeCount--;
            index = _freeIndices[_freeCount];
        }
        else if (_count < _items.Length)
        {
            index = _count;
        }
        else
        {
            int oldCapacity = _items.Length;
            int newCapacity = oldCapacity * 2;

            T[] newItems = new T[newCapacity];
            int[] newGenerations = new int[newCapacity];
            int[] newFreeIndices = new int[newCapacity];

            Array.Copy(_items, newItems, _items.Length);
            Array.Copy(_generations, newGenerations, _generations.Length);
            Array.Copy(_freeIndices, newFreeIndices, _freeIndices.Length);

            for (int i = _items.Length; i < newCapacity; i++)
            {
                newItems[i] = new T();
                newGenerations[i] = 1;
            }

            _items = newItems;
            _generations = newGenerations;
            _freeIndices = newFreeIndices;

            OnArrayExpanded(oldCapacity, newCapacity);

            index = _count;
        }

        generation = _generations[index];
        _count++;

        _onSpawn?.Invoke(ref _items[index]);

        return index;
    }

    /// <summary>
    /// 世代番号が一致する場合、オブジェクトスロットを解放します。
    /// </summary>
    protected bool DeallocateInternal(int index, int generation)
    {
        if (index < 0 || index >= _items.Length)
        {
            return false;
        }

        if (_generations[index] != generation)
        {
            return false;
        }

        _onDespawn?.Invoke(ref _items[index]);

        _items[index] = new T();

        _generations[index]++;
        if (_generations[index] <= 0)
        {
            _generations[index] = 1;
        }

        _freeIndices[_freeCount] = index;
        _freeCount++;
        _count--;

        return true;
    }

    /// <summary>
    /// インデックスと世代番号を指定してオブジェクトの取得を試みます。
    /// </summary>
    protected ref T TryGetRefInternal(int index, int generation, out bool valid)
    {
        return ref GetItemRef(index, generation, out valid);
    }

    /// <summary>
    /// 配列が拡張されたときに呼び出されます。
    /// </summary>
    protected virtual void OnArrayExpanded(int oldCapacity, int newCapacity)
    {
    }

    /// <summary>
    /// ハンドルが有効かどうかをチェックします。
    /// </summary>
    protected bool IsValidInternal(int index, int generation)
    {
        if (index < 0 || index >= _items.Length)
        {
            return false;
        }

        return _generations[index] == generation;
    }
}
