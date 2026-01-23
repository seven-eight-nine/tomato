using Tomato.SerializationSystem;

namespace Tomato.InventorySystem;

/// <summary>
/// インベントリに格納可能なアイテムのインターフェース。
/// </summary>
public interface IInventoryItem : ISerializable
{
    /// <summary>アイテム定義（種類）のID</summary>
    ItemDefinitionId DefinitionId { get; }

    /// <summary>このアイテムインスタンスの一意なID</summary>
    ItemInstanceId InstanceId { get; }

    /// <summary>スタック数</summary>
    int StackCount { get; set; }

    /// <summary>アイテムの複製を作成する</summary>
    IInventoryItem Clone();
}
