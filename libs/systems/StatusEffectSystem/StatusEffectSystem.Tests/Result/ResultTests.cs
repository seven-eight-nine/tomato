using Tomato.Time;
using Xunit;

namespace Tomato.StatusEffectSystem.Tests
{
    /// <summary>
    /// テスト用の結果構造体
    /// </summary>
    public struct TestResult
    {
        public int AttackFlatBonus;
        public int AttackPercentBonus;  // 100 = 100%
        public int DefenseFlatBonus;
        public bool IsStunned;
        public bool IsSilenced;
    }

    public class ResultTests
    {
        private readonly TagRegistry _tagRegistry;
        private readonly EffectRegistry _effectRegistry;
        private readonly EffectManager _manager;

        public ResultTests()
        {
            _tagRegistry = new TagRegistry();
            _effectRegistry = new EffectRegistry();
            _manager = new EffectManager(_effectRegistry, _tagRegistry);
        }

        private void Tick(long tickValue = 0)
        {
            _manager.ProcessTick(new GameTick(tickValue));
        }

        [Fact]
        public void CalculateResult_WithNoEffects_ReturnsInitial()
        {
            var initial = new TestResult
            {
                AttackFlatBonus = 0,
                AttackPercentBonus = 0,
                IsStunned = false
            };

            var result = _manager.CalculateResult(targetId: 1, initial);

            Assert.Equal(0, result.AttackFlatBonus);
            Assert.Equal(0, result.AttackPercentBonus);
            Assert.False(result.IsStunned);
        }

        [Fact]
        public void CalculateResult_WithSingleEffect_AppliesContributor()
        {
            var attackUp = _effectRegistry.Register("AttackUp", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.AttackPercentBonus += 10 * stacks;
                }));

            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            Tick();

            var result = _manager.CalculateResult<TestResult>(1, default);

            Assert.Equal(10, result.AttackPercentBonus);
        }

        [Fact]
        public void CalculateResult_WithMultipleStacks_StacksAffectResult()
        {
            var attackUp = _effectRegistry.Register("AttackUp", b => b
                .WithDuration(new TickDuration(100))
                .WithStackConfig(StackConfig.Additive(5))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.AttackPercentBonus += 10 * stacks;
                }));

            // 3回適用 → 3スタック
            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            Tick();

            var result = _manager.CalculateResult<TestResult>(1, default);

            Assert.Equal(30, result.AttackPercentBonus);  // 10 * 3 stacks
        }

        [Fact]
        public void CalculateResult_WithMultipleEffects_CombinesAll()
        {
            var attackUp = _effectRegistry.Register("AttackUp", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.AttackPercentBonus += 20;
                }));

            var defenseUp = _effectRegistry.Register("DefenseUp", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.DefenseFlatBonus += 50;
                }));

            var stun = _effectRegistry.Register("Stun", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.IsStunned = true;
                }));

            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: defenseUp, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: stun, sourceId: 0);
            Tick();

            var result = _manager.CalculateResult<TestResult>(1, default);

            Assert.Equal(20, result.AttackPercentBonus);
            Assert.Equal(50, result.DefenseFlatBonus);
            Assert.True(result.IsStunned);
        }

        [Fact]
        public void CalculateResult_WithInitialValue_StartsFromInitial()
        {
            var attackUp = _effectRegistry.Register("AttackUp", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.AttackFlatBonus += 10;
                }));

            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            Tick();

            var initial = new TestResult { AttackFlatBonus = 100 };
            var result = _manager.CalculateResult(1, initial);

            Assert.Equal(110, result.AttackFlatBonus);  // 100 (initial) + 10 (effect)
        }

        [Fact]
        public void CalculateResult_AfterEffectRemoved_DoesNotIncludeRemovedEffect()
        {
            var attackUp = _effectRegistry.Register("AttackUp", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.AttackPercentBonus += 20;
                }));

            var applyResult = _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            Tick();

            // 効果がある状態
            var beforeRemoval = _manager.CalculateResult<TestResult>(1, default);
            Assert.Equal(20, beforeRemoval.AttackPercentBonus);

            // 効果を削除
            _manager.Remove(applyResult.InstanceId, new RemovalReasonId(1));
            Tick();

            // 削除後
            var afterRemoval = _manager.CalculateResult<TestResult>(1, default);
            Assert.Equal(0, afterRemoval.AttackPercentBonus);
        }

        [Fact]
        public void CalculateResult_WithoutContributor_SkipsEffect()
        {
            // コントリビュータなしの効果
            var effectWithoutContributor = _effectRegistry.Register("NoContributor", b => b
                .WithDuration(new TickDuration(100)));

            // コントリビュータありの効果
            var effectWithContributor = _effectRegistry.Register("WithContributor", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.AttackFlatBonus += 100;
                }));

            _manager.TryApply(targetId: 1, effectId: effectWithoutContributor, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: effectWithContributor, sourceId: 0);
            Tick();

            var result = _manager.CalculateResult<TestResult>(1, default);

            // コントリビュータありの効果だけ適用される
            Assert.Equal(100, result.AttackFlatBonus);
        }

        [Fact]
        public void CalculateResult_DifferentTargets_AreIndependent()
        {
            var attackUp = _effectRegistry.Register("AttackUp", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.AttackPercentBonus += 25;
                }));

            // Target 1 に2回適用
            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: attackUp, sourceId: 0);

            // Target 2 に1回適用
            _manager.TryApply(targetId: 2, effectId: attackUp, sourceId: 0);
            Tick();

            var result1 = _manager.CalculateResult<TestResult>(1, default);
            var result2 = _manager.CalculateResult<TestResult>(2, default);

            // Non-stacking effect, so each is 25
            Assert.Equal(25, result1.AttackPercentBonus);
            Assert.Equal(25, result2.AttackPercentBonus);
        }

        [Fact]
        public void CalculateResult_CanCompareWithPrevious_ForTriggerDetection()
        {
            var stun = _effectRegistry.Register("Stun", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<TestResult>((ref TestResult s, int stacks) =>
                {
                    s.IsStunned = true;
                }));

            // フレーム1: スタンなし
            var prevResult = _manager.CalculateResult<TestResult>(1, default);
            Assert.False(prevResult.IsStunned);

            // スタン付与
            _manager.TryApply(targetId: 1, effectId: stun, sourceId: 0);
            Tick();

            // フレーム2: スタンあり
            var currResult = _manager.CalculateResult<TestResult>(1, default);
            Assert.True(currResult.IsStunned);

            // 差分検出（スタン開始トリガー）
            bool stunStarted = !prevResult.IsStunned && currResult.IsStunned;
            Assert.True(stunStarted);
        }

        #region Priority Tests

        /// <summary>
        /// 適用順を記録するための結果
        /// </summary>
        public struct OrderTrackingResult
        {
            public string ApplyOrder;

            public void Append(string name)
            {
                ApplyOrder = string.IsNullOrEmpty(ApplyOrder) ? name : ApplyOrder + "," + name;
            }
        }

        [Fact]
        public void CalculateResult_SortsBy_Priority_ThenByEffectId()
        {
            // 優先度: 高い数値 = 後で適用
            // effectC: priority=0, effectB: priority=100, effectA: priority=200
            // 同じ優先度ならEffectId順

            var effectA = _effectRegistry.Register("EffectA", b => b
                .WithDuration(new TickDuration(100))
                .WithPriority(200)
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("A");
                }));

            var effectB = _effectRegistry.Register("EffectB", b => b
                .WithDuration(new TickDuration(100))
                .WithPriority(100)
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("B");
                }));

            var effectC = _effectRegistry.Register("EffectC", b => b
                .WithDuration(new TickDuration(100))
                .WithPriority(0)
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("C");
                }));

            // 登録とは逆順で適用（A, B, C）
            _manager.TryApply(targetId: 1, effectId: effectA, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: effectB, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: effectC, sourceId: 0);
            Tick();

            var result = _manager.CalculateResult<OrderTrackingResult>(1, default);

            // Priority順: C(0) → B(100) → A(200)
            Assert.Equal("C,B,A", result.ApplyOrder);
        }

        [Fact]
        public void CalculateResult_SamePriority_SortsByEffectId()
        {
            // 全て同じ優先度（デフォルト=0）、EffectId（定義順）でソートされる

            var effectFirst = _effectRegistry.Register("First", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("First");
                }));

            var effectSecond = _effectRegistry.Register("Second", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("Second");
                }));

            var effectThird = _effectRegistry.Register("Third", b => b
                .WithDuration(new TickDuration(100))
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("Third");
                }));

            // 逆順で適用
            _manager.TryApply(targetId: 1, effectId: effectThird, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: effectFirst, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: effectSecond, sourceId: 0);
            Tick();

            var result = _manager.CalculateResult<OrderTrackingResult>(1, default);

            // EffectId（定義順）: First → Second → Third
            Assert.Equal("First,Second,Third", result.ApplyOrder);
        }

        [Fact]
        public void CalculateResult_IsDeterministic_RegardlessOfApplicationOrder()
        {
            // 適用順が異なっても、結果は常に同じになることを確認

            var effectA = _effectRegistry.Register("A", b => b
                .WithDuration(new TickDuration(100))
                .WithPriority(10)
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("A");
                }));

            var effectB = _effectRegistry.Register("B", b => b
                .WithDuration(new TickDuration(100))
                .WithPriority(10)
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("B");
                }));

            var effectC = _effectRegistry.Register("C", b => b
                .WithDuration(new TickDuration(100))
                .WithPriority(5)
                .WithContributor<OrderTrackingResult>((ref OrderTrackingResult s, int stacks) =>
                {
                    s.Append("C");
                }));

            // Target 1: A → B → C の順で適用
            _manager.TryApply(targetId: 1, effectId: effectA, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: effectB, sourceId: 0);
            _manager.TryApply(targetId: 1, effectId: effectC, sourceId: 0);

            // Target 2: C → B → A の順で適用
            _manager.TryApply(targetId: 2, effectId: effectC, sourceId: 0);
            _manager.TryApply(targetId: 2, effectId: effectB, sourceId: 0);
            _manager.TryApply(targetId: 2, effectId: effectA, sourceId: 0);
            Tick();

            var result1 = _manager.CalculateResult<OrderTrackingResult>(1, default);
            var result2 = _manager.CalculateResult<OrderTrackingResult>(2, default);

            // 適用順に関係なく、同じ結果になる
            // Priority: C(5) → A(10) → B(10)、同じPriorityはEffectId順（A < B）
            Assert.Equal("C,A,B", result1.ApplyOrder);
            Assert.Equal("C,A,B", result2.ApplyOrder);
        }

        #endregion
    }
}
