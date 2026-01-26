using System;
using ResourceLoader = Tomato.ResourceSystem.Loader;

namespace Tomato.ResourceSystem.Tests.Mocks;

/// <summary>
/// テスト用のモックリソース
/// </summary>
public class MockResource : IResource<string>
{
    private readonly string _value;
    private readonly int _ticksToLoad;
    private readonly int _point;
    private int _currentTicks;
    private bool _loaded;
    private bool _startCalled;
    private bool _unloadCalled;
    private bool _shouldFail;
    private int _failCount;
    private int _maxFailCount;

    public MockResource(string value, int ticksToLoad = 1, int point = 1)
    {
        _value = value;
        _ticksToLoad = ticksToLoad;
        _point = point;
        _currentTicks = 0;
        _loaded = false;
        _startCalled = false;
        _unloadCalled = false;
        _shouldFail = false;
        _failCount = 0;
        _maxFailCount = 0;
    }

    /// <summary>
    /// ロード失敗をシミュレートするように設定
    /// </summary>
    /// <param name="maxFailCount">失敗する回数（その後は成功）</param>
    public void SetFailure(int maxFailCount = 1)
    {
        _shouldFail = true;
        _maxFailCount = maxFailCount;
    }

    public bool StartCalled => _startCalled;
    public bool UnloadCalled => _unloadCalled;
    public int CurrentTicks => _currentTicks;
    public int FailCount => _failCount;

    public int Point => _point;

    public void Start()
    {
        _startCalled = true;
        _currentTicks = 0;
        _loaded = false;
    }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        if (_shouldFail && _failCount < _maxFailCount)
        {
            _failCount++;
            return ResourceLoadState.Failed;
        }

        _currentTicks++;
        if (_currentTicks >= _ticksToLoad)
        {
            _loaded = true;
            return ResourceLoadState.Loaded;
        }
        return ResourceLoadState.Loading;
    }

    public string? GetResource()
    {
        return _loaded ? _value : null;
    }

    object? IResource.GetResource() => GetResource();

    public void Unload()
    {
        _unloadCalled = true;
        _loaded = false;
        _startCalled = false;
        _currentTicks = 0;
    }
}

/// <summary>
/// 依存リソースをロードするモックリソース
/// </summary>
public class MockDependentResource : IResource<string>
{
    private readonly string _value;
    private readonly string[] _dependencies;
    private ResourceLoader? _dependencyLoader;
    private bool _loaded;

    public MockDependentResource(string value, params string[] dependencies)
    {
        _value = value;
        _dependencies = dependencies;
        _loaded = false;
    }

    public void Start()
    {
        _loaded = false;
    }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        if (_dependencyLoader == null)
        {
            _dependencyLoader = new ResourceLoader(catalog);
            foreach (var dep in _dependencies)
            {
                _dependencyLoader.Request(dep);
            }
            _dependencyLoader.Execute();
        }

        if (!_dependencyLoader.Tick())
        {
            return ResourceLoadState.Loading;
        }

        _loaded = true;
        return ResourceLoadState.Loaded;
    }

    public string? GetResource()
    {
        return _loaded ? _value : null;
    }

    object? IResource.GetResource() => GetResource();

    public void Unload()
    {
        _dependencyLoader?.Dispose();
        _dependencyLoader = null;
        _loaded = false;
    }
}
