using System;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Enums;

/// <summary>
/// ResourceLoadResult enum comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class ResourceLoadResultTests
{
    #region Enum Value Tests

    [Fact]
    public void Success_ShouldBeZero()
    {
        Assert.Equal(0, (int)ResourceLoadResult.Success);
    }

    [Fact]
    public void Failed_ShouldBeOne()
    {
        Assert.Equal(1, (int)ResourceLoadResult.Failed);
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void Success_ShouldNotEqualFailed()
    {
        Assert.NotEqual(ResourceLoadResult.Success, ResourceLoadResult.Failed);
    }

    [Fact]
    public void SameValue_ShouldBeEqual()
    {
        Assert.Equal(ResourceLoadResult.Success, ResourceLoadResult.Success);
        Assert.Equal(ResourceLoadResult.Failed, ResourceLoadResult.Failed);
    }

    #endregion

    #region Enum Type Tests

    [Fact]
    public void ShouldBeEnum()
    {
        Assert.True(typeof(ResourceLoadResult).IsEnum);
    }

    [Fact]
    public void ShouldHaveTwoValues()
    {
        var values = Enum.GetValues(typeof(ResourceLoadResult));
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void AllValuesAreDistinct()
    {
        var values = Enum.GetValues(typeof(ResourceLoadResult));
        var distinct = new System.Collections.Generic.HashSet<int>();
        foreach (var value in values)
        {
            Assert.True(distinct.Add((int)value));
        }
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void Success_ToStringShouldReturnSuccess()
    {
        Assert.Equal("Success", ResourceLoadResult.Success.ToString());
    }

    [Fact]
    public void Failed_ToStringShouldReturnFailed()
    {
        Assert.Equal("Failed", ResourceLoadResult.Failed.ToString());
    }

    #endregion

    #region Boolean-like Usage Tests

    [Fact]
    public void CanBeUsedInBooleanContext()
    {
        var result = ResourceLoadResult.Success;
        var isSuccess = result == ResourceLoadResult.Success;
        Assert.True(isSuccess);
    }

    [Fact]
    public void CanCheckForFailure()
    {
        var result = ResourceLoadResult.Failed;
        var isFailed = result == ResourceLoadResult.Failed;
        Assert.True(isFailed);
    }

    [Fact]
    public void CanUseInSwitch()
    {
        var result = ResourceLoadResult.Success;
        string message;
        switch (result)
        {
            case ResourceLoadResult.Success:
                message = "ok";
                break;
            case ResourceLoadResult.Failed:
                message = "error";
                break;
            default:
                message = "unknown";
                break;
        }
        Assert.Equal("ok", message);
    }

    #endregion
}
