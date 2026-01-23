namespace Tomato.InventorySystem;

/// <summary>
/// アイテム追加操作のコンテキスト情報。
/// </summary>
public readonly struct AddContext
{
    /// <summary>追加操作のソース（入手経路など）</summary>
    public readonly AddSource Source;

    /// <summary>スタックを許可するかどうか</summary>
    public readonly bool AllowStacking;

    /// <summary>カスタムデータ（ゲーム固有の追加情報）</summary>
    public readonly object? CustomData;

    public AddContext(AddSource source = AddSource.Unknown, bool allowStacking = true, object? customData = null)
    {
        Source = source;
        AllowStacking = allowStacking;
        CustomData = customData;
    }

    /// <summary>デフォルトのコンテキスト</summary>
    public static readonly AddContext Default = new(AddSource.Unknown, true, null);

    /// <summary>スタックを禁止したコンテキスト</summary>
    public static AddContext NoStacking => new(AddSource.Unknown, false, null);

    public override string ToString() => $"AddContext(Source={Source}, AllowStacking={AllowStacking})";
}

/// <summary>
/// アイテム追加のソース（入手経路）。
/// </summary>
public enum AddSource
{
    /// <summary>不明</summary>
    Unknown,
    /// <summary>拾得</summary>
    Pickup,
    /// <summary>購入</summary>
    Purchase,
    /// <summary>クラフト</summary>
    Craft,
    /// <summary>報酬</summary>
    Reward,
    /// <summary>転送</summary>
    Transfer,
    /// <summary>システム付与</summary>
    System
}
