using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// 生成されるArena型の抽象基底クラス。
/// 共通のプーリングロジックと世代番号ベースのハンドル検証機能を提供します。
///
/// <para>主な機能:</para>
/// <list type="bullet">
///   <item><description>エンティティのプール管理（メモリ効率的な再利用）</description></item>
///   <item><description>世代番号によるハンドル無効化（削除済みエンティティへのアクセス防止）</description></item>
///   <item><description>スレッドセーフな操作（ロックによる同期）</description></item>
///   <item><description>自動的なプール拡張（容量不足時）</description></item>
/// </list>
///
/// <para>世代番号の仕組み:</para>
/// <list type="number">
///   <item><description>各スロットに世代番号を割り当て（初期値は1）</description></item>
///   <item><description>ハンドル作成時にスロットのインデックスと世代番号を記録</description></item>
///   <item><description>エンティティ削除時に世代番号をインクリメント</description></item>
///   <item><description>ハンドル使用時に世代番号を比較（一致しなければ無効）</description></item>
/// </list>
///
/// <remarks>
/// このクラスは Source Generator によって継承されます。
/// 直接使用することはありません。
/// </remarks>
/// </summary>
/// <typeparam name="TEntity">管理対象のエンティティ型（new()制約が必要）</typeparam>
/// <typeparam name="THandle">エンティティのハンドル型</typeparam>
public abstract class EntityArenaBase<TEntity, THandle>
    where TEntity : new()
{
    /// <summary>
    /// スレッドセーフな操作のためのロックオブジェクト。
    /// すべての公開メソッドはこのロックを使用して同期されます。
    /// </summary>
    protected readonly object _lock = new object();

    /// <summary>
    /// エンティティがプールから取得された（spawn）際に呼び出されるコールバック。
    /// エンティティの初期化処理を実装できます。
    /// ref パラメータにより、struct エンティティでも直接変更可能です。
    /// </summary>
    protected readonly RefAction<TEntity> _onSpawn;

    /// <summary>
    /// エンティティがプールに返却された（despawn）際に呼び出されるコールバック。
    /// エンティティのクリーンアップ処理を実装できます。
    /// </summary>
    protected readonly RefAction<TEntity> _onDespawn;

    /// <summary>
    /// プールされたエンティティインスタンスの配列。
    /// すべてのインスタンスは事前に作成され、再利用されます。
    /// </summary>
    protected TEntity[] _entities;

    /// <summary>
    /// 各スロットの世代カウンター。
    /// エンティティ削除のたびにインクリメントされ、ハンドルの有効性を検証するために使用されます。
    /// </summary>
    protected int[] _generations;

    /// <summary>
    /// 割り当て可能な空きインデックスのスタック。
    /// 削除されたエンティティのスロットを効率的に再利用します。
    /// </summary>
    protected int[] _freeIndices;

    /// <summary>
    /// 空きインデックススタックの要素数。
    /// _freeIndices配列の有効な要素数を示します。
    /// </summary>
    protected int _freeCount;

    /// <summary>
    /// 現在割り当てられているエンティティの数。
    /// アクティブなエンティティ数を表します。
    /// </summary>
    protected int _count;

    /// <summary>
    /// EntityArenaBaseクラスの新しいインスタンスを初期化します。
    ///
    /// <para>初期化処理:</para>
    /// <list type="number">
    ///   <item><description>指定された容量で配列を確保</description></item>
    ///   <item><description>すべてのエンティティインスタンスを事前作成</description></item>
    ///   <item><description>各スロットの世代番号を1で初期化</description></item>
    /// </list>
    /// </summary>
    /// <param name="initialCapacity">エンティティプールの初期容量（0以下の場合は1に設定）</param>
    /// <param name="onSpawn">エンティティがspawn時に呼び出されるオプションのコールバック</param>
    /// <param name="onDespawn">エンティティがdespawn時に呼び出されるオプションのコールバック</param>
    protected EntityArenaBase(
        int initialCapacity,
        RefAction<TEntity> onSpawn,
        RefAction<TEntity> onDespawn)
    {
        if (initialCapacity <= 0)
        {
            initialCapacity = 1;
        }

        _onSpawn = onSpawn;
        _onDespawn = onDespawn;

        _entities = new TEntity[initialCapacity];
        _generations = new int[initialCapacity];
        _freeIndices = new int[initialCapacity];
        _freeCount = 0;
        _count = 0;

        // Pre-allocate all entity instances
        for (int i = 0; i < initialCapacity; i++)
        {
            _entities[i] = new TEntity();
            _generations[i] = 1; // Start at generation 1 (0 is reserved for invalid)
        }
    }

    /// <summary>
    /// 指定インデックスのエンティティへの参照を検証なしで取得します。
    /// パフォーマンスクリティカルな場面で、事前に有効性を確認済みの場合に使用します。
    /// </summary>
    /// <param name="index">エンティティのインデックス</param>
    /// <returns>エンティティへの参照</returns>
    protected ref TEntity GetEntityRefUnchecked(int index)
    {
        return ref _entities[index];
    }

    /// <summary>
    /// 検証付きでエンティティ参照を取得します。
    /// 世代番号が一致しない場合、validはfalseになります。
    /// </summary>
    /// <param name="index">エンティティのインデックス</param>
    /// <param name="generation">期待される世代番号</param>
    /// <param name="valid">有効な場合はtrue</param>
    /// <returns>エンティティへの参照（無効な場合はインデックス0のダミー参照）</returns>
    protected ref TEntity GetEntityRef(int index, int generation, out bool valid)
    {
        if (index < 0 || index >= _entities.Length || _generations[index] != generation)
        {
            valid = false;
            return ref _entities[0]; // ダミー参照（使用されない）
        }
        valid = true;
        return ref _entities[index];
    }

    /// <summary>
    /// エンティティスロットを割り当て、インデックスと世代番号を返します。
    /// ロック内で呼び出す必要があります。
    ///
    /// <para>割り当てロジック:</para>
    /// <list type="number">
    ///   <item><description>空きスロットがあればそれを再利用</description></item>
    ///   <item><description>未使用スロットがあればそれを使用</description></item>
    ///   <item><description>どちらもなければプールを2倍に拡張</description></item>
    /// </list>
    /// </summary>
    /// <param name="generation">割り当てられたスロットの世代番号</param>
    /// <returns>割り当てられたスロットのインデックス</returns>
    protected int AllocateInternal(out int generation)
    {
        int index;

        if (_freeCount > 0)
        {
            // Reuse a free slot
            _freeCount--;
            index = _freeIndices[_freeCount];
        }
        else if (_count < _entities.Length)
        {
            // Use next unused slot
            index = _count;
        }
        else
        {
            // Need to expand
            int oldCapacity = _entities.Length;
            int newCapacity = oldCapacity * 2;

            TEntity[] newEntities = new TEntity[newCapacity];
            int[] newGenerations = new int[newCapacity];
            int[] newFreeIndices = new int[newCapacity];

            Array.Copy(_entities, newEntities, _entities.Length);
            Array.Copy(_generations, newGenerations, _generations.Length);
            Array.Copy(_freeIndices, newFreeIndices, _freeIndices.Length);

            // Initialize new slots
            for (int i = _entities.Length; i < newCapacity; i++)
            {
                newEntities[i] = new TEntity();
                newGenerations[i] = 1;
            }

            _entities = newEntities;
            _generations = newGenerations;
            _freeIndices = newFreeIndices;

            // Allow derived classes to expand component arrays
            OnArrayExpanded(oldCapacity, newCapacity);

            index = _count;
        }

        generation = _generations[index];
        _count++;

        // Invoke spawn callback with ref
        _onSpawn?.Invoke(ref _entities[index]);

        return index;
    }

    /// <summary>
    /// 世代番号が一致する場合、エンティティスロットを解放します。
    /// ロック内で呼び出す必要があります。
    ///
    /// <para>解放処理:</para>
    /// <list type="number">
    ///   <item><description>世代番号を検証（不一致なら失敗）</description></item>
    ///   <item><description>onDespawnコールバックを呼び出し</description></item>
    ///   <item><description>世代番号をインクリメント（既存ハンドルを無効化）</description></item>
    ///   <item><description>スロットを空きリストに追加</description></item>
    /// </list>
    /// </summary>
    /// <param name="index">解放するスロットのインデックス</param>
    /// <param name="generation">期待される世代番号</param>
    /// <returns>解放に成功した場合はtrue、世代番号が不一致の場合はfalse</returns>
    protected bool DeallocateInternal(int index, int generation)
    {
        if (index < 0 || index >= _entities.Length)
        {
            return false;
        }

        if (_generations[index] != generation)
        {
            return false;
        }

        // Invoke despawn callback with ref
        _onDespawn?.Invoke(ref _entities[index]);

        // Reset entity to default state
        _entities[index] = new TEntity();

        // Increment generation to invalidate existing handles
        _generations[index]++;

        // Handle generation overflow (wrap around, but skip 0)
        if (_generations[index] <= 0)
        {
            _generations[index] = 1;
        }

        // Add to free list
        _freeIndices[_freeCount] = index;
        _freeCount++;
        _count--;

        return true;
    }

    /// <summary>
    /// インデックスと世代番号を指定してエンティティの取得を試みます（ref 版）。
    /// ロック内で呼び出す必要があります。
    /// </summary>
    /// <param name="index">エンティティのインデックス</param>
    /// <param name="generation">期待される世代番号</param>
    /// <param name="valid">有効な場合はtrue</param>
    /// <returns>エンティティへの参照</returns>
    protected ref TEntity TryGetRefInternal(int index, int generation, out bool valid)
    {
        return ref GetEntityRef(index, generation, out valid);
    }

    /// <summary>
    /// 配列が拡張されたときに呼び出されます。
    /// 派生クラスでコンポーネント配列の拡張を実装するためにオーバーライドしてください。
    /// ロック内から呼び出されます。
    /// </summary>
    /// <param name="oldCapacity">拡張前の容量</param>
    /// <param name="newCapacity">拡張後の容量</param>
    protected virtual void OnArrayExpanded(int oldCapacity, int newCapacity)
    {
        // 派生クラスでコンポーネント配列の拡張を実装
    }

    /// <summary>
    /// Checks if a handle with given index and generation is still valid.
    /// Must be called within a lock.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <param name="generation">The generation to check.</param>
    /// <returns>True if the handle is valid.</returns>
    protected bool IsValidInternal(int index, int generation)
    {
        if (index < 0 || index >= _entities.Length)
        {
            return false;
        }

        return _generations[index] == generation;
    }
}
