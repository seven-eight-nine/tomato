namespace Tomato.ResourceSystem;

/// <summary>
/// リソースのロード状態を表す列挙型
/// </summary>
public enum ResourceLoadState : byte
{
    /// <summary>未ロード</summary>
    Unloaded = 0,

    /// <summary>ロード中</summary>
    Loading = 1,

    /// <summary>ロード完了</summary>
    Loaded = 2,

    /// <summary>ロード失敗（リトライ待ち）</summary>
    Failed = 3
}
