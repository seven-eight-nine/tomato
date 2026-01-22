namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 効果コンテキスト（IConditionContextを拡張）
    /// </summary>
    public interface IEffectContext : IConditionContext
    {
        /// <summary>効果インスタンス</summary>
        EffectInstance Instance { get; }

        /// <summary>効果定義</summary>
        StatusEffectDefinition Definition { get; }

        /// <summary>スナップショット値を取得</summary>
        bool TryGetSnapshot(string key, out FixedPoint value);
    }

    /// <summary>
    /// 値ソースインターフェース
    /// </summary>
    public interface IValueSource
    {
        /// <summary>値を評価</summary>
        FixedPoint Evaluate(IEffectContext context);
    }
}
