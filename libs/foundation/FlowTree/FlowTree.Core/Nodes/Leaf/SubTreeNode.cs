using System;
using System.Collections.Generic;

namespace Tomato.FlowTree;

/// <summary>
/// サブツリーを呼び出すノード。
/// FlowTree参照を直接保持することで、自己再帰・相互再帰をサポート。
/// </summary>
public sealed class SubTreeNode : IFlowNode
{
    private const int InitialCapacity = 4;

    private readonly FlowTree _tree;

    // Running状態の継続追跡用（呼び出し深度ごと）
    private readonly List<bool> _wasRunningStack;

    /// <summary>
    /// SubTreeNodeを作成する。
    /// </summary>
    /// <param name="tree">呼び出すツリー</param>
    public SubTreeNode(FlowTree tree)
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _wasRunningStack = new List<bool>(InitialCapacity) { false };
    }

    /// <inheritdoc/>
    public NodeStatus Tick(ref FlowContext context)
    {
        // ツリーが構築されていない場合は失敗
        if (_tree.Root == null)
            return NodeStatus.Failure;

        // 現在の呼び出し深度を取得
        int depth = context.CurrentCallDepth;
        EnsureDepth(depth);

        // コールスタックに追加
        if (context.CallStack != null)
        {
            // スタックオーバーフローチェック
            if (context.CallStack.Count >= context.MaxCallDepth)
                return NodeStatus.Failure;

            if (!context.CallStack.TryPush(new CallFrame(_tree)))
                return NodeStatus.Failure;
        }

        // サブツリーを実行
        var status = _tree.Tick(ref context);

        // コールスタックからポップ
        if (context.CallStack != null)
        {
            context.CallStack.TryPop(out _);
        }

        // Runningの場合は次のTickで継続できるようにフラグを設定
        _wasRunningStack[depth] = (status == NodeStatus.Running);

        return status;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        for (int i = 0; i < _wasRunningStack.Count; i++)
        {
            _wasRunningStack[i] = false;
        }
    }

    private void EnsureDepth(int depth)
    {
        while (_wasRunningStack.Count <= depth)
        {
            _wasRunningStack.Add(false);
        }
    }
}
