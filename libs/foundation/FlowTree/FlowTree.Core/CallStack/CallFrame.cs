namespace Tomato.FlowTree;

/// <summary>
/// コールスタックフレーム（readonly struct）。
/// </summary>
public readonly struct CallFrame
{
    /// <summary>
    /// 呼び出し先のツリー。
    /// </summary>
    public readonly FlowTree Tree;

    /// <summary>
    /// ノードインデックス（デバッグ用）。
    /// </summary>
    public readonly int NodeIndex;

    /// <summary>
    /// CallFrameを作成する。
    /// </summary>
    /// <param name="tree">ツリー</param>
    /// <param name="nodeIndex">ノードインデックス</param>
    public CallFrame(FlowTree tree, int nodeIndex = 0)
    {
        Tree = tree;
        NodeIndex = nodeIndex;
    }

    public override string ToString() => $"CallFrame(Tree={Tree?.Name ?? "(anonymous)"}, Node={NodeIndex})";
}
