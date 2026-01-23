using System;
using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// クラフトレシピのインターフェース。
/// </summary>
public interface ICraftingRecipe
{
    /// <summary>レシピの一意な識別子</summary>
    RecipeId Id { get; }

    /// <summary>レシピ名</summary>
    string Name { get; }

    /// <summary>必要な材料リスト</summary>
    IReadOnlyList<CraftingIngredient> Ingredients { get; }

    /// <summary>出力アイテムリスト</summary>
    IReadOnlyList<CraftingOutput> Outputs { get; }

    /// <summary>クラフトに必要なtick数（即時クラフトの場合は0）</summary>
    int CraftingTicks { get; }

    /// <summary>タグ（カテゴリ分類等に使用）</summary>
    IReadOnlyList<string> Tags { get; }
}

/// <summary>
/// レシピを一意に識別するID。
/// </summary>
public readonly struct RecipeId : IEquatable<RecipeId>, IComparable<RecipeId>
{
    public readonly int Value;

    public RecipeId(int value) => Value = value;

    public bool Equals(RecipeId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is RecipeId other && Equals(other);
    public override int GetHashCode() => Value;
    public int CompareTo(RecipeId other) => Value.CompareTo(other.Value);

    public static bool operator ==(RecipeId left, RecipeId right) => left.Equals(right);
    public static bool operator !=(RecipeId left, RecipeId right) => !left.Equals(right);

    public override string ToString() => $"RecipeId({Value})";

    public static readonly RecipeId Invalid = new(-1);
    public bool IsValid => Value >= 0;
}
