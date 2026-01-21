using System;

namespace Tomato.DiagnosticsSystem;

/// <summary>
/// 固定サイズの循環バッファ。
/// </summary>
public sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive");

        _buffer = new T[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>要素数</summary>
    public int Count => _count;

    /// <summary>容量</summary>
    public int Capacity => _buffer.Length;

    /// <summary>要素を追加</summary>
    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
        {
            _count++;
        }
    }

    /// <summary>全要素をクリア</summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _count = 0;
    }

    /// <summary>全要素を配列として取得（古い順）</summary>
    public T[] ToArray()
    {
        var result = new T[_count];
        if (_count == 0) return result;

        if (_count < _buffer.Length)
        {
            // バッファが満杯でない場合
            Array.Copy(_buffer, 0, result, 0, _count);
        }
        else
        {
            // バッファが満杯の場合
            int start = _head;
            int firstPart = _buffer.Length - start;
            Array.Copy(_buffer, start, result, 0, firstPart);
            Array.Copy(_buffer, 0, result, firstPart, start);
        }

        return result;
    }

    /// <summary>インデクサ（0が最も古い要素）</summary>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            int actualIndex;
            if (_count < _buffer.Length)
            {
                actualIndex = index;
            }
            else
            {
                actualIndex = (_head + index) % _buffer.Length;
            }
            return _buffer[actualIndex];
        }
    }
}
