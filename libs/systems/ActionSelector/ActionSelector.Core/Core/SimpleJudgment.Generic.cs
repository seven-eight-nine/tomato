using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector
{
    /// <summary>
    /// シンプルな汎用ジャッジメント実装。
    /// </summary>
    /// <typeparam name="TCategory">カテゴリのenum型</typeparam>
    /// <typeparam name="TInput">入力状態の型</typeparam>
    /// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
    public class SimpleJudgment<TCategory, TInput, TContext>
        : IControllableJudgment<TCategory, TInput, TContext>
        where TCategory : struct, Enum
    {
        private readonly string _label;
        private readonly TCategory _category;
        private readonly IInputTrigger<TInput>? _input;
        private readonly ICondition<TContext>? _condition;
        private readonly ActionPriority _priority;
        private readonly Func<FrameState<TInput, TContext>, ActionPriority>? _dynamicPriority;
        private readonly string[] _tags;
        private readonly IActionResolver<TInput, TContext>? _resolver;
        private bool _isForcedInput;

        /// <summary>
        /// 固定優先度を持つジャッジメントを生成する。
        /// </summary>
        public SimpleJudgment(
            string label,
            TCategory category,
            IInputTrigger<TInput>? input,
            ICondition<TContext>? condition,
            ActionPriority priority,
            string[]? tags = null,
            IActionResolver<TInput, TContext>? resolver = null)
        {
            _label = label ?? throw new ArgumentNullException(nameof(label));
            _category = category;
            _input = input;
            _condition = condition;
            _priority = priority;
            _dynamicPriority = null;
            _tags = tags ?? Array.Empty<string>();
            _resolver = resolver;
        }

        /// <summary>
        /// 動的優先度を持つジャッジメントを生成する。
        /// </summary>
        public SimpleJudgment(
            string label,
            TCategory category,
            IInputTrigger<TInput>? input,
            ICondition<TContext>? condition,
            Func<FrameState<TInput, TContext>, ActionPriority> dynamicPriority,
            string[]? tags = null,
            IActionResolver<TInput, TContext>? resolver = null)
        {
            _label = label ?? throw new ArgumentNullException(nameof(label));
            _category = category;
            _input = input;
            _condition = condition;
            _priority = ActionPriority.Normal;
            _dynamicPriority = dynamicPriority ?? throw new ArgumentNullException(nameof(dynamicPriority));
            _tags = tags ?? Array.Empty<string>();
            _resolver = resolver;
        }

        // IActionJudgment implementation
        public string Label => _label;
        public TCategory Category => _category;
        public IInputTrigger<TInput>? Input => _input;
        public ICondition<TContext>? Condition => _condition;
        public IActionResolver<TInput, TContext>? Resolver => _resolver;
        public ReadOnlySpan<string> Tags => _tags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActionPriority GetPriority(in FrameState<TInput, TContext> state)
            => _dynamicPriority != null ? _dynamicPriority(state) : _priority;

        // IControllableJudgment implementation
        public bool IsForcedInput => _isForcedInput;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForceInput() => _isForcedInput = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearForceInput() => _isForcedInput = false;

        public void ResetInput()
        {
            _input?.OnJudgmentStop();
            _input?.OnJudgmentStart();
        }

        public override string ToString() => $"Judgment[{_label}]";
    }
}
