namespace Tomato.ActionSelector;

/// <summary>
/// ボタン種別の短縮名。
/// static using と組み合わせて使用する。
/// </summary>
public static class Buttons
{
    // 基本ボタン（Face buttons）
    public static ButtonType B0 => ButtonType.Button0;
    public static ButtonType B1 => ButtonType.Button1;
    public static ButtonType B2 => ButtonType.Button2;
    public static ButtonType B3 => ButtonType.Button3;

    // 追加ボタン
    public static ButtonType B4 => ButtonType.Button4;
    public static ButtonType B5 => ButtonType.Button5;
    public static ButtonType B6 => ButtonType.Button6;
    public static ButtonType B7 => ButtonType.Button7;

    // ショルダー/トリガー
    public static ButtonType L1 => ButtonType.L1;
    public static ButtonType L2 => ButtonType.L2;
    public static ButtonType R1 => ButtonType.R1;
    public static ButtonType R2 => ButtonType.R2;

    // 方向入力（ボタンとして）
    public static ButtonType Up => ButtonType.Up;
    public static ButtonType Down => ButtonType.Down;
    public static ButtonType Left => ButtonType.Left;
    public static ButtonType Right => ButtonType.Right;

    // システム
    public static ButtonType Start => ButtonType.Start;
    public static ButtonType Select => ButtonType.Select;
}
