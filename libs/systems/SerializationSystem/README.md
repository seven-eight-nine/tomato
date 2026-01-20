# SerializationSystem

高性能バイナリシリアライズシステム。ゲームステートのスナップショットやネットワーク通信に最適化。

## 構造

```
SerializationSystem/
├── SerializationSystem.Core/
│   ├── BinarySerializer.cs     # シリアライザ
│   ├── BinaryDeserializer.cs   # デシリアライザ (ref struct)
│   └── ISerializable.cs        # インターフェース
└── SerializationSystem.Tests/
```

## 使用例

### 基本的な使用法

```csharp
// シリアライズ
var serializer = new BinarySerializer();
serializer.Write(42);
serializer.Write(3.14f);
serializer.Write("Hello");
serializer.Write(new Vector3(1, 2, 3));

byte[] data = serializer.ToArray();

// デシリアライズ
var deserializer = new BinaryDeserializer(data);
int intValue = deserializer.ReadInt32();
float floatValue = deserializer.ReadSingle();
string stringValue = deserializer.ReadString();
Vector3 vector = deserializer.ReadVector3();
```

### サポートする型

| 型 | Write | Read |
|---|-------|------|
| bool | Write(bool) | ReadBoolean() |
| byte | Write(byte) | ReadByte() |
| sbyte | Write(sbyte) | ReadSByte() |
| short | Write(short) | ReadInt16() |
| ushort | Write(ushort) | ReadUInt16() |
| int | Write(int) | ReadInt32() |
| uint | Write(uint) | ReadUInt32() |
| long | Write(long) | ReadInt64() |
| ulong | Write(ulong) | ReadUInt64() |
| float | Write(float) | ReadSingle() |
| double | Write(double) | ReadDouble() |
| string | Write(string?) | ReadString() |
| Vector3 | Write(Vector3) | ReadVector3() |
| AABB | Write(AABB) | ReadAABB() |
| byte[] | Write(ReadOnlySpan<byte>) | ReadBytes(length) |

### 配列のシリアライズ

```csharp
// 配列書き込み
serializer.WriteArray(new[] { 1, 2, 3, 4, 5 });
serializer.WriteArray(new[] { 1.5f, 2.5f, 3.5f });
serializer.WriteBoolArray(new[] { true, false, true });

// 配列読み込み
int[] ints = deserializer.ReadInt32Array();
float[] floats = deserializer.ReadSingleArray();
bool[] bools = deserializer.ReadBoolArray();  // ビットパック
```

### カスタム型のシリアライズ

```csharp
public class PlayerState : ISerializable
{
    public int Health { get; set; }
    public Vector3 Position { get; set; }
    public string Name { get; set; } = "";

    public void Serialize(BinarySerializer serializer)
    {
        serializer.Write(Health);
        serializer.Write(Position);
        serializer.Write(Name);
    }

    public void Deserialize(ref BinaryDeserializer deserializer)
    {
        Health = deserializer.ReadInt32();
        Position = deserializer.ReadVector3();
        Name = deserializer.ReadString() ?? "";
    }
}

// 使用
serializer.Write(playerState);
var loaded = deserializer.Read<PlayerState>();
```

### シリアライザの再利用

```csharp
var serializer = new BinarySerializer();

// フレームごとに再利用
foreach (var frame in frames)
{
    serializer.Reset();  // 位置をリセット
    SerializeFrame(frame, serializer);
    SendData(serializer.GetWrittenSpan());
}
```

## パフォーマンス

- **BinaryDeserializer**: `ref struct`によりヒープアロケーションなし
- **書き込み**: `Unsafe.WriteUnaligned`による高速書き込み
- **読み込み**: `Unsafe.ReadUnaligned`による高速読み込み
- **ビットパック**: bool配列は8倍の圧縮効率
- **自動拡張**: バッファ不足時は自動的に2倍に拡張

## 依存関係

- CollisionSystem.Core（Vector3, AABB）

## テスト

```bash
dotnet test libs/SerializationSystem/SerializationSystem.Tests/
```

## ライセンス

MIT License
