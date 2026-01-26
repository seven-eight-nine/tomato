using System;
using System.Collections.Generic;
using Xunit;
using Tomato.ResourceSystem.Tests.Mocks;
using ResourceLoader = Tomato.ResourceSystem.Loader;

namespace Tomato.ResourceSystem.Tests.Catalog;

public class ResourceCatalogTests
{
    [Fact]
    public void Register_ValidResource_AddsToCount()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");

        catalog.Register("test/resource", resource);

        Assert.Equal(1, catalog.Count);
        Assert.True(catalog.Contains("test/resource"));
    }

    [Fact]
    public void Register_NullKey_ThrowsArgumentNullException()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");

        Assert.Throws<ArgumentNullException>(() => catalog.Register(null!, resource));
    }

    [Fact]
    public void Register_NullResource_ThrowsArgumentNullException()
    {
        var catalog = new ResourceCatalog();

        Assert.Throws<ArgumentNullException>(() => catalog.Register("test/resource", null!));
    }

    [Fact]
    public void Register_DuplicateKey_ThrowsArgumentException()
    {
        var catalog = new ResourceCatalog();
        var resource1 = new MockResource("test1");
        var resource2 = new MockResource("test2");

        catalog.Register("test/resource", resource1);

        Assert.Throws<ArgumentException>(() => catalog.Register("test/resource", resource2));
    }

    [Fact]
    public void Contains_ExistingKey_ReturnsTrue()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");

        catalog.Register("test/resource", resource);

        Assert.True(catalog.Contains("test/resource"));
    }

    [Fact]
    public void Contains_NonExistingKey_ReturnsFalse()
    {
        var catalog = new ResourceCatalog();

        Assert.False(catalog.Contains("test/resource"));
    }

    [Fact]
    public void Contains_NullKey_ReturnsFalse()
    {
        var catalog = new ResourceCatalog();

        Assert.False(catalog.Contains(null!));
    }

    [Fact]
    public void Unregister_ExistingKey_RemovesFromCatalog()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");

        catalog.Register("test/resource", resource);
        catalog.Unregister("test/resource");

        Assert.Equal(0, catalog.Count);
        Assert.False(catalog.Contains("test/resource"));
    }

    [Fact]
    public void Unregister_NonExistingKey_DoesNothing()
    {
        var catalog = new ResourceCatalog();

        catalog.Unregister("test/resource"); // Should not throw
    }

    [Fact]
    public void Unregister_NullKey_ThrowsArgumentNullException()
    {
        var catalog = new ResourceCatalog();

        Assert.Throws<ArgumentNullException>(() => catalog.Unregister(null!));
    }

    [Fact]
    public void Unregister_ResourceWithReference_ThrowsInvalidOperationException()
    {
        var catalog = new ResourceCatalog();
        var resource = new MockResource("test");
        catalog.Register("test/resource", resource);

        var loader = new ResourceLoader(catalog);
        loader.Request("test/resource");

        Assert.Throws<InvalidOperationException>(() => catalog.Unregister("test/resource"));

        loader.Dispose();
    }

    [Fact]
    public void GetAllKeys_ReturnsAllRegisteredKeys()
    {
        var catalog = new ResourceCatalog();
        catalog.Register("resource/a", new MockResource("a"));
        catalog.Register("resource/b", new MockResource("b"));
        catalog.Register("resource/c", new MockResource("c"));

        var keys = catalog.GetAllKeys();

        Assert.Equal(3, keys.Count);
        Assert.Contains("resource/a", keys);
        Assert.Contains("resource/b", keys);
        Assert.Contains("resource/c", keys);
    }

    [Fact]
    public void GetAllKeys_EmptyCatalog_ReturnsEmptyCollection()
    {
        var catalog = new ResourceCatalog();

        var keys = catalog.GetAllKeys();

        Assert.Empty(keys);
    }
}
