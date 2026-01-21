using Xunit;
using static Tomato.ActionSelector.Trig;
using Tomato.ActionSelector.Tests;
using static Tomato.ActionSelector.Tests.Cond;
using static Tomato.ActionSelector.Tests.GameButtonTypes;

namespace Tomato.ActionSelector.Tests.Dsl
{
    /// <summary>
    /// Trig/Condファクトリのテスト。
    /// </summary>
    public class TrigCondTests
    {
        // ===========================================
        // Trig テスト
        // ===========================================

        [Fact]
        public void Trig_Press_CreatesTrigger()
        {
            var trigger = Press(Attack);
            Assert.NotNull(trigger);

            var input = new InputState(ButtonType.None, Attack, ButtonType.None);
            Assert.True(trigger.IsTriggered(in input));
        }

        [Fact]
        public void Trig_Release_CreatesTrigger()
        {
            var trigger = Release(Attack);
            Assert.NotNull(trigger);

            var input = new InputState(ButtonType.None, ButtonType.None, Attack);
            Assert.True(trigger.IsTriggered(in input));
        }

        [Fact]
        public void Trig_Hold_CreatesTrigger()
        {
            var trigger = Hold(Attack);
            Assert.NotNull(trigger);

            var input = new InputState(Attack, ButtonType.None, ButtonType.None);
            Assert.True(trigger.IsTriggered(in input));
        }

        [Fact]
        public void Trig_Always_AlwaysTriggered()
        {
            var trigger = Trig.Always;
            var input = InputState.Empty;
            Assert.True(trigger.IsTriggered(in input));
        }

        [Fact]
        public void Trig_Never_NeverTriggered()
        {
            var trigger = Trig.Never;
            var input = new InputState(Attack, Attack, ButtonType.None);
            Assert.False(trigger.IsTriggered(in input));
        }

        [Fact]
        public void Trig_All_RequiresAllTriggers()
        {
            var trigger = All(Press(Attack), Press(Jump));

            var inputBoth = new InputState(
                Attack | Jump,
                Attack | Jump,
                ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputBoth));

            var inputOne = new InputState(Attack, Attack, ButtonType.None);
            Assert.False(trigger.IsTriggered(in inputOne));
        }

        [Fact]
        public void Trig_Any_RequiresAnyTrigger()
        {
            var trigger = Any(Press(Attack), Press(Jump));

            var inputAttack = new InputState(Attack, Attack, ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputAttack));

            var inputJump = new InputState(Jump, Jump, ButtonType.None);
            Assert.True(trigger.IsTriggered(in inputJump));

            var inputNone = InputState.Empty;
            Assert.False(trigger.IsTriggered(in inputNone));
        }

        // ===========================================
        // Cond テスト
        // ===========================================

        [Fact]
        public void Cond_Grounded_EvaluatesCorrectly()
        {
            var condition = Grounded;

            var groundedState = GameStateExtensions.DefaultGrounded;
            Assert.True(condition.Evaluate(in groundedState));

            var airborneState = GameStateExtensions.DefaultAirborne;
            Assert.False(condition.Evaluate(in airborneState));
        }

        [Fact]
        public void Cond_Airborne_EvaluatesCorrectly()
        {
            var condition = Airborne;

            var airborneState = GameStateExtensions.DefaultAirborne;
            Assert.True(condition.Evaluate(in airborneState));

            var groundedState = GameStateExtensions.DefaultGrounded;
            Assert.False(condition.Evaluate(in groundedState));
        }

        [Fact]
        public void Cond_Always_AlwaysTrue()
        {
            var condition = Cond.Always;
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void Cond_Never_AlwaysFalse()
        {
            var condition = Cond.Never;
            Assert.False(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.False(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void Cond_Not_InvertsCondition()
        {
            var condition = Not(Grounded);

            var groundedState = GameStateExtensions.DefaultGrounded;
            Assert.False(condition.Evaluate(in groundedState));

            var airborneState = GameStateExtensions.DefaultAirborne;
            Assert.True(condition.Evaluate(in airborneState));
        }

        [Fact]
        public void Cond_All_RequiresAllConditions()
        {
            // Grounded と Always の両方が満たされる場合のテスト
            var condition = All(Grounded, Cond.Always);

            var groundedState = GameStateExtensions.DefaultGrounded;
            Assert.True(condition.Evaluate(in groundedState));

            var airborneState = GameStateExtensions.DefaultAirborne;
            Assert.False(condition.Evaluate(in airborneState));
        }

        [Fact]
        public void Cond_Any_RequiresAnyCondition()
        {
            var condition = Any(Grounded, Airborne);

            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultGrounded));
            Assert.True(condition.Evaluate(in GameStateExtensions.DefaultAirborne));
        }

        [Fact]
        public void Cond_HealthAbove_ReturnsCondition()
        {
            // HealthAbove はゲーム固有条件のファクトリメソッドをテスト
            var condition = HealthAbove(0.5f);
            Assert.NotNull(condition);

            // 簡易実装ではフラグに依存しないため、任意の状態で評価可能
            var state = GameStateExtensions.DefaultGrounded;
            Assert.True(condition.Evaluate(in state));
        }

        [Fact]
        public void Cond_HealthBelow_ReturnsCondition()
        {
            // HealthBelow はゲーム固有条件のファクトリメソッドをテスト
            var condition = HealthBelow(0.3f);
            Assert.NotNull(condition);

            // 簡易実装ではフラグに依存しないため、任意の状態で評価可能
            var state = GameStateExtensions.DefaultGrounded;
            Assert.False(condition.Evaluate(in state));
        }

        // ===========================================
        // CmdStep テスト
        // ===========================================

        [Fact]
        public void CmdStep_ImplicitFromDirection()
        {
            CmdStep step = Direction.Down;
            Assert.Equal(Direction.Down, step.Direction);
            Assert.Null(step.Button);
        }

        [Fact]
        public void CmdStep_ImplicitFromButton()
        {
            CmdStep step = Punch;
            Assert.Null(step.Direction);
            Assert.Equal(Punch, step.Button);
        }

        [Fact]
        public void CmdStep_Plus_CombinesDirectionAndButton()
        {
            var step = Direction.Right.Plus(Punch);
            Assert.Equal(Direction.Right, step.Direction);
            Assert.Equal(Punch, step.Button);
        }

        [Fact]
        public void Trig_Cmd_CreatesCommandTrigger()
        {
            // 波動拳コマンド: ↓↘→+P
            var trigger = Cmd(Direction.Down, Direction.DownRight, Direction.Right.Plus(Punch));
            Assert.NotNull(trigger);
        }
    }
}
