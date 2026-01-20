using Xunit;
using static Tomato.ActionSelector.Trig;
using Tomato.ActionSelector.Tests;
using static Tomato.ActionSelector.Tests.Cond;
using static Tomato.ActionSelector.Tests.GameButtonTypes;

namespace Tomato.ActionSelector.Tests.Dsl
{
    /// <summary>
    /// ComposableCondition/ComposableTriggerの演算子テスト。
    /// </summary>
    public class ComposableOperatorsTests
    {
        // ===========================================
        // ConditionExtensions テスト
        // ===========================================

        [Fact]
        public void Condition_And_CombinesConditions()
        {
            // Grounded AND Always
            var condition = Grounded.And(Cond.Always);

            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.False(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void Condition_Or_CombinesConditions()
        {
            var condition = Grounded.Or(Airborne);

            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void Condition_Not_InvertsCondition()
        {
            var condition = Grounded.Not();

            Assert.False(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void Condition_Chain_MultipleOperations()
        {
            // (Grounded AND Always) OR Airborne
            var condition = Grounded.And(Cond.Always).Or(Airborne);

            // 接地中
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultGrounded));

            // 空中
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void Condition_AndAll_CombinesMultiple()
        {
            // Grounded AND Always AND Always
            var condition = Grounded.AndAll(Cond.Always, Cond.Always);

            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.False(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        // ===========================================
        // ComposableCondition 演算子テスト
        // ===========================================

        [Fact]
        public void ComposableCondition_AndOperator()
        {
            var left = Grounded.Compose();
            var right = Cond.Always.Compose();
            var combined = left & right;

            Assert.True(combined.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.False(combined.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void ComposableCondition_OrOperator()
        {
            var left = Grounded.Compose();
            var right = Airborne.Compose();
            var combined = left | right;

            Assert.True(combined.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.True(combined.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void ComposableCondition_NotOperator()
        {
            var condition = !Grounded.Compose();

            Assert.False(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        // ===========================================
        // TriggerExtensions テスト
        // ===========================================

        [Fact]
        public void Trigger_And_CombinesTriggers()
        {
            var trigger = Press(Attack).And(Press(Jump));

            var inputBoth = new InputState(
                Attack | Jump,
                Attack | Jump,
                ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputBoth));

            var inputOne = new InputState(Attack, Attack, ButtonType.None);
            Assert.False(trigger.IsTriggered(in inputOne));
        }

        [Fact]
        public void Trigger_Or_CombinesTriggers()
        {
            var trigger = Press(Attack).Or(Press(Jump));

            var inputAttack = new InputState(Attack, Attack, ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputAttack));

            var inputJump = new InputState(Jump, Jump, ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputJump));

            Assert.False(trigger.IsTriggered(in InputState.Empty));
        }

        [Fact]
        public void Trigger_AndAll_CombinesMultiple()
        {
            var trigger = Press(Attack).AndAll(Hold(Guard), Press(Special));

            var input = new InputState(
                Attack | Guard | Special,
                Attack | Special,
                ButtonType.None);
            Assert.True(trigger.IsTriggered(in input));

            var partialInput = new InputState(
                Attack | Guard,
                Attack,
                ButtonType.None);
            Assert.False(trigger.IsTriggered(in partialInput));
        }

        // ===========================================
        // ComposableTrigger 演算子テスト
        // ===========================================

        [Fact]
        public void ComposableTrigger_AndOperator()
        {
            var left = Press(Attack).Compose();
            var right = Press(Jump).Compose();
            var combined = left & right;

            var inputBoth = new InputState(
                Attack | Jump,
                Attack | Jump,
                ButtonType.None);
            Assert.True(combined.IsTriggered(in inputBoth));
        }

        [Fact]
        public void ComposableTrigger_OrOperator()
        {
            var left = Press(Attack).Compose();
            var right = Press(Jump).Compose();
            var combined = left | right;

            var inputAttack = new InputState(Attack, Attack, ButtonType.None);
            Assert.True(combined.IsTriggered(in inputAttack));

            var inputJump = new InputState(Jump, Jump, ButtonType.None);
            Assert.True(combined.IsTriggered(in inputJump));
        }

        [Fact]
        public void ComposableTrigger_Chain()
        {
            var trigger = Press(Attack).Compose() | Press(Jump).Compose() | Press(Dash).Compose();

            var inputAttack = new InputState(Attack, Attack, ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputAttack));

            var inputJump = new InputState(Jump, Jump, ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputJump));

            var inputDash = new InputState(Dash, Dash, ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputDash));

            Assert.False(trigger.IsTriggered(in InputState.Empty));
        }
    }
}
