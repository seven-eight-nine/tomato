using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// FlowTreeを構築するためのビルダーDSL（ステートレス）。
/// </summary>
public sealed class FlowTreeBuilder
{
    private readonly FlowTree _tree;
    private readonly Stack<List<IFlowNode>> _nodeStack;
    private readonly Stack<NodeType> _typeStack;
    private IFlowNode? _root;

    private enum NodeType
    {
        // Composites
        Sequence,
        Selector,
        Parallel,
        Race,
        Join,
        RandomSelector,
        ShuffledSelector,
        WeightedRandomSelector,
        RoundRobin,
        // Decorators (single child)
        Retry,
        Timeout,
        Delay,
        Repeat,
        RepeatUntilFail,
        RepeatUntilSuccess,
        Inverter,
        Succeeder,
        Failer
    }

    // WeightedRandomSelector用の重みリスト
    private readonly Stack<List<float>> _weightStack;

    // デコレータ用パラメータスタック
    private readonly Stack<float> _decoratorParamStack = new();

    // Event用のペンディングハンドラ
    private FlowEventHandler? _pendingOnEnter;
    private FlowExitEventHandler? _pendingOnExit;

    /// <summary>
    /// FlowTreeBuilderを作成する（内部用）。
    /// FlowTree.Build()から呼び出される。
    /// </summary>
    /// <param name="tree">構築対象のツリー</param>
    internal FlowTreeBuilder(FlowTree tree)
    {
        _tree = tree;
        _nodeStack = new Stack<List<IFlowNode>>();
        _typeStack = new Stack<NodeType>();
        _weightStack = new Stack<List<float>>();
    }

    // =====================================================
    // Composite Nodes
    // =====================================================

    /// <summary>
    /// Sequenceノードを開始する。
    /// </summary>
    public FlowTreeBuilder Sequence()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Sequence);
        return this;
    }

    /// <summary>
    /// Selectorノードを開始する。
    /// </summary>
    public FlowTreeBuilder Selector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Selector);
        return this;
    }

    /// <summary>
    /// Parallelノードを開始する。
    /// </summary>
    /// <param name="policy">評価ポリシー（デフォルト: RequireAll）</param>
    public FlowTreeBuilder Parallel(ParallelPolicy policy = ParallelPolicy.RequireAll)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Parallel);
        return this;
    }

    /// <summary>
    /// Raceノードを開始する。
    /// </summary>
    public FlowTreeBuilder Race()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Race);
        return this;
    }

    /// <summary>
    /// Joinノードを開始する。
    /// </summary>
    /// <param name="policy">評価ポリシー（デフォルト: RequireAll）</param>
    public FlowTreeBuilder Join(JoinPolicy policy = JoinPolicy.RequireAll)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Join);
        return this;
    }

    /// <summary>
    /// RandomSelectorノードを開始する。
    /// </summary>
    public FlowTreeBuilder RandomSelector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RandomSelector);
        return this;
    }

    /// <summary>
    /// ShuffledSelectorノードを開始する。
    /// 全選択肢を一巡するまで同じものを選ばない（シャッフル再生）。
    /// </summary>
    public FlowTreeBuilder ShuffledSelector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.ShuffledSelector);
        return this;
    }

    /// <summary>
    /// WeightedRandomSelectorノードを開始する。
    /// 子ノード追加時にWeighted()で重みを指定する。
    /// </summary>
    public FlowTreeBuilder WeightedRandomSelector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.WeightedRandomSelector);
        _weightStack.Push(new List<float>());
        return this;
    }

    /// <summary>
    /// 重み付きで子ノードを追加する（WeightedRandomSelector内で使用）。
    /// </summary>
    /// <param name="weight">重み</param>
    /// <param name="node">子ノード</param>
    public FlowTreeBuilder Weighted(float weight, IFlowNode node)
    {
        if (_typeStack.Count == 0 || _typeStack.Peek() != NodeType.WeightedRandomSelector)
            throw new InvalidOperationException("Weighted() can only be used inside WeightedRandomSelector.");

        _nodeStack.Peek().Add(node);
        _weightStack.Peek().Add(weight);
        return this;
    }

    /// <summary>
    /// RoundRobinノードを開始する。
    /// 0→1→2→0→... と順番に選択する。
    /// </summary>
    public FlowTreeBuilder RoundRobin()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RoundRobin);
        return this;
    }

    /// <summary>
    /// 現在のComposite/Decoratorノードを終了する。
    /// </summary>
    public FlowTreeBuilder End()
    {
        if (_nodeStack.Count == 0)
            throw new InvalidOperationException("No composite/decorator node to end.");

        var children = _nodeStack.Pop().ToArray();
        var type = _typeStack.Pop();

        IFlowNode node = type switch
        {
            // Composites
            NodeType.Sequence => new SequenceNode(children),
            NodeType.Selector => new SelectorNode(children),
            NodeType.Parallel => new ParallelNode(children),
            NodeType.Race => new RaceNode(children),
            NodeType.Join => new JoinNode(children),
            NodeType.RandomSelector => new RandomSelectorNode(children),
            NodeType.ShuffledSelector => new ShuffledSelectorNode(children),
            NodeType.WeightedRandomSelector => CreateWeightedRandomSelector(children),
            NodeType.RoundRobin => new RoundRobinSelectorNode(children),
            // Decorators
            NodeType.Retry => CreateDecorator(children, p => new RetryNode((int)p, children[0])),
            NodeType.Timeout => CreateDecorator(children, p => new TimeoutNode(p, children[0])),
            NodeType.Delay => CreateDecorator(children, p => new DelayNode(p, children[0])),
            NodeType.Repeat => CreateDecorator(children, p => new RepeatNode((int)p, children[0])),
            NodeType.RepeatUntilFail => CreateDecoratorNoParam(children, () => new RepeatUntilFailNode(children[0])),
            NodeType.RepeatUntilSuccess => CreateDecoratorNoParam(children, () => new RepeatUntilSuccessNode(children[0])),
            NodeType.Inverter => CreateDecoratorNoParam(children, () => new InverterNode(children[0])),
            NodeType.Succeeder => CreateDecoratorNoParam(children, () => new SucceederNode(children[0])),
            NodeType.Failer => CreateDecoratorNoParam(children, () => new FailerNode(children[0])),
            _ => throw new InvalidOperationException($"Unknown node type: {type}")
        };

        AddNode(node);
        return this;
    }

    private IFlowNode CreateDecorator(IFlowNode[] children, Func<float, IFlowNode> factory)
    {
        if (children.Length != 1)
            throw new InvalidOperationException("Decorator must have exactly one child.");
        var param = _decoratorParamStack.Pop();
        return factory(param);
    }

    private IFlowNode CreateDecoratorNoParam(IFlowNode[] children, Func<IFlowNode> factory)
    {
        if (children.Length != 1)
            throw new InvalidOperationException("Decorator must have exactly one child.");
        return factory();
    }

    // =====================================================
    // Decorator Nodes (Scope-based Fluent API)
    // =====================================================

    /// <summary>
    /// Retryデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="maxRetries">最大リトライ回数</param>
    public FlowTreeBuilder Retry(int maxRetries)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Retry);
        _decoratorParamStack.Push(maxRetries);
        return this;
    }

    /// <summary>
    /// Timeoutデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="timeout">タイムアウト時間（秒）</param>
    public FlowTreeBuilder Timeout(float timeout)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Timeout);
        _decoratorParamStack.Push(timeout);
        return this;
    }

    /// <summary>
    /// Delayデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="delay">遅延時間（秒）</param>
    public FlowTreeBuilder Delay(float delay)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Delay);
        _decoratorParamStack.Push(delay);
        return this;
    }

    /// <summary>
    /// Repeatデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="count">繰り返し回数</param>
    public FlowTreeBuilder Repeat(int count)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Repeat);
        _decoratorParamStack.Push(count);
        return this;
    }

    /// <summary>
    /// RepeatUntilFailデコレータを開始する。
    /// 子ノードがFailureを返すまで繰り返す。
    /// </summary>
    public FlowTreeBuilder RepeatUntilFail()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RepeatUntilFail);
        return this;
    }

    /// <summary>
    /// RepeatUntilSuccessデコレータを開始する。
    /// 子ノードがSuccessを返すまで繰り返す。
    /// </summary>
    public FlowTreeBuilder RepeatUntilSuccess()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RepeatUntilSuccess);
        return this;
    }

    /// <summary>
    /// Inverterデコレータを開始する。
    /// 子ノードの結果を反転する。
    /// </summary>
    public FlowTreeBuilder Inverter()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Inverter);
        return this;
    }

    /// <summary>
    /// Succeederデコレータを開始する。
    /// 子ノードの結果を常にSuccessにする。
    /// </summary>
    public FlowTreeBuilder Succeeder()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Succeeder);
        return this;
    }

    /// <summary>
    /// Failerデコレータを開始する。
    /// 子ノードの結果を常にFailureにする。
    /// </summary>
    public FlowTreeBuilder Failer()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Failer);
        return this;
    }

    /// <summary>
    /// Guardデコレータを追加する。
    /// </summary>
    /// <param name="condition">実行条件</param>
    /// <param name="child">子ノード</param>
    public FlowTreeBuilder Guard(FlowCondition condition, IFlowNode child)
    {
        AddNode(new GuardNode(condition, child));
        return this;
    }

    /// <summary>
    /// Eventデコレータを開始する。
    /// 次に追加されるノードにイベントハンドラを適用する。
    /// </summary>
    /// <param name="onEnter">入った瞬間に発火するイベント</param>
    /// <param name="onExit">出た瞬間に発火するイベント</param>
    public FlowTreeBuilder Event(FlowEventHandler? onEnter = null, FlowExitEventHandler? onExit = null)
    {
        if (_pendingOnEnter != null || _pendingOnExit != null)
            throw new InvalidOperationException("Event() already called. Add a node before calling Event() again.");

        _pendingOnEnter = onEnter;
        _pendingOnExit = onExit;
        return this;
    }

    // =====================================================
    // Leaf Nodes
    // =====================================================

    /// <summary>
    /// Actionノードを追加する。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public FlowTreeBuilder Action(FlowAction action)
    {
        AddNode(new ActionNode(action));
        return this;
    }

    /// <summary>
    /// voidアクションを実行してSuccessを返すノードを追加する。
    /// Action()の短縮形。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public FlowTreeBuilder Do(System.Action action)
    {
        AddNode(new ActionNode(() => { action(); return NodeStatus.Success; }));
        return this;
    }

    /// <summary>
    /// Conditionノードを追加する。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public FlowTreeBuilder Condition(FlowCondition condition)
    {
        AddNode(new ConditionNode(condition));
        return this;
    }

    /// <summary>
    /// SubTreeノードを追加する（静的ツリー）。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    public FlowTreeBuilder SubTree(FlowTree tree)
    {
        AddNode(new SubTreeNode(tree));
        return this;
    }

    /// <summary>
    /// SubTreeノードを追加する（動的ツリー）。
    /// </summary>
    /// <param name="treeProvider">ツリーを提供するラムダ</param>
    public FlowTreeBuilder SubTree(FlowTreeProvider treeProvider)
    {
        AddNode(new SubTreeNode(treeProvider));
        return this;
    }

    /// <summary>
    /// Waitノードを追加する。
    /// </summary>
    /// <param name="duration">待機時間（秒）</param>
    public FlowTreeBuilder Wait(float duration)
    {
        AddNode(new WaitNode(duration));
        return this;
    }

    /// <summary>
    /// 条件が満たされるまで待機するノードを追加する。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public FlowTreeBuilder Wait(FlowCondition condition)
    {
        AddNode(new WaitUntilNode(condition));
        return this;
    }

    /// <summary>
    /// Yieldノードを追加する。
    /// </summary>
    public FlowTreeBuilder Yield()
    {
        AddNode(new YieldNode());
        return this;
    }

    /// <summary>
    /// Successノードを追加する。
    /// </summary>
    public FlowTreeBuilder Success()
    {
        AddNode(SuccessNode.Instance);
        return this;
    }

    /// <summary>
    /// Failureノードを追加する。
    /// </summary>
    public FlowTreeBuilder Failure()
    {
        AddNode(FailureNode.Instance);
        return this;
    }

    /// <summary>
    /// 任意のノードを追加する。
    /// </summary>
    /// <param name="node">追加するノード</param>
    public FlowTreeBuilder Node(IFlowNode node)
    {
        AddNode(node);
        return this;
    }

    // =====================================================
    // Build
    // =====================================================

    /// <summary>
    /// FlowTreeの構築を完了する。
    /// </summary>
    /// <returns>構築されたFlowTree</returns>
    public FlowTree Complete()
    {
        if (_nodeStack.Count > 0)
            throw new InvalidOperationException($"Unclosed composite nodes remain. Call End() {_nodeStack.Count} more time(s).");

        if (_pendingOnEnter != null || _pendingOnExit != null)
            throw new InvalidOperationException("Event() was called but no node was added after it.");

        if (_root == null)
            throw new InvalidOperationException("No root node defined.");

        _tree.SetRoot(_root);
        return _tree;
    }

    private void AddNode(IFlowNode node)
    {
        // ペンディングのEventハンドラがあればラップ
        if (_pendingOnEnter != null || _pendingOnExit != null)
        {
            node = new EventNode(node, _pendingOnEnter, _pendingOnExit);
            _pendingOnEnter = null;
            _pendingOnExit = null;
        }

        if (_nodeStack.Count > 0)
        {
            _nodeStack.Peek().Add(node);
        }
        else
        {
            if (_root != null)
                throw new InvalidOperationException("Root node already defined. Use Sequence/Selector to group multiple nodes.");

            _root = node;
        }
    }

    private WeightedRandomSelectorNode CreateWeightedRandomSelector(IFlowNode[] children)
    {
        var weights = _weightStack.Pop();
        var weightedChildren = new (float weight, IFlowNode node)[children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            weightedChildren[i] = (weights[i], children[i]);
        }
        return new WeightedRandomSelectorNode(weightedChildren);
    }
}

/// <summary>
/// 型付きFlowTreeを構築するためのビルダーDSL。
/// </summary>
/// <typeparam name="T">状態の型</typeparam>
public sealed class FlowTreeBuilder<T> where T : class, IFlowState
{
    private readonly FlowTree _tree;
    private readonly Stack<List<IFlowNode>> _nodeStack;
    private readonly Stack<NodeType> _typeStack;
    private IFlowNode? _root;

    private enum NodeType
    {
        // Composites
        Sequence,
        Selector,
        Parallel,
        Race,
        Join,
        RandomSelector,
        ShuffledSelector,
        WeightedRandomSelector,
        RoundRobin,
        // Decorators (single child)
        Retry,
        Timeout,
        Delay,
        Repeat,
        RepeatUntilFail,
        RepeatUntilSuccess,
        Inverter,
        Succeeder,
        Failer
    }

    // WeightedRandomSelector用の重みリスト
    private readonly Stack<List<float>> _weightStack;

    // デコレータ用パラメータスタック
    private readonly Stack<float> _decoratorParamStack = new();

    // Event用のペンディングハンドラ
    private FlowEventHandler<T>? _pendingOnEnter;
    private FlowExitEventHandler<T>? _pendingOnExit;

    /// <summary>
    /// FlowTreeBuilderを作成する（内部用）。
    /// </summary>
    /// <param name="tree">構築対象のツリー</param>
    internal FlowTreeBuilder(FlowTree tree)
    {
        _tree = tree;
        _nodeStack = new Stack<List<IFlowNode>>();
        _typeStack = new Stack<NodeType>();
        _weightStack = new Stack<List<float>>();
    }

    // =====================================================
    // Composite Nodes
    // =====================================================

    /// <summary>
    /// Sequenceノードを開始する。
    /// </summary>
    public FlowTreeBuilder<T> Sequence()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Sequence);
        return this;
    }

    /// <summary>
    /// Selectorノードを開始する。
    /// </summary>
    public FlowTreeBuilder<T> Selector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Selector);
        return this;
    }

    /// <summary>
    /// Parallelノードを開始する。
    /// </summary>
    /// <param name="policy">評価ポリシー（デフォルト: RequireAll）</param>
    public FlowTreeBuilder<T> Parallel(ParallelPolicy policy = ParallelPolicy.RequireAll)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Parallel);
        return this;
    }

    /// <summary>
    /// Raceノードを開始する。
    /// </summary>
    public FlowTreeBuilder<T> Race()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Race);
        return this;
    }

    /// <summary>
    /// Joinノードを開始する。
    /// </summary>
    /// <param name="policy">評価ポリシー（デフォルト: RequireAll）</param>
    public FlowTreeBuilder<T> Join(JoinPolicy policy = JoinPolicy.RequireAll)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Join);
        return this;
    }

    /// <summary>
    /// RandomSelectorノードを開始する。
    /// </summary>
    public FlowTreeBuilder<T> RandomSelector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RandomSelector);
        return this;
    }

    /// <summary>
    /// ShuffledSelectorノードを開始する。
    /// </summary>
    public FlowTreeBuilder<T> ShuffledSelector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.ShuffledSelector);
        return this;
    }

    /// <summary>
    /// WeightedRandomSelectorノードを開始する。
    /// </summary>
    public FlowTreeBuilder<T> WeightedRandomSelector()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.WeightedRandomSelector);
        _weightStack.Push(new List<float>());
        return this;
    }

    /// <summary>
    /// 重み付きで子ノードを追加する（WeightedRandomSelector内で使用）。
    /// </summary>
    /// <param name="weight">重み</param>
    /// <param name="node">子ノード</param>
    public FlowTreeBuilder<T> Weighted(float weight, IFlowNode node)
    {
        if (_typeStack.Count == 0 || _typeStack.Peek() != NodeType.WeightedRandomSelector)
            throw new InvalidOperationException("Weighted() can only be used inside WeightedRandomSelector.");

        _nodeStack.Peek().Add(node);
        _weightStack.Peek().Add(weight);
        return this;
    }

    /// <summary>
    /// RoundRobinノードを開始する。
    /// </summary>
    public FlowTreeBuilder<T> RoundRobin()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RoundRobin);
        return this;
    }

    /// <summary>
    /// 現在のComposite/Decoratorノードを終了する。
    /// </summary>
    public FlowTreeBuilder<T> End()
    {
        if (_nodeStack.Count == 0)
            throw new InvalidOperationException("No composite/decorator node to end.");

        var children = _nodeStack.Pop().ToArray();
        var type = _typeStack.Pop();

        IFlowNode node = type switch
        {
            // Composites
            NodeType.Sequence => new SequenceNode(children),
            NodeType.Selector => new SelectorNode(children),
            NodeType.Parallel => new ParallelNode(children),
            NodeType.Race => new RaceNode(children),
            NodeType.Join => new JoinNode(children),
            NodeType.RandomSelector => new RandomSelectorNode(children),
            NodeType.ShuffledSelector => new ShuffledSelectorNode(children),
            NodeType.WeightedRandomSelector => CreateWeightedRandomSelector(children),
            NodeType.RoundRobin => new RoundRobinSelectorNode(children),
            // Decorators
            NodeType.Retry => CreateDecorator(children, p => new RetryNode((int)p, children[0])),
            NodeType.Timeout => CreateDecorator(children, p => new TimeoutNode(p, children[0])),
            NodeType.Delay => CreateDecorator(children, p => new DelayNode(p, children[0])),
            NodeType.Repeat => CreateDecorator(children, p => new RepeatNode((int)p, children[0])),
            NodeType.RepeatUntilFail => CreateDecoratorNoParam(children, () => new RepeatUntilFailNode(children[0])),
            NodeType.RepeatUntilSuccess => CreateDecoratorNoParam(children, () => new RepeatUntilSuccessNode(children[0])),
            NodeType.Inverter => CreateDecoratorNoParam(children, () => new InverterNode(children[0])),
            NodeType.Succeeder => CreateDecoratorNoParam(children, () => new SucceederNode(children[0])),
            NodeType.Failer => CreateDecoratorNoParam(children, () => new FailerNode(children[0])),
            _ => throw new InvalidOperationException($"Unknown node type: {type}")
        };

        AddNode(node);
        return this;
    }

    private IFlowNode CreateDecorator(IFlowNode[] children, Func<float, IFlowNode> factory)
    {
        if (children.Length != 1)
            throw new InvalidOperationException("Decorator must have exactly one child.");
        var param = _decoratorParamStack.Pop();
        return factory(param);
    }

    private IFlowNode CreateDecoratorNoParam(IFlowNode[] children, Func<IFlowNode> factory)
    {
        if (children.Length != 1)
            throw new InvalidOperationException("Decorator must have exactly one child.");
        return factory();
    }

    // =====================================================
    // Decorator Nodes (Scope-based Fluent API)
    // =====================================================

    /// <summary>
    /// Retryデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="maxRetries">最大リトライ回数</param>
    public FlowTreeBuilder<T> Retry(int maxRetries)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Retry);
        _decoratorParamStack.Push(maxRetries);
        return this;
    }

    /// <summary>
    /// Timeoutデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="timeout">タイムアウト時間（秒）</param>
    public FlowTreeBuilder<T> Timeout(float timeout)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Timeout);
        _decoratorParamStack.Push(timeout);
        return this;
    }

    /// <summary>
    /// Delayデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="delay">遅延時間（秒）</param>
    public FlowTreeBuilder<T> Delay(float delay)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Delay);
        _decoratorParamStack.Push(delay);
        return this;
    }

    /// <summary>
    /// Repeatデコレータを開始する。
    /// End()で閉じるまでに追加した1つの子ノードに適用される。
    /// </summary>
    /// <param name="count">繰り返し回数</param>
    public FlowTreeBuilder<T> Repeat(int count)
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Repeat);
        _decoratorParamStack.Push(count);
        return this;
    }

    /// <summary>
    /// RepeatUntilFailデコレータを開始する。
    /// 子ノードがFailureを返すまで繰り返す。
    /// </summary>
    public FlowTreeBuilder<T> RepeatUntilFail()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RepeatUntilFail);
        return this;
    }

    /// <summary>
    /// RepeatUntilSuccessデコレータを開始する。
    /// 子ノードがSuccessを返すまで繰り返す。
    /// </summary>
    public FlowTreeBuilder<T> RepeatUntilSuccess()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.RepeatUntilSuccess);
        return this;
    }

    /// <summary>
    /// Inverterデコレータを開始する。
    /// 子ノードの結果を反転する。
    /// </summary>
    public FlowTreeBuilder<T> Inverter()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Inverter);
        return this;
    }

    /// <summary>
    /// Succeederデコレータを開始する。
    /// 子ノードの結果を常にSuccessにする。
    /// </summary>
    public FlowTreeBuilder<T> Succeeder()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Succeeder);
        return this;
    }

    /// <summary>
    /// Failerデコレータを開始する。
    /// 子ノードの結果を常にFailureにする。
    /// </summary>
    public FlowTreeBuilder<T> Failer()
    {
        _nodeStack.Push(new List<IFlowNode>());
        _typeStack.Push(NodeType.Failer);
        return this;
    }

    /// <summary>
    /// Guardデコレータを追加する（型付き）。
    /// </summary>
    public FlowTreeBuilder<T> Guard(FlowCondition<T> condition, IFlowNode child)
    {
        AddNode(new GuardNode<T>(condition, child));
        return this;
    }

    /// <summary>
    /// Eventデコレータを開始する（型付き）。
    /// 次に追加されるノードにイベントハンドラを適用する。
    /// </summary>
    public FlowTreeBuilder<T> Event(FlowEventHandler<T>? onEnter = null, FlowExitEventHandler<T>? onExit = null)
    {
        if (_pendingOnEnter != null || _pendingOnExit != null)
            throw new InvalidOperationException("Event() already called. Add a node before calling Event() again.");

        _pendingOnEnter = onEnter;
        _pendingOnExit = onExit;
        return this;
    }

    // =====================================================
    // Leaf Nodes
    // =====================================================

    /// <summary>
    /// Actionノードを追加する（型付き）。
    /// </summary>
    public FlowTreeBuilder<T> Action(FlowAction<T> action)
    {
        AddNode(new ActionNode<T>(action));
        return this;
    }

    /// <summary>
    /// Actionノードを追加する（ステートレス）。
    /// 状態を使用しない場合に使用する。
    /// </summary>
    public FlowTreeBuilder<T> Action(FlowAction action)
    {
        AddNode(new ActionNode(action));
        return this;
    }

    /// <summary>
    /// voidアクションを実行してSuccessを返すノードを追加する（型付き）。
    /// Action()の短縮形。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public FlowTreeBuilder<T> Do(System.Action<T> action)
    {
        AddNode(new ActionNode<T>(s => { action(s); return NodeStatus.Success; }));
        return this;
    }

    /// <summary>
    /// voidアクションを実行してSuccessを返すノードを追加する（ステートレス）。
    /// Action()の短縮形。
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public FlowTreeBuilder<T> Do(System.Action action)
    {
        AddNode(new ActionNode(() => { action(); return NodeStatus.Success; }));
        return this;
    }

    /// <summary>
    /// Conditionノードを追加する（型付き）。
    /// </summary>
    public FlowTreeBuilder<T> Condition(FlowCondition<T> condition)
    {
        AddNode(new ConditionNode<T>(condition));
        return this;
    }

    /// <summary>
    /// Conditionノードを追加する（ステートレス）。
    /// 状態を使用しない場合に使用する。
    /// </summary>
    public FlowTreeBuilder<T> Condition(FlowCondition condition)
    {
        AddNode(new ConditionNode(condition));
        return this;
    }

    /// <summary>
    /// SubTreeノードを追加する（静的ツリー、親のStateを継承）。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    public FlowTreeBuilder<T> SubTree(FlowTree tree)
    {
        AddNode(new SubTreeNode(tree));
        return this;
    }

    /// <summary>
    /// SubTreeノードを追加する（動的ツリー、親のStateを継承）。
    /// </summary>
    /// <param name="treeProvider">ツリーを提供するラムダ</param>
    public FlowTreeBuilder<T> SubTree(FlowTreeProvider<T> treeProvider)
    {
        AddNode(new SubTreeNode<T>(treeProvider));
        return this;
    }

    /// <summary>
    /// SubTreeノードを追加する（静的ツリー、新しいStateを注入）。
    /// </summary>
    /// <typeparam name="TChild">子状態の型</typeparam>
    /// <param name="tree">呼び出すツリー</param>
    /// <param name="stateProvider">子Stateを作成するラムダ。Parentプロパティは自動設定される。</param>
    public FlowTreeBuilder<T> SubTree<TChild>(FlowTree tree, FlowStateProvider<T, TChild> stateProvider)
        where TChild : class, IFlowState
    {
        AddNode(new SubTreeNode<T, TChild>(tree, stateProvider));
        return this;
    }

    /// <summary>
    /// SubTreeノードを追加する（動的ツリー、新しいStateを注入）。
    /// </summary>
    /// <typeparam name="TChild">子状態の型</typeparam>
    /// <param name="treeProvider">ツリーを提供するラムダ</param>
    /// <param name="stateProvider">子Stateを作成するラムダ。Parentプロパティは自動設定される。</param>
    public FlowTreeBuilder<T> SubTree<TChild>(FlowTreeProvider<T> treeProvider, FlowStateProvider<T, TChild> stateProvider)
        where TChild : class, IFlowState
    {
        AddNode(new SubTreeNode<T, TChild>(treeProvider, stateProvider));
        return this;
    }

    /// <summary>
    /// Waitノードを追加する。
    /// </summary>
    public FlowTreeBuilder<T> Wait(float duration)
    {
        AddNode(new WaitNode(duration));
        return this;
    }

    /// <summary>
    /// 条件が満たされるまで待機するノードを追加する（型付き）。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public FlowTreeBuilder<T> Wait(FlowCondition<T> condition)
    {
        AddNode(new WaitUntilNode<T>(condition));
        return this;
    }

    /// <summary>
    /// 条件が満たされるまで待機するノードを追加する（ステートレス）。
    /// </summary>
    /// <param name="condition">待機条件（trueで待機終了）</param>
    public FlowTreeBuilder<T> Wait(FlowCondition condition)
    {
        AddNode(new WaitUntilNode(condition));
        return this;
    }

    /// <summary>
    /// Yieldノードを追加する。
    /// </summary>
    public FlowTreeBuilder<T> Yield()
    {
        AddNode(new YieldNode());
        return this;
    }

    /// <summary>
    /// Successノードを追加する。
    /// </summary>
    public FlowTreeBuilder<T> Success()
    {
        AddNode(SuccessNode.Instance);
        return this;
    }

    /// <summary>
    /// Failureノードを追加する。
    /// </summary>
    public FlowTreeBuilder<T> Failure()
    {
        AddNode(FailureNode.Instance);
        return this;
    }

    /// <summary>
    /// 任意のノードを追加する。
    /// </summary>
    public FlowTreeBuilder<T> Node(IFlowNode node)
    {
        AddNode(node);
        return this;
    }

    // =====================================================
    // Build
    // =====================================================

    /// <summary>
    /// FlowTreeの構築を完了する。
    /// </summary>
    public FlowTree Complete()
    {
        if (_nodeStack.Count > 0)
            throw new InvalidOperationException($"Unclosed composite nodes remain. Call End() {_nodeStack.Count} more time(s).");

        if (_pendingOnEnter != null || _pendingOnExit != null)
            throw new InvalidOperationException("Event() was called but no node was added after it.");

        if (_root == null)
            throw new InvalidOperationException("No root node defined.");

        _tree.SetRoot(_root);
        return _tree;
    }

    private void AddNode(IFlowNode node)
    {
        // ペンディングのEventハンドラがあればラップ
        if (_pendingOnEnter != null || _pendingOnExit != null)
        {
            node = new EventNode<T>(node, _pendingOnEnter, _pendingOnExit);
            _pendingOnEnter = null;
            _pendingOnExit = null;
        }

        if (_nodeStack.Count > 0)
        {
            _nodeStack.Peek().Add(node);
        }
        else
        {
            if (_root != null)
                throw new InvalidOperationException("Root node already defined. Use Sequence/Selector to group multiple nodes.");

            _root = node;
        }
    }

    private WeightedRandomSelectorNode CreateWeightedRandomSelector(IFlowNode[] children)
    {
        var weights = _weightStack.Pop();
        var weightedChildren = new (float weight, IFlowNode node)[children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            weightedChildren[i] = (weights[i], children[i]);
        }
        return new WeightedRandomSelectorNode(weightedChildren);
    }
}
