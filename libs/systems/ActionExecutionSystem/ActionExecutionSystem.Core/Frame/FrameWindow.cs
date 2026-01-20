namespace Tomato.ActionExecutionSystem;

/// <summary>
/// フレームウィンドウ。
/// 開始フレームと終了フレームの範囲を表す。
/// </summary>
public readonly struct FrameWindow
{
    public readonly int Start;
    public readonly int End;

    public FrameWindow(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// 指定フレームがウィンドウ内かどうか。
    /// </summary>
    public bool Contains(int frame) => frame >= Start && frame <= End;

    /// <summary>
    /// 開始フレームと終了フレームからウィンドウを作成。
    /// </summary>
    public static FrameWindow FromStartEnd(int start, int end) => new(start, end);

    /// <summary>
    /// 開始フレームと継続フレーム数からウィンドウを作成。
    /// </summary>
    public static FrameWindow FromStartDuration(int start, int duration) => new(start, start + duration - 1);
}
