namespace Tomato.ActionSelector;

/// <summary>
/// 優先度の短縮名。
///
/// static using と組み合わせて使用する:
/// <code>
/// using static Tomato.ActionSelector.Priorities;
///
/// var judgment = new SimpleJudgment&lt;MyCategory&gt;(
///     actionId: "Attack",
///     category: MyCategory.FullBody,
///     trigger: Triggers.Press(ButtonType.Attack),
///     priority: Normal);
/// </code>
/// </summary>
public static class Priorities
{
    /// <summary>
    /// 最優先。緊急回避、無敵技、割り込み不可アクションなど。
    /// </summary>
    public static ActionPriority Highest => ActionPriority.Highest;

    /// <summary>
    /// 高優先。ジャストガード、カウンター、特殊技など。
    /// </summary>
    public static ActionPriority High => ActionPriority.High;

    /// <summary>
    /// 通常優先。標準的な攻撃・移動アクション。
    /// </summary>
    public static ActionPriority Normal => ActionPriority.Normal;

    /// <summary>
    /// 低優先。待機、歩行など。
    /// </summary>
    public static ActionPriority Low => ActionPriority.Low;

    /// <summary>
    /// 最低優先。デフォルト待機、何も入力がない時のアクション。
    /// </summary>
    public static ActionPriority Lowest => ActionPriority.Lowest;

    /// <summary>
    /// 無効化。このジャッジメントは評価から除外される。
    /// </summary>
    public static ActionPriority Disabled => ActionPriority.Disabled;

    /// <summary>
    /// カスタム優先度を生成。
    /// </summary>
    /// <param name="layer">大分類（0が最高優先）</param>
    /// <param name="group">中分類</param>
    /// <param name="detail">小分類（デフォルト: 0）</param>
    public static ActionPriority Custom(int layer, int group, int detail = 0)
        => new ActionPriority(layer, group, detail);
}

/// <summary>
/// 優先度の超短縮名。
/// </summary>
public static class Pri
{
    public static ActionPriority Max => ActionPriority.Highest;
    public static ActionPriority Hi => ActionPriority.High;
    public static ActionPriority Norm => ActionPriority.Normal;
    public static ActionPriority Lo => ActionPriority.Low;
    public static ActionPriority Min => ActionPriority.Lowest;
    public static ActionPriority Off => ActionPriority.Disabled;
}
