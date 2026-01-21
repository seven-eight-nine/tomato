namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドのEnqueueタイミングを指定する。
/// </summary>
public enum EnqueueTiming
{
    /// <summary>
    /// 次のWaveで実行される（デフォルト）。
    /// 同一フレーム内の次Wave処理時に実行される。
    /// </summary>
    NextWave = 0,

    /// <summary>
    /// 次のフレームで実行される。
    /// 現在のフレームでは処理されず、次フレームの先頭Wave処理時に実行される。
    /// </summary>
    NextFrame = 1
}
