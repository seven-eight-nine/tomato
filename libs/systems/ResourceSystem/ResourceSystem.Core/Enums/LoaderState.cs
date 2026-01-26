namespace Tomato.ResourceSystem;

/// <summary>
/// Loaderの状態を表す列挙型
/// </summary>
public enum LoaderState : byte
{
    /// <summary>待機中</summary>
    Idle = 0,

    /// <summary>ロード中</summary>
    Loading = 1,

    /// <summary>全リソースロード完了</summary>
    Loaded = 2
}
