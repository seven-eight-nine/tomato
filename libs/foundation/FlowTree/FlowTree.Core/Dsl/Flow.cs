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
    /// Parallelノードを作成する。
    /// </summary>
    /// <param name="children">子ノードの配列</param>
    public static ParallelNode Parallel(params IFlowNode[] children)
        => new(children);

    /// <summary>
    /// Parallelノードを作成する（ポリシー指定）。
    /// </summary>
    /// <param name="policy">評価ポリシー</param>
    /// <param name="children">子ノードの配列</param>
    public static ParallelNode Parallel(ParallelPolicy policy, params IFlowNode[] children)
        => new(policy, children);

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
}
