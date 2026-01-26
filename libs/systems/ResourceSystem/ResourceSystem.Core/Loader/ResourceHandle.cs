namespace Tomato.ResourceSystem;

/// <summary>
/// リソースへのハンドル構造体
/// ResourceEntryへの参照を保持し、リソースへのアクセスを提供する
/// </summary>
public readonly struct ResourceHandle
{
    private readonly ResourceEntry? _entry;

    internal ResourceHandle(ResourceEntry entry)
    {
        _entry = entry;
    }

    /// <summary>
    /// ハンドルが有効かどうか
    /// </summary>
    public bool IsValid => _entry != null;

    /// <summary>
    /// リソースのロード状態
    /// </summary>
    public ResourceLoadState State => _entry?.State ?? ResourceLoadState.Unloaded;

    /// <summary>
    /// リソースがロード済みかどうか
    /// </summary>
    public bool IsLoaded => _entry?.IsLoaded ?? false;

    /// <summary>
    /// 型安全にリソースを取得する
    /// </summary>
    /// <typeparam name="TResource">期待するリソースの型</typeparam>
    /// <param name="resource">取得されたリソース</param>
    /// <returns>取得できた場合はtrue</returns>
    public bool TryGet<TResource>(out TResource? resource) where TResource : class
    {
        if (_entry != null && _entry.IsLoaded)
        {
            var loaded = _entry.LoadedResource;
            if (loaded is TResource typedResource)
            {
                resource = typedResource;
                return true;
            }
        }
        resource = null;
        return false;
    }

    /// <summary>
    /// リソースを型チェックなしで取得する
    /// </summary>
    /// <returns>リソース、またはnull</returns>
    public object? GetResourceUnsafe()
    {
        return _entry?.LoadedResource;
    }
}
