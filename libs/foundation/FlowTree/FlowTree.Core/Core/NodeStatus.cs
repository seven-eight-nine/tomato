namespace Tomato.FlowTree;

/// <summary>
/// ノードの評価結果を表す。
/// </summary>
public enum NodeStatus : byte
{
    /// <summary>
    /// ノードが成功した。
    /// </summary>
    Success = 0,

    /// <summary>
    /// ノードが失敗した。
    /// </summary>
    Failure = 1,

    /// <summary>
    /// ノードが実行中（次のTickで継続）。
    /// </summary>
    Running = 2
}
