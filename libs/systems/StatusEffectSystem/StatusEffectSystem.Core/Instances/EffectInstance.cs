using System.Collections.Generic;
using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 状態異常の実行時インスタンス
    /// </summary>
    public sealed class EffectInstance
    {
        /// <summary>インスタンスID</summary>
        public EffectInstanceId InstanceId { get; }

        /// <summary>定義ID</summary>
        public EffectId DefinitionId { get; }

        /// <summary>所有者エンティティID</summary>
        public ulong OwnerId { get; }

        /// <summary>付与者エンティティID</summary>
        public ulong SourceId { get; }

        /// <summary>付与されたティック</summary>
        public GameTick AppliedAt { get; }

        /// <summary>期限切れティック</summary>
        public GameTick ExpiresAt { get; internal set; }

        /// <summary>最後にティック処理されたティック</summary>
        public GameTick LastTickedAt { get; internal set; }

        /// <summary>現在のスタック数</summary>
        public int CurrentStacks { get; internal set; }

        /// <summary>フラグセット</summary>
        public FlagSet Flags { get; internal set; }

        /// <summary>有効フラグ</summary>
        public bool IsActive { get; internal set; }

        /// <summary>スナップショット値</summary>
        public IReadOnlyDictionary<string, FixedPoint>? Snapshot { get; }

        internal EffectInstance(
            EffectInstanceId instanceId,
            EffectId definitionId,
            ulong ownerId,
            ulong sourceId,
            GameTick appliedAt,
            GameTick expiresAt,
            int initialStacks,
            FlagSet initialFlags,
            IReadOnlyDictionary<string, FixedPoint>? snapshot)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            OwnerId = ownerId;
            SourceId = sourceId;
            AppliedAt = appliedAt;
            ExpiresAt = expiresAt;
            LastTickedAt = appliedAt;
            CurrentStacks = initialStacks;
            Flags = initialFlags;
            IsActive = true;
            Snapshot = snapshot;
        }

        /// <summary>残り時間を取得</summary>
        public TickDuration GetRemainingDuration(GameTick currentTick)
        {
            if (ExpiresAt == GameTick.MaxValue) return TickDuration.Infinite;
            return ExpiresAt - currentTick;
        }

        /// <summary>期限切れか判定</summary>
        public bool IsExpired(GameTick currentTick) => currentTick >= ExpiresAt;
    }
}
