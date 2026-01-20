using System;
using System.Linq;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Enums;

/// <summary>
/// CharacterInternalState enum comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class CharacterInternalStateTests
{
    #region NotPlaced State Tests

    [Fact]
    public void NotPlaced_ShouldBeZero()
    {
        Assert.Equal(0, (int)CharacterInternalState.NotPlaced);
    }

    [Fact]
    public void NotPlaced_ShouldBeTheLowestValue()
    {
        var allValues = Enum.GetValues(typeof(CharacterInternalState))
            .Cast<int>()
            .ToArray();
        Assert.Equal(0, allValues.Min());
    }

    #endregion

    #region Data Loading State Tests

    [Fact]
    public void PlacedDataLoading_ShouldBe10()
    {
        Assert.Equal(10, (int)CharacterInternalState.PlacedDataLoading);
    }

    [Fact]
    public void PlacedDataLoaded_ShouldBe11()
    {
        Assert.Equal(11, (int)CharacterInternalState.PlacedDataLoaded);
    }

    [Fact]
    public void PlacedDataLoading_ShouldBeLessThanPlacedDataLoaded()
    {
        Assert.True(CharacterInternalState.PlacedDataLoading < CharacterInternalState.PlacedDataLoaded);
    }

    #endregion

    #region GameObject State Tests

    [Fact]
    public void InstantiatingGOLoading_ShouldBe20()
    {
        Assert.Equal(20, (int)CharacterInternalState.InstantiatingGOLoading);
    }

    [Fact]
    public void InstantiatedInactive_ShouldBe21()
    {
        Assert.Equal(21, (int)CharacterInternalState.InstantiatedInactive);
    }

    [Fact]
    public void InstantiatedActive_ShouldBe22()
    {
        Assert.Equal(22, (int)CharacterInternalState.InstantiatedActive);
    }

    [Fact]
    public void InstantiatedInactive_ShouldBeLessThanInstantiatedActive()
    {
        Assert.True(CharacterInternalState.InstantiatedInactive < CharacterInternalState.InstantiatedActive);
    }

    #endregion

    #region Error State Tests

    [Fact]
    public void DataLoadFailed_ShouldBe90()
    {
        Assert.Equal(90, (int)CharacterInternalState.DataLoadFailed);
    }

    [Fact]
    public void GameObjectLoadFailed_ShouldBe91()
    {
        Assert.Equal(91, (int)CharacterInternalState.GameObjectLoadFailed);
    }

    [Fact]
    public void ErrorStates_ShouldBeHigherThanNormalStates()
    {
        Assert.True((int)CharacterInternalState.DataLoadFailed > (int)CharacterInternalState.InstantiatedActive);
        Assert.True((int)CharacterInternalState.GameObjectLoadFailed > (int)CharacterInternalState.InstantiatedActive);
    }

    #endregion

    #region State Grouping Tests

    [Fact]
    public void DataRelatedStates_ShouldBeIn10Range()
    {
        Assert.Equal(10, (int)CharacterInternalState.PlacedDataLoading);
        Assert.Equal(11, (int)CharacterInternalState.PlacedDataLoaded);
    }

    [Fact]
    public void GameObjectRelatedStates_ShouldBeIn20Range()
    {
        Assert.Equal(20, (int)CharacterInternalState.InstantiatingGOLoading);
        Assert.Equal(21, (int)CharacterInternalState.InstantiatedInactive);
        Assert.Equal(22, (int)CharacterInternalState.InstantiatedActive);
    }

    [Fact]
    public void ErrorStates_ShouldBeIn90Range()
    {
        Assert.Equal(90, (int)CharacterInternalState.DataLoadFailed);
        Assert.Equal(91, (int)CharacterInternalState.GameObjectLoadFailed);
    }

    #endregion

    #region Enum Type Tests

    [Fact]
    public void ShouldBeEnum()
    {
        Assert.True(typeof(CharacterInternalState).IsEnum);
    }

    [Fact]
    public void ShouldHaveEightValues()
    {
        var values = Enum.GetValues(typeof(CharacterInternalState));
        Assert.Equal(8, values.Length);
    }

    [Fact]
    public void AllValuesAreDistinct()
    {
        var values = Enum.GetValues(typeof(CharacterInternalState));
        var distinct = new System.Collections.Generic.HashSet<int>();
        foreach (var value in values)
        {
            Assert.True(distinct.Add((int)value));
        }
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void NotPlaced_ToStringShouldReturnNotPlaced()
    {
        Assert.Equal("NotPlaced", CharacterInternalState.NotPlaced.ToString());
    }

    [Fact]
    public void PlacedDataLoading_ToStringShouldReturnPlacedDataLoading()
    {
        Assert.Equal("PlacedDataLoading", CharacterInternalState.PlacedDataLoading.ToString());
    }

    [Fact]
    public void InstantiatedActive_ToStringShouldReturnInstantiatedActive()
    {
        Assert.Equal("InstantiatedActive", CharacterInternalState.InstantiatedActive.ToString());
    }

    [Fact]
    public void DataLoadFailed_ToStringShouldReturnDataLoadFailed()
    {
        Assert.Equal("DataLoadFailed", CharacterInternalState.DataLoadFailed.ToString());
    }

    #endregion
}
