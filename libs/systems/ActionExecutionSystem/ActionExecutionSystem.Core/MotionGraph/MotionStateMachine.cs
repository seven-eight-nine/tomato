using Tomato.HierarchicalStateMachine;

namespace Tomato.ActionExecutionSystem.MotionGraph;

/// <summary>
/// モーション専用のステートマシン。
/// HierarchicalStateMachineをラップし、フレームベースの状態管理を提供。
/// </summary>
public sealed class MotionStateMachine
{
    private readonly HierarchicalStateMachine<MotionContext> _hsm;
    private readonly MotionContext _context;

    /// <summary>
    /// 現在のモーション状態ID。
    /// </summary>
    public StateId? CurrentStateId => _hsm.CurrentStateId;

    /// <summary>
    /// 現在のモーション状態。
    /// </summary>
    public MotionState? CurrentMotionState => _context.CurrentMotionState;

    /// <summary>
    /// 経過フレーム数。
    /// </summary>
    public int ElapsedFrames => _context.ElapsedFrames;

    /// <summary>
    /// 状態グラフ。
    /// </summary>
    public StateGraph<MotionContext> Graph => _hsm.Graph;

    /// <summary>
    /// コンテキスト。
    /// </summary>
    public MotionContext Context => _context;

    public MotionStateMachine(StateGraph<MotionContext> graph, IMotionExecutor? executor = null)
    {
        _hsm = new HierarchicalStateMachine<MotionContext>(graph);
        _context = new MotionContext();
        _context.Executor = executor;
    }

    /// <summary>
    /// 初期状態を設定。
    /// </summary>
    public void Initialize(StateId initialState)
    {
        _hsm.Initialize(initialState, _context);
    }

    /// <summary>
    /// モーションエグゼキューターを設定。
    /// </summary>
    public void SetExecutor(IMotionExecutor? executor)
    {
        _context.Executor = executor;
    }

    /// <summary>
    /// フレーム更新。
    /// </summary>
    public void Update(float deltaTime)
    {
        var currentState = _hsm.CurrentState;
        currentState?.OnUpdate(_context, deltaTime);
    }

    /// <summary>
    /// 指定した状態への遷移を試みる。
    /// 遷移条件が満たされている場合のみ成功。
    /// </summary>
    public bool TryTransitionTo(StateId target)
    {
        return _hsm.TransitionTo(target, _context);
    }

    /// <summary>
    /// 強制的に指定した状態へ遷移。
    /// 遷移条件をチェックしない。
    /// </summary>
    public void ForceTransitionTo(StateId target)
    {
        _hsm.ForceTransitionTo(target, _context);
    }

    /// <summary>
    /// 現在のモーションが完了したかどうか。
    /// </summary>
    public bool IsCurrentMotionComplete()
    {
        return _context.CurrentMotionState?.IsComplete(_context) ?? true;
    }
}
