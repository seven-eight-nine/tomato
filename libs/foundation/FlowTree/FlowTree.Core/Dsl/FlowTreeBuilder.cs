using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// FlowTreeを構築するためのビルダーDSL。
/// </summary>
public sealed class FlowTreeBuilder
{
    private readonly FlowTree _tree;
    private readonly Stack<List<IFlowNode>> _nodeStack;
    private readonly Stack<NodeType> _typeStack;
    private IFlowNode? _root;

    private enum NodeType
    {
        Sequence,
        Selector,
        Parallel,
        Race,
        Join,
        RandomSelector
    }

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
    /// 現在のCompositeノードを終了する。
    /// </summary>
    public FlowTreeBuilder End()
    {
        if (_nodeStack.Count == 0)
            throw new InvalidOperationException("No composite node to end.");

        var children = _nodeStack.Pop().ToArray();
        var type = _typeStack.Pop();

        IFlowNode node = type switch
        {
            NodeType.Sequence => new SequenceNode(children),
            NodeType.Selector => new SelectorNode(children),
            NodeType.Parallel => new ParallelNode(children),
            NodeType.Race => new RaceNode(children),
            NodeType.Join => new JoinNode(children),
            NodeType.RandomSelector => new RandomSelectorNode(children),
            _ => throw new InvalidOperationException($"Unknown node type: {type}")
        };

        AddNode(node);
        return this;
    }

    // =====================================================
    // Decorator Nodes
    // =====================================================

    /// <summary>
    /// Inverterデコレータを追加する（次の1ノードに適用）。
    /// </summary>
    public FlowTreeBuilder Inverter(IFlowNode child)
    {
        AddNode(new InverterNode(child));
        return this;
    }

    /// <summary>
    /// Succeederデコレータを追加する。
    /// </summary>
    public FlowTreeBuilder Succeeder(IFlowNode child)
    {
        AddNode(new SucceederNode(child));
        return this;
    }

    /// <summary>
    /// Failerデコレータを追加する。
    /// </summary>
    public FlowTreeBuilder Failer(IFlowNode child)
    {
        AddNode(new FailerNode(child));
        return this;
    }

    /// <summary>
    /// Repeatデコレータを追加する。
    /// </summary>
    /// <param name="count">繰り返し回数</param>
    /// <param name="child">子ノード</param>
    public FlowTreeBuilder Repeat(int count, IFlowNode child)
    {
        AddNode(new RepeatNode(count, child));
        return this;
    }

    /// <summary>
    /// Retryデコレータを追加する。
    /// </summary>
    /// <param name="maxRetries">最大リトライ回数</param>
    /// <param name="child">子ノード</param>
    public FlowTreeBuilder Retry(int maxRetries, IFlowNode child)
    {
        AddNode(new RetryNode(maxRetries, child));
        return this;
    }

    /// <summary>
    /// Timeoutデコレータを追加する。
    /// </summary>
    /// <param name="timeout">タイムアウト時間（秒）</param>
    /// <param name="child">子ノード</param>
    public FlowTreeBuilder Timeout(float timeout, IFlowNode child)
    {
        AddNode(new TimeoutNode(timeout, child));
        return this;
    }

    /// <summary>
    /// Delayデコレータを追加する。
    /// </summary>
    /// <param name="delay">遅延時間（秒）</param>
    /// <param name="child">子ノード</param>
    public FlowTreeBuilder Delay(float delay, IFlowNode child)
    {
        AddNode(new DelayNode(delay, child));
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
    /// Conditionノードを追加する。
    /// </summary>
    /// <param name="condition">評価する条件</param>
    public FlowTreeBuilder Condition(FlowCondition condition)
    {
        AddNode(new ConditionNode(condition));
        return this;
    }

    /// <summary>
    /// SubTreeノードを追加する。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    public FlowTreeBuilder SubTree(FlowTree tree)
    {
        AddNode(new SubTreeNode(tree));
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

        if (_root == null)
            throw new InvalidOperationException("No root node defined.");

        _tree.SetRoot(_root);
        return _tree;
    }

    private void AddNode(IFlowNode node)
    {
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
}
