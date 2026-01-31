using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 条件評価コンテキスト
    /// </summary>
    public interface IConditionContext
    {
        /// <summary>効果の付与者</summary>
        ulong SourceId { get; }

        /// <summary>効果の対象者</summary>
        ulong TargetId { get; }

        /// <summary>現在のティック</summary>
        GameTick CurrentTick { get; }

        /// <summary>現在のスタック数</summary>
        int CurrentStacks { get; }

        /// <summary>拡張データを取得</summary>
        T? GetExtension<T>() where T : class;
    }

    /// <summary>
    /// 条件インターフェース
    /// </summary>
    public interface ICondition
    {
        /// <summary>条件を評価</summary>
        bool Evaluate(IConditionContext context);
    }
}
