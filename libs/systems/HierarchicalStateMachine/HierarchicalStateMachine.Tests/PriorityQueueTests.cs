using System;
using Tomato.HierarchicalStateMachine;
using Xunit;

namespace HierarchicalStateMachine.Tests;

public class PriorityQueueTests
{
    [Fact]
    public void Enqueue_SingleItem_CanBeDequeued()
    {
        var queue = new PriorityQueue<string>();
        queue.Enqueue("item", 1.0f);

        Assert.Equal(1, queue.Count);
        Assert.False(queue.IsEmpty);
        Assert.Equal("item", queue.Dequeue());
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void Dequeue_ReturnsItemsInPriorityOrder()
    {
        var queue = new PriorityQueue<string>();
        queue.Enqueue("low", 3.0f);
        queue.Enqueue("high", 1.0f);
        queue.Enqueue("medium", 2.0f);

        Assert.Equal("high", queue.Dequeue());
        Assert.Equal("medium", queue.Dequeue());
        Assert.Equal("low", queue.Dequeue());
    }

    [Fact]
    public void Dequeue_EmptyQueue_ThrowsException()
    {
        var queue = new PriorityQueue<string>();

        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
    }

    [Fact]
    public void Peek_ReturnsMinWithoutRemoving()
    {
        var queue = new PriorityQueue<string>();
        queue.Enqueue("low", 2.0f);
        queue.Enqueue("high", 1.0f);

        Assert.Equal("high", queue.Peek());
        Assert.Equal(2, queue.Count);
        Assert.Equal("high", queue.Peek());
    }

    [Fact]
    public void Peek_EmptyQueue_ThrowsException()
    {
        var queue = new PriorityQueue<string>();

        Assert.Throws<InvalidOperationException>(() => queue.Peek());
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var queue = new PriorityQueue<string>();
        queue.Enqueue("a", 1.0f);
        queue.Enqueue("b", 2.0f);
        queue.Enqueue("c", 3.0f);

        queue.Clear();

        Assert.Equal(0, queue.Count);
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void ManyItems_MaintainsHeapProperty()
    {
        var queue = new PriorityQueue<int>();
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            queue.Enqueue(i, (float)random.NextDouble());
        }

        int count = 0;
        while (!queue.IsEmpty)
        {
            _ = queue.Dequeue();
            count++;
        }

        Assert.Equal(100, count);
    }

    [Fact]
    public void SamePriority_AllItemsRetrieved()
    {
        var queue = new PriorityQueue<string>();
        queue.Enqueue("a", 1.0f);
        queue.Enqueue("b", 1.0f);
        queue.Enqueue("c", 1.0f);

        var items = new[] { queue.Dequeue(), queue.Dequeue(), queue.Dequeue() };

        Assert.Contains("a", items);
        Assert.Contains("b", items);
        Assert.Contains("c", items);
    }

    [Fact]
    public void NegativePriority_Works()
    {
        var queue = new PriorityQueue<string>();
        queue.Enqueue("positive", 1.0f);
        queue.Enqueue("negative", -1.0f);
        queue.Enqueue("zero", 0.0f);

        Assert.Equal("negative", queue.Dequeue());
        Assert.Equal("zero", queue.Dequeue());
        Assert.Equal("positive", queue.Dequeue());
    }
}
