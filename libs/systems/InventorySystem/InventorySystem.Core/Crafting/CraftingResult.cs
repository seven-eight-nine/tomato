using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// クラフト操作の結果を表す。
/// </summary>
public readonly struct CraftingResult
{
    /// <summary>クラフトが成功したかどうか</summary>
    public readonly bool Success;

    /// <summary>生成されたアイテム（成功時）</summary>
    public readonly IReadOnlyList<IInventoryItem>? CreatedItems;

    /// <summary>失敗理由</summary>
    public readonly CraftingFailureReason FailureReason;

    /// <summary>不足している材料（InsufficientMaterials時）</summary>
    public readonly IReadOnlyList<CraftingIngredient>? MissingIngredients;

    /// <summary>詳細メッセージ</summary>
    public readonly string? Message;

    private CraftingResult(
        bool success,
        IReadOnlyList<IInventoryItem>? createdItems,
        CraftingFailureReason failureReason,
        IReadOnlyList<CraftingIngredient>? missingIngredients,
        string? message)
    {
        Success = success;
        CreatedItems = createdItems;
        FailureReason = failureReason;
        MissingIngredients = missingIngredients;
        Message = message;
    }

    /// <summary>成功結果を作成する</summary>
    public static CraftingResult Succeeded(IReadOnlyList<IInventoryItem> createdItems) =>
        new(true, createdItems, CraftingFailureReason.None, null, null);

    /// <summary>レシピが見つからない場合の失敗結果</summary>
    public static CraftingResult RecipeNotFound() =>
        new(false, null, CraftingFailureReason.RecipeNotFound, null, "Recipe not found");

    /// <summary>材料不足の失敗結果</summary>
    public static CraftingResult InsufficientMaterials(IReadOnlyList<CraftingIngredient> missing) =>
        new(false, null, CraftingFailureReason.InsufficientMaterials, missing, "Insufficient materials");

    /// <summary>出力先が満杯の失敗結果</summary>
    public static CraftingResult OutputFull() =>
        new(false, null, CraftingFailureReason.OutputInventoryFull, null, "Output inventory is full");

    /// <summary>アイテムファクトリが未設定の失敗結果</summary>
    public static CraftingResult NoItemFactory() =>
        new(false, null, CraftingFailureReason.NoItemFactory, null, "Item factory not provided");

    /// <summary>カスタム失敗結果</summary>
    public static CraftingResult Failed(CraftingFailureReason reason, string message) =>
        new(false, null, reason, null, message);

    public override string ToString() =>
        Success
            ? $"CraftingResult(Success, Items={CreatedItems?.Count ?? 0})"
            : $"CraftingResult(Failed, {FailureReason}, {Message})";
}

/// <summary>
/// クラフト失敗の理由。
/// </summary>
public enum CraftingFailureReason
{
    /// <summary>失敗なし（成功時）</summary>
    None,
    /// <summary>レシピが見つからない</summary>
    RecipeNotFound,
    /// <summary>材料が不足している</summary>
    InsufficientMaterials,
    /// <summary>出力先インベントリが満杯</summary>
    OutputInventoryFull,
    /// <summary>アイテムファクトリが未設定</summary>
    NoItemFactory,
    /// <summary>クラフト中にキャンセルされた</summary>
    Cancelled,
    /// <summary>カスタム失敗</summary>
    Custom
}
