using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 指定時間を超えた場合にFailureを返すノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class TimeoutNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly IFlowNode _child;
    private readonly float _timeout;
    private readonly List<float> _elapsedStack;

    /// <summary>
    /// TimeoutNodeを作成する。
    /// </summary>
    /// <param name="timeout">タイムアウト時間（秒）</param>
    /// <param name="child">子ノード</param>
    public TimeoutNode(float timeout, IFlowNode child)
    {
        if (timeout <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        _child = child ?? throw new ArgumentNullException(nameof(child));
        _timeout = timeout;
        _elapsedStack = new List<float>(InitialCapacity) { 0f };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        _elapsedStack[depth] += context.DeltaTime;

        if (_elapsedStack[depth] >= _timeout)
        {
            _elapsedStack[depth] = 0;
            return NodeStatus.Failure;
        }

        var status = _child.Tick(ref context);

        if (status != NodeStatus.Running)
        {
            _elapsedStack[depth] = 0;
        }

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int i = 0; i < _elapsedStack.Count; i++)
        {
            _elapsedStack[i] = 0;
        }
        _child.Reset();
    }

    private void EnsureDepth(int depth)
    {
        while (_elapsedStack.Count <= depth)
        {
            _elapsedStack.Add(0f);
        }
    }
}
