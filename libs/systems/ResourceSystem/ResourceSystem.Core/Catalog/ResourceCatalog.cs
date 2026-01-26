using System.Collections.Generic;

namespace Tomato.ResourceSystem;

/// <summary>
/// IResourceを登録し、内部でResourceEntryを生成・管理するカタログ
/// </summary>
public sealed class ResourceCatalog
{
    private readonly Dictionary<string, ResourceEntry> _entries;
    private readonly object _lock;

    public ResourceCatalog()
    {
        _entries = new Dictionary<string, ResourceEntry>();
        _lock = new object();
    }

    /// <summary>
    /// 登録されているリソースの数
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// リソースを登録する
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <param name="resource">リソース</param>
    /// <exception cref="System.ArgumentNullException">keyまたはresourceがnullの場合</exception>
    /// <exception cref="System.ArgumentException">keyが既に登録済みの場合</exception>
    public void Register(string key, IResource resource)
    {
        if (key == null)
            throw new System.ArgumentNullException(nameof(key));
        if (resource == null)
            throw new System.ArgumentNullException(nameof(resource));

        lock (_lock)
        {
            if (_entries.ContainsKey(key))
                throw new System.ArgumentException($"Key '{key}' is already registered.", nameof(key));

            _entries[key] = new ResourceEntry(resource);
        }
    }

    /// <summary>
    /// リソースの登録を解除する
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <exception cref="System.ArgumentNullException">keyがnullの場合</exception>
    /// <exception cref="System.InvalidOperationException">リソースがまだ参照されている場合</exception>
    public void Unregister(string key)
    {
        if (key == null)
            throw new System.ArgumentNullException(nameof(key));

        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.RefCount > 0)
                    throw new System.InvalidOperationException($"Cannot unregister '{key}': resource is still referenced (RefCount={entry.RefCount}).");

                entry.Unload();
                _entries.Remove(key);
            }
        }
    }

    /// <summary>
    /// 指定したキーのリソースが登録されているかどうかを確認する
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <returns>登録されている場合はtrue</returns>
    public bool Contains(string key)
    {
        if (key == null)
            return false;

        lock (_lock)
        {
            return _entries.ContainsKey(key);
        }
    }

    /// <summary>
    /// 登録されている全てのキーを取得する
    /// </summary>
    /// <returns>キーのコレクション</returns>
    public IReadOnlyCollection<string> GetAllKeys()
    {
        lock (_lock)
        {
            return new List<string>(_entries.Keys);
        }
    }

    /// <summary>
    /// 指定したキーのResourceEntryを取得する（Loader用）
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <param name="entry">取得されたエントリ</param>
    /// <returns>取得できた場合はtrue</returns>
    internal bool TryGetEntry(string key, out ResourceEntry? entry)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var e))
            {
                entry = e;
                return true;
            }
            entry = null;
            return false;
        }
    }

    /// <summary>
    /// 指定したキーのResourceEntryを取得する（Loader用）
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <returns>取得されたエントリ</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">キーが見つからない場合</exception>
    internal ResourceEntry GetEntry(string key)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                return entry;
            }
            throw new KeyNotFoundException($"Resource key '{key}' not found in catalog.");
        }
    }
}
