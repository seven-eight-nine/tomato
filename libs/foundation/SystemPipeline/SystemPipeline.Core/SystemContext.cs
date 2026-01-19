using System.Threading;
using Tomato.SystemPipeline.Query;

namespace Tomato.SystemPipeline;

/// <summary>
/// システム実行時のコンテキスト情報。
/// 各システムの処理メソッドに渡され、フレーム情報やキャンセル制御を提供します。
/// </summary>
public readonly struct SystemContext
{
    /// <summary>
    /// 前フレームからの経過時間（秒）。
    /// </summary>
    public readonly float DeltaTime;

    /// <summary>
    /// パイプライン開始からの累積時間（秒）。
    /// </summary>
    public readonly float TotalTime;

    /// <summary>
    /// フレームカウント。
    /// </summary>
    public readonly int FrameCount;

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
    /// <param name="deltaTime">前フレームからの経過時間（秒）</param>
    /// <param name="totalTime">累積時間（秒）</param>
    /// <param name="frameCount">フレームカウント</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    public SystemContext(float deltaTime, float totalTime, int frameCount, CancellationToken cancellationToken)
        : this(deltaTime, totalTime, frameCount, cancellationToken, null)
    {
    }

    /// <summary>
    /// SystemContextを作成します。
    /// </summary>
    /// <param name="deltaTime">前フレームからの経過時間（秒）</param>
    /// <param name="totalTime">累積時間（秒）</param>
    /// <param name="frameCount">フレームカウント</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <param name="queryCache">クエリキャッシュ</param>
    public SystemContext(float deltaTime, float totalTime, int frameCount, CancellationToken cancellationToken, QueryCache queryCache)
    {
        DeltaTime = deltaTime;
        TotalTime = totalTime;
        FrameCount = frameCount;
        CancellationToken = cancellationToken;
        QueryCache = queryCache;
    }
}
