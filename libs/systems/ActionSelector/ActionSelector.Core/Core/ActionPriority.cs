using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// ジャッジメントの優先順位を表す値型。
///
/// 小さい値ほど高優先度。Layer → Group → Detail の順で比較される。
///
/// <example>
/// 優先度の例:
/// - (0, 0, 0): 緊急回避など最優先アクション
/// - (0, 1, 0): ジャストガードなど高優先アクション
/// - (1, 0, 0): 通常攻撃などの標準アクション
/// - (2, 0, 0): 待機など低優先アクション
/// </example>
/// </summary>
/// <remarks>
/// パフォーマンス最適化:
/// - readonly struct による値渡し（ヒープアロケーション回避）
/// - IEquatable&lt;T&gt; による boxing 回避
/// - AggressiveInlining による呼び出しオーバーヘッド削減
/// </remarks>
public readonly struct ActionPriority : IComparable<ActionPriority>, IEquatable<ActionPriority>
{
    // ===========================================
    // 定数・静的フィールド
    // ===========================================

    /// <summary>
    /// 無効化された優先度。この優先度を持つジャッジメントは評価から除外される。
    /// </summary>
    public static readonly ActionPriority Disabled = new(int.MaxValue, int.MaxValue, int.MaxValue);

    /// <summary>
    /// 最優先。緊急回避、無敵技、割り込み不可アクションなど。
    /// Layer=0
    /// </summary>
    public static readonly ActionPriority Highest = new(0, 0, 0);

    /// <summary>
    /// 高優先。ジャストガード、カウンター、特殊技など。
    /// Layer=0, Group=1
    /// </summary>
    public static readonly ActionPriority High = new(0, 1, 0);

    /// <summary>
    /// 通常優先。標準的な攻撃・移動アクション。
    /// Layer=1
    /// </summary>
    public static readonly ActionPriority Normal = new(1, 0, 0);

    /// <summary>
    /// 低優先。待機、歩行など。
    /// Layer=2
    /// </summary>
    public static readonly ActionPriority Low = new(2, 0, 0);

    /// <summary>
    /// 最低優先。デフォルト待機、何も入力がない時のアクション。
    /// Layer=3
    /// </summary>
    public static readonly ActionPriority Lowest = new(3, 0, 0);

    // ===========================================
    // フィールド
    // ===========================================

    /// <summary>
    /// 大分類。緊急回避=0, 通常=1, 低優先=2 など。
    /// 最も優先される比較基準。
    /// </summary>
    public readonly int Layer;

    /// <summary>
    /// 中分類。同一Layer内でのアクション種別ごとの順位。
    /// </summary>
    public readonly int Group;

    /// <summary>
    /// 小分類。同一Layer・Group内での微調整用。
    /// </summary>
    public readonly int Detail;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// 優先度を生成する。
    /// </summary>
    /// <param name="layer">大分類（0が最高優先）</param>
    /// <param name="group">中分類</param>
    /// <param name="detail">小分類（デフォルト: 0）</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionPriority(int layer, int group, int detail = 0)
    {
        Layer = layer;
        Group = group;
        Detail = detail;
    }

    // ===========================================
    // プロパティ
    // ===========================================

    /// <summary>
    /// この優先度が無効化されているかどうか。
    /// </summary>
    public bool IsDisabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Layer == int.MaxValue && Group == int.MaxValue && Detail == int.MaxValue;
    }

    // ===========================================
    // 比較
    // ===========================================

    /// <summary>
    /// 優先度を比較する。小さい方が高優先度。
    /// </summary>
    /// <returns>
    /// 負: this が高優先度
    /// 0: 同一優先度
    /// 正: other が高優先度
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ActionPriority other)
    {
        // Layer が最優先
        var layerCmp = Layer.CompareTo(other.Layer);
        if (layerCmp != 0) return layerCmp;

        // 次に Group
        var groupCmp = Group.CompareTo(other.Group);
        if (groupCmp != 0) return groupCmp;

        // 最後に Detail
        return Detail.CompareTo(other.Detail);
    }

    // ===========================================
    // 等価性
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ActionPriority other)
    {
        return Layer == other.Layer && Group == other.Group && Detail == other.Detail;
    }

    public override bool Equals(object? obj)
    {
        return obj is ActionPriority other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        // 高速なハッシュ計算
        return Layer ^ (Group << 10) ^ (Detail << 20);
    }

    // ===========================================
    // 演算子
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ActionPriority left, ActionPriority right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ActionPriority left, ActionPriority right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ActionPriority left, ActionPriority right) => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ActionPriority left, ActionPriority right) => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ActionPriority left, ActionPriority right) => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ActionPriority left, ActionPriority right) => left.CompareTo(right) >= 0;

    // ===========================================
    // ファクトリメソッド
    // ===========================================

    /// <summary>
    /// 指定レベルの優先度を生成する。
    /// </summary>
    /// <param name="level">0=Highest, 1=High, 2=Normal, 3=Low, 4=Lowest</param>
    /// <param name="subPriority">同一レベル内での優先度（0が最高）</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ActionPriority FromLevel(int level, int subPriority = 0)
    {
        return level switch
        {
            0 => new ActionPriority(0, subPriority, 0),
            1 => new ActionPriority(0, 1 + subPriority, 0),
            2 => new ActionPriority(1, subPriority, 0),
            3 => new ActionPriority(2, subPriority, 0),
            _ => new ActionPriority(3, subPriority, 0),
        };
    }

    /// <summary>
    /// この優先度より1段階低い優先度を返す。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionPriority Lower() => new(Layer, Group, Detail + 1);

    /// <summary>
    /// この優先度より1段階高い優先度を返す。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionPriority Higher() => new(Layer, Group, Detail > 0 ? Detail - 1 : 0);

    /// <summary>
    /// Group を指定して新しい優先度を返す。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionPriority WithGroup(int group) => new(Layer, group, Detail);

    /// <summary>
    /// Detail を指定して新しい優先度を返す。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionPriority WithDetail(int detail) => new(Layer, Group, detail);

    // ===========================================
    // 文字列表現
    // ===========================================

    public override string ToString()
    {
        if (IsDisabled) return "Disabled";
        if (this == Highest) return "Highest";
        if (this == High) return "High";
        if (this == Normal) return "Normal";
        if (this == Low) return "Low";
        if (this == Lowest) return "Lowest";
        return $"({Layer},{Group},{Detail})";
    }
}
