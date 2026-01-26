namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドのEnqueueタイミングを指定する。
/// </summary>
public enum EnqueueTiming
{
    /// <summary>
    /// 次のStepで実行される（デフォルト）。
    /// 同一フレーム内の次Step処理時に実行される。
    /// </summary>
    NextStep = 0,

    /// <summary>
    /// 次のフレームで実行される。
    /// 現在のフレームでは処理されず、次フレームの先頭Step処理時に実行される。
    /// </summary>
    NextFrame = 1
}
