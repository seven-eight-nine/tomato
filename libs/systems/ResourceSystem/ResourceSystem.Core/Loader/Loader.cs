using System;
using System.Collections.Generic;

namespace Tomato.ResourceSystem;

/// <summary>
/// リソースのロードを管理するローダー
/// ResourceCatalogを参照として持ち、複数のリソースのロードリクエストを管理する
/// </summary>
public sealed class Loader : IDisposable
{
    private readonly ResourceCatalog _catalog;
    private readonly HashSet<ResourceEntry> _requests;
    private LoaderState _state;
    private bool _disposed;

    /// <summary>
    /// 新しいLoaderを作成する
    /// </summary>
    /// <param name="catalog">参照するカタログ</param>
    /// <exception cref="ArgumentNullException">catalogがnullの場合</exception>
    public Loader(ResourceCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _requests = new HashSet<ResourceEntry>();
        _state = LoaderState.Idle;
        _disposed = false;
    }

    /// <summary>
    /// Loaderの現在の状態
    /// </summary>
    public LoaderState State => _state;

    /// <summary>
    /// リクエストされたリソースの数
    /// </summary>
    public int RequestCount => _requests.Count;

    /// <summary>
    /// ロード完了したリソースの数
    /// </summary>
    public int LoadedCount
    {
        get
        {
            int count = 0;
            foreach (var entry in _requests)
            {
                if (entry.IsLoaded)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// リクエストされた全リソースの合計ポイント
    /// </summary>
    public int TotalPoints
    {
        get
        {
            int total = 0;
            foreach (var entry in _requests)
            {
                total += entry.Point;
            }
            return total;
        }
    }

    /// <summary>
    /// ロード完了したリソースの合計ポイント
    /// </summary>
    public int LoadedPoints
    {
        get
        {
            int loaded = 0;
            foreach (var entry in _requests)
            {
                if (entry.IsLoaded)
                    loaded += entry.Point;
            }
            return loaded;
        }
    }

    /// <summary>
    /// ロード進捗（0.0～1.0）
    /// リクエストがない場合は1.0を返す
    /// </summary>
    public float Progress
    {
        get
        {
            int total = TotalPoints;
            if (total == 0)
                return 1f;
            return (float)LoadedPoints / total;
        }
    }

    /// <summary>
    /// 全てのリクエストされたリソースがロード済みかどうか
    /// </summary>
    public bool AllLoaded
    {
        get
        {
            if (_requests.Count == 0)
                return true;

            foreach (var entry in _requests)
            {
                if (!entry.IsLoaded)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// リソースのロードをリクエストする
    /// 既にリクエスト済みの場合は何もしない（重複無視）
    /// </summary>
    /// <param name="resourceKey">リソースキー</param>
    /// <returns>リソースハンドル</returns>
    /// <exception cref="ObjectDisposedException">Loaderが破棄済みの場合</exception>
    /// <exception cref="ArgumentNullException">resourceKeyがnullの場合</exception>
    /// <exception cref="KeyNotFoundException">指定したキーがカタログに存在しない場合</exception>
    public ResourceHandle Request(string resourceKey)
    {
        ThrowIfDisposed();

        if (resourceKey == null)
            throw new ArgumentNullException(nameof(resourceKey));

        var entry = _catalog.GetEntry(resourceKey);

        // 既にこのLoaderでリクエスト済みなら参照カウントを増やさない
        if (!_requests.Contains(entry))
        {
            _requests.Add(entry);
            entry.Acquire();
        }

        return new ResourceHandle(entry);
    }

    /// <summary>
    /// ロードを開始する（Unloadedのリソースのみ）
    /// </summary>
    /// <exception cref="ObjectDisposedException">Loaderが破棄済みの場合</exception>
    public void Execute()
    {
        ThrowIfDisposed();

        foreach (var entry in _requests)
        {
            // Unloadedのリソースのみロード開始
            if (entry.State == ResourceLoadState.Unloaded)
            {
                entry.Start();
            }
        }

        // 状態を更新
        if (_requests.Count > 0)
        {
            _state = AllLoaded ? LoaderState.Loaded : LoaderState.Loading;
        }
    }

    /// <summary>
    /// Tick処理を行う
    /// 全てのリソースがロード完了した場合はtrueを返す
    /// 失敗したリソースはリトライされる
    /// </summary>
    /// <returns>全てロード完了した場合はtrue</returns>
    /// <exception cref="ObjectDisposedException">Loaderが破棄済みの場合</exception>
    public bool Tick()
    {
        ThrowIfDisposed();

        if (_state == LoaderState.Idle)
            return false;

        foreach (var entry in _requests)
        {
            // Unloadedのリソースがあれば開始（ロード中に追加されたリソース対応）
            if (entry.State == ResourceLoadState.Unloaded)
            {
                entry.Start();
            }

            // Loading/Failedの場合はTick
            if (entry.State == ResourceLoadState.Loading || entry.State == ResourceLoadState.Failed)
            {
                entry.Tick(_catalog);
            }
        }

        // 全完了チェック
        if (AllLoaded)
        {
            _state = LoaderState.Loaded;
            return true;
        }

        return false;
    }

    /// <summary>
    /// リソースを解放してLoaderを破棄する
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var entry in _requests)
        {
            var newRefCount = entry.Release();
            if (newRefCount == 0)
            {
                entry.Unload();
            }
        }

        _requests.Clear();
        _state = LoaderState.Idle;
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Loader));
    }
}
