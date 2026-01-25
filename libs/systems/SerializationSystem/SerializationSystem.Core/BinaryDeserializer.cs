using System;
using System.Runtime.CompilerServices;
using System.Text;
using Tomato.Math;

namespace Tomato.SerializationSystem;

/// <summary>
/// 高性能バイナリデシリアライザ。
/// </summary>
public ref struct BinaryDeserializer
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    /// <summary>現在の読み取り位置</summary>
    public int Position => _position;

    /// <summary>データの長さ</summary>
    public int Length => _data.Length;

    /// <summary>残りバイト数</summary>
    public int Remaining => _data.Length - _position;

    /// <summary>読み取り完了したか</summary>
    public bool IsEnd => _position >= _data.Length;

    public BinaryDeserializer(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    /// <summary>位置をリセット</summary>
    public void Reset()
    {
        _position = 0;
    }

    /// <summary>位置をスキップ</summary>
    public void Skip(int bytes)
    {
        EnsureAvailable(bytes);
        _position += bytes;
    }

    #region Read Primitives

    /// <summary>bool を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBoolean()
    {
        EnsureAvailable(1);
        return _data[_position++] != 0;
    }

    /// <summary>byte を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _data[_position++];
    }

    /// <summary>sbyte を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadSByte()
    {
        EnsureAvailable(1);
        return (sbyte)_data[_position++];
    }

    /// <summary>short を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        EnsureAvailable(2);
        var value = Unsafe.ReadUnaligned<short>(ref Unsafe.AsRef(in _data[_position]));
        _position += 2;
        return value;
    }

    /// <summary>ushort を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        var value = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef(in _data[_position]));
        _position += 2;
        return value;
    }

    /// <summary>int を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        EnsureAvailable(4);
        var value = Unsafe.ReadUnaligned<int>(ref Unsafe.AsRef(in _data[_position]));
        _position += 4;
        return value;
    }

    /// <summary>uint を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        EnsureAvailable(4);
        var value = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef(in _data[_position]));
        _position += 4;
        return value;
    }

    /// <summary>long を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        EnsureAvailable(8);
        var value = Unsafe.ReadUnaligned<long>(ref Unsafe.AsRef(in _data[_position]));
        _position += 8;
        return value;
    }

    /// <summary>ulong を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        EnsureAvailable(8);
        var value = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef(in _data[_position]));
        _position += 8;
        return value;
    }

    /// <summary>float を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadSingle()
    {
        EnsureAvailable(4);
        var value = Unsafe.ReadUnaligned<float>(ref Unsafe.AsRef(in _data[_position]));
        _position += 4;
        return value;
    }

    /// <summary>double を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        EnsureAvailable(8);
        var value = Unsafe.ReadUnaligned<double>(ref Unsafe.AsRef(in _data[_position]));
        _position += 8;
        return value;
    }

    #endregion

    #region Read Complex Types

    /// <summary>Vector3 を読み取り</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ReadVector3()
    {
        return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
    }

    /// <summary>AABB を読み取り</summary>
    public AABB ReadAABB()
    {
        return new AABB(ReadVector3(), ReadVector3());
    }

    /// <summary>文字列を読み取り（null対応）</summary>
    public string? ReadString()
    {
        var length = ReadInt32();
        if (length < 0)
            return null;

        if (length == 0)
            return string.Empty;

        EnsureAvailable(length);
        var value = Encoding.UTF8.GetString(_data.Slice(_position, length));
        _position += length;
        return value;
    }

    /// <summary>バイト配列を読み取り</summary>
    public byte[] ReadBytes(int length)
    {
        EnsureAvailable(length);
        var result = _data.Slice(_position, length).ToArray();
        _position += length;
        return result;
    }

    /// <summary>シリアライズ可能オブジェクトを読み取り</summary>
    public T Read<T>() where T : ISerializable, new()
    {
        var value = new T();
        value.Deserialize(ref this);
        return value;
    }

    #endregion

    #region Read Arrays

    /// <summary>int配列を読み取り</summary>
    public int[] ReadInt32Array()
    {
        var length = ReadInt32();
        var result = new int[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = ReadInt32();
        }
        return result;
    }

    /// <summary>float配列を読み取り</summary>
    public float[] ReadSingleArray()
    {
        var length = ReadInt32();
        var result = new float[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = ReadSingle();
        }
        return result;
    }

    /// <summary>bool配列を読み取り（ビットパック）</summary>
    public bool[] ReadBoolArray()
    {
        var length = ReadInt32();
        var result = new bool[length];

        var byteCount = (length + 7) / 8;
        EnsureAvailable(byteCount);

        for (int i = 0; i < length; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            result[i] = (_data[_position + byteIndex] & (1 << bitIndex)) != 0;
        }

        _position += byteCount;
        return result;
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureAvailable(int bytes)
    {
        if (_position + bytes > _data.Length)
        {
            throw new InvalidOperationException(
                $"Attempted to read {bytes} bytes but only {Remaining} bytes remaining");
        }
    }
}
