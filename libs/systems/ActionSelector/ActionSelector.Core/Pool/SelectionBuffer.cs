using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;
/// <summary>
/// 選択エンジンの内部バッファ。
///
/// 毎フレームのアロケーションを回避するため、配列を事前確保して再利用する。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <remarks>
/// パフォーマンス最適化:
/// - 配列は初期化時に1回だけ確保
/// - Reset() でカウンタとフラグのみクリア（配列はクリアしない）
/// - ulong ビットフラグで最大64カテゴリをサポート
///
/// メモリ使用量（デフォルト設定）:
/// - Candidates: 256 * (参照 + ActionPriority) ≈ 4KB
/// - RequestedActions: カテゴリ数 * 参照 ≈ 40B
/// - 合計: 約5KB
/// </remarks>
internal struct SelectionBuffer<TCategory> where TCategory : struct, Enum
{
    // ===========================================
    // フィールド
    // ===========================================

    /// <summary>
    /// 候補リスト。優先度計算済みのジャッジメントを格納。
    /// </summary>
    public (IActionJudgment<TCategory, InputState, GameState> judgment, ActionPriority priority)[] Candidates;

    /// <summary>
    /// 候補の数。
    /// </summary>
    public int CandidateCount;

    /// <summary>
    /// 埋まったカテゴリのビットフラグ。
    /// カテゴリの enum 値をビット位置として使用。
    /// </summary>
    public ulong FilledCategories;

    /// <summary>
    /// カテゴリごとのリクエストされたアクション。
    /// </summary>
    public IActionJudgment<TCategory, InputState, GameState>?[] RequestedActions;

    // ===========================================
    // ファクトリ
    // ===========================================

    /// <summary>
    /// バッファを生成する。
    /// </summary>
    /// <param name="maxJudgments">最大ジャッジメント数（デフォルト: 256）</param>
    /// <returns>初期化済みバッファ</returns>
    public static SelectionBuffer<TCategory> Create(int maxJudgments = 256)
    {
        var categoryValues = (TCategory[])Enum.GetValues(typeof(TCategory));
        return new SelectionBuffer<TCategory>
        {
            Candidates = new (IActionJudgment<TCategory, InputState, GameState>, ActionPriority)[maxJudgments],
            RequestedActions = new IActionJudgment<TCategory, InputState, GameState>[categoryValues.Length]
        };
    }

    // ===========================================
    // 操作
    // ===========================================

    /// <summary>
    /// バッファをリセットする。
    /// </summary>
    /// <remarks>
    /// 配列はクリアせず、カウンタとフラグのみリセット。
    /// RequestedActions は null クリアが必要（前フレームの結果が残るため）。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        CandidateCount = 0;
        FilledCategories = 0;
        Array.Clear(RequestedActions, 0, RequestedActions.Length);
    }

    /// <summary>
    /// 候補を追加する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddCandidate(IActionJudgment<TCategory, InputState, GameState> judgment, ActionPriority priority)
    {
        if (CandidateCount < Candidates.Length)
        {
            Candidates[CandidateCount++] = (judgment, priority);
        }
    }

    /// <summary>
    /// カテゴリが埋まっているかチェックする。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCategoryFilled(int categoryIndex)
    {
        return (FilledCategories & (1UL << categoryIndex)) != 0;
    }

    /// <summary>
    /// カテゴリを埋める。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillCategory(int categoryIndex)
    {
        FilledCategories |= (1UL << categoryIndex);
    }

    /// <summary>
    /// リクエストされたアクションを設定する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetRequested(int categoryIndex, IActionJudgment<TCategory, InputState, GameState> judgment)
    {
        RequestedActions[categoryIndex] = judgment;
        FillCategory(categoryIndex);
    }
}

/// <summary>
/// 軽量オブジェクトプール。
///
/// 頻繁に生成・破棄されるオブジェクトを再利用する。
/// </summary>
/// <typeparam name="T">プールするオブジェクトの型</typeparam>
/// <remarks>
/// パフォーマンス最適化:
/// - ロックなし（シングルスレッド専用）
/// - 固定サイズ配列（リサイズなし）
/// - AggressiveInlining で呼び出しオーバーヘッド削減
///
/// 使用例:
/// <code>
/// var pool = new ObjectPool&lt;MyObject&gt;(() => new MyObject());
/// var obj = pool.Rent();
/// // 使用後
/// pool.Return(obj);
/// </code>
/// </remarks>
public sealed class ObjectPool<T> where T : class
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly Func<T> _factory;
    private readonly T?[] _items;
    private int _count;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// オブジェクトプールを生成する。
    /// </summary>
    /// <param name="factory">オブジェクト生成関数</param>
    /// <param name="capacity">プール容量（デフォルト: 4）</param>
    public ObjectPool(Func<T> factory, int capacity = 4)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _items = new T[capacity];
    }

    // ===========================================
    // 操作
    // ===========================================

    /// <summary>
    /// オブジェクトを借りる。
    /// </summary>
    /// <returns>借りたオブジェクト</returns>
    /// <remarks>
    /// プールにあれば再利用、なければ新規生成。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (_count > 0)
        {
            var item = _items[--_count];
            _items[_count] = null;
            return item!;
        }
        return _factory();
    }

    /// <summary>
    /// オブジェクトを返却する。
    /// </summary>
    /// <param name="item">返却するオブジェクト</param>
    /// <remarks>
    /// プールに空きがあれば格納、なければ破棄。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (_count < _items.Length)
        {
            _items[_count++] = item;
        }
    }

    /// <summary>
    /// プール内のオブジェクト数。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// プールの容量。
    /// </summary>
    public int Capacity => _items.Length;
}
