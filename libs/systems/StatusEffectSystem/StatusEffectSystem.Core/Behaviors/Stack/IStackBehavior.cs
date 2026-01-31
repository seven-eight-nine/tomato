using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// スタック合成時のコンテキスト
    /// </summary>
    public readonly struct StackMergeContext
    {
        public readonly EffectInstance ExistingInstance;
        public readonly int IncomingStacks;
        public readonly int MaxStacks;
        public readonly ulong IncomingSourceId;
        public readonly GameTick CurrentTick;

        public StackMergeContext(
            EffectInstance existingInstance,
            int incomingStacks,
            int maxStacks,
            ulong incomingSourceId,
            GameTick currentTick)
        {
            ExistingInstance = existingInstance;
            IncomingStacks = incomingStacks;
            MaxStacks = maxStacks;
            IncomingSourceId = incomingSourceId;
            CurrentTick = currentTick;
        }
    }

    /// <summary>
    /// スタック合成の結果
    /// </summary>
    public readonly struct StackMergeResult
    {
        public readonly int NewStackCount;
        public readonly StackMergeAction Action;

        private StackMergeResult(int newStackCount, StackMergeAction action)
        {
            NewStackCount = newStackCount;
            Action = action;
        }

        public static StackMergeResult Update(int newStacks)
            => new(newStacks, StackMergeAction.UpdateExisting);

        public static StackMergeResult CreateNew(int stacks)
            => new(stacks, StackMergeAction.CreateNew);

        public static StackMergeResult Reject()
            => new(0, StackMergeAction.Reject);
    }

    public enum StackMergeAction
    {
        UpdateExisting,
        CreateNew,
        Reject
    }

    /// <summary>
    /// スタック合成の振る舞い
    /// </summary>
    public interface IStackBehavior
    {
        StackMergeResult Merge(in StackMergeContext context);
    }
}
