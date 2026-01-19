using System;

namespace Tomato.CharacterSpawnSystem;

/// <summary>
/// リソースロード完了コールバック
/// </summary>
/// <param name="result">ロード結果</param>
/// <param name="resource">ロードされたリソース（失敗時はnull）</param>
public delegate void ResourceLoadCallback(ResourceLoadResult result, object resource);

/// <summary>
/// リソースローダーインターフェース
/// </summary>
public interface IResourceLoader
{
    /// <summary>
    /// データリソースの非同期ロード開始
    /// </summary>
    /// <param name="characterId">キャラクターID</param>
    /// <param name="callback">完了コールバック</param>
    void LoadDataResourceAsync(string characterId, ResourceLoadCallback callback);

    /// <summary>
    /// ゲームオブジェクトリソースの非同期ロード開始
    /// </summary>
    /// <param name="characterId">キャラクターID</param>
    /// <param name="callback">完了コールバック</param>
    void LoadGameObjectResourceAsync(string characterId, ResourceLoadCallback callback);

    /// <summary>
    /// データリソースのアンロード
    /// </summary>
    /// <param name="resource">リソース</param>
    void UnloadDataResource(object resource);

    /// <summary>
    /// ゲームオブジェクトリソースのアンロード
    /// </summary>
    /// <param name="resource">リソース</param>
    void UnloadGameObjectResource(object resource);
}

/// <summary>
/// GameObjectの抽象化インターフェース
/// </summary>
public interface IGameObjectProxy
{
    /// <summary>
    /// アクティブ状態の取得・設定
    /// </summary>
    bool IsActive { get; set; }

    /// <summary>
    /// 破棄
    /// </summary>
    void Destroy();
}

/// <summary>
/// GameObjectファクトリインターフェース
/// </summary>
public interface IGameObjectFactory
{
    /// <summary>
    /// GameObjectの生成
    /// </summary>
    /// <param name="gameObjectResource">ゲームオブジェクトリソース</param>
    /// <param name="dataResource">データリソース</param>
    /// <returns>生成されたGameObjectのプロキシ</returns>
    IGameObjectProxy CreateGameObject(object gameObjectResource, object dataResource);
}
