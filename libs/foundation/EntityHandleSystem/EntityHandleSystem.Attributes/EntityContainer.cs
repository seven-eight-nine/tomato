using System;
using System.Collections.Generic;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// エンティティハンドルを効率的にイテレートするためのコンテナ。
///
/// <para>特徴:</para>
/// <list type="bullet">
///   <item><description>無効になったスロットの自動再利用</description></item>
///   <item><description>フレーム分散更新をサポートするイテレータ</description></item>
///   <item><description>インデックスアクセスによるランダムアクセス</description></item>
/// </list>
///
/// <example>
/// <code>
/// var bossContainer = new EntityContainer&lt;BossHandle&gt;();
/// bossContainer.Add(boss1);
/// bossContainer.Add(boss2);
///
/// // 全てイテレート
/// var iterator = bossContainer.GetIterator();
/// while (iterator.MoveNext())
/// {
///     iterator.Current.Update();
/// }
///
/// // 2フレームに分散して更新
/// // Frame 0: skip=1, offset=0
/// // Frame 1: skip=1, offset=1
/// var frameIterator = bossContainer.GetIterator(skip: 1, offset: frameIndex % 2);
/// while (frameIterator.MoveNext())
/// {
///     frameIterator.Current.Update();
/// }
/// </code>
/// </example>
/// </summary>
/// <typeparam name="THandle">IEntityHandleを実装する構造体ハンドル型</typeparam>
public sealed class EntityContainer<THandle> where THandle : struct, IEntityHandle
{
    private readonly List<THandle> _handles;
    private int _freeHint;

    /// <summary>
    /// コンテナ内のスロット数を取得します。
    /// 無効なスロットも含みます。
    /// </summary>
    public int Capacity => _handles.Count;

    /// <summary>
    /// 指定したインデックスのハンドルを取得します。
    /// </summary>
    /// <param name="index">インデックス</param>
    /// <returns>ハンドル</returns>
    public THandle this[int index] => _handles[index];

    /// <summary>
    /// 新しいEntityContainerを作成します。
    /// </summary>
    public EntityContainer()
    {
        _handles = new List<THandle>();
        _freeHint = 0;
    }

    /// <summary>
    /// 新しいEntityContainerを指定した初期容量で作成します。
    /// </summary>
    /// <param name="initialCapacity">内部リストの初期容量</param>
    public EntityContainer(int initialCapacity)
    {
        _handles = new List<THandle>(initialCapacity);
        _freeHint = 0;
    }

    /// <summary>
    /// ハンドルをコンテナに追加します。
    /// 無効なスロットがあれば再利用し、なければ末尾に追加します。
    /// </summary>
    /// <param name="handle">追加するハンドル</param>
    public void Add(THandle handle)
    {
        // freeHintから探索開始
        for (int i = _freeHint; i < _handles.Count; i++)
        {
            if (!_handles[i].IsValid)
            {
                _handles[i] = handle;
                _freeHint = i + 1;
                return;
            }
        }

        // 先頭からfreeHintまでも探索
        for (int i = 0; i < _freeHint && i < _handles.Count; i++)
        {
            if (!_handles[i].IsValid)
            {
                _handles[i] = handle;
                _freeHint = i + 1;
                return;
            }
        }

        // 見つからなければ末尾に追加
        _handles.Add(handle);
        _freeHint = _handles.Count;
    }

    /// <summary>
    /// コンテナをクリアします。
    /// </summary>
    public void Clear()
    {
        _handles.Clear();
        _freeHint = 0;
    }

    /// <summary>
    /// イテレータを取得します。
    /// </summary>
    /// <param name="skip">スキップするスロット数（0で全て、1で1つおき）</param>
    /// <param name="offset">開始オフセット</param>
    /// <returns>イテレータ</returns>
    public Iterator GetIterator(int skip = 0, int offset = 0)
    {
        return new Iterator(this, skip, offset);
    }

    /// <summary>
    /// 内部のfreeHintを更新します。
    /// イテレータから呼び出されます。
    /// </summary>
    internal void UpdateFreeHint(int index)
    {
        if (index < _freeHint)
        {
            _freeHint = index;
        }
    }

    /// <summary>
    /// フレーム分散更新をサポートするイテレータ。
    /// 無効なハンドルは自動的にスキップされます。
    /// </summary>
    public struct Iterator
    {
        private readonly EntityContainer<THandle> _container;
        private readonly int _step;
        private int _index;

        internal Iterator(EntityContainer<THandle> container, int skip, int offset)
        {
            _container = container;
            _step = skip + 1;
            _index = offset - _step; // MoveNextで最初の位置に移動
        }

        /// <summary>
        /// 現在のハンドルを取得します。
        /// </summary>
        public THandle Current => _container._handles[_index];

        /// <summary>
        /// 次の有効なハンドルに移動します。
        /// </summary>
        /// <returns>有効なハンドルがあればtrue</returns>
        public bool MoveNext()
        {
            while (true)
            {
                _index += _step;
                if (_index >= _container._handles.Count)
                {
                    return false;
                }

                if (_container._handles[_index].IsValid)
                {
                    return true;
                }

                // 無効なスロットを発見、freeHintを更新
                _container.UpdateFreeHint(_index);
            }
        }
    }
}
