namespace Tomato.FlowTree;

/// <summary>
/// フローツリー実行コンテキスト（struct）。
/// </summary>
public struct FlowContext
{
    /// <summary>
    /// Blackboard（共有データ）。
    /// </summary>
    public Blackboard Blackboard;

    /// <summary>
    /// コールスタック（サブツリー呼び出し追跡用）。
    /// </summary>
    public FlowCallStack? CallStack;

    /// <summary>
    /// 最大コールスタック深度。
    /// </summary>
    public int MaxCallDepth;

    /// <summary>
    /// 前フレームからの経過時間（秒）。
    /// </summary>
    public float DeltaTime;

    /// <summary>
    /// 累計経過時間（秒）。
    /// </summary>
    public float TotalTime;

    /// <summary>
    /// 任意のユーザーデータ。
    /// </summary>
    public object? UserData;

    /// <summary>
    /// 現在の呼び出し深度を取得する。
    /// 再帰呼び出し時のノード状態管理に使用。
    /// </summary>
    public readonly int CurrentCallDepth => CallStack?.Count ?? 0;

    /// <summary>
    /// デフォルトのFlowContextを作成する。
    /// </summary>
    /// <param name="blackboard">Blackboard</param>
    /// <param name="deltaTime">経過時間</param>
    /// <returns>FlowContext</returns>
    public static FlowContext Create(Blackboard blackboard, float deltaTime = 0f)
    {
        return new FlowContext
        {
            Blackboard = blackboard,
            CallStack = null,
            MaxCallDepth = 32,
            DeltaTime = deltaTime,
            TotalTime = 0f,
            UserData = null
        };
    }

    /// <summary>
    /// 詳細なFlowContextを作成する。
    /// </summary>
    /// <param name="blackboard">Blackboard</param>
    /// <param name="callStack">コールスタック</param>
    /// <param name="deltaTime">経過時間</param>
    /// <param name="maxCallDepth">最大コールスタック深度</param>
    /// <returns>FlowContext</returns>
    public static FlowContext Create(
        Blackboard blackboard,
        FlowCallStack? callStack,
        float deltaTime = 0f,
        int maxCallDepth = 32)
    {
        return new FlowContext
        {
            Blackboard = blackboard,
            CallStack = callStack,
            MaxCallDepth = maxCallDepth,
            DeltaTime = deltaTime,
            TotalTime = 0f,
            UserData = null
        };
    }
}
