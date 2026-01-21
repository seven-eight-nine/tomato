using System.Threading;
using Tomato.SystemPipeline.Query;

namespace Tomato.SystemPipeline;

/// <summary>
/// システムパイプラインの管理クラス。
/// ISystemGroupインスタンスを直接指定して実行します。
///
/// <example>
/// 使用例:
/// <code>
/// public class GameBootstrap : MonoBehaviour
/// {
///     private Pipeline _pipeline;
///     private ISystemGroup _updateGroup;
///     private ISystemGroup _lateUpdateGroup;
///
///     void Awake()
///     {
///         var registry = new GameEntityRegistry();
///
///         // 直列グループ
///         _updateGroup = new SerialSystemGroup(
///             new InputSystem(),
///             new ParallelSystemGroup(  // 並列グループを入れ子
///                 new AISystem(),
///                 new AnimationSystem()
///             ),
///             new PhysicsSystem()
///         );
///
///         _lateUpdateGroup = new SerialSystemGroup(
///             new ReconciliationSystem(),
///             new CleanupSystem()
///         );
///
///         _pipeline = new Pipeline(registry);
///     }
///
///     void Update() => _pipeline.Execute(_updateGroup, Time.deltaTime);
///     void LateUpdate() => _pipeline.Execute(_lateUpdateGroup, Time.deltaTime);
/// }
/// </code>
/// </example>
/// </summary>
public sealed class Pipeline
{
    private readonly IEntityRegistry _registry;
    private readonly QueryCache _queryCache;
    private float _totalTime;
    private int _frameCount;
    private CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// 累積時間（秒）を取得します。
    /// </summary>
    public float TotalTime => _totalTime;

    /// <summary>
    /// フレームカウントを取得します。
    /// </summary>
    public int FrameCount => _frameCount;

    /// <summary>
    /// Pipelineを作成します。
    /// </summary>
    /// <param name="registry">エンティティレジストリ</param>
    public Pipeline(IEntityRegistry registry)
    {
        _registry = registry;
        _queryCache = new QueryCache();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// ISystemGroupを実行します。
    /// </summary>
    /// <param name="group">実行するグループ</param>
    /// <param name="deltaTime">前フレームからの経過時間（秒）</param>
    public void Execute(ISystemGroup group, float deltaTime)
    {
        _totalTime += deltaTime;
        _frameCount++;

        var context = new SystemContext(
            deltaTime,
            _totalTime,
            _frameCount,
            _cancellationTokenSource.Token,
            _queryCache);

        group.Execute(_registry, in context);
    }

    /// <summary>
    /// 時間とフレームカウントをリセットします。
    /// </summary>
    public void Reset()
    {
        _totalTime = 0;
        _frameCount = 0;
        _queryCache.Clear();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// 現在実行中の処理をキャンセルします。
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }
}
