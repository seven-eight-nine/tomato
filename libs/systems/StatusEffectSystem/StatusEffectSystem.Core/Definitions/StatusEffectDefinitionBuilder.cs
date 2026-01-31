using System;
using System.Collections.Generic;
using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// StatusEffectDefinitionのビルダー
    /// </summary>
    public sealed class StatusEffectDefinitionBuilder
    {
        private readonly EffectId _id;
        private readonly string _internalName;
        private GroupId _groupId = GroupId.None;
        private TagSet _tags = TagSet.Empty;
        private TickDuration _baseDuration = new(300); // 5秒@60fps
        private bool _isPermanent;
        private StackConfig _stackConfig = StackConfig.Default;
        private ICondition _applyCondition = AlwaysTrueCondition.Instance;
        private ICondition _removeCondition = AlwaysFalseCondition.Instance;
        private FlagSet _initialFlags = FlagSet.Empty;
        private int _priority;
        private Dictionary<Type, object>? _contributors;

        internal StatusEffectDefinitionBuilder(EffectId id, string internalName)
        {
            _id = id;
            _internalName = internalName ?? throw new ArgumentNullException(nameof(internalName));
        }

        public StatusEffectDefinitionBuilder WithGroupId(GroupId groupId)
        {
            _groupId = groupId;
            return this;
        }

        public StatusEffectDefinitionBuilder WithTags(TagSet tags)
        {
            _tags = tags;
            return this;
        }

        public StatusEffectDefinitionBuilder AddTag(TagId tag)
        {
            _tags = _tags.With(tag);
            return this;
        }

        public StatusEffectDefinitionBuilder WithDuration(TickDuration duration)
        {
            _baseDuration = duration;
            _isPermanent = false;
            return this;
        }

        public StatusEffectDefinitionBuilder AsPermanent()
        {
            _isPermanent = true;
            _baseDuration = TickDuration.Infinite;
            return this;
        }

        public StatusEffectDefinitionBuilder WithStackConfig(StackConfig config)
        {
            _stackConfig = config ?? throw new ArgumentNullException(nameof(config));
            return this;
        }

        public StatusEffectDefinitionBuilder WithApplyCondition(ICondition condition)
        {
            _applyCondition = condition ?? AlwaysTrueCondition.Instance;
            return this;
        }

        public StatusEffectDefinitionBuilder WithRemoveCondition(ICondition condition)
        {
            _removeCondition = condition ?? AlwaysFalseCondition.Instance;
            return this;
        }

        public StatusEffectDefinitionBuilder WithInitialFlags(FlagSet flags)
        {
            _initialFlags = flags;
            return this;
        }

        public StatusEffectDefinitionBuilder AddInitialFlag(FlagId flag)
        {
            _initialFlags = _initialFlags.With(flag);
            return this;
        }

        /// <summary>
        /// 効果結果の適用時の優先度を設定する（小さいほど先に適用）
        /// </summary>
        /// <param name="priority">優先度（デフォルト: 0）</param>
        public StatusEffectDefinitionBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// 結果コントリビュータを登録する
        /// </summary>
        /// <typeparam name="TResult">結果の型</typeparam>
        /// <param name="contributor">コントリビュータデリゲート</param>
        public StatusEffectDefinitionBuilder WithContributor<TResult>(ResultContributor<TResult> contributor)
            where TResult : struct
        {
            if (contributor == null)
                throw new ArgumentNullException(nameof(contributor));

            _contributors ??= new Dictionary<Type, object>();
            _contributors[typeof(TResult)] = contributor;
            return this;
        }

        public StatusEffectDefinition Build()
        {
            return new StatusEffectDefinition(
                _id,
                _internalName,
                _groupId,
                _tags,
                _baseDuration,
                _isPermanent,
                _stackConfig,
                _applyCondition,
                _removeCondition,
                _initialFlags,
                _priority,
                _contributors);
        }
    }
}
