using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// 指定時間待機するノード。
/// 再帰呼び出しをサポート（呼び出し深度ごとに状態を管理）。
/// </summary>
public sealed class WaitNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly float _duration;
    private readonly List<float> _elapsedStack;

    /// <summary>
    /// WaitNodeを作成する。
    /// </summary>
    /// <param name="duration">待機時間（秒）</param>
    public WaitNode(float duration)
    {
        if (duration < 0)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be non-negative.");

        _duration = duration;
        _elapsedStack = new List<float>(InitialCapacity) { 0f };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        _elapsedStack[depth] += context.DeltaTime;

        if (_elapsedStack[depth] >= _duration)
        {
            _elapsedStack[depth] = 0;
            return NodeStatus.Success;
        }

        return NodeStatus.Running;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int i = 0; i < _elapsedStack.Count; i++)
        {
            _elapsedStack[i] = 0;
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_elapsedStack.Count <= depth)
        {
            _elapsedStack.Add(0f);
        }
    }
}
