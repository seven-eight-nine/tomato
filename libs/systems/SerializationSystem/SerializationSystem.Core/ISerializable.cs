namespace Tomato.SerializationSystem;

/// <summary>
/// シリアライズ可能なオブジェクトのインターフェース。
/// </summary>
public interface ISerializable
{
    /// <summary>オブジェクトをシリアライズ</summary>
    void Serialize(BinarySerializer serializer);

    /// <summary>オブジェクトをデシリアライズ</summary>
    void Deserialize(ref BinaryDeserializer deserializer);
}
