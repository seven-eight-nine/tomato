using System;
using Xunit;

namespace Tomato.DiagnosticsSystem.Tests;

/// <summary>
/// CircularBuffer テスト
/// </summary>
public class CircularBufferTests
{
    [Fact]
    public void Constructor_ShouldSetCapacity()
    {
        var buffer = new CircularBuffer<int>(10);
        Assert.Equal(10, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Constructor_ZeroCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(0));
    }

    [Fact]
    public void Constructor_NegativeCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(-1));
    }

    [Fact]
    public void Add_ShouldIncreaseCount()
    {
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(1);
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void Add_WhenFull_ShouldOverwrite()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // Overwrites 1

        Assert.Equal(3, buffer.Count);
    }

    [Fact]
    public void ToArray_ShouldReturnInOrder()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        var array = buffer.ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, array);
    }

    [Fact]
    public void ToArray_WhenWrapped_ShouldReturnCorrectOrder()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // 1 is overwritten
        buffer.Add(5); // 2 is overwritten

        var array = buffer.ToArray();
        Assert.Equal(new[] { 3, 4, 5 }, array);
    }

    [Fact]
    public void Indexer_ShouldReturnCorrectElement()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(10);
        buffer.Add(20);
        buffer.Add(30);

        Assert.Equal(10, buffer[0]);
        Assert.Equal(20, buffer[1]);
        Assert.Equal(30, buffer[2]);
    }

    [Fact]
    public void Indexer_WhenWrapped_ShouldReturnCorrectElement()
    {
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);
        buffer.Add(5);

        Assert.Equal(3, buffer[0]);
        Assert.Equal(4, buffer[1]);
        Assert.Equal(5, buffer[2]);
    }

    [Fact]
    public void Indexer_OutOfRange_ShouldThrow()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
    }

    [Fact]
    public void Clear_ShouldResetBuffer()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(new int[0], buffer.ToArray());
    }

    [Fact]
    public void ToArray_WhenEmpty_ShouldReturnEmptyArray()
    {
        var buffer = new CircularBuffer<int>(5);
        var array = buffer.ToArray();
        Assert.Empty(array);
    }

    [Fact]
    public void WithReferenceTypes_ShouldWork()
    {
        var buffer = new CircularBuffer<string>(3);
        buffer.Add("a");
        buffer.Add("b");
        buffer.Add("c");
        buffer.Add("d");

        Assert.Equal(new[] { "b", "c", "d" }, buffer.ToArray());
    }
}
