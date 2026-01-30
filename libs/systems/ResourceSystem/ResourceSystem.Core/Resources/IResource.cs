namespace Tomato.ResourceSystem;

/// <summary>
/// ユーザーが実装するリソースインターフェース
/// ロード/アンロードのロジックを定義する
/// </summary>
public interface IResource
{
    /// <summary>
    /// ロード進捗計算用のポイント
    /// 重いリソースは大きな値を返すことで、ローディングバーの進捗を正確に表現できる
    /// デフォルトは1
    /// </summary>
    int Point { get; }

    /// <summary>
    /// ロード開始（非同期ロードを開始する）
    /// </summary>
    void Start();

    /// <summary>
    /// ロード処理（毎Tick呼ばれる）
    /// catalogを使って依存リソースを動的にロードできる
    /// </summary>
    /// <param name="catalog">依存リソースのロードに使用するカタログ</param>
    /// <returns>
    /// Loaded: ロード完了
    /// Loading: まだロード中
    /// Failed: ロード失敗
    /// </returns>
    ResourceLoadState Tick(ResourceCatalog catalog);

    /// <summary>
    /// ロード完了後のリソースを取得
    /// </summary>
    /// <returns>ロードされたリソース、未ロードの場合はnull</returns>
    object? GetResource();

    /// <summary>
    /// アンロード（リソースを解放する）
    /// ProcessLoad内で立てたLoaderもここで解放する
    /// </summary>
    void Unload();
}

/// <summary>
/// 型安全版のリソースインターフェース
/// </summary>
/// <typeparam name="TResource">リソースの型</typeparam>
public interface IResource<TResource> : IResource
    where TResource : class
{
    /// <summary>
    /// 型安全なリソース取得
    /// </summary>
    /// <returns>ロードされたリソース、未ロードの場合はnull</returns>
    new TResource? GetResource();

}
