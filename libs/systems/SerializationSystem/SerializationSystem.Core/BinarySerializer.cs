using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Tomato.CollisionSystem;

namespace Tomato.SerializationSystem;

/// <summary>
/// 高性能バイナリシリアライザ。
/// </summary>
public sealed class BinarySerializer
{
    private byte[] _buffer;
    private int _position;

    /// <summary>現在の書き込み位置</summary>
    public int Position => _position;

    /// <summary>バッファ容量</summary>
    public int Capacity => _buffer.Length;

    public BinarySerializer(int capacity = 65536)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be positive", nameof(capacity));

        _buffer = new byte[capacity];
        _position = 0;
    }

    /// <summary>位置をリセット</summary>
    public void Reset()
    {
        _position = 0;
    }

    /// <summary>書き込まれたデータを取得</summary>
    public ReadOnlySpan<byte> GetWrittenSpan()
    {
        return _buffer.AsSpan(0, _position);
    }

    /// <summary>書き込まれたデータを配列として取得</summary>
    public byte[] ToArray()
    {
        var result = new byte[_position];
        Array.Copy(_buffer, result, _position);
        return result;
    }

    #region Write Primitives

    /// <summary>bool を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(bool value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = value ? (byte)1 : (byte)0;
    }

    /// <summary>byte を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = value;
    }

    /// <summary>sbyte を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(sbyte value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = (byte)value;
    }

    /// <summary>short を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(short value)
    {
        EnsureCapacity(2);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 2;
    }

    /// <summary>ushort を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort value)
    {
        EnsureCapacity(2);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 2;
    }

    /// <summary>int を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int value)
    {
        EnsureCapacity(4);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 4;
    }

    /// <summary>uint を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(uint value)
    {
        EnsureCapacity(4);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 4;
    }

    /// <summary>long を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(long value)
    {
        EnsureCapacity(8);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 8;
    }

    /// <summary>ulong を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ulong value)
    {
        EnsureCapacity(8);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 8;
    }

    /// <summary>float を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(float value)
    {
        EnsureCapacity(4);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 4;
    }

    /// <summary>double を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(double value)
    {
        EnsureCapacity(8);
        Unsafe.WriteUnaligned(ref _buffer[_position], value);
        _position += 8;
    }

    #endregion

    #region Write Complex Types

    /// <summary>Vector3 を書き込み</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(Vector3 value)
    {
        Write(value.X);
        Write(value.Y);
        Write(value.Z);
    }

    /// <summary>AABB を書き込み</summary>
    public void Write(AABB value)
    {
        Write(value.Min);
        Write(value.Max);
    }

    /// <summary>文字列を書き込み（null対応）</summary>
    public void Write(string? value)
    {
        if (value == null)
        {
            Write(-1);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        Write(bytes.Length);
        Write(bytes.AsSpan());
    }

    /// <summary>バイト配列を書き込み</summary>
    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_position));
        _position += data.Length;
    }

    /// <summary>シリアライズ可能オブジェクトを書き込み</summary>
    public void Write(ISerializable value)
    {
        value.Serialize(this);
    }

    #endregion

    #region Write Arrays

    /// <summary>int配列を書き込み</summary>
    public void WriteArray(ReadOnlySpan<int> values)
    {
        Write(values.Length);
        foreach (var value in values)
        {
            Write(value);
        }
    }

    /// <summary>float配列を書き込み</summary>
    public void WriteArray(ReadOnlySpan<float> values)
    {
        Write(values.Length);
        foreach (var value in values)
        {
            Write(value);
        }
    }

    /// <summary>bool配列を書き込み（ビットパック）</summary>
    public void WriteBoolArray(ReadOnlySpan<bool> values)
    {
        Write(values.Length);

        var byteCount = (values.Length + 7) / 8;
        EnsureCapacity(byteCount);

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                _buffer[_position + byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        _position += byteCount;
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int additionalBytes)
    {
        var required = _position + additionalBytes;
        if (required > _buffer.Length)
        {
            Grow(required);
        }
    }

    private void Grow(int minCapacity)
    {
        var newCapacity = Math.Max(_buffer.Length * 2, minCapacity);
        var newBuffer = new byte[newCapacity];
        Array.Copy(_buffer, newBuffer, _position);
        _buffer = newBuffer;
    }
}
