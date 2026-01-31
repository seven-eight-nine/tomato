using System;
using Tomato.Time;

namespace Tomato.ActionSelector;

// ActionSelectorの内部型定義。
// 継承することで、これらの型に短い名前でアクセスできる。
public partial class ActionSelector<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    // ===========================================
    // 内部型定義
    // ===========================================

    /// <summary>
    /// ジャッジメント。アクションの成立条件を定義する。
    /// </summary>
    public class Judgment : SimpleJudgment<TCategory, TInput, TContext>
    {
        public Judgment(
            string label,
            TCategory category,
            IInputTrigger<TInput>? input,
            ICondition<TContext>? condition,
            ActionPriority priority,
            string[]? tags = null,
            IActionResolver<TInput, TContext>? resolver = null)
            : base(label, category, input, condition, priority, tags, resolver)
        {
        }

        public Judgment(
            string label,
            TCategory category,
            IInputTrigger<TInput>? input,
            ICondition<TContext>? condition,
            Func<FrameState<TInput, TContext>, ActionPriority> dynamicPriority,
            string[]? tags = null,
            IActionResolver<TInput, TContext>? resolver = null)
            : base(label, category, input, condition, dynamicPriority, tags, resolver)
        {
        }
    }

    /// <summary>
    /// Fluent APIでジャッジメントを構築するビルダー。
    /// </summary>
    public class Builder : JudgmentBuilder<TCategory, TInput, TContext>
    {
        public static new Builder Begin() => new Builder();
    }

    /// <summary>
    /// ジャッジメントのリスト。
    /// </summary>
    public class List : JudgmentList<TCategory, TInput, TContext>
    {
    }

    /// <summary>
    /// 選択結果。
    /// </summary>
    public class Result : SelectionResult<TCategory, TInput, TContext>
    {
    }

    /// <summary>
    /// 1フレームの状態。
    /// </summary>
    public readonly struct Frame
    {
        public readonly TInput Input;
        public readonly TContext Context;
        public readonly int DeltaTicks;
        public readonly GameTick CurrentTick;

        public Frame(
            TInput input,
            TContext context,
            int deltaTicks = 1,
            GameTick currentTick = default)
        {
            Input = input;
            Context = context;
            DeltaTicks = deltaTicks;
            CurrentTick = currentTick;
        }

        /// <summary>
        /// FrameState への暗黙変換。
        /// </summary>
        public static implicit operator FrameState<TInput, TContext>(Frame frame)
            => new FrameState<TInput, TContext>(
                frame.Input,
                frame.Context,
                frame.DeltaTicks,
                frame.CurrentTick);
    }

    // ===========================================
    // カテゴリルール（静的プロパティ）
    // ===========================================

    /// <summary>
    /// 排他性なしルール（全カテゴリ独立）。
    /// </summary>
    public static NoExclusivityRules<TCategory> NoExclusivity
        => NoExclusivityRules<TCategory>.Instance;

    /// <summary>
    /// 完全排他ルール（1つのアクションのみ）。
    /// </summary>
    public static FullExclusivityRules<TCategory> FullExclusivity
        => FullExclusivityRules<TCategory>.Instance;
}
