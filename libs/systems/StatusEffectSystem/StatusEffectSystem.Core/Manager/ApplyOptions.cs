using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 付与オプション
    /// </summary>
    public sealed class ApplyOptions
    {
        /// <summary>初期スタック数（デフォルト: 1）</summary>
        public int InitialStacks { get; set; } = 1;

        /// <summary>持続時間のオーバーライド（nullで定義のデフォルト）</summary>
        public TickDuration? Duration { get; set; }

        /// <summary>初期フラグのオーバーライド（nullで定義のデフォルト）</summary>
        public FlagSet? InitialFlags { get; set; }

        public static ApplyOptions Default { get; } = new();
    }

    /// <summary>
    /// 付与結果
    /// </summary>
    public readonly struct ApplyResult
    {
        public readonly bool Success;
        public readonly EffectInstanceId InstanceId;
        public readonly FailureReasonId FailureReason;
        public readonly bool WasMerged;

        private ApplyResult(bool success, EffectInstanceId instanceId, FailureReasonId failureReason, bool wasMerged)
        {
            Success = success;
            InstanceId = instanceId;
            FailureReason = failureReason;
            WasMerged = wasMerged;
        }

        public static ApplyResult Succeeded(EffectInstanceId instanceId, bool wasMerged = false)
            => new(true, instanceId, default, wasMerged);

        public static ApplyResult Failed(FailureReasonId reason)
            => new(false, EffectInstanceId.Invalid, reason, false);
    }
}
