using System;
using System.Collections.Generic;
using Xunit;
using Tomato.ResourceSystem.Tests.Mocks;
using ResourceLoader = Tomato.ResourceSystem.Loader;

namespace Tomato.ResourceSystem.Tests.LoaderTests;

public class LoaderTests
{
    [Fact]
    public void Constructor_NullCatalog_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ResourceLoader(null!));
    }

    [Fact]
    public void Constructor_InitialState_IsIdle()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        Assert.Equal(LoaderState.Idle, loader.State);
        Assert.Equal(0, loader.RequestCount);
        Assert.Equal(0, loader.LoadedCount);
        Assert.True(loader.AllLoaded);

        loader.Dispose();
    }

    [Fact]
    public void Request_ValidKey_IncreasesRequestCount()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");

        Assert.Equal(1, loader.RequestCount);

        loader.Dispose();
    }

    [Fact]
    public void Request_NullKey_ThrowsArgumentNullException()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        Assert.Throws<ArgumentNullException>(() => loader.Request(null!));

        loader.Dispose();
    }

    [Fact]
    public void Request_NonExistingKey_ThrowsKeyNotFoundException()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        Assert.Throws<KeyNotFoundException>(() => loader.Request("not/exist"));

        loader.Dispose();
    }

    [Fact]
    public void Request_DuplicateKey_DoesNotIncreaseCount()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Request("test/resource");

        Assert.Equal(1, loader.RequestCount);

        loader.Dispose();
    }

    [Fact]
    public void Execute_ChangesStateToLoading()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Execute();

        Assert.Equal(LoaderState.Loading, loader.State);

        loader.Dispose();
    }

    [Fact]
    public void Execute_CallsResourceStart()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");
        catalog.Register("test/resource", resource);
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Execute();

        Assert.True(resource.StartCalled);

        loader.Dispose();
    }

    [Fact]
    public void Tick_AllLoaded_ReturnsTrue()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test", ticksToLoad: 1));
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Execute();
        var result = loader.Tick();

        Assert.True(result);
        Assert.Equal(LoaderState.Loaded, loader.State);
        Assert.Equal(1, loader.LoadedCount);

        loader.Dispose();
    }

    [Fact]
    public void Tick_NotAllLoaded_ReturnsFalse()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test", ticksToLoad: 3));
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Execute();
        var result = loader.Tick();

        Assert.False(result);
        Assert.Equal(LoaderState.Loading, loader.State);

        loader.Dispose();
    }

    [Fact]
    public void Tick_MultipleResourcesAtDifferentSpeeds()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("fast", new MockResource("fast", ticksToLoad: 1));
        catalog.Register("slow", new MockResource("slow", ticksToLoad: 3));
        var loader = new ResourceLoader(catalog);

        loader.Request("fast");
        loader.Request("slow");
        loader.Execute();

        Assert.False(loader.Tick()); // Tick 1
        Assert.Equal(1, loader.LoadedCount);

        Assert.False(loader.Tick()); // Tick 2
        Assert.Equal(1, loader.LoadedCount);

        Assert.True(loader.Tick()); // Tick 3
        Assert.Equal(2, loader.LoadedCount);
        Assert.Equal(LoaderState.Loaded, loader.State);

        loader.Dispose();
    }

    [Fact]
    public void Tick_FailedResourceRetries()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test", ticksToLoad: 1);
        resource.SetFailure(maxFailCount: 2);
        catalog.Register("test/resource", resource);
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Execute();

        Assert.False(loader.Tick()); // Fail 1
        Assert.False(loader.Tick()); // Fail 2
        Assert.True(loader.Tick());  // Success

        Assert.Equal(2, resource.FailCount);

        loader.Dispose();
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");
        catalog.Register("test/resource", resource);
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Execute();
        loader.Tick();

        loader.Dispose();

        Assert.True(resource.UnloadCalled);
    }

    [Fact]
    public void Dispose_WhileLoading_ResetsState()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test", ticksToLoad: 10);
        catalog.Register("test/resource", resource);
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        loader.Execute();
        loader.Tick();

        loader.Dispose();

        Assert.True(resource.UnloadCalled);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        loader.Dispose();
        loader.Dispose(); // Should not throw

        Assert.True(true);
    }

    [Fact]
    public void Request_AfterDispose_ThrowsObjectDisposedException()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        loader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => loader.Request("test/resource"));
    }

    [Fact]
    public void Execute_AfterDispose_ThrowsObjectDisposedException()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        loader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => loader.Execute());
    }

    [Fact]
    public void Tick_AfterDispose_ThrowsObjectDisposedException()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        loader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => loader.Tick());
    }

    [Fact]
    public void Tick_WhenIdle_ReturnsFalse()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        loader.Request("test/resource");
        // Execute not called

        var result = loader.Tick();

        Assert.False(result);

        loader.Dispose();
    }

    [Fact]
    public void Request_WhileLoading_AddsToRequests()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("resource/a", new MockResource("a", ticksToLoad: 3));
        catalog.Register("resource/b", new MockResource("b", ticksToLoad: 1));
        var loader = new ResourceLoader(catalog);

        loader.Request("resource/a");
        loader.Execute();

        // Add another request while loading
        loader.Request("resource/b");

        Assert.Equal(2, loader.RequestCount);

        // Tick should process both
        Assert.False(loader.Tick()); // b loaded, a still loading
        Assert.Equal(1, loader.LoadedCount);

        Assert.False(loader.Tick()); // a still loading
        Assert.True(loader.Tick());  // a loaded

        Assert.Equal(2, loader.LoadedCount);

        loader.Dispose();
    }

    [Fact]
    public void Execute_CalledMultipleTimes_OnlyStartsNewResources()
    {
        var catalog = new ResourceCatalog();
        var resourceA = new MockResource("a", ticksToLoad: 1);
        var resourceB = new MockResource("b", ticksToLoad: 1);
        catalog.Register("resource/a", resourceA);
        catalog.Register("resource/b", resourceB);
        var loader = new ResourceLoader(catalog);

        loader.Request("resource/a");
        loader.Execute();
        loader.Tick(); // a loaded

        loader.Request("resource/b");
        loader.Execute(); // Should only start b

        Assert.True(resourceA.StartCalled);
        Assert.True(resourceB.StartCalled);

        loader.Dispose();
    }

    [Fact]
    public void TotalPoints_DefaultPoint_EqualsRequestCount()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("resource/a", new MockResource("a"));
        catalog.Register("resource/b", new MockResource("b"));
        catalog.Register("resource/c", new MockResource("c"));
        var loader = new ResourceLoader(catalog);

        loader.Request("resource/a");
        loader.Request("resource/b");
        loader.Request("resource/c");

        Assert.Equal(3, loader.TotalPoints);

        loader.Dispose();
    }

    [Fact]
    public void TotalPoints_CustomPoints_SumsCorrectly()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("small", new MockResource("small", point: 1));
        catalog.Register("medium", new MockResource("medium", point: 5));
        catalog.Register("large", new MockResource("large", point: 10));
        var loader = new ResourceLoader(catalog);

        loader.Request("small");
        loader.Request("medium");
        loader.Request("large");

        Assert.Equal(16, loader.TotalPoints); // 1 + 5 + 10

        loader.Dispose();
    }

    [Fact]
    public void LoadedPoints_BeforeLoad_IsZero()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("resource", new MockResource("test", point: 10));
        var loader = new ResourceLoader(catalog);

        loader.Request("resource");

        Assert.Equal(0, loader.LoadedPoints);

        loader.Dispose();
    }

    [Fact]
    public void LoadedPoints_AfterLoad_SumsLoadedResources()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("fast", new MockResource("fast", ticksToLoad: 1, point: 3));
        catalog.Register("slow", new MockResource("slow", ticksToLoad: 3, point: 7));
        var loader = new ResourceLoader(catalog);

        loader.Request("fast");
        loader.Request("slow");
        loader.Execute();

        loader.Tick(); // fast loads
        Assert.Equal(3, loader.LoadedPoints);

        loader.Tick();
        loader.Tick(); // slow loads
        Assert.Equal(10, loader.LoadedPoints);

        loader.Dispose();
    }

    [Fact]
    public void Progress_NoRequests_ReturnsOne()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        Assert.Equal(1f, loader.Progress);

        loader.Dispose();
    }

    [Fact]
    public void Progress_BeforeLoad_ReturnsZero()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("resource", new MockResource("test", point: 10));
        var loader = new ResourceLoader(catalog);

        loader.Request("resource");

        Assert.Equal(0f, loader.Progress);

        loader.Dispose();
    }

    [Fact]
    public void Progress_PartialLoad_ReturnsCorrectRatio()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("small", new MockResource("small", ticksToLoad: 1, point: 1));
        catalog.Register("large", new MockResource("large", ticksToLoad: 3, point: 9));
        var loader = new ResourceLoader(catalog);

        loader.Request("small");
        loader.Request("large");
        loader.Execute();

        // Before any load
        Assert.Equal(0f, loader.Progress);

        // After small loads (1 of 10 points)
        loader.Tick();
        Assert.Equal(0.1f, loader.Progress);

        // After all loads
        loader.Tick();
        loader.Tick();
        Assert.Equal(1f, loader.Progress);

        loader.Dispose();
    }

    [Fact]
    public void Progress_AllLoaded_ReturnsOne()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("resource", new MockResource("test", point: 5));
        var loader = new ResourceLoader(catalog);

        loader.Request("resource");
        loader.Execute();
        loader.Tick();

        Assert.Equal(1f, loader.Progress);

        loader.Dispose();
    }
}
