using Xunit;
using Tomato.ResourceSystem.Tests.Mocks;
using ResourceLoader = Tomato.ResourceSystem.Loader;

namespace Tomato.ResourceSystem.Tests.Integration;

public class IntegrationTests
{
    [Fact]
    public void MultipleLoaders_SameResource_SharesRefCount()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("shared");
        catalog.Register("shared/resource", resource);

        var loaderA = new ResourceLoader(catalog);
        var loaderB = new ResourceLoader(catalog);

        // Both loaders request the same resource
        var handleA = loaderA.Request("shared/resource");
        loaderA.Execute();
        loaderA.Tick();

        var handleB = loaderB.Request("shared/resource");
        // Already loaded, should not call Start again
        loaderB.Execute();

        // Both handles should see loaded resource
        Assert.True(handleA.IsLoaded);
        Assert.True(handleB.IsLoaded);
        Assert.True(handleA.TryGet<string>(out var valueA));
        Assert.True(handleB.TryGet<string>(out var valueB));
        Assert.Equal("shared", valueA);
        Assert.Equal("shared", valueB);

        // Dispose A - resource should still be available for B
        loaderA.Dispose();
        Assert.False(resource.UnloadCalled); // RefCount still > 0

        // Dispose B - resource should now be unloaded
        loaderB.Dispose();
        Assert.True(resource.UnloadCalled); // RefCount = 0
    }

    [Fact]
    public void DuplicateRequestInSameLoader_DoesNotIncrementRefCount()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");
        catalog.Register("test/resource", resource);

        var loader = new ResourceLoader(catalog);

        // Request same resource twice
        var handle1 = loader.Request("test/resource");
        var handle2 = loader.Request("test/resource");

        loader.Execute();
        loader.Tick();

        // Both handles should work
        Assert.True(handle1.TryGet<string>(out var val1));
        Assert.True(handle2.TryGet<string>(out var val2));
        Assert.Equal("test", val1);
        Assert.Equal("test", val2);

        // Single dispose should release the resource
        loader.Dispose();
        Assert.True(resource.UnloadCalled);
    }

    [Fact]
    public void DependentResource_LoadsDepenciesFirst()
    {
        var catalog = new ResourceCatalog();

        // Register dependencies with multiple ticks needed
        catalog.Register("texture/diffuse", new MockResource("diffuse_data", ticksToLoad: 2));
        catalog.Register("texture/normal", new MockResource("normal_data", ticksToLoad: 2));

        // Register resource that depends on textures
        catalog.Register("material/main", new MockDependentResource("material_data",
            "texture/diffuse", "texture/normal"));

        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("material/main");
        loader.Execute();

        // First tick: material starts loading, creates internal loader for deps, deps start loading
        Assert.False(loader.Tick());
        Assert.False(handle.IsLoaded);

        // Second tick: deps complete, material completes
        Assert.True(loader.Tick());
        Assert.True(handle.IsLoaded);
        Assert.True(handle.TryGet<string>(out var material));
        Assert.Equal("material_data", material);

        loader.Dispose();
    }

    [Fact]
    public void CompleteLoadCycle_RegisterLoadUseDispose()
    {
        // 1. Create catalog and register resources
        var catalog = new ResourceCatalog();
        catalog.Register("texture/player", new MockResource("player_texture"));
        catalog.Register("audio/bgm", new MockResource("bgm_audio"));

        // 2. Create loader
        var loader = new ResourceLoader(catalog);

        // 3. Request loads
        var texHandle = loader.Request("texture/player");
        var bgmHandle = loader.Request("audio/bgm");

        // Verify initial state
        Assert.Equal(LoaderState.Idle, loader.State);
        Assert.False(texHandle.IsLoaded);
        Assert.False(bgmHandle.IsLoaded);

        // 4. Execute
        loader.Execute();
        Assert.Equal(LoaderState.Loading, loader.State);

        // 5. Tick until complete
        Assert.True(loader.Tick());
        Assert.Equal(LoaderState.Loaded, loader.State);
        Assert.True(loader.AllLoaded);

        // 6. Use resources
        Assert.True(texHandle.TryGet<string>(out var texture));
        Assert.True(bgmHandle.TryGet<string>(out var bgm));
        Assert.Equal("player_texture", texture);
        Assert.Equal("bgm_audio", bgm);

        // 7. Dispose
        loader.Dispose();
    }

    [Fact]
    public void SceneTransition_OldLoaderReleasesNewRetains()
    {
        var catalog = new ResourceCatalog();
        var sharedResource = new MockResource("shared");
        var sceneAResource = new MockResource("sceneA");
        var sceneBResource = new MockResource("sceneB");

        catalog.Register("resource/shared", sharedResource);
        catalog.Register("resource/sceneA", sceneAResource);
        catalog.Register("resource/sceneB", sceneBResource);

        // Scene A loads shared and sceneA
        var loaderA = new ResourceLoader(catalog);
        loaderA.Request("resource/shared");
        loaderA.Request("resource/sceneA");
        loaderA.Execute();
        while (!loaderA.Tick()) { }

        // Scene B starts loading while A is still active
        var loaderB = new ResourceLoader(catalog);
        loaderB.Request("resource/shared"); // Shared between scenes
        loaderB.Request("resource/sceneB");
        loaderB.Execute();
        while (!loaderB.Tick()) { }

        // Verify all resources are loaded
        Assert.True(sharedResource.StartCalled);
        Assert.True(sceneAResource.StartCalled);
        Assert.True(sceneBResource.StartCalled);

        // Dispose Scene A
        loaderA.Dispose();

        // Shared resource should NOT be unloaded (Scene B still uses it)
        Assert.False(sharedResource.UnloadCalled);
        // Scene A resource should be unloaded
        Assert.True(sceneAResource.UnloadCalled);

        // Dispose Scene B
        loaderB.Dispose();

        // Now all resources should be unloaded
        Assert.True(sharedResource.UnloadCalled);
        Assert.True(sceneBResource.UnloadCalled);
    }

    [Fact]
    public void AddRequestDuringLoading_HandledCorrectly()
    {
        var catalog = new ResourceCatalog();
        var slowResource = new MockResource("slow", ticksToLoad: 5);
        var fastResource = new MockResource("fast", ticksToLoad: 1);

        catalog.Register("resource/slow", slowResource);
        catalog.Register("resource/fast", fastResource);

        var loader = new ResourceLoader(catalog);

        // Start with slow resource
        loader.Request("resource/slow");
        loader.Execute();

        // Tick a few times
        Assert.False(loader.Tick());
        Assert.False(loader.Tick());

        // Add fast resource mid-load
        var fastHandle = loader.Request("resource/fast");

        // Continue ticking
        Assert.False(loader.Tick()); // fast loads
        Assert.True(fastHandle.IsLoaded);
        Assert.False(loader.AllLoaded); // slow still loading

        Assert.False(loader.Tick());
        Assert.True(loader.Tick()); // slow finally loads

        Assert.True(loader.AllLoaded);
        Assert.Equal(2, loader.LoadedCount);

        loader.Dispose();
    }

    [Fact]
    public void FailAndRetry_EventuallySucceeds()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("flaky", ticksToLoad: 1);
        resource.SetFailure(maxFailCount: 3);

        catalog.Register("resource/flaky", resource);

        var loader = new ResourceLoader(catalog);
        var handle = loader.Request("resource/flaky");
        loader.Execute();

        // Will fail 3 times then succeed
        Assert.False(loader.Tick()); // Fail 1
        Assert.Equal(ResourceLoadState.Failed, handle.State);

        Assert.False(loader.Tick()); // Fail 2
        Assert.False(loader.Tick()); // Fail 3
        Assert.True(loader.Tick());  // Success

        Assert.True(handle.IsLoaded);
        Assert.True(handle.TryGet<string>(out var value));
        Assert.Equal("flaky", value);

        loader.Dispose();
    }

    [Fact]
    public void EmptyCatalog_LoaderStillWorks()
    {
        var catalog = new ResourceCatalog();
        var loader = new ResourceLoader(catalog);

        Assert.Equal(LoaderState.Idle, loader.State);
        Assert.Equal(0, loader.RequestCount);
        Assert.True(loader.AllLoaded);

        loader.Execute();
        Assert.Equal(LoaderState.Idle, loader.State); // No resources to load

        Assert.False(loader.Tick()); // No-op when idle

        loader.Dispose();
    }

    [Fact]
    public void CatalogUnregister_AfterDispose_Works()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("resource/temp", new MockResource("temp"));

        var loader = new ResourceLoader(catalog);
        loader.Request("resource/temp");
        loader.Execute();
        loader.Tick();
        loader.Dispose();

        // Now we can unregister
        catalog.Unregister("resource/temp");
        Assert.False(catalog.Contains("resource/temp"));
    }
}
