using System;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Helpers;

/// <summary>
/// CharacterStateHelper comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class CharacterStateHelperTests
{
    #region IsLoading Tests

    [Fact]
    public void IsLoading_PlacedDataLoading_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsLoading(CharacterInternalState.PlacedDataLoading));
    }

    [Fact]
    public void IsLoading_InstantiatingGOLoading_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsLoading(CharacterInternalState.InstantiatingGOLoading));
    }

    [Fact]
    public void IsLoading_NotPlaced_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsLoading(CharacterInternalState.NotPlaced));
    }

    [Fact]
    public void IsLoading_PlacedDataLoaded_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsLoading(CharacterInternalState.PlacedDataLoaded));
    }

    [Fact]
    public void IsLoading_InstantiatedInactive_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsLoading(CharacterInternalState.InstantiatedInactive));
    }

    [Fact]
    public void IsLoading_InstantiatedActive_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsLoading(CharacterInternalState.InstantiatedActive));
    }

    [Fact]
    public void IsLoading_DataLoadFailed_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsLoading(CharacterInternalState.DataLoadFailed));
    }

    [Fact]
    public void IsLoading_GameObjectLoadFailed_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsLoading(CharacterInternalState.GameObjectLoadFailed));
    }

    #endregion

    #region IsError Tests

    [Fact]
    public void IsError_DataLoadFailed_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsError(CharacterInternalState.DataLoadFailed));
    }

    [Fact]
    public void IsError_GameObjectLoadFailed_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsError(CharacterInternalState.GameObjectLoadFailed));
    }

    [Fact]
    public void IsError_NotPlaced_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsError(CharacterInternalState.NotPlaced));
    }

    [Fact]
    public void IsError_PlacedDataLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsError(CharacterInternalState.PlacedDataLoading));
    }

    [Fact]
    public void IsError_PlacedDataLoaded_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsError(CharacterInternalState.PlacedDataLoaded));
    }

    [Fact]
    public void IsError_InstantiatingGOLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsError(CharacterInternalState.InstantiatingGOLoading));
    }

    [Fact]
    public void IsError_InstantiatedInactive_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsError(CharacterInternalState.InstantiatedInactive));
    }

    [Fact]
    public void IsError_InstantiatedActive_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsError(CharacterInternalState.InstantiatedActive));
    }

    #endregion

    #region HasGameObject Tests

    [Fact]
    public void HasGameObject_InstantiatedInactive_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.HasGameObject(CharacterInternalState.InstantiatedInactive));
    }

    [Fact]
    public void HasGameObject_InstantiatedActive_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.HasGameObject(CharacterInternalState.InstantiatedActive));
    }

    [Fact]
    public void HasGameObject_NotPlaced_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.HasGameObject(CharacterInternalState.NotPlaced));
    }

    [Fact]
    public void HasGameObject_PlacedDataLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.HasGameObject(CharacterInternalState.PlacedDataLoading));
    }

    [Fact]
    public void HasGameObject_PlacedDataLoaded_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.HasGameObject(CharacterInternalState.PlacedDataLoaded));
    }

    [Fact]
    public void HasGameObject_InstantiatingGOLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.HasGameObject(CharacterInternalState.InstantiatingGOLoading));
    }

    [Fact]
    public void HasGameObject_DataLoadFailed_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.HasGameObject(CharacterInternalState.DataLoadFailed));
    }

    [Fact]
    public void HasGameObject_GameObjectLoadFailed_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.HasGameObject(CharacterInternalState.GameObjectLoadFailed));
    }

    #endregion

    #region IsActive Tests

    [Fact]
    public void IsActive_InstantiatedActive_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsActive(CharacterInternalState.InstantiatedActive));
    }

    [Fact]
    public void IsActive_InstantiatedInactive_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsActive(CharacterInternalState.InstantiatedInactive));
    }

    [Fact]
    public void IsActive_NotPlaced_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsActive(CharacterInternalState.NotPlaced));
    }

    [Fact]
    public void IsActive_PlacedDataLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsActive(CharacterInternalState.PlacedDataLoading));
    }

    [Fact]
    public void IsActive_PlacedDataLoaded_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsActive(CharacterInternalState.PlacedDataLoaded));
    }

    [Fact]
    public void IsActive_InstantiatingGOLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsActive(CharacterInternalState.InstantiatingGOLoading));
    }

    [Fact]
    public void IsActive_DataLoadFailed_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsActive(CharacterInternalState.DataLoadFailed));
    }

    [Fact]
    public void IsActive_GameObjectLoadFailed_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsActive(CharacterInternalState.GameObjectLoadFailed));
    }

    #endregion

    #region IsStable Tests

    [Fact]
    public void IsStable_NotPlaced_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsStable(CharacterInternalState.NotPlaced));
    }

    [Fact]
    public void IsStable_PlacedDataLoaded_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsStable(CharacterInternalState.PlacedDataLoaded));
    }

    [Fact]
    public void IsStable_InstantiatedInactive_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsStable(CharacterInternalState.InstantiatedInactive));
    }

    [Fact]
    public void IsStable_InstantiatedActive_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsStable(CharacterInternalState.InstantiatedActive));
    }

    [Fact]
    public void IsStable_DataLoadFailed_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsStable(CharacterInternalState.DataLoadFailed));
    }

    [Fact]
    public void IsStable_GameObjectLoadFailed_ShouldReturnTrue()
    {
        Assert.True(CharacterStateHelper.IsStable(CharacterInternalState.GameObjectLoadFailed));
    }

    [Fact]
    public void IsStable_PlacedDataLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsStable(CharacterInternalState.PlacedDataLoading));
    }

    [Fact]
    public void IsStable_InstantiatingGOLoading_ShouldReturnFalse()
    {
        Assert.False(CharacterStateHelper.IsStable(CharacterInternalState.InstantiatingGOLoading));
    }

    #endregion

    #region Static Class Tests

    [Fact]
    public void CharacterStateHelper_ShouldBeStatic()
    {
        Assert.True(typeof(CharacterStateHelper).IsAbstract && typeof(CharacterStateHelper).IsSealed);
    }

    [Fact]
    public void CharacterStateHelper_ShouldNotBeInstantiable()
    {
        var constructors = typeof(CharacterStateHelper).GetConstructors();
        Assert.Empty(constructors);
    }

    #endregion
}
