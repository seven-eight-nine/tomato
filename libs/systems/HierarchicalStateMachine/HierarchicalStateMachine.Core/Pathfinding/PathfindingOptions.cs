namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// パス探索のオプション。
/// </summary>
public class PathfindingOptions
{
    /// <summary>
    /// 最大反復回数（無限ループ防止）。
    /// </summary>
    public int MaxIterations { get; set; } = 10000;

    /// <summary>
    /// タイムアウト時間（ミリ秒）。0以下で無制限。
    /// </summary>
    public double TimeoutMilliseconds { get; set; } = 0;

    /// <summary>
    /// 部分探索を許可するか。
    /// タイムアウト時に現時点で最もゴールに近いパスを返す。
    /// </summary>
    public bool AllowPartialPath { get; set; } = false;

    /// <summary>
    /// 階層状態のサブグラフを探索するか。
    /// </summary>
    public bool SearchSubGraphs { get; set; } = true;

    /// <summary>
    /// 最大探索深度（階層探索時）。
    /// </summary>
    public int MaxDepth { get; set; } = 100;

    /// <summary>
    /// デフォルトオプション。
    /// </summary>
    public static readonly PathfindingOptions Default = new();
}
