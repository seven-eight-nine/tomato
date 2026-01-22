using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 状態異常定義（マスタデータ）
    /// </summary>
    public sealed class StatusEffectDefinition
    {
        /// <summary>定義ID</summary>
        public EffectId Id { get; }

        /// <summary>内部名（デバッグ用）</summary>
        public string InternalName { get; }

        /// <summary>排他グループID</summary>
        public GroupId GroupId { get; }

        /// <summary>タグセット</summary>
        public TagSet Tags { get; }

        /// <summary>基本持続時間</summary>
        public TickDuration BaseDuration { get; }

        /// <summary>永続フラグ</summary>
        public bool IsPermanent { get; }

        /// <summary>スタック設定</summary>
        public StackConfig StackConfig { get; }

        /// <summary>付与条件</summary>
        public ICondition ApplyCondition { get; }

        /// <summary>除去条件（trueで自動除去）</summary>
        public ICondition RemoveCondition { get; }

        /// <summary>初期フラグセット</summary>
        public FlagSet InitialFlags { get; }

        /// <summary>
        /// 効果結果の適用時の優先度（小さいほど先に適用）
        /// 同じ優先度の場合はEffectId（定義順）で決まる
        /// </summary>
        public int Priority { get; }

        // 結果コントリビュータ（型→デリゲートのマップ）
        private readonly Dictionary<Type, object> _contributors;

        internal StatusEffectDefinition(
            EffectId id,
            string internalName,
            GroupId groupId,
            TagSet tags,
            TickDuration baseDuration,
            bool isPermanent,
            StackConfig stackConfig,
            ICondition applyCondition,
            ICondition removeCondition,
            FlagSet initialFlags,
            int priority,
            Dictionary<Type, object>? contributors)
        {
            Id = id;
            InternalName = internalName ?? throw new ArgumentNullException(nameof(internalName));
            GroupId = groupId;
            Tags = tags;
            BaseDuration = baseDuration;
            IsPermanent = isPermanent;
            StackConfig = stackConfig ?? throw new ArgumentNullException(nameof(stackConfig));
            ApplyCondition = applyCondition ?? AlwaysTrueCondition.Instance;
            RemoveCondition = removeCondition ?? AlwaysFalseCondition.Instance;
            InitialFlags = initialFlags;
            Priority = priority;
            _contributors = contributors ?? new Dictionary<Type, object>();
        }

        /// <summary>
        /// 指定した型の結果コントリビュータを取得する
        /// </summary>
        public ResultContributor<TResult>? GetContributor<TResult>() where TResult : struct
        {
            return _contributors.TryGetValue(typeof(TResult), out var contributor)
                ? (ResultContributor<TResult>)contributor
                : null;
        }

        /// <summary>
        /// 効果結果に適用する
        /// </summary>
        public void ApplyToResult<TResult>(ref TResult result, int stacks) where TResult : struct
        {
            var contributor = GetContributor<TResult>();
            contributor?.Invoke(ref result, stacks);
        }
    }
}
