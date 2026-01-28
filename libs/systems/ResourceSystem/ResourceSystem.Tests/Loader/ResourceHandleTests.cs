using Xunit;
using Tomato.ResourceSystem.Tests.Mocks;
using ResourceLoader = Tomato.ResourceSystem.Loader;

namespace Tomato.ResourceSystem.Tests.LoaderTests;

public class ResourceHandleTests
{
    [Fact]
    public void DefaultHandle_IsNotValid()
    {
        var handle = default(ResourceHandle);

        Assert.False(handle.IsValid);
        Assert.False(handle.IsLoaded);
        Assert.Equal(ResourceLoadState.Unloaded, handle.State);
    }

    [Fact]
    public void Handle_FromRequest_IsValid()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");

        Assert.True(handle.IsValid);
        loader.Dispose();
    }

    [Fact]
    public void Handle_BeforeLoad_IsNotLoaded()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");

        Assert.False(handle.IsLoaded);
        Assert.Equal(ResourceLoadState.Unloaded, handle.State);

        loader.Dispose();
    }

    [Fact]
    public void Handle_AfterLoad_IsLoaded()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");
        loader.Execute();
        catalog.Tick();
        loader.Tick();

        Assert.True(handle.IsLoaded);
        Assert.Equal(ResourceLoadState.Loaded, handle.State);

        loader.Dispose();
    }

    [Fact]
    public void TryGet_AfterLoad_ReturnsResource()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("hello world"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");
        loader.Execute();
        catalog.Tick();
        loader.Tick();

        Assert.True(handle.TryGet<string>(out var resource));
        Assert.Equal("hello world", resource);

        loader.Dispose();
    }

    [Fact]
    public void TryGet_BeforeLoad_ReturnsFalse()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");

        Assert.False(handle.TryGet<string>(out var resource));
        Assert.Null(resource);

        loader.Dispose();
    }

    [Fact]
    public void TryGet_WrongType_ReturnsFalse()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("test"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");
        loader.Execute();
        catalog.Tick();
        loader.Tick();

        Assert.False(handle.TryGet<int[]>(out var resource));
        Assert.Null(resource);

        loader.Dispose();
    }

    [Fact]
    public void GetResourceUnsafe_AfterLoad_ReturnsResource()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("hello"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");
        loader.Execute();
        catalog.Tick();
        loader.Tick();

        var resource = handle.GetResourceUnsafe();

        Assert.Equal("hello", resource);

        loader.Dispose();
    }

    [Fact]
    public void GetResourceUnsafe_BeforeLoad_ReturnsNull()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("test/resource", new MockResource("hello"));
        var loader = new ResourceLoader(catalog);

        var handle = loader.Request("test/resource");

        var resource = handle.GetResourceUnsafe();

        Assert.Null(resource);

        loader.Dispose();
    }

    [Fact]
    public void DefaultHandle_TryGet_ReturnsFalse()
    {
        var handle = default(ResourceHandle);

        Assert.False(handle.TryGet<string>(out var resource));
        Assert.Null(resource);
    }

    [Fact]
    public void DefaultHandle_GetResourceUnsafe_ReturnsNull()
    {
        var handle = default(ResourceHandle);

        Assert.Null(handle.GetResourceUnsafe());
    }
}
