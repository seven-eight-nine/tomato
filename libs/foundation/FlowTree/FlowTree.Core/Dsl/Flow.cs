namespace Tomato.FlowTree;

/// <summary>
/// DSL用の短縮名クラス（static using用）。
/// </summary>
public static class Flow
{
    // =====================================================
    // Tree Creation
    // =====================================================

    /// <summary>
    /// 新しいFlowTreeを作成する。
    /// </summary>
    /// <param name="name">ツリー名（オプション）</param>
    /// <returns>FlowTree</returns>
    public static FlowTree Tree(string? name = null)
        => new(name);

    // =====================================================
    // Leaf Node Factories
    // =====================================================

    /// <summary>
    /// Actionノードを作成する。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public static ActionNode Action(FlowAction action)
        => new(action);

    /// <summary>
    /// Conditionノードを作成する。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public static ConditionNode Condition(FlowCondition condition)
        => new(condition);

    /// <summary>
    /// SubTreeノードを作成する。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    public static SubTreeNode SubTree(FlowTree tree)
        => new(tree);

    /// <summary>
    /// Waitノードを作成する。
    /// </summary>
    /// <param name="duration">待機時間（秒）</param>
    public static WaitNode Wait(float duration)
        => new(duration);

    /// <summary>
    /// Yieldノードを作成する。
    /// </summary>
    public static YieldNode Yield()
        => new();

    /// <summary>
    /// 即座にSuccessを返すノード。
    /// </summary>
    public static SuccessNode Success => SuccessNode.Instance;

    /// <summary>
    /// 即座にFailureを返すノード。
    /// </summary>
    public static FailureNode Failure => FailureNode.Instance;

    /// <summary>
    /// ツリーの早期終了を要求するノードを作成する（Success）。
    /// 実行されると、現在のツリーをリセットし、EventNodeのonExitを発火させる。
    /// </summary>
    public static ReturnNode ReturnSuccess() => new(NodeStatus.Success);

    /// <summary>
    /// ツリーの早期終了を要求するノードを作成する（Failure）。
    /// 実行されると、現在のツリーをリセットし、EventNodeのonExitを発火させる。
    /// </summary>
    public static ReturnNode ReturnFailure() => new(NodeStatus.Failure);

    /// <summary>
    /// ツリーの早期終了を要求するノードを作成する。
    /// 実行されると、現在のツリーをリセットし、EventNodeのonExitを発火させる。
    /// </summary>
    /// <param name="status">返すステータス（SuccessまたはFailure）</param>
    public static ReturnNode Return(NodeStatus status) => new(status);

    // =====================================================
    // Composite Node Factories
    // =====================================================

    /// <summary>
    /// Sequenceノードを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static SequenceNode Sequence(params IFlowNode[] children)
        => new(children);

    /// <summary>
    /// Selectorノードを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static SelectorNode Selector(params IFlowNode[] children)
        => new(children);

    /// <summary>
    /// Raceノードを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static RaceNode Race(params IFlowNode[] children)
        => new(children);

    /// <summary>
    /// Joinノードを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static JoinNode Join(params IFlowNode[] children)
        => new(children);

    /// <summary>
    /// Joinノードを作成する（ポリシー指定）。
    /// </summary>
    /// <param name="policy">評価ポリシー</param>
    /// <param name="children">子ノードの配列</param>
    public static JoinNode Join(JoinPolicy policy, params IFlowNode[] children)
        => new(policy, children);

    /// <summary>
    /// RandomSelectorノードを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static RandomSelectorNode RandomSelector(params IFlowNode[] children)
        => new(children);

    /// <summary>
    /// ShuffledSelectorノードを作成する。
    /// 全選択肢を一巡するまで同じものを選ばない。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static ShuffledSelectorNode ShuffledSelector(params IFlowNode[] children)
        => new(children);

    /// <summary>
    /// WeightedRandomSelectorノードを作成する。
    /// </summary>
    /// <param name="weightedChildren">重みとノードのタプル配列</param>
    public static WeightedRandomSelectorNode WeightedRandomSelector(params (float weight, IFlowNode node)[] weightedChildren)
        => new(weightedChildren);

    /// <summary>
    /// RoundRobinノードを作成する。
    /// 0→1→2→0→...と順番に選択する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static RoundRobinSelectorNode RoundRobin(params IFlowNode[] children)
        => new(children);

    // =====================================================
    // Decorator Node Factories
    // =====================================================

    /// <summary>
    /// Inverterノードを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public static InverterNode Inverter(IFlowNode child)
        => new(child);

    /// <summary>
    /// Succeederノードを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public static SucceederNode Succeeder(IFlowNode child)
        => new(child);

    /// <summary>
    /// Failerノードを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public static FailerNode Failer(IFlowNode child)
        => new(child);

    /// <summary>
    /// Repeatノードを作成する。
    /// </summary>
    /// <param name="count">繰り返し回数</param>
    /// <param name="child">子ノード</param>
    public static RepeatNode Repeat(int count, IFlowNode child)
        => new(count, child);

    /// <summary>
    /// RepeatUntilFailノードを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public static RepeatUntilFailNode RepeatUntilFail(IFlowNode child)
        => new(child);

    /// <summary>
    /// RepeatUntilSuccessノードを作成する。
    /// </summary>
    /// <param name="child">子ノード</param>
    public static RepeatUntilSuccessNode RepeatUntilSuccess(IFlowNode child)
        => new(child);

    /// <summary>
    /// Retryノードを作成する。
    /// </summary>
    /// <param name="maxRetries">最大リトライ回数</param>
    /// <param name="child">子ノード</param>
    public static RetryNode Retry(int maxRetries, IFlowNode child)
        => new(maxRetries, child);

    /// <summary>
    /// Timeoutノードを作成する。
    /// </summary>
    /// <param name="timeout">タイムアウト時間（秒）</param>
    /// <param name="child">子ノード</param>
    public static TimeoutNode Timeout(float timeout, IFlowNode child)
        => new(timeout, child);

    /// <summary>
    /// Delayノードを作成する。
    /// </summary>
    /// <param name="delay">遅延時間（秒）</param>
    /// <param name="child">子ノード</param>
    public static DelayNode Delay(float delay, IFlowNode child)
        => new(delay, child);

    /// <summary>
    /// Guardノードを作成する。
    /// </summary>
    /// <param name="condition">実行条件</param>
    /// <param name="child">子ノード</param>
    public static GuardNode Guard(FlowCondition condition, IFlowNode child)
        => new(condition, child);

    /// <summary>
    /// Eventノードを作成する（ステートレス）。
    /// </summary>
    /// <param name="onEnter">入った瞬間に発火するイベント</param>
    /// <param name="onExit">出た瞬間に発火するイベント</param>
    /// <param name="child">子ノード</param>
    public static EventNode Event(FlowEventHandler? onEnter, FlowExitEventHandler? onExit, IFlowNode child)
        => new(onEnter, onExit, child);

    /// <summary>
    /// Eventノードを作成する（型付き）。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="onEnter">入った瞬間に発火するイベント</param>
    /// <param name="onExit">出た瞬間に発火するイベント</param>
    /// <param name="child">子ノード</param>
    public static EventNode<T> Event<T>(FlowEventHandler<T>? onEnter, FlowExitEventHandler<T>? onExit, IFlowNode child) where T : class, IFlowState
        => new(onEnter, onExit, child);

    // =====================================================
    // Additional Leaf Node Factories
    // =====================================================

    /// <summary>
    /// WaitUntilノードを作成する（ステートレス）。
    /// 条件が満たされるまで待機する。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public static WaitUntilNode WaitUntil(FlowCondition condition)
        => new(condition);

    /// <summary>
    /// WaitUntilノードを作成する（ステートレス、間隔評価）。
    /// 条件が満たされるまで待機する。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    /// <param name="interval">チェック間隔（秒）</param>
    public static WaitUntilNode WaitUntil(FlowCondition condition, float interval)
        => new(condition, interval);

    /// <summary>
    /// WaitUntilノードを作成する（型付き）。
    /// 条件が満たされるまで待機する。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public static WaitUntilNode<T> WaitUntil<T>(FlowCondition<T> condition) where T : class, IFlowState
        => new(condition);

    /// <summary>
    /// WaitUntilノードを作成する（型付き、間隔評価）。
    /// 条件が満たされるまで待機する。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    /// <param name="interval">チェック間隔（秒）</param>
    public static WaitUntilNode<T> WaitUntil<T>(FlowCondition<T> condition, float interval) where T : class, IFlowState
        => new(condition, interval);

    /// <summary>
    /// voidアクションを実行してSuccessを返すノードを作成する。
    /// Action()の短縮形。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public static ActionNode Do(System.Action action)
        => new(() => { action(); return NodeStatus.Success; });

    // =====================================================
    // Typed Factories
    // =====================================================

    /// <summary>
    /// Actionノードを作成する（型付き）。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="action">実行するアクション</param>
    public static ActionNode<T> Action<T>(FlowAction<T> action) where T : class, IFlowState
        => new(action);

    /// <summary>
    /// voidアクションを実行してSuccessを返すノードを作成する（型付き）。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="action">実行するアクション</param>
    public static ActionNode<T> Do<T>(System.Action<T> action) where T : class, IFlowState
        => new(s => { action(s); return NodeStatus.Success; });

    /// <summary>
    /// Conditionノードを作成する（型付き）。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="condition">評価する条件</param>
    public static ConditionNode<T> Condition<T>(FlowCondition<T> condition) where T : class, IFlowState
        => new(condition);

    /// <summary>
    /// Guardノードを作成する（型付き）。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="condition">実行条件</param>
    /// <param name="child">子ノード</param>
    public static GuardNode<T> Guard<T>(FlowCondition<T> condition, IFlowNode child) where T : class, IFlowState
        => new(condition, child);

    // =====================================================
    // Dynamic SubTree Factories
    // =====================================================

    /// <summary>
    /// 動的SubTreeノードを作成する（ステートレス）。
    /// </summary>
    /// <param name="provider">ツリーを提供するラムダ</param>
    public static SubTreeNode SubTree(FlowTreeProvider provider)
        => new(provider);

    /// <summary>
    /// 動的SubTreeノードを作成する（型付き）。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="provider">ツリーを提供するラムダ</param>
    public static SubTreeNode<T> SubTree<T>(FlowTreeProvider<T> provider) where T : class, IFlowState
        => new(provider);

    /// <summary>
    /// State注入付きSubTreeノードを作成する（静的ツリー）。
    /// </summary>
    /// <typeparam name="TParent">親状態の型</typeparam>
    /// <typeparam name="TChild">子状態の型</typeparam>
    /// <param name="tree">呼び出すツリー</param>
    /// <param name="stateProvider">子Stateを作成するラムダ</param>
    public static SubTreeNode<TParent, TChild> SubTree<TParent, TChild>(FlowTree tree, FlowStateProvider<TParent, TChild> stateProvider)
        where TParent : class, IFlowState
        where TChild : class, IFlowState
        => new(tree, stateProvider);

    /// <summary>
    /// State注入付きSubTreeノードを作成する（動的ツリー）。
    /// </summary>
    /// <typeparam name="TParent">親状態の型</typeparam>
    /// <typeparam name="TChild">子状態の型</typeparam>
    /// <param name="treeProvider">ツリーを提供するラムダ</param>
    /// <param name="stateProvider">子Stateを作成するラムダ</param>
    public static SubTreeNode<TParent, TChild> SubTree<TParent, TChild>(FlowTreeProvider<TParent> treeProvider, FlowStateProvider<TParent, TChild> stateProvider)
        where TParent : class, IFlowState
        where TChild : class, IFlowState
        => new(treeProvider, stateProvider);
}
