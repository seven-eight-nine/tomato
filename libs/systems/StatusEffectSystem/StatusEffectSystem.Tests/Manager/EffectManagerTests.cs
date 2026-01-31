using System.Linq;
using Tomato.Time;
using Xunit;

namespace Tomato.StatusEffectSystem.Tests
{
    public class EffectManagerTests
    {
        private readonly TagRegistry _tagRegistry;
        private readonly EffectRegistry _effectRegistry;
        private readonly EffectManager _manager;

        public EffectManagerTests()
        {
            _tagRegistry = new TagRegistry();
            _effectRegistry = new EffectRegistry();
            _manager = new EffectManager(_effectRegistry, _tagRegistry);
        }

        private void Tick(long tickValue = 0)
        {
            _manager.ProcessTick(new GameTick(tickValue));
        }

        #region Apply Tests

        [Fact]
        public void TryApply_WithValidEffect_ShouldSucceed()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            var result = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);

            Assert.True(result.Success);
            Assert.True(result.InstanceId.IsValid);
            Assert.False(result.WasMerged);
        }

        [Fact]
        public void TryApply_WithUnregisteredEffect_ShouldFail()
        {
            var result = _manager.TryApply(targetId: 1, effectId: new EffectId(999), sourceId: 2);

            Assert.False(result.Success);
            Assert.Equal(FailureReasonId.DefinitionNotFound, result.FailureReason.Value);
        }

        [Fact]
        public void TryApply_WithStacking_ShouldMerge()
        {
            var effectId = _effectRegistry.Register("StackingEffect", b => b
                .WithDuration(new TickDuration(100))
                .WithStackConfig(StackConfig.Additive(5)));

            var result1 = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            var result2 = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.True(result2.WasMerged);

            var instance = _manager.GetInstance(result1.InstanceId);
            Assert.NotNull(instance);
            Assert.Equal(2, instance!.CurrentStacks);
        }

        [Fact]
        public void TryApply_WithMaxStacks_ShouldCapStacks()
        {
            var effectId = _effectRegistry.Register("MaxStackEffect", b => b
                .WithDuration(new TickDuration(100))
                .WithStackConfig(StackConfig.Additive(3)));

            for (int i = 0; i < 5; i++)
            {
                _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            }

            Tick(); // ProcessTickでリクエスト処理

            var effects = _manager.GetEffects(1).ToList();
            Assert.Single(effects);
            Assert.Equal(3, effects[0].CurrentStacks);
        }

        #endregion

        #region Remove Tests

        [Fact]
        public void Remove_ShouldRemoveEffect()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            var result = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick(); // 適用を処理

            Assert.True(_manager.HasEffect(1, effectId));

            _manager.Remove(result.InstanceId, new RemovalReasonId(1));
            Tick(); // 削除を処理

            Assert.False(_manager.HasEffect(1, effectId));
        }

        [Fact]
        public void RemoveAll_ShouldRemoveAllEffects()
        {
            var effect1 = _effectRegistry.Register("Effect1", b => b.WithDuration(new TickDuration(100)));
            var effect2 = _effectRegistry.Register("Effect2", b => b.WithDuration(new TickDuration(100)));

            _manager.TryApply(targetId: 1, effectId: effect1, sourceId: 2);
            _manager.TryApply(targetId: 1, effectId: effect2, sourceId: 2);
            Tick();

            _manager.RemoveAll(1, new RemovalReasonId(1));
            Tick();

            Assert.Empty(_manager.GetEffects(1));
        }

        [Fact]
        public void RemoveByTag_ShouldRemoveMatchingEffects()
        {
            var debuffTag = _tagRegistry.Register("debuff");
            var buffTag = _tagRegistry.Register("buff");

            var debuff = _effectRegistry.Register("Debuff", b => b
                .WithDuration(new TickDuration(100))
                .WithTags(_tagRegistry.CreateSet(debuffTag)));
            var buff = _effectRegistry.Register("Buff", b => b
                .WithDuration(new TickDuration(100))
                .WithTags(_tagRegistry.CreateSet(buffTag)));

            _manager.TryApply(targetId: 1, effectId: debuff, sourceId: 2);
            _manager.TryApply(targetId: 1, effectId: buff, sourceId: 2);
            Tick();

            var removed = _manager.RemoveByTag(1, debuffTag, new RemovalReasonId(1));
            Tick();

            Assert.Equal(1, removed);
            Assert.False(_manager.HasEffect(1, debuff));
            Assert.True(_manager.HasEffect(1, buff));
        }

        #endregion

        #region Query Tests

        [Fact]
        public void GetEffects_ShouldReturnAllEffectsForTarget()
        {
            var effect1 = _effectRegistry.Register("Effect1", b => b.WithDuration(new TickDuration(100)));
            var effect2 = _effectRegistry.Register("Effect2", b => b.WithDuration(new TickDuration(100)));

            _manager.TryApply(targetId: 1, effectId: effect1, sourceId: 2);
            _manager.TryApply(targetId: 1, effectId: effect2, sourceId: 2);
            _manager.TryApply(targetId: 2, effectId: effect1, sourceId: 2); // Different target
            Tick();

            var effects = _manager.GetEffects(1).ToList();

            Assert.Equal(2, effects.Count);
        }

        [Fact]
        public void HasEffect_WithExistingEffect_ShouldReturnTrue()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            Assert.True(_manager.HasEffect(1, effectId));
        }

        [Fact]
        public void HasEffect_WithoutEffect_ShouldReturnFalse()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            Assert.False(_manager.HasEffect(1, effectId));
        }

        [Fact]
        public void HasEffectWithTag_ShouldWork()
        {
            var poisonTag = _tagRegistry.Register("poison");
            var effectId = _effectRegistry.Register("Poison", b => b
                .WithDuration(new TickDuration(100))
                .WithTags(_tagRegistry.CreateSet(poisonTag)));

            _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            Assert.True(_manager.HasEffectWithTag(1, poisonTag));
        }

        #endregion

        #region Tick Tests

        [Fact]
        public void ProcessTick_ShouldRemoveExpiredEffects()
        {
            var effectId = _effectRegistry.Register("ShortEffect", b => b
                .WithDuration(new TickDuration(10)));

            _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick(); // 適用を処理
            Assert.True(_manager.HasEffect(1, effectId));

            // Advance past expiration
            _manager.ProcessTick(new GameTick(15));

            Assert.False(_manager.HasEffect(1, effectId));
        }

        [Fact]
        public void ProcessTick_ShouldKeepPermanentEffects()
        {
            var effectId = _effectRegistry.Register("PermanentEffect", b => b.AsPermanent());

            _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            _manager.ProcessTick(new GameTick(1000000));

            Assert.True(_manager.HasEffect(1, effectId));
        }

        #endregion

        #region Modify Tests

        [Fact]
        public void AddStacks_ShouldIncreaseStacks()
        {
            var effectId = _effectRegistry.Register("StackEffect", b => b
                .WithDuration(new TickDuration(100))
                .WithStackConfig(StackConfig.Additive(10)));

            var result = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            _manager.AddStacks(result.InstanceId, 3);
            Tick();

            var instance = _manager.GetInstance(result.InstanceId);
            Assert.Equal(4, instance!.CurrentStacks);
        }

        [Fact]
        public void AddStacks_ToZero_ShouldRemoveEffect()
        {
            var effectId = _effectRegistry.Register("StackEffect", b => b
                .WithDuration(new TickDuration(100))
                .WithStackConfig(StackConfig.Additive(10)));

            var result = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            _manager.AddStacks(result.InstanceId, -1);
            Tick();

            Assert.False(_manager.HasEffect(1, effectId));
        }

        [Fact]
        public void ExtendDuration_ShouldIncreaseDuration()
        {
            var effectId = _effectRegistry.Register("ShortEffect", b => b
                .WithDuration(new TickDuration(100)));

            var result = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            var instanceBefore = _manager.GetInstance(result.InstanceId);
            var expiryBefore = instanceBefore!.ExpiresAt;

            _manager.ExtendDuration(result.InstanceId, new TickDuration(50));
            Tick();

            var instanceAfter = _manager.GetInstance(result.InstanceId);
            Assert.Equal(expiryBefore.Value + 50, instanceAfter!.ExpiresAt.Value);
        }

        #endregion

        #region Event Tests

        [Fact]
        public void TryApply_ShouldRaiseOnEffectApplied()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            EffectAppliedEvent? receivedEvent = null;
            _manager.OnEffectApplied += e => receivedEvent = e;

            _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick(); // イベントはProcessTickで発火

            Assert.NotNull(receivedEvent);
            Assert.Equal(effectId, receivedEvent!.Value.DefinitionId);
            Assert.Equal(1UL, receivedEvent.Value.TargetId);
            Assert.Equal(2UL, receivedEvent.Value.SourceId);
        }

        [Fact]
        public void Remove_ShouldRaiseOnEffectRemoved()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            EffectRemovedEvent? receivedEvent = null;
            _manager.OnEffectRemoved += e => receivedEvent = e;

            var result = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            _manager.Remove(result.InstanceId, new RemovalReasonId(42));
            Tick();

            Assert.NotNull(receivedEvent);
            Assert.Equal(effectId, receivedEvent!.Value.DefinitionId);
            Assert.Equal(42, receivedEvent.Value.Reason.Value);
        }

        #endregion

        #region Deferred Execution Tests

        [Fact]
        public void TryApply_BeforeProcessTick_EffectNotVisibleInQueries()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);

            // ProcessTickを呼ぶ前はHasEffectがfalse
            Assert.False(_manager.HasEffect(1, effectId));
            Assert.Empty(_manager.GetEffects(1));

            Tick();

            // ProcessTick後はtrueになる
            Assert.True(_manager.HasEffect(1, effectId));
        }

        [Fact]
        public void Remove_BeforeProcessTick_EffectStillVisible()
        {
            var effectId = _effectRegistry.Register("TestEffect", b => b
                .WithDuration(new TickDuration(100)));

            var result = _manager.TryApply(targetId: 1, effectId: effectId, sourceId: 2);
            Tick();

            Assert.True(_manager.HasEffect(1, effectId));

            _manager.Remove(result.InstanceId, new RemovalReasonId(1));

            // ProcessTickを呼ぶ前はまだ存在する
            Assert.True(_manager.HasEffect(1, effectId));

            Tick();

            // ProcessTick後は削除される
            Assert.False(_manager.HasEffect(1, effectId));
        }

        #endregion
    }
}
