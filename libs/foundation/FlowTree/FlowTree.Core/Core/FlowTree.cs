using System;

namespace Tomato.FlowTree;

/// <summary>
/// フローツリー定義。
/// 空で作成し、Build()でルートノードを設定する。
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
    /// ツリーを構築する（ステートレス）。
    /// </summary>
    /// <param name="root">ルートノード</param>
    /// <returns>this（メソッドチェーン用）</returns>
    public FlowTree Build(IFlowNode root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _state = null;
        return this;
    }

    /// <summary>
    /// ツリーを構築する（型付き状態付き）。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="state">状態オブジェクト</param>
    /// <param name="root">ルートノード</param>
    /// <returns>this（メソッドチェーン用）</returns>
    public FlowTree Build<T>(T state, IFlowNode root) where T : class, IFlowState
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        return this;
    }

    /// <summary>
    /// ツリーを構築する（型付き状態付き、ビルダー経由）。
    /// 型推論を効かせてラムダ内で型指定を省略できる。
    /// </summary>
    /// <typeparam name="T">状態の型</typeparam>
    /// <param name="state">状態オブジェクト</param>
    /// <param name="builder">ルートノードを構築するラムダ</param>
    /// <returns>this（メソッドチェーン用）</returns>
    public FlowTree Build<T>(T state, Func<FlowBuilder<T>, IFlowNode> builder) where T : class, IFlowState
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        _state = state;
        _root = builder(FlowBuilder<T>.Instance);
        return this;
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
    /// ツリーを評価する（deltaTimeのみ指定）。
    /// </summary>
    /// <param name="deltaTime">前フレームからの経過時間（秒）</param>
    /// <returns>ルートノードの状態</returns>
    public NodeStatus Tick(float deltaTime)
    {
        if (_root == null)
            throw new InvalidOperationException("Tree not built. Call Build() first.");

        _totalTime += deltaTime;

        var context = new FlowContext
        {
            State = _state,
            CallStack = _callStack,
            MaxCallDepth = _maxCallDepth,
            DeltaTime = deltaTime,
            TotalTime = _totalTime
        };

        var status = _root.Tick(ref context);

        // ReturnNodeによる早期終了要求をチェック
        if (context.ReturnRequested)
        {
            _root.Reset(fireExitEvents: true);
            return context.ReturnStatus;
        }

        return status;
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
            throw new InvalidOperationException("Tree not built. Call Build() first.");

        var status = _root.Tick(ref context);

        // ReturnNodeによる早期終了要求をチェック
        if (context.ReturnRequested)
        {
            _root.Reset(fireExitEvents: true);
            // ReturnRequestedをクリアして呼び出し元に制御を返す
            context.ReturnRequested = false;
            return context.ReturnStatus;
        }

        return status;
    }

    /// <summary>
    /// ツリーの状態をリセットする。
    /// </summary>
    /// <param name="fireExitEvents">trueの場合、Running中のEventNodeでOnExitを発火する</param>
    public void Reset(bool fireExitEvents = true)
    {
        _totalTime = 0f;
        _root?.Reset(fireExitEvents);
    }
}
