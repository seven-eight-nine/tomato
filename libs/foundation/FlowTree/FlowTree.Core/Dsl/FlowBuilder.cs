using System;
using Tomato.Time;

namespace Tomato.FlowTree;

/// <summary>
/// 型付きフロービルダー。
/// Build(state, b => ...) のラムダ内で使用し、型推論を効かせる。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class FlowBuilder<T> where T : class, IFlowState
{
    /// <summary>
    /// シングルトンインスタンス（状態を持たないため共有可能）。
    /// </summary>
    public static readonly FlowBuilder<T> Instance = new();

    private FlowBuilder() { }

    // =====================================================
    // Typed Leaf Factories
    // =====================================================

    /// <summary>
    /// Actionノードを作成する。
    /// </summary>
    public ActionNode<T> Action(FlowAction<T> action) => new(action);

    /// <summary>
    /// voidアクションを実行してSuccessを返すノードを作成する。
    /// </summary>
    public ActionNode<T> Do(Action<T> action) => new(s => { action(s); return NodeStatus.Success; });

    /// <summary>
    /// Conditionノードを作成する。
    /// </summary>
    public ConditionNode<T> Condition(FlowCondition<T> condition) => new(condition);

    /// <summary>
    /// WaitUntilノードを作成する。
    /// </summary>
    public WaitUntilNode<T> WaitUntil(FlowCondition<T> condition) => new(condition);

    /// <summary>
    /// WaitUntilノードを作成する（間隔評価）。
    /// </summary>
    public WaitUntilNode<T> WaitUntil(FlowCondition<T> condition, TickDuration interval) => new(condition, interval);

    // =====================================================
    // Typed Decorator Factories
    // =====================================================

    /// <summary>
    /// Guardノードを作成する。
    /// </summary>
    public GuardNode<T> Guard(FlowCondition<T> condition, IFlowNode child) => new(condition, child);

    /// <summary>
    /// Scopeノードを作成する。
    /// </summary>
    public ScopeNode<T> Scope(FlowScopeEnterHandler<T>? onEnter, FlowScopeExitHandler<T>? onExit, IFlowNode child)
        => new(onEnter, onExit, child);

    // =====================================================
    // Typed SubTree Factories
    // =====================================================

    /// <summary>
    /// 動的SubTreeノードを作成する。
    /// </summary>
    public SubTreeNode<T> SubTree(FlowTreeProvider<T> provider) => new(provider);

    /// <summary>
    /// State注入付きSubTreeノードを作成する（静的ツリー）。
    /// </summary>
    public SubTreeNode<T, TChild> SubTree<TChild>(FlowTree tree, FlowStateProvider<T, TChild> stateProvider)
        where TChild : class, IFlowState
        => new(tree, stateProvider);

    /// <summary>
    /// State注入付きSubTreeノードを作成する（動的ツリー）。
    /// </summary>
    public SubTreeNode<T, TChild> SubTree<TChild>(FlowTreeProvider<T> treeProvider, FlowStateProvider<T, TChild> stateProvider)
        where TChild : class, IFlowState
        => new(treeProvider, stateProvider);

    // =====================================================
    // Stateless Composite Factories (delegates to Flow)
    // =====================================================

    /// <summary>
    /// Sequenceノードを作成する。
    /// </summary>
    public SequenceNode Sequence(params IFlowNode[] children) => Flow.Sequence(children);

    /// <summary>
    /// Selectorノードを作成する。
    /// </summary>
    public SelectorNode Selector(params IFlowNode[] children) => Flow.Selector(children);

    /// <summary>
    /// Raceノードを作成する。
    /// </summary>
    public RaceNode Race(params IFlowNode[] children) => Flow.Race(children);

    /// <summary>
    /// Joinノードを作成する。
    /// </summary>
    public JoinNode Join(params IFlowNode[] children) => Flow.Join(children);

    /// <summary>
    /// Joinノードを作成する（ポリシー指定）。
    /// </summary>
    public JoinNode Join(JoinPolicy policy, params IFlowNode[] children) => Flow.Join(policy, children);

    /// <summary>
    /// RandomSelectorノードを作成する。
    /// </summary>
    public RandomSelectorNode RandomSelector(params IFlowNode[] children) => Flow.RandomSelector(children);

    /// <summary>
    /// ShuffledSelectorノードを作成する。
    /// </summary>
    public ShuffledSelectorNode ShuffledSelector(params IFlowNode[] children) => Flow.ShuffledSelector(children);

    /// <summary>
    /// WeightedRandomSelectorノードを作成する。
    /// </summary>
    public WeightedRandomSelectorNode WeightedRandomSelector(params (float weight, IFlowNode node)[] weightedChildren)
        => Flow.WeightedRandomSelector(weightedChildren);

    /// <summary>
    /// RoundRobinノードを作成する。
    /// </summary>
    public RoundRobinSelectorNode RoundRobin(params IFlowNode[] children) => Flow.RoundRobin(children);

    // =====================================================
    // Stateless Decorator Factories (delegates to Flow)
    // =====================================================

    /// <summary>
    /// Inverterノードを作成する。
    /// </summary>
    public InverterNode Inverter(IFlowNode child) => Flow.Inverter(child);

    /// <summary>
    /// Succeederノードを作成する。
    /// </summary>
    public SucceederNode Succeeder(IFlowNode child) => Flow.Succeeder(child);

    /// <summary>
    /// Failerノードを作成する。
    /// </summary>
    public FailerNode Failer(IFlowNode child) => Flow.Failer(child);

    /// <summary>
    /// Repeatノードを作成する。
    /// </summary>
    public RepeatNode Repeat(int count, IFlowNode child) => Flow.Repeat(count, child);

    /// <summary>
    /// RepeatUntilFailノードを作成する。
    /// </summary>
    public RepeatUntilFailNode RepeatUntilFail(IFlowNode child) => Flow.RepeatUntilFail(child);

    /// <summary>
    /// RepeatUntilSuccessノードを作成する。
    /// </summary>
    public RepeatUntilSuccessNode RepeatUntilSuccess(IFlowNode child) => Flow.RepeatUntilSuccess(child);

    /// <summary>
    /// Retryノードを作成する。
    /// </summary>
    public RetryNode Retry(int maxRetries, IFlowNode child) => Flow.Retry(maxRetries, child);

    /// <summary>
    /// Timeoutノードを作成する。
    /// </summary>
    public TimeoutNode Timeout(TickDuration timeout, IFlowNode child) => Flow.Timeout(timeout, child);

    /// <summary>
    /// Delayノードを作成する。
    /// </summary>
    public DelayNode Delay(TickDuration delay, IFlowNode child) => Flow.Delay(delay, child);

    // =====================================================
    // Stateless Leaf Factories (delegates to Flow)
    // =====================================================

    /// <summary>
    /// Actionノードを作成する（ステートレス）。
    /// </summary>
    public ActionNode Action(FlowAction action) => Flow.Action(action);

    /// <summary>
    /// Doノードを作成する（ステートレス）。
    /// </summary>
    public ActionNode Do(System.Action action) => Flow.Do(action);

    /// <summary>
    /// Conditionノードを作成する（ステートレス）。
    /// </summary>
    public ConditionNode Condition(FlowCondition condition) => Flow.Condition(condition);

    /// <summary>
    /// Waitノードを作成する。
    /// </summary>
    public WaitNode Wait(TickDuration duration) => Flow.Wait(duration);

    /// <summary>
    /// Yieldノードを作成する。
    /// </summary>
    public YieldNode Yield() => Flow.Yield();

    /// <summary>
    /// Successノード。
    /// </summary>
    public SuccessNode Success => Flow.Success;

    /// <summary>
    /// Failureノード。
    /// </summary>
    public FailureNode Failure => Flow.Failure;

    /// <summary>
    /// ツリーの早期終了を要求するノードを作成する（Success）。
    /// </summary>
    public ReturnNode ReturnSuccess() => Flow.ReturnSuccess();

    /// <summary>
    /// ツリーの早期終了を要求するノードを作成する（Failure）。
    /// </summary>
    public ReturnNode ReturnFailure() => Flow.ReturnFailure();

    /// <summary>
    /// ツリーの早期終了を要求するノードを作成する。
    /// </summary>
    public ReturnNode Return(NodeStatus status) => Flow.Return(status);

    /// <summary>
    /// SubTreeノードを作成する（静的参照）。
    /// </summary>
    public SubTreeNode SubTree(FlowTree tree) => Flow.SubTree(tree);

    /// <summary>
    /// 動的SubTreeノードを作成する（ステートレス）。
    /// </summary>
    public SubTreeNode SubTree(FlowTreeProvider provider) => Flow.SubTree(provider);

    /// <summary>
    /// Guardノードを作成する（ステートレス）。
    /// </summary>
    public GuardNode Guard(FlowCondition condition, IFlowNode child) => Flow.Guard(condition, child);

    /// <summary>
    /// Scopeノードを作成する（ステートレス）。
    /// </summary>
    public ScopeNode Scope(FlowScopeEnterHandler? onEnter, FlowScopeExitHandler? onExit, IFlowNode child)
        => Flow.Scope(onEnter, onExit, child);

    /// <summary>
    /// WaitUntilノードを作成する（ステートレス）。
    /// </summary>
    public WaitUntilNode WaitUntil(FlowCondition condition) => Flow.WaitUntil(condition);

    /// <summary>
    /// WaitUntilノードを作成する（ステートレス、間隔評価）。
    /// </summary>
    public WaitUntilNode WaitUntil(FlowCondition condition, TickDuration interval) => Flow.WaitUntil(condition, interval);
}
