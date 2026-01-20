using System;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Enums;

/// <summary>
/// CharacterRequestState enum comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class CharacterRequestStateTests
{
    #region Enum Value Tests

    [Fact]
    public void None_ShouldBeZero()
    {
        Assert.Equal(0, (int)CharacterRequestState.None);
    }

    [Fact]
    public void PlacedOnly_ShouldBeOne()
    {
        Assert.Equal(1, (int)CharacterRequestState.PlacedOnly);
    }

    [Fact]
    public void Ready_ShouldBeTwo()
    {
        Assert.Equal(2, (int)CharacterRequestState.Ready);
    }

    [Fact]
    public void Active_ShouldBeThree()
    {
        Assert.Equal(3, (int)CharacterRequestState.Active);
    }

    #endregion

    #region Ordering Tests

    [Fact]
    public void None_ShouldBeLessThanPlacedOnly()
    {
        Assert.True(CharacterRequestState.None < CharacterRequestState.PlacedOnly);
    }

    [Fact]
    public void PlacedOnly_ShouldBeLessThanReady()
    {
        Assert.True(CharacterRequestState.PlacedOnly < CharacterRequestState.Ready);
    }

    [Fact]
    public void Ready_ShouldBeLessThanActive()
    {
        Assert.True(CharacterRequestState.Ready < CharacterRequestState.Active);
    }

    [Fact]
    public void Active_ShouldBeTheHighestValue()
    {
        Assert.True(CharacterRequestState.Active >= CharacterRequestState.None);
        Assert.True(CharacterRequestState.Active >= CharacterRequestState.PlacedOnly);
        Assert.True(CharacterRequestState.Active >= CharacterRequestState.Ready);
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void SameValue_ShouldBeEqual()
    {
        Assert.Equal(CharacterRequestState.Ready, CharacterRequestState.Ready);
    }

    [Fact]
    public void DifferentValues_ShouldNotBeEqual()
    {
        Assert.NotEqual(CharacterRequestState.None, CharacterRequestState.Active);
    }

    [Fact]
    public void CanCompareWithGreaterThanOrEqual()
    {
        Assert.True(CharacterRequestState.Active >= CharacterRequestState.Ready);
        Assert.True(CharacterRequestState.Ready >= CharacterRequestState.PlacedOnly);
    }

    #endregion

    #region Enum Type Tests

    [Fact]
    public void ShouldBeEnum()
    {
        Assert.True(typeof(CharacterRequestState).IsEnum);
    }

    [Fact]
    public void ShouldHaveFourValues()
    {
        var values = Enum.GetValues(typeof(CharacterRequestState));
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void AllValuesAreDistinct()
    {
        var values = Enum.GetValues(typeof(CharacterRequestState));
        var distinct = new System.Collections.Generic.HashSet<int>();
        foreach (var value in values)
        {
            Assert.True(distinct.Add((int)value));
        }
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void None_ToStringShouldReturnNone()
    {
        Assert.Equal("None", CharacterRequestState.None.ToString());
    }

    [Fact]
    public void PlacedOnly_ToStringShouldReturnPlacedOnly()
    {
        Assert.Equal("PlacedOnly", CharacterRequestState.PlacedOnly.ToString());
    }

    [Fact]
    public void Ready_ToStringShouldReturnReady()
    {
        Assert.Equal("Ready", CharacterRequestState.Ready.ToString());
    }

    [Fact]
    public void Active_ToStringShouldReturnActive()
    {
        Assert.Equal("Active", CharacterRequestState.Active.ToString());
    }

    #endregion
}
