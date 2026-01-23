using System;

namespace Tomato.FlowTree;

/// <summary>
/// フローツリー定義。
/// 空で作成し、Build()でビルダーを取得して構築する。
/// </summary>
public sealed class FlowTree
{
    private IFlowNode? _root;

    /// <summary>
    /// ルートノード。
    /// </summary>
    public IFlowNode? Root => _root;

    /// <summary>
    /// ツリー名（デバッグ用）。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 空のFlowTreeを作成する。
    /// </summary>
    /// <param name="name">ツリー名（デバッグ用）</param>
    public FlowTree(string? name = null)
    {
        Name = name;
    }

    /// <summary>
    /// ビルダーを取得してツリーを構築する。
    /// </summary>
    /// <returns>FlowTreeBuilder</returns>
    public FlowTreeBuilder Build()
    {
        return new FlowTreeBuilder(this);
    }

    /// <summary>
    /// ルートノードを設定する（ビルダーから呼び出される）。
    /// </summary>
    /// <param name="root">ルートノード</param>
    internal void SetRoot(IFlowNode root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>
    /// ツリーを評価する。
    /// </summary>
    /// <param name="context">実行コンテキスト</param>
    /// <returns>ルートノードの状態</returns>
    public NodeStatus Tick(ref FlowContext context)
    {
        if (_root == null)
            throw new InvalidOperationException("Tree not built. Call Build()...Complete() first.");

        return _root.Tick(ref context);
    }

    /// <summary>
    /// ツリーの状態をリセットする。
    /// </summary>
    public void Reset()
    {
        _root?.Reset();
    }
}
