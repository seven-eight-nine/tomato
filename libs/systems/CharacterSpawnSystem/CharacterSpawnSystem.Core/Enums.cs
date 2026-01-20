using System;

namespace Tomato.CharacterSpawnSystem;

/// <summary>
/// 外部から指定する目標状態
/// </summary>
public enum CharacterRequestState
{
    /// <summary>配置なし・完全に削除</summary>
    None = 0,

    /// <summary>配置のみ（データのみ存在、ゲームオブジェクトなし）</summary>
    PlacedOnly = 1,

    /// <summary>実体化済み・非アクティブ（すぐ有効化できる準備済み）</summary>
    Ready = 2,

    /// <summary>実体化済み・アクティブ（完全に動作中）</summary>
    Active = 3
}

/// <summary>
/// 内部で管理する詳細な状態
/// </summary>
public enum CharacterInternalState
{
    // === 配置なし ===
    /// <summary>配置されていない・データリソースもロードされていない</summary>
    NotPlaced = 0,

    // === 配置のみ（データリソース関連） ===
    /// <summary>配置要求あり・データリソースロード中</summary>
    PlacedDataLoading = 10,
    /// <summary>配置済み・データリソースロード完了（GOなし）</summary>
    PlacedDataLoaded = 11,

    // === 実体化（ゲームオブジェクトリソース関連） ===
    /// <summary>実体化要求あり・GOリソースロード中</summary>
    InstantiatingGOLoading = 20,
    /// <summary>実体化済み・GOリソースロード完了・GameObject非アクティブ</summary>
    InstantiatedInactive = 21,
    /// <summary>実体化済み・GameObject アクティブ</summary>
    InstantiatedActive = 22,

    // === エラー状態 ===
    /// <summary>データリソースのロード失敗</summary>
    DataLoadFailed = 90,
    /// <summary>GOリソースのロード失敗</summary>
    GameObjectLoadFailed = 91,
}

/// <summary>
/// リソースロード結果
/// </summary>
public enum ResourceLoadResult
{
    Success,
    Failed
}
