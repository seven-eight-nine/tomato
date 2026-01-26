namespace Tomato.ResourceSystem;

/// <summary>
/// IResourceをラップし、参照カウント・状態を管理する内部クラス
/// </summary>
internal sealed class ResourceEntry
{
    private readonly IResource _resource;
    private ResourceLoadState _state;
    private int _refCount;

    public ResourceEntry(IResource resource)
    {
        _resource = resource ?? throw new System.ArgumentNullException(nameof(resource));
        _state = ResourceLoadState.Unloaded;
        _refCount = 0;
    }

    /// <summary>
    /// リソースの現在のロード状態
    /// </summary>
    public ResourceLoadState State => _state;

    /// <summary>
    /// 参照カウント
    /// </summary>
    public int RefCount => _refCount;

    /// <summary>
    /// リソースがロード済みかどうか
    /// </summary>
    public bool IsLoaded => _state == ResourceLoadState.Loaded;

    /// <summary>
    /// ロード進捗計算用のポイント
    /// </summary>
    public int Point => _resource.Point;

    /// <summary>
    /// ロード済みリソースを取得
    /// </summary>
    public object? LoadedResource => _resource.GetResource();

    /// <summary>
    /// ロード開始（未ロード状態の場合のみ）
    /// </summary>
    public void Start()
    {
        if (_state == ResourceLoadState.Unloaded)
        {
            _state = ResourceLoadState.Loading;
            _resource.Start();
        }
    }

    /// <summary>
    /// Tick処理（ロード中の場合のみ）
    /// </summary>
    /// <param name="catalog">依存リソースのロードに使用するカタログ</param>
    public void Tick(ResourceCatalog catalog)
    {
        if (_state == ResourceLoadState.Loading || _state == ResourceLoadState.Failed)
        {
            _state = _resource.Tick(catalog);
        }
    }

    /// <summary>
    /// アンロード処理
    /// </summary>
    public void Unload()
    {
        if (_state != ResourceLoadState.Unloaded)
        {
            _resource.Unload();
            _state = ResourceLoadState.Unloaded;
        }
    }

    /// <summary>
    /// 参照カウントを増加
    /// </summary>
    /// <returns>新しい参照カウント</returns>
    public int Acquire()
    {
        return ++_refCount;
    }

    /// <summary>
    /// 参照カウントを減少
    /// </summary>
    /// <returns>新しい参照カウント</returns>
    public int Release()
    {
        if (_refCount > 0)
        {
            --_refCount;
        }
        return _refCount;
    }
}
