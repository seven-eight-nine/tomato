using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 子ノードが失敗した場合に指定回数リトライするノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class RetryNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode _child;
    private readonly int _maxRetries;
    private readonly List<int> _currentRetryStack;

    /// <summary>
    /// RetryNodeを作成する。
    /// </summary>
    /// <param name="maxRetries">最大リトライ回数</param>
    /// <param name="child">子ノード</param>
    public RetryNode(int maxRetries, IFlowNode child)
    {
        if (maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "MaxRetries must be non-negative.");

        _child = child ?? throw new ArgumentNullException(nameof(child));
        _maxRetries = maxRetries;
        _currentRetryStack = new List<int>(InitialCapacity) { 0 };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        var status = _child.Tick(ref context);

        switch (status)
        {
            case NodeStatus.Running:
                return NodeStatus.Running;

            case NodeStatus.Success:
                _currentRetryStack[depth] = 0;
                return NodeStatus.Success;

            case NodeStatus.Failure:
                if (_currentRetryStack[depth] < _maxRetries)
                {
                    _currentRetryStack[depth]++;
                    _child.Reset();
                    return NodeStatus.Running;
                }
                _currentRetryStack[depth] = 0;
                return NodeStatus.Failure;
        }

        return NodeStatus.Failure;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int i = 0; i < _currentRetryStack.Count; i++)
        {
            _currentRetryStack[i] = 0;
        }
        _child.Reset();
    }

    private void EnsureDepth(int depth)
    {
        while (_currentRetryStack.Count <= depth)
        {
            _currentRetryStack.Add(0);
        }
    }
}
