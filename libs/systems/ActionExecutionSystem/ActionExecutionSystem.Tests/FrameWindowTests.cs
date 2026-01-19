using System;
using Xunit;
using Tomato.ActionExecutionSystem;

namespace Tomato.ActionExecutionSystem.Tests;

/// <summary>
/// FrameWindow テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] FrameWindowを作成できる
/// - [x] Contains でフレームが範囲内か判定できる
/// - [x] FromStartEnd でインスタンスを作成できる
/// - [x] FromStartDuration でインスタンスを作成できる
/// - [x] 境界値で正しく判定できる
/// </summary>
public class FrameWindowTests
{
    [Fact]
    public void FrameWindow_ShouldBeCreatable()
    {
        var window = new FrameWindow(10, 20);

        Assert.Equal(10, window.Start);
        Assert.Equal(20, window.End);
    }

    [Fact]
    public void Contains_ShouldReturnTrue_WhenFrameIsInRange()
    {
        var window = new FrameWindow(10, 20);

        Assert.True(window.Contains(15));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenFrameIsOutOfRange()
    {
        var window = new FrameWindow(10, 20);

        Assert.False(window.Contains(5));
        Assert.False(window.Contains(25));
    }

    [Fact]
    public void Contains_ShouldReturnTrue_AtBoundaryStart()
    {
        var window = new FrameWindow(10, 20);

        Assert.True(window.Contains(10));
    }

    [Fact]
    public void Contains_ShouldReturnTrue_AtBoundaryEnd()
    {
        var window = new FrameWindow(10, 20);

        Assert.True(window.Contains(20));
    }

    [Fact]
    public void FromStartEnd_ShouldCreateWindow()
    {
        var window = FrameWindow.FromStartEnd(5, 15);

        Assert.Equal(5, window.Start);
        Assert.Equal(15, window.End);
    }

    [Fact]
    public void FromStartDuration_ShouldCreateWindow()
    {
        // Start=5, Duration=10 -> End=14 (5から10フレーム分 = 5,6,7,8,9,10,11,12,13,14)
        var window = FrameWindow.FromStartDuration(5, 10);

        Assert.Equal(5, window.Start);
        Assert.Equal(14, window.End);
    }

    [Fact]
    public void FromStartDuration_WithDuration1_ShouldCreateSingleFrameWindow()
    {
        var window = FrameWindow.FromStartDuration(10, 1);

        Assert.Equal(10, window.Start);
        Assert.Equal(10, window.End);
        Assert.True(window.Contains(10));
        Assert.False(window.Contains(9));
        Assert.False(window.Contains(11));
    }
}
