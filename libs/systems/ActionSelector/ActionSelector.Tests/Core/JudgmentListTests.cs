using System;
using Xunit;
using static Tomato.ActionSelector.Priorities;
using Tomato.ActionSelector.Tests;

namespace Tomato.ActionSelector.Tests.Core
{
    public enum ListTestCategory
    {
        FullBody,
        Upper,
        Lower
    }

    /// <summary>
    /// JudgmentListのテスト。
    /// </summary>
    public class JudgmentListTests
    {
        private static readonly FrameState<InputState, GameState> DefaultFrame =
            new(InputState.Empty, GameStateExtensions.DefaultGrounded);
        // ===========================================
        // 基本的なAdd/Removeテスト
        // ===========================================

        [Fact]
        public void Add_SingleJudgment_AddsToList()
        {
            var judgment = CreateJudgment("Attack", Normal);
            var list = new JudgmentList<ListTestCategory>()
                .Add(judgment);

            Assert.Equal(1, list.Count);
            Assert.Same(judgment, list[0].Judgment);
            Assert.Null(list[0].OverridePriority);
        }

        [Fact]
        public void Add_WithOverridePriority_SetsOverride()
        {
            var judgment = CreateJudgment("Attack", Normal);
            var list = new JudgmentList<ListTestCategory>()
                .Add(judgment, Highest);

            Assert.Equal(1, list.Count);
            Assert.Same(judgment, list[0].Judgment);
            Assert.Equal(Highest, list[0].OverridePriority);
        }

        [Fact]
        public void Add_MultipleJudgments_MethodChain()
        {
            var attack = CreateJudgment("Attack", Normal);
            var jump = CreateJudgment("Jump", High);
            var idle = CreateJudgment("Idle", Lowest);

            var list = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .Add(jump, Highest)
                .Add(idle);

            Assert.Equal(3, list.Count);
            Assert.Same(attack, list[0].Judgment);
            Assert.Same(jump, list[1].Judgment);
            Assert.Same(idle, list[2].Judgment);
            Assert.Null(list[0].OverridePriority);
            Assert.Equal(Highest, list[1].OverridePriority);
            Assert.Null(list[2].OverridePriority);
        }

        [Fact]
        public void AddRange_AddsFromAnotherList()
        {
            var attack = CreateJudgment("Attack", Normal);
            var jump = CreateJudgment("Jump", High);

            var source = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .Add(jump, Highest);  // 優先度上書きあり

            var list = new JudgmentList<ListTestCategory>()
                .AddRange(source);

            Assert.Equal(2, list.Count);
            Assert.Same(attack, list[0].Judgment);
            Assert.Null(list[0].OverridePriority);
            Assert.Same(jump, list[1].Judgment);
            Assert.Equal(Highest, list[1].OverridePriority);  // 優先度上書きが引き継がれる
        }

        [Fact]
        public void Remove_ExistingJudgment_ReturnsTrue()
        {
            var attack = CreateJudgment("Attack", Normal);
            var jump = CreateJudgment("Jump", High);

            var list = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .Add(jump);

            var result = list.Remove(attack);

            Assert.True(result);
            Assert.Equal(1, list.Count);
            Assert.Same(jump, list[0].Judgment);
        }

        [Fact]
        public void Remove_NonExistingJudgment_ReturnsFalse()
        {
            var attack = CreateJudgment("Attack", Normal);
            var jump = CreateJudgment("Jump", High);

            var list = new JudgmentList<ListTestCategory>()
                .Add(attack);

            var result = list.Remove(jump);

            Assert.False(result);
            Assert.Equal(1, list.Count);
        }

        [Fact]
        public void Clear_RemovesAllEntries()
        {
            var list = new JudgmentList<ListTestCategory>()
                .Add(CreateJudgment("Attack", Normal))
                .Add(CreateJudgment("Jump", High))
                .Clear();

            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void Contains_ExistingJudgment_ReturnsTrue()
        {
            var attack = CreateJudgment("Attack", Normal);
            var list = new JudgmentList<ListTestCategory>()
                .Add(attack);

            Assert.True(list.Contains(attack));
        }

        [Fact]
        public void Contains_NonExistingJudgment_ReturnsFalse()
        {
            var attack = CreateJudgment("Attack", Normal);
            var jump = CreateJudgment("Jump", High);
            var list = new JudgmentList<ListTestCategory>()
                .Add(attack);

            Assert.False(list.Contains(jump));
        }

        // ===========================================
        // SetPriority/ClearPriority テスト
        // ===========================================

        [Fact]
        public void SetPriority_ExistingJudgment_UpdatesOverride()
        {
            var attack = CreateJudgment("Attack", Normal);
            var list = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .SetPriority(attack, Highest);

            Assert.Equal(1, list.Count);
            Assert.Equal(Highest, list[0].OverridePriority);
        }

        [Fact]
        public void SetPriority_NonExistingJudgment_AddsWithOverride()
        {
            var attack = CreateJudgment("Attack", Normal);
            var jump = CreateJudgment("Jump", High);

            var list = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .SetPriority(jump, Highest);

            Assert.Equal(2, list.Count);
            Assert.Same(jump, list[1].Judgment);
            Assert.Equal(Highest, list[1].OverridePriority);
        }

        [Fact]
        public void ClearPriority_RemovesOverride()
        {
            var attack = CreateJudgment("Attack", Normal);
            var list = new JudgmentList<ListTestCategory>()
                .Add(attack, Highest)
                .ClearPriority(attack);

            Assert.Equal(1, list.Count);
            Assert.Null(list[0].OverridePriority);
        }

        // ===========================================
        // GetEffectivePriority テスト
        // ===========================================

        [Fact]
        public void GetEffectivePriority_NoOverride_ReturnsJudgmentPriority()
        {
            var attack = CreateJudgment("Attack", Normal);
            var entry = new JudgmentEntry<ListTestCategory, InputState, GameState>(attack);

            var priority = entry.GetEffectivePriority(in DefaultFrame);

            Assert.Equal(Normal, priority);
        }

        [Fact]
        public void GetEffectivePriority_WithOverride_ReturnsOverridePriority()
        {
            var attack = CreateJudgment("Attack", Normal);
            var entry = new JudgmentEntry<ListTestCategory, InputState, GameState>(attack, Highest);

            var priority = entry.GetEffectivePriority(in DefaultFrame);

            Assert.Equal(Highest, priority);
        }

        // ===========================================
        // ActionSelector統合テスト
        // ===========================================

        [Fact]
        public void ProcessFrame_JudgmentList_Works()
        {
            var attack = CreateJudgment("Attack", Normal);
            var idle = CreateJudgment("Idle", Lowest);

            var list = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .Add(idle);

            var engine = new ActionSelector<ListTestCategory>();
            var result = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);

            // トリガーなしは常に成立、優先度が高いAttackが選択される
            Assert.True(result.TryGetRequested(ListTestCategory.FullBody, out var selected));
            Assert.Equal("Attack", selected.Label);
        }

        [Fact]
        public void ProcessFrame_WithOverridePriority_UsesOverride()
        {
            // 通常はIdleがLowestで最低優先度
            var attack = CreateJudgment("Attack", Normal);
            var idle = CreateJudgment("Idle", Lowest);

            // IdleをHighestに上書き
            var list = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .Add(idle, Highest);

            var engine = new ActionSelector<ListTestCategory>();
            var result = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);

            // Idleが優先度上書きでHighestになったので、Idleが先に評価され選択される
            Assert.True(result.TryGetRequested(ListTestCategory.FullBody, out var selected));
            Assert.Equal("Idle", selected.Label);
        }

        [Fact]
        public void ProcessFrame_OverridePriorityCanDisable()
        {
            var attack = CreateJudgment("Attack", Normal);
            var idle = CreateJudgment("Idle", Lowest);

            // AttackをDisabledに上書き
            var list = new JudgmentList<ListTestCategory>()
                .Add(attack, ActionPriority.Disabled)
                .Add(idle);

            var engine = new ActionSelector<ListTestCategory>();
            var result = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);

            // AttackはDisabledなので候補にならず、Idleが選択される
            Assert.True(result.TryGetRequested(ListTestCategory.FullBody, out var selected));
            Assert.Equal("Idle", selected.Label);
        }

        [Fact]
        public void ProcessFrame_ClearAndReuse_Works()
        {
            var attack = CreateJudgment("Attack", Normal);
            var idle = CreateJudgment("Idle", Lowest);
            var jump = CreateJudgment("Jump", High);

            var list = new JudgmentList<ListTestCategory>();
            var engine = new ActionSelector<ListTestCategory>();

            // 1回目: Attackが優先度高いので選択
            list.Add(attack).Add(idle);
            var result1 = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);
            Assert.True(result1.TryGetRequested(ListTestCategory.FullBody, out var s1));
            Assert.Equal("Attack", s1.Label);

            // クリアして再利用: Jumpが優先度高いので選択
            list.Clear().Add(jump).Add(idle);
            var result2 = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);
            Assert.True(result2.TryGetRequested(ListTestCategory.FullBody, out var s2));
            Assert.Equal("Jump", s2.Label);
        }

        [Fact]
        public void ProcessFrame_SetPriorityDynamically_Works()
        {
            var attack = CreateJudgment("Attack", Normal);
            var idle = CreateJudgment("Idle", Lowest);

            var list = new JudgmentList<ListTestCategory>()
                .Add(attack)
                .Add(idle);

            var engine = new ActionSelector<ListTestCategory>();

            // 1回目: 通常の優先度でAttackが選択
            var result1 = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);
            Assert.True(result1.TryGetRequested(ListTestCategory.FullBody, out var s1));
            Assert.Equal("Attack", s1.Label);

            // IdleをHighestに上書き → Idleが選択
            list.SetPriority(idle, Highest);
            var result2 = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);
            Assert.True(result2.TryGetRequested(ListTestCategory.FullBody, out var s2));
            Assert.Equal("Idle", s2.Label);

            // 優先度をクリア（元に戻す） → 再びAttackが選択
            list.ClearPriority(idle);
            var result3 = engine.ProcessFrame(list, in GameStateExtensions.DefaultGrounded);
            Assert.True(result3.TryGetRequested(ListTestCategory.FullBody, out var s3));
            Assert.Equal("Attack", s3.Label);
        }

        // ===========================================
        // ヘルパー
        // ===========================================

        private static SimpleJudgment<ListTestCategory> CreateJudgment(string actionId, ActionPriority priority)
        {
            // Input=Alwaysを指定して常に入力成立とする
            return new SimpleJudgment<ListTestCategory>(
                actionId,
                ListTestCategory.FullBody,
                Triggers.Always,
                null,
                priority,
                null);
        }
    }
}
