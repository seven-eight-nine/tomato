using System.Threading;
using Tomato.SystemPipeline.Query;
using Tomato.Time;

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
///     void Update() => _pipeline.Execute(_updateGroup, deltaTicks: 1);
///     void LateUpdate() => _pipeline.Execute(_lateUpdateGroup, deltaTicks: 1);
/// }
/// </code>
/// </example>
/// </summary>
public sealed class Pipeline
{
    private readonly IEntityRegistry _registry;
    private readonly QueryCache _queryCache;
    private GameTick _currentTick;
    private CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// 現在のゲームティックを取得します。
    /// </summary>
    public GameTick CurrentTick => _currentTick;

    /// <summary>
    /// Pipelineを作成します。
    /// </summary>
    /// <param name="registry">エンティティレジストリ</param>
    public Pipeline(IEntityRegistry registry)
    {
        _registry = registry;
        _queryCache = new QueryCache();
        _cancellationTokenSource = new CancellationTokenSource();
        _currentTick = GameTick.Zero;
    }

    /// <summary>
    /// ISystemGroupを実行します。
    /// </summary>
    /// <param name="group">実行するグループ</param>
    /// <param name="deltaTicks">経過tick数（デフォルト: 1）</param>
    public void Execute(ISystemGroup group, int deltaTicks = 1)
    {
        _currentTick = _currentTick + deltaTicks;

        var context = new SystemContext(
            deltaTicks,
            _currentTick,
            _cancellationTokenSource.Token,
            _queryCache);

        group.Execute(_registry, in context);
    }

    /// <summary>
    /// tickとキャッシュをリセットします。
    /// </summary>
    public void Reset()
    {
        _currentTick = GameTick.Zero;
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
