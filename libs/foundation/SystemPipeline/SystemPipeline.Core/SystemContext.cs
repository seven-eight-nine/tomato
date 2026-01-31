using System.Threading;
using Tomato.SystemPipeline.Query;
using Tomato.Time;

namespace Tomato.SystemPipeline;

/// <summary>
/// システム実行時のコンテキスト情報。
/// 各システムの処理メソッドに渡され、tick情報やキャンセル制御を提供します。
/// </summary>
public readonly struct SystemContext
{
    /// <summary>
    /// 前回からの経過tick数。
    /// </summary>
    public readonly int DeltaTicks;

    /// <summary>
    /// 現在のゲームティック。
    /// </summary>
    public readonly GameTick CurrentTick;

    /// <summary>
    /// キャンセルトークン。
    /// 長時間実行される処理のキャンセルに使用します。
    /// </summary>
    public readonly CancellationToken CancellationToken;

    /// <summary>
    /// クエリ結果のキャッシュ。
    /// 同じフレーム内で同じクエリを実行した場合、キャッシュから結果を返します。
    /// </summary>
    public readonly QueryCache QueryCache;

    /// <summary>
    /// SystemContextを作成します。
    /// </summary>
    /// <param name="deltaTicks">経過tick数</param>
    /// <param name="currentTick">現在のゲームティック</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    public SystemContext(int deltaTicks, GameTick currentTick, CancellationToken cancellationToken)
        : this(deltaTicks, currentTick, cancellationToken, null)
    {
    }

    /// <summary>
    /// SystemContextを作成します。
    /// </summary>
    /// <param name="deltaTicks">経過tick数</param>
    /// <param name="currentTick">現在のゲームティック</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <param name="queryCache">クエリキャッシュ</param>
    public SystemContext(int deltaTicks, GameTick currentTick, CancellationToken cancellationToken, QueryCache queryCache)
    {
        DeltaTicks = deltaTicks;
        CurrentTick = currentTick;
        CancellationToken = cancellationToken;
        QueryCache = queryCache;
    }
}
