using Xunit;
using static Tomato.ActionSelector.Buttons;
using static Tomato.ActionSelector.Dirs;
using static Tomato.ActionSelector.NumPad;
using static Tomato.ActionSelector.Priorities;
using static Tomato.ActionSelector.Pri;
using static Tomato.ActionSelector.T;

namespace Tomato.ActionSelector.Tests.Dsl
{
    /// <summary>
    /// 短縮名staticクラスのテスト。
    /// </summary>
    public class ShortNameTests
    {
        // ===========================================
        // Buttons テスト（汎用ボタン名）
        // ===========================================

        [Fact]
        public void Buttons_ReturnsCorrectButtonTypes()
        {
            Assert.Equal(ButtonType.Button0, B0);
            Assert.Equal(ButtonType.Button1, B1);
            Assert.Equal(ButtonType.Button2, B2);
            Assert.Equal(ButtonType.Button3, B3);
            Assert.Equal(ButtonType.Button4, B4);
            Assert.Equal(ButtonType.Button5, B5);
        }

        [Fact]
        public void Buttons_ShoulderButtons_ReturnsCorrectButtonTypes()
        {
            Assert.Equal(ButtonType.L1, L1);
            Assert.Equal(ButtonType.L2, L2);
            Assert.Equal(ButtonType.R1, R1);
            Assert.Equal(ButtonType.R2, R2);
        }

        [Fact]
        public void Buttons_DirectionalButtons_ReturnsCorrectButtonTypes()
        {
            Assert.Equal(ButtonType.Up, Buttons.Up);
            Assert.Equal(ButtonType.Down, Buttons.Down);
            Assert.Equal(ButtonType.Left, Buttons.Left);
            Assert.Equal(ButtonType.Right, Buttons.Right);
        }

        [Fact]
        public void Buttons_CanCombineWithOr()
        {
            var combined = B0 | B1;
            Assert.Equal(ButtonType.Button0 | ButtonType.Button1, combined);
        }

        // ===========================================
        // Dirs テスト
        // ===========================================

        [Fact]
        public void Dirs_ReturnsCorrectDirections()
        {
            Assert.Equal(Direction.Up, U);
            Assert.Equal(Direction.Down, D);
            Assert.Equal(Direction.Left, L);
            Assert.Equal(Direction.Right, R);
            Assert.Equal(Direction.DownRight, DR);
            Assert.Equal(Direction.Neutral, N);
        }

        [Fact]
        public void Dirs_LongNames_ReturnsCorrectDirections()
        {
            Assert.Equal(Direction.Up, Dirs.Up);
            Assert.Equal(Direction.Down, Dirs.Down);
            Assert.Equal(Direction.DownRight, Dirs.DownRight);
        }

        // ===========================================
        // NumPad テスト
        // ===========================================

        [Fact]
        public void NumPad_ReturnsCorrectDirections()
        {
            // テンキー配置確認
            Assert.Equal(Direction.DownLeft, _1);
            Assert.Equal(Direction.Down, _2);
            Assert.Equal(Direction.DownRight, _3);
            Assert.Equal(Direction.Left, _4);
            Assert.Equal(Direction.Neutral, _5);
            Assert.Equal(Direction.Right, _6);
            Assert.Equal(Direction.UpLeft, _7);
            Assert.Equal(Direction.Up, _8);
            Assert.Equal(Direction.UpRight, _9);
        }

        // ===========================================
        // Priorities テスト
        // ===========================================

        [Fact]
        public void Priorities_ReturnsCorrectValues()
        {
            Assert.Equal(ActionPriority.Highest, Highest);
            Assert.Equal(ActionPriority.High, High);
            Assert.Equal(ActionPriority.Normal, Normal);
            Assert.Equal(ActionPriority.Low, Low);
            Assert.Equal(ActionPriority.Lowest, Lowest);
            Assert.Equal(ActionPriority.Disabled, Disabled);
        }

        [Fact]
        public void Pri_ShortNames_ReturnsCorrectValues()
        {
            Assert.Equal(ActionPriority.Highest, Max);
            Assert.Equal(ActionPriority.High, Hi);
            Assert.Equal(ActionPriority.Normal, Norm);
            Assert.Equal(ActionPriority.Low, Lo);
            Assert.Equal(ActionPriority.Lowest, Min);
            Assert.Equal(ActionPriority.Disabled, Off);
        }

        [Fact]
        public void Priorities_Custom_CreatesCorrectPriority()
        {
            var custom = Priorities.Custom(1, 2, 3);
            Assert.Equal(1, custom.Layer);
            Assert.Equal(2, custom.Group);
            Assert.Equal(3, custom.Detail);
        }

        // ===========================================
        // T (Composableトリガー) テスト
        // ===========================================

        [Fact]
        public void T_ReturnsComposableTriggers()
        {
            var press = Press(B0);
            var hold = Hold(B1);
            var always = T.Always;
            var never = T.Never;

            Assert.IsType<ComposableTrigger>(press);
            Assert.IsType<ComposableTrigger>(hold);
            Assert.IsType<ComposableTrigger>(always);
            Assert.IsType<ComposableTrigger>(never);
        }

        [Fact]
        public void T_SupportsOrOperator()
        {
            var combined = Press(B0) | Press(B1);

            Assert.IsType<ComposableTrigger>(combined);
        }

        [Fact]
        public void T_SupportsAndOperator()
        {
            var combined = Press(B0) & Hold(B1);

            Assert.IsType<ComposableTrigger>(combined);
        }
    }
}
