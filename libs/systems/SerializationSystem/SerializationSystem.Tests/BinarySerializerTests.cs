using System;
using Tomato.CollisionSystem;
using Xunit;

namespace Tomato.SerializationSystem.Tests;

/// <summary>
/// BinarySerializer/BinaryDeserializer テスト
/// </summary>
public class BinarySerializerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultCapacity_ShouldBe65536()
    {
        var serializer = new BinarySerializer();
        Assert.Equal(65536, serializer.Capacity);
    }

    [Fact]
    public void Constructor_CustomCapacity_ShouldUseProvided()
    {
        var serializer = new BinarySerializer(1024);
        Assert.Equal(1024, serializer.Capacity);
    }

    [Fact]
    public void Constructor_ZeroCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new BinarySerializer(0));
    }

    [Fact]
    public void Constructor_NegativeCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new BinarySerializer(-1));
    }

    #endregion

    #region Boolean Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoundTrip_Boolean_ShouldPreserveValue(bool value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadBoolean());
    }

    #endregion

    #region Integer Tests

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)127)]
    [InlineData((byte)255)]
    public void RoundTrip_Byte_ShouldPreserveValue(byte value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadByte());
    }

    [Theory]
    [InlineData((sbyte)-128)]
    [InlineData((sbyte)0)]
    [InlineData((sbyte)127)]
    public void RoundTrip_SByte_ShouldPreserveValue(sbyte value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadSByte());
    }

    [Theory]
    [InlineData(short.MinValue)]
    [InlineData((short)0)]
    [InlineData(short.MaxValue)]
    public void RoundTrip_Int16_ShouldPreserveValue(short value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadInt16());
    }

    [Theory]
    [InlineData(ushort.MinValue)]
    [InlineData((ushort)32768)]
    [InlineData(ushort.MaxValue)]
    public void RoundTrip_UInt16_ShouldPreserveValue(ushort value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadUInt16());
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    [InlineData(12345678)]
    public void RoundTrip_Int32_ShouldPreserveValue(int value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadInt32());
    }

    [Theory]
    [InlineData(uint.MinValue)]
    [InlineData(uint.MaxValue)]
    public void RoundTrip_UInt32_ShouldPreserveValue(uint value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadUInt32());
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void RoundTrip_Int64_ShouldPreserveValue(long value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadInt64());
    }

    [Theory]
    [InlineData(ulong.MinValue)]
    [InlineData(ulong.MaxValue)]
    public void RoundTrip_UInt64_ShouldPreserveValue(ulong value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadUInt64());
    }

    #endregion

    #region Floating Point Tests

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(-3.14159f)]
    [InlineData(float.MinValue)]
    [InlineData(float.MaxValue)]
    public void RoundTrip_Single_ShouldPreserveValue(float value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadSingle());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(-3.141592653589793)]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    public void RoundTrip_Double_ShouldPreserveValue(double value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadDouble());
    }

    #endregion

    #region String Tests

    [Fact]
    public void RoundTrip_String_Null_ShouldPreserve()
    {
        var serializer = new BinarySerializer();
        serializer.Write((string?)null);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Null(deserializer.ReadString());
    }

    [Fact]
    public void RoundTrip_String_Empty_ShouldPreserve()
    {
        var serializer = new BinarySerializer();
        serializer.Write("");

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal("", deserializer.ReadString());
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("Hello, World!")]
    [InlineData("日本語テスト")]
    [InlineData("🎮🎲🎯")]
    public void RoundTrip_String_ShouldPreserveValue(string value)
    {
        var serializer = new BinarySerializer();
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(value, deserializer.ReadString());
    }

    #endregion

    #region Vector3 Tests

    [Fact]
    public void RoundTrip_Vector3_Zero_ShouldPreserve()
    {
        var serializer = new BinarySerializer();
        var value = new Vector3(0, 0, 0);
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.ReadVector3();

        Assert.Equal(value.X, result.X);
        Assert.Equal(value.Y, result.Y);
        Assert.Equal(value.Z, result.Z);
    }

    [Fact]
    public void RoundTrip_Vector3_ShouldPreserveValue()
    {
        var serializer = new BinarySerializer();
        var value = new Vector3(1.5f, -2.5f, 3.5f);
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.ReadVector3();

        Assert.Equal(value.X, result.X);
        Assert.Equal(value.Y, result.Y);
        Assert.Equal(value.Z, result.Z);
    }

    #endregion

    #region AABB Tests

    [Fact]
    public void RoundTrip_AABB_ShouldPreserveValue()
    {
        var serializer = new BinarySerializer();
        var value = new AABB(new Vector3(-1, -2, -3), new Vector3(4, 5, 6));
        serializer.Write(value);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.ReadAABB();

        Assert.Equal(value.Min.X, result.Min.X);
        Assert.Equal(value.Min.Y, result.Min.Y);
        Assert.Equal(value.Min.Z, result.Min.Z);
        Assert.Equal(value.Max.X, result.Max.X);
        Assert.Equal(value.Max.Y, result.Max.Y);
        Assert.Equal(value.Max.Z, result.Max.Z);
    }

    #endregion

    #region Array Tests

    [Fact]
    public void RoundTrip_Int32Array_Empty_ShouldPreserve()
    {
        var serializer = new BinarySerializer();
        serializer.WriteArray(Array.Empty<int>());

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.ReadInt32Array();

        Assert.Empty(result);
    }

    [Fact]
    public void RoundTrip_Int32Array_ShouldPreserveValues()
    {
        var serializer = new BinarySerializer();
        var values = new[] { 1, 2, 3, -4, 5 };
        serializer.WriteArray(values);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.ReadInt32Array();

        Assert.Equal(values, result);
    }

    [Fact]
    public void RoundTrip_SingleArray_ShouldPreserveValues()
    {
        var serializer = new BinarySerializer();
        var values = new[] { 1.5f, -2.5f, 3.14f };
        serializer.WriteArray(values);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.ReadSingleArray();

        Assert.Equal(values, result);
    }

    [Fact]
    public void RoundTrip_BoolArray_ShouldPreserveValues()
    {
        var serializer = new BinarySerializer();
        var values = new[] { true, false, true, true, false, false, true, false, true };
        serializer.WriteBoolArray(values);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.ReadBoolArray();

        Assert.Equal(values, result);
    }

    #endregion

    #region Mixed Types Tests

    [Fact]
    public void RoundTrip_MixedTypes_ShouldPreserveOrder()
    {
        var serializer = new BinarySerializer();

        serializer.Write(42);
        serializer.Write(3.14f);
        serializer.Write(true);
        serializer.Write("Hello");
        serializer.Write(new Vector3(1, 2, 3));

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());

        Assert.Equal(42, deserializer.ReadInt32());
        Assert.Equal(3.14f, deserializer.ReadSingle());
        Assert.True(deserializer.ReadBoolean());
        Assert.Equal("Hello", deserializer.ReadString());
        var vec = deserializer.ReadVector3();
        Assert.Equal(1f, vec.X);
        Assert.Equal(2f, vec.Y);
        Assert.Equal(3f, vec.Z);
    }

    #endregion

    #region Reset and Reuse Tests

    [Fact]
    public void Reset_ShouldAllowReuse()
    {
        var serializer = new BinarySerializer();

        serializer.Write(100);
        serializer.Reset();
        serializer.Write(200);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(200, deserializer.ReadInt32());
        Assert.True(deserializer.IsEnd);
    }

    [Fact]
    public void ToArray_ShouldCreateCopy()
    {
        var serializer = new BinarySerializer();
        serializer.Write(12345);

        var array1 = serializer.ToArray();
        serializer.Write(67890);
        var array2 = serializer.ToArray();

        Assert.Equal(4, array1.Length);
        Assert.Equal(8, array2.Length);
    }

    #endregion

    #region Capacity Tests

    [Fact]
    public void Write_ExceedingCapacity_ShouldGrow()
    {
        var serializer = new BinarySerializer(8);

        // Write more than initial capacity
        for (int i = 0; i < 100; i++)
        {
            serializer.Write(i);
        }

        Assert.Equal(400, serializer.Position); // 100 * 4 bytes

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(i, deserializer.ReadInt32());
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Deserializer_ReadPastEnd_ShouldThrow()
    {
        var serializer = new BinarySerializer();
        serializer.Write(42);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        deserializer.ReadInt32();

        InvalidOperationException? caught = null;
        try
        {
            deserializer.ReadInt32();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
    }

    [Fact]
    public void Deserializer_Skip_ShouldAdvancePosition()
    {
        var serializer = new BinarySerializer();
        serializer.Write(100);
        serializer.Write(200);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        deserializer.Skip(4);

        Assert.Equal(200, deserializer.ReadInt32());
    }

    [Fact]
    public void Deserializer_Remaining_ShouldBeCorrect()
    {
        var serializer = new BinarySerializer();
        serializer.Write(42);
        serializer.Write(3.14f);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        Assert.Equal(8, deserializer.Remaining);

        deserializer.ReadInt32();
        Assert.Equal(4, deserializer.Remaining);

        deserializer.ReadSingle();
        Assert.Equal(0, deserializer.Remaining);
        Assert.True(deserializer.IsEnd);
    }

    #endregion

    #region ISerializable Tests

    [Fact]
    public void RoundTrip_ISerializable_ShouldWork()
    {
        var serializer = new BinarySerializer();
        var original = new TestData { Id = 42, Name = "Test", Position = new Vector3(1, 2, 3) };

        serializer.Write(original);

        var deserializer = new BinaryDeserializer(serializer.GetWrittenSpan());
        var result = deserializer.Read<TestData>();

        Assert.Equal(original.Id, result.Id);
        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.Position.X, result.Position.X);
        Assert.Equal(original.Position.Y, result.Position.Y);
        Assert.Equal(original.Position.Z, result.Position.Z);
    }

    private class TestData : ISerializable
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Vector3 Position { get; set; }

        public void Serialize(BinarySerializer serializer)
        {
            serializer.Write(Id);
            serializer.Write(Name);
            serializer.Write(Position);
        }

        public void Deserialize(ref BinaryDeserializer deserializer)
        {
            Id = deserializer.ReadInt32();
            Name = deserializer.ReadString();
            Position = deserializer.ReadVector3();
        }
    }

    #endregion
}
