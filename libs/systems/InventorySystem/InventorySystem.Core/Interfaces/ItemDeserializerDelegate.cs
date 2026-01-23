using Tomato.SerializationSystem;

namespace Tomato.InventorySystem;

/// <summary>
/// アイテムをデシリアライズするデリゲート。
/// ref structであるBinaryDeserializerを扱うために必要。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public delegate TItem ItemDeserializerDelegate<TItem>(ref BinaryDeserializer deserializer)
    where TItem : class, IInventoryItem;
