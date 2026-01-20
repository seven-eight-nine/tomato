using System;
using Tomato.CharacterSpawnSystem;
using Xunit;

namespace Tomato.CharacterSpawnSystem.Tests.Events;

/// <summary>
/// StateChangedEventArgs comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class StateChangedEventArgsTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldStoreOldState()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.NotPlaced,
            CharacterInternalState.PlacedDataLoading);

        Assert.Equal(CharacterInternalState.NotPlaced, args.OldState);
    }

    [Fact]
    public void Constructor_ShouldStoreNewState()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.NotPlaced,
            CharacterInternalState.PlacedDataLoading);

        Assert.Equal(CharacterInternalState.PlacedDataLoading, args.NewState);
    }

    [Fact]
    public void Constructor_ShouldAcceptSameOldAndNewState()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.InstantiatedActive,
            CharacterInternalState.InstantiatedActive);

        Assert.Equal(CharacterInternalState.InstantiatedActive, args.OldState);
        Assert.Equal(CharacterInternalState.InstantiatedActive, args.NewState);
    }

    #endregion

    #region Inheritance Tests

    [Fact]
    public void ShouldInheritFromEventArgs()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.NotPlaced,
            CharacterInternalState.PlacedDataLoading);

        Assert.IsAssignableFrom<EventArgs>(args);
    }

    [Fact]
    public void ShouldBeAssignableToEventArgs()
    {
        EventArgs args = new StateChangedEventArgs(
            CharacterInternalState.NotPlaced,
            CharacterInternalState.PlacedDataLoading);

        Assert.NotNull(args);
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public void FromNotPlaced_ToPlacedDataLoading()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.NotPlaced,
            CharacterInternalState.PlacedDataLoading);

        Assert.Equal(CharacterInternalState.NotPlaced, args.OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, args.NewState);
    }

    [Fact]
    public void FromPlacedDataLoading_ToPlacedDataLoaded()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.PlacedDataLoading,
            CharacterInternalState.PlacedDataLoaded);

        Assert.Equal(CharacterInternalState.PlacedDataLoading, args.OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoaded, args.NewState);
    }

    [Fact]
    public void FromPlacedDataLoaded_ToInstantiatingGOLoading()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.PlacedDataLoaded,
            CharacterInternalState.InstantiatingGOLoading);

        Assert.Equal(CharacterInternalState.PlacedDataLoaded, args.OldState);
        Assert.Equal(CharacterInternalState.InstantiatingGOLoading, args.NewState);
    }

    [Fact]
    public void FromInstantiatedInactive_ToInstantiatedActive()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.InstantiatedInactive,
            CharacterInternalState.InstantiatedActive);

        Assert.Equal(CharacterInternalState.InstantiatedInactive, args.OldState);
        Assert.Equal(CharacterInternalState.InstantiatedActive, args.NewState);
    }

    [Fact]
    public void FromInstantiatedActive_ToNotPlaced()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.InstantiatedActive,
            CharacterInternalState.NotPlaced);

        Assert.Equal(CharacterInternalState.InstantiatedActive, args.OldState);
        Assert.Equal(CharacterInternalState.NotPlaced, args.NewState);
    }

    #endregion

    #region Error State Tests

    [Fact]
    public void ToDataLoadFailed_ShouldBeRecorded()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.PlacedDataLoading,
            CharacterInternalState.DataLoadFailed);

        Assert.Equal(CharacterInternalState.DataLoadFailed, args.NewState);
    }

    [Fact]
    public void ToGameObjectLoadFailed_ShouldBeRecorded()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.InstantiatingGOLoading,
            CharacterInternalState.GameObjectLoadFailed);

        Assert.Equal(CharacterInternalState.GameObjectLoadFailed, args.NewState);
    }

    [Fact]
    public void FromErrorState_ToRecovery()
    {
        var args = new StateChangedEventArgs(
            CharacterInternalState.DataLoadFailed,
            CharacterInternalState.PlacedDataLoading);

        Assert.Equal(CharacterInternalState.DataLoadFailed, args.OldState);
        Assert.Equal(CharacterInternalState.PlacedDataLoading, args.NewState);
    }

    #endregion

    #region Property Immutability Tests

    [Fact]
    public void OldState_ShouldBeReadOnly()
    {
        var property = typeof(StateChangedEventArgs).GetProperty("OldState");
        Assert.NotNull(property);
        Assert.NotNull(property!.GetMethod);
        // Check that there's no public setter
        Assert.True(property!.SetMethod == null || !property.SetMethod.IsPublic);
    }

    [Fact]
    public void NewState_ShouldBeReadOnly()
    {
        var property = typeof(StateChangedEventArgs).GetProperty("NewState");
        Assert.NotNull(property);
        Assert.NotNull(property!.GetMethod);
        // Check that there's no public setter
        Assert.True(property!.SetMethod == null || !property.SetMethod.IsPublic);
    }

    #endregion
}
