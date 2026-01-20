using System;

namespace Tomato.ActionSelector
{
    /// <summary>
    /// 評価結果の種類。
    /// </summary>
    public enum EvaluationOutcome
    {
        /// <summary>選択された。</summary>
        Selected,
        /// <summary>優先度が無効（Disabled）だった。</summary>
        Disabled,
        /// <summary>入力が発火しなかった。</summary>
        InputNotFired,
        /// <summary>条件が不成立だった。</summary>
        ConditionFailed,
        /// <summary>同一カテゴリに既に選択があった。</summary>
        CategoryOccupied,
        /// <summary>排他カテゴリとの競合があった。</summary>
        ExclusivityConflict,
        /// <summary>Resolverが該当なし（None）を返した。</summary>
        ResolverRejected,
    }

    /// <summary>
    /// ジャッジメントの評価結果。
    /// </summary>
    /// <typeparam name="TCategory">カテゴリのenum型</typeparam>
    public readonly struct JudgmentEvaluation<TCategory>
        where TCategory : struct, Enum
    {
        /// <summary>ジャッジメントのラベル。</summary>
        public readonly string Label;

        /// <summary>カテゴリ。</summary>
        public readonly TCategory Category;

        /// <summary>評価時の優先度。</summary>
        public readonly ActionPriority Priority;

        /// <summary>評価結果。</summary>
        public readonly EvaluationOutcome Outcome;

        public JudgmentEvaluation(
            string label,
            TCategory category,
            ActionPriority priority,
            EvaluationOutcome outcome)
        {
            Label = label;
            Category = category;
            Priority = priority;
            Outcome = outcome;
        }

        public override string ToString() =>
            $"{Label} ({Category}): {Outcome} [Priority={Priority}]";
    }
}
