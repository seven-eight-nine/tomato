using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 効果適用リクエスト
    /// </summary>
    internal readonly struct ApplyRequest
    {
        public readonly EffectInstanceId InstanceId;
        public readonly EffectId EffectId;
        public readonly ulong TargetId;
        public readonly StatusEffectDefinition Definition;

        public ApplyRequest(EffectInstanceId instanceId, EffectId effectId, ulong targetId, StatusEffectDefinition definition)
        {
            InstanceId = instanceId;
            EffectId = effectId;
            TargetId = targetId;
            Definition = definition;
        }
    }

    /// <summary>
    /// 効果削除リクエスト
    /// </summary>
    internal readonly struct RemoveRequest
    {
        public readonly EffectInstanceId InstanceId;
        public readonly RemovalReasonId Reason;

        public RemoveRequest(EffectInstanceId instanceId, RemovalReasonId reason)
        {
            InstanceId = instanceId;
            Reason = reason;
        }
    }

    /// <summary>
    /// スタック変更リクエスト
    /// </summary>
    internal readonly struct StackChangeRequest
    {
        public readonly EffectInstanceId InstanceId;
        public readonly int Delta;
        public readonly bool IsAbsolute; // true: SetStacks, false: AddStacks

        public StackChangeRequest(EffectInstanceId instanceId, int delta, bool isAbsolute)
        {
            InstanceId = instanceId;
            Delta = delta;
            IsAbsolute = isAbsolute;
        }
    }

    /// <summary>
    /// 期間延長リクエスト
    /// </summary>
    internal readonly struct ExtendDurationRequest
    {
        public readonly EffectInstanceId InstanceId;
        public readonly TickDuration Extension;

        public ExtendDurationRequest(EffectInstanceId instanceId, TickDuration extension)
        {
            InstanceId = instanceId;
            Extension = extension;
        }
    }

    /// <summary>
    /// フラグ設定リクエスト
    /// </summary>
    internal readonly struct SetFlagRequest
    {
        public readonly EffectInstanceId InstanceId;
        public readonly FlagId Flag;
        public readonly bool Value;

        public SetFlagRequest(EffectInstanceId instanceId, FlagId flag, bool value)
        {
            InstanceId = instanceId;
            Flag = flag;
            Value = value;
        }
    }
}
