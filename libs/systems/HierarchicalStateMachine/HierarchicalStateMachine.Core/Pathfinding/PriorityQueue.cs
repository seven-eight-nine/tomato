using System;
using System.Collections.Generic;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 最小ヒープベースの優先度キュー。
/// A*探索のオープンリストとして使用。
/// </summary>
/// <typeparam name="T">要素の型</typeparam>
public class PriorityQueue<T>
{
    private readonly List<(T Item, float Priority)> _heap = new();
    private readonly IComparer<float> _comparer;

    public int Count => _heap.Count;
    public bool IsEmpty => _heap.Count == 0;

    public PriorityQueue()
    {
        _comparer = Comparer<float>.Default;
    }

    /// <summary>
    /// 要素を追加。
    /// </summary>
    public void Enqueue(T item, float priority)
    {
        _heap.Add((item, priority));
        HeapifyUp(_heap.Count - 1);
    }

    /// <summary>
    /// 最小優先度の要素を取り出す。
    /// </summary>
    public T Dequeue()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("Queue is empty.");

        var result = _heap[0].Item;
        var lastIndex = _heap.Count - 1;

        _heap[0] = _heap[lastIndex];
        _heap.RemoveAt(lastIndex);

        if (_heap.Count > 0)
            HeapifyDown(0);

        return result;
    }

    /// <summary>
    /// 最小優先度の要素を削除せずに取得。
    /// </summary>
    public T Peek()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("Queue is empty.");

        return _heap[0].Item;
    }

    /// <summary>
    /// キューをクリア。
    /// </summary>
    public void Clear()
    {
        _heap.Clear();
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            var parentIndex = (index - 1) / 2;
            if (_comparer.Compare(_heap[index].Priority, _heap[parentIndex].Priority) >= 0)
                break;

            Swap(index, parentIndex);
            index = parentIndex;
        }
    }

    private void HeapifyDown(int index)
    {
        while (true)
        {
            var leftChild = 2 * index + 1;
            var rightChild = 2 * index + 2;
            var smallest = index;

            if (leftChild < _heap.Count &&
                _comparer.Compare(_heap[leftChild].Priority, _heap[smallest].Priority) < 0)
            {
                smallest = leftChild;
            }

            if (rightChild < _heap.Count &&
                _comparer.Compare(_heap[rightChild].Priority, _heap[smallest].Priority) < 0)
            {
                smallest = rightChild;
            }

            if (smallest == index)
                break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int i, int j)
    {
        var temp = _heap[i];
        _heap[i] = _heap[j];
        _heap[j] = temp;
    }
}
