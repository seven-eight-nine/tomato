using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>効果付与イベント</summary>
    public readonly struct EffectAppliedEvent
    {
        public readonly EffectInstanceId InstanceId;
        public readonly EffectId DefinitionId;
        public readonly ulong TargetId;
        public readonly ulong SourceId;
        public readonly GameTick AppliedAt;
        public readonly int InitialStacks;
        public readonly bool WasMerged;

        public EffectAppliedEvent(
            EffectInstanceId instanceId,
            EffectId definitionId,
            ulong targetId,
            ulong sourceId,
            GameTick appliedAt,
            int initialStacks,
            bool wasMerged)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            TargetId = targetId;
            SourceId = sourceId;
            AppliedAt = appliedAt;
            InitialStacks = initialStacks;
            WasMerged = wasMerged;
        }
    }

    /// <summary>効果除去イベント</summary>
    public readonly struct EffectRemovedEvent
    {
        public readonly EffectInstanceId InstanceId;
        public readonly EffectId DefinitionId;
        public readonly ulong TargetId;
        public readonly RemovalReasonId Reason;
        public readonly GameTick RemovedAt;
        public readonly int FinalStacks;

        public EffectRemovedEvent(
            EffectInstanceId instanceId,
            EffectId definitionId,
            ulong targetId,
            RemovalReasonId reason,
            GameTick removedAt,
            int finalStacks)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            TargetId = targetId;
            Reason = reason;
            RemovedAt = removedAt;
            FinalStacks = finalStacks;
        }
    }

    /// <summary>スタック変更イベント</summary>
    public readonly struct StackChangedEvent
    {
        public readonly EffectInstanceId InstanceId;
        public readonly int OldStacks;
        public readonly int NewStacks;
        public readonly GameTick ChangedAt;

        public StackChangedEvent(
            EffectInstanceId instanceId,
            int oldStacks,
            int newStacks,
            GameTick changedAt)
        {
            InstanceId = instanceId;
            OldStacks = oldStacks;
            NewStacks = newStacks;
            ChangedAt = changedAt;
        }
    }

    /// <summary>効果ティックイベント</summary>
    public readonly struct EffectTickedEvent
    {
        public readonly EffectInstanceId InstanceId;
        public readonly GameTick TickedAt;

        public EffectTickedEvent(EffectInstanceId instanceId, GameTick tickedAt)
        {
            InstanceId = instanceId;
            TickedAt = tickedAt;
        }
    }
}
