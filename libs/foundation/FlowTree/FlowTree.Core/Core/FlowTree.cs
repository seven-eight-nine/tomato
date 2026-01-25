using System;

namespace Tomato.FlowTree;

/// <summary>
/// フローツリー定義。
/// 空で作成し、Build()でビルダーを取得して構築する。
/// </summary>
public sealed class FlowTree
{
    private IFlowNode? _root;
    private object? _state;
    private FlowCallStack? _callStack;
    private int _maxCallDepth = 32;
    private float _totalTime;

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
    /// 状態なしでビルダーを取得してツリーを構築する。
    /// </summary>
    /// <returns>FlowTreeBuilder</returns>
    public FlowTreeBuilder Build()
    {
        _state = null;
        return new FlowTreeBuilder(this);
    }

    /// <summary>
    /// 型付き状態を設定してビルダーを取得する。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="state">状態オブジェクト</param>
    /// <returns>型付きFlowTreeBuilder</returns>
    public FlowTreeBuilder<T> Build<T>(T state) where T : class, IFlowState
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        return new FlowTreeBuilder<T>(this);
    }

    /// <summary>
    /// コールスタックを設定する。
    /// サブツリー呼び出しを使用する場合に必要。
    /// </summary>
    /// <param name="callStack">コールスタック</param>
    /// <returns>this（メソッドチェーン用）</returns>
    public FlowTree WithCallStack(FlowCallStack callStack)
    {
        _callStack = callStack ?? throw new ArgumentNullException(nameof(callStack));
        return this;
    }

    /// <summary>
    /// 最大コールスタック深度を設定する。
    /// </summary>
    /// <param name="maxDepth">最大深度</param>
    /// <returns>this（メソッドチェーン用）</returns>
    public FlowTree WithMaxCallDepth(int maxDepth)
    {
        _maxCallDepth = maxDepth;
        return this;
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
    /// ツリーを評価する（deltaTimeのみ指定）。
    /// </summary>
    /// <param name="deltaTime">前フレームからの経過時間（秒）</param>
    /// <returns>ルートノードの状態</returns>
    public NodeStatus Tick(float deltaTime)
    {
        if (_root == null)
            throw new InvalidOperationException("Tree not built. Call Build()...Complete() first.");

        _totalTime += deltaTime;

        var context = new FlowContext
        {
            State = _state,
            CallStack = _callStack,
            MaxCallDepth = _maxCallDepth,
            DeltaTime = deltaTime,
            TotalTime = _totalTime
        };

        return _root.Tick(ref context);
    }

    /// <summary>
    /// ツリーを評価する（コンテキスト直接指定）。
    /// サブツリーからの呼び出しやカスタムコンテキストが必要な場合に使用。
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
        _totalTime = 0f;
        _root?.Reset();
    }
}
