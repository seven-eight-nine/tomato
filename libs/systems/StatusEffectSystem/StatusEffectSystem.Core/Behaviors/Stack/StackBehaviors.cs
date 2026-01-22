using System;

namespace Tomato.StatusEffectSystem
{
    /// <summary>加算スタック</summary>
    public sealed class AdditiveStackBehavior : IStackBehavior
    {
        public static readonly AdditiveStackBehavior Instance = new();
        private AdditiveStackBehavior() { }

        public StackMergeResult Merge(in StackMergeContext context)
        {
            int newStacks = context.ExistingInstance.CurrentStacks + context.IncomingStacks;
            if (context.MaxStacks > 0)
                newStacks = Math.Min(newStacks, context.MaxStacks);
            return StackMergeResult.Update(newStacks);
        }
    }

    /// <summary>独立スタック（別インスタンス）</summary>
    public sealed class IndependentStackBehavior : IStackBehavior
    {
        public static readonly IndependentStackBehavior Instance = new();
        private IndependentStackBehavior() { }

        public StackMergeResult Merge(in StackMergeContext context)
            => StackMergeResult.CreateNew(context.IncomingStacks);
    }

    /// <summary>最大値のみ維持</summary>
    public sealed class HighestOnlyStackBehavior : IStackBehavior
    {
        public static readonly HighestOnlyStackBehavior Instance = new();
        private HighestOnlyStackBehavior() { }

        public StackMergeResult Merge(in StackMergeContext context)
        {
            if (context.IncomingStacks > context.ExistingInstance.CurrentStacks)
                return StackMergeResult.Update(context.IncomingStacks);
            return StackMergeResult.Reject();
        }
    }

    /// <summary>上書き（既存を置き換え）</summary>
    public sealed class OverwriteStackBehavior : IStackBehavior
    {
        public static readonly OverwriteStackBehavior Instance = new();
        private OverwriteStackBehavior() { }

        public StackMergeResult Merge(in StackMergeContext context)
            => StackMergeResult.Update(context.IncomingStacks);
    }
}
