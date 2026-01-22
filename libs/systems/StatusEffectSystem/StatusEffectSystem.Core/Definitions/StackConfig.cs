using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// スタック設定
    /// </summary>
    public sealed class StackConfig
    {
        /// <summary>最大スタック数（0で無制限）</summary>
        public int MaxStacks { get; }

        /// <summary>スタック合成の振る舞い</summary>
        public IStackBehavior StackBehavior { get; }

        /// <summary>時間更新の振る舞い</summary>
        public IDurationBehavior DurationBehavior { get; }

        /// <summary>スタック源の識別方法</summary>
        public IStackSourceIdentifier SourceIdentifier { get; }

        public StackConfig(
            int maxStacks,
            IStackBehavior stackBehavior,
            IDurationBehavior durationBehavior,
            IStackSourceIdentifier sourceIdentifier)
        {
            if (maxStacks < 0)
                throw new ArgumentOutOfRangeException(nameof(maxStacks));

            MaxStacks = maxStacks;
            StackBehavior = stackBehavior ?? throw new ArgumentNullException(nameof(stackBehavior));
            DurationBehavior = durationBehavior ?? throw new ArgumentNullException(nameof(durationBehavior));
            SourceIdentifier = sourceIdentifier ?? throw new ArgumentNullException(nameof(sourceIdentifier));
        }

        /// <summary>デフォルト設定（最大1スタック、上書き）</summary>
        public static StackConfig Default { get; } = new(
            maxStacks: 1,
            stackBehavior: OverwriteStackBehavior.Instance,
            durationBehavior: RefreshDurationBehavior.Instance,
            sourceIdentifier: AnySourceIdentifier.Instance);

        /// <summary>加算スタック設定</summary>
        public static StackConfig Additive(int maxStacks) => new(
            maxStacks: maxStacks,
            stackBehavior: AdditiveStackBehavior.Instance,
            durationBehavior: RefreshDurationBehavior.Instance,
            sourceIdentifier: AnySourceIdentifier.Instance);

        /// <summary>独立スタック設定（ソースごとに別インスタンス）</summary>
        public static StackConfig Independent { get; } = new(
            maxStacks: 0,
            stackBehavior: IndependentStackBehavior.Instance,
            durationBehavior: RefreshDurationBehavior.Instance,
            sourceIdentifier: PerSourceIdentifier.Instance);
    }
}
