using System.Collections.Generic;

namespace Tomato.ResourceSystem;

/// <summary>
/// IResourceを登録し、内部でResourceEntryを生成・管理するカタログ
/// </summary>
public sealed class ResourceCatalog
{
    private enum RequestType : byte { Load, Unload }

    private readonly struct PendingRequest
    {
        public readonly ResourceEntry Entry;
        public readonly RequestType Type;

        public PendingRequest(ResourceEntry entry, RequestType type)
        {
            Entry = entry;
            Type = type;
        }
    }

    private readonly Dictionary<string, ResourceEntry> _entries;
    private readonly object _lock;
    private List<PendingRequest> _pendingRequests;
    private List<PendingRequest> _processingRequests;

    public ResourceCatalog()
    {
        _entries = new Dictionary<string, ResourceEntry>();
        _lock = new object();
        _pendingRequests = new List<PendingRequest>();
        _processingRequests = new List<PendingRequest>();
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

    /// <summary>
    /// リソースのロードリクエストを追加する（Loader用）
    /// </summary>
    /// <param name="entry">エントリ</param>
    internal void RequestLoad(ResourceEntry entry)
    {
        lock (_lock)
        {
            _pendingRequests.Add(new PendingRequest(entry, RequestType.Load));
        }
    }

    /// <summary>
    /// リソースのアンロードリクエストを追加する（Loader用）
    /// </summary>
    /// <param name="entry">エントリ</param>
    internal void RequestUnload(ResourceEntry entry)
    {
        lock (_lock)
        {
            _pendingRequests.Add(new PendingRequest(entry, RequestType.Unload));
        }
    }

    /// <summary>
    /// 保留中のリクエストを処理し、ロード中のリソースをTickする
    /// </summary>
    public void Tick()
    {
        ProcessPendingRequests();
        TickActiveEntries();
    }

    private void ProcessPendingRequests()
    {
        // ダブルバッファリング: swap
        List<PendingRequest> toProcess;
        lock (_lock)
        {
            toProcess = _pendingRequests;
            _pendingRequests = _processingRequests;
            _processingRequests = toProcess;
        }

        if (toProcess.Count == 0)
            return;

        // エントリごとにグループ化して処理
        var entryCounts = new Dictionary<ResourceEntry, (int loadCount, int unloadCount)>();
        foreach (var request in toProcess)
        {
            if (!entryCounts.TryGetValue(request.Entry, out var counts))
            {
                counts = (0, 0);
            }

            if (request.Type == RequestType.Load)
            {
                counts = (counts.loadCount + 1, counts.unloadCount);
            }
            else
            {
                counts = (counts.loadCount, counts.unloadCount + 1);
            }

            entryCounts[request.Entry] = counts;
        }

        // 各エントリに対して処理
        foreach (var kvp in entryCounts)
        {
            var entry = kvp.Key;
            var (loadCount, unloadCount) = kvp.Value;
            var netChange = loadCount - unloadCount;

            // RefCount 更新
            if (netChange > 0)
            {
                for (int i = 0; i < netChange; i++)
                {
                    entry.Acquire();
                }
            }
            else if (netChange < 0)
            {
                for (int i = 0; i < -netChange; i++)
                {
                    entry.Release();
                }
            }
            // netChange == 0: アンロード→ロードの最適化（RefCount変更なし）

            // ロード開始判定: ロードリクエストがあり、Unloaded状態で、RefCount > 0
            if (loadCount > 0 && entry.State == ResourceLoadState.Unloaded && entry.RefCount > 0)
            {
                entry.Start();
            }

            // アンロード判定: RefCount == 0 で、アンロードリクエストがあり、Unloaded以外
            if (entry.RefCount == 0 && unloadCount > 0 && entry.State != ResourceLoadState.Unloaded)
            {
                entry.Unload();
            }
        }

        toProcess.Clear();
    }

    private void TickActiveEntries()
    {
        // Loading/Failed のエントリに Tick を呼ぶ
        lock (_lock)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.State == ResourceLoadState.Loading || entry.State == ResourceLoadState.Failed)
                {
                    entry.Tick(this);
                }
            }
        }
    }
}
