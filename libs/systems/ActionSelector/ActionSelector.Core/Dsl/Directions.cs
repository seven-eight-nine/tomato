namespace Tomato.ActionSelector;

/// <summary>
/// 方向の短縮名。
///
/// static using と組み合わせて使用する:
/// <code>
/// using static Tomato.ActionSelector.Dirs;
/// using static Tomato.ActionSelector.Buttons;
///
/// // Plusメソッドで方向+ボタンを合成
/// var cmd = Cmd(D, DR, R.Plus(Punch));
/// </code>
/// </summary>
public static class Dirs
{
    public static Direction N => Direction.Neutral;
    public static Direction U => Direction.Up;
    public static Direction UR => Direction.UpRight;
    public static Direction R => Direction.Right;
    public static Direction DR => Direction.DownRight;
    public static Direction D => Direction.Down;
    public static Direction DL => Direction.DownLeft;
    public static Direction L => Direction.Left;
    public static Direction UL => Direction.UpLeft;

    // 長い名前も提供
    public static Direction Neutral => Direction.Neutral;
    public static Direction Up => Direction.Up;
    public static Direction UpRight => Direction.UpRight;
    public static Direction Right => Direction.Right;
    public static Direction DownRight => Direction.DownRight;
    public static Direction Down => Direction.Down;
    public static Direction DownLeft => Direction.DownLeft;
    public static Direction Left => Direction.Left;
    public static Direction UpLeft => Direction.UpLeft;
}

/// <summary>
/// テンキー表記の方向（格闘ゲーム標準）。
///
/// <code>
/// using static Tomato.ActionSelector.NumPad;
///
/// // 波動拳: 236P (↓↘→+P)
/// var cmd = Cmd(_2, _3, _6 + B.P);
/// </code>
/// </summary>
/// <remarks>
/// テンキー表記:
/// 7 8 9     ↖ ↑ ↗
/// 4 5 6  =  ← N →
/// 1 2 3     ↙ ↓ ↘
/// </remarks>
public static class NumPad
{
    public static Direction _1 => Direction.DownLeft;
    public static Direction _2 => Direction.Down;
    public static Direction _3 => Direction.DownRight;
    public static Direction _4 => Direction.Left;
    public static Direction _5 => Direction.Neutral;
    public static Direction _6 => Direction.Right;
    public static Direction _7 => Direction.UpLeft;
    public static Direction _8 => Direction.Up;
    public static Direction _9 => Direction.UpRight;
}
