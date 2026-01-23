using System;
using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// シンプルなレシピ実装。
/// </summary>
public sealed class SimpleRecipe : ICraftingRecipe
{
    public RecipeId Id { get; }
    public string Name { get; }
    public IReadOnlyList<CraftingIngredient> Ingredients { get; }
    public IReadOnlyList<CraftingOutput> Outputs { get; }
    public int CraftingTicks { get; }
    public IReadOnlyList<string> Tags { get; }

    internal SimpleRecipe(
        RecipeId id,
        string name,
        IReadOnlyList<CraftingIngredient> ingredients,
        IReadOnlyList<CraftingOutput> outputs,
        int craftingTicks,
        IReadOnlyList<string> tags)
    {
        Id = id;
        Name = name;
        Ingredients = ingredients;
        Outputs = outputs;
        CraftingTicks = craftingTicks;
        Tags = tags;
    }

    public override string ToString() => $"Recipe({Name}, Id={Id})";
}

/// <summary>
/// レシピビルダー。
/// </summary>
public sealed class RecipeBuilder
{
    private static int _nextId;

    private RecipeId _id;
    private string _name = "";
    private readonly List<CraftingIngredient> _ingredients = new();
    private readonly List<CraftingOutput> _outputs = new();
    private int _craftingTicks;
    private readonly List<string> _tags = new();

    private RecipeBuilder() { }

    /// <summary>新しいレシピの構築を開始する</summary>
    public static RecipeBuilder Create()
    {
        return new RecipeBuilder
        {
            _id = new RecipeId(System.Threading.Interlocked.Increment(ref _nextId))
        };
    }

    /// <summary>指定したIDでレシピの構築を開始する</summary>
    public static RecipeBuilder Create(int id)
    {
        return new RecipeBuilder
        {
            _id = new RecipeId(id)
        };
    }

    /// <summary>レシピ名を設定する</summary>
    public RecipeBuilder Name(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>材料を追加する</summary>
    public RecipeBuilder Ingredient(int definitionId, int count)
    {
        _ingredients.Add(new CraftingIngredient(definitionId, count));
        return this;
    }

    /// <summary>材料を追加する</summary>
    public RecipeBuilder Ingredient(ItemDefinitionId definitionId, int count)
    {
        _ingredients.Add(new CraftingIngredient(definitionId, count));
        return this;
    }

    /// <summary>出力を追加する</summary>
    public RecipeBuilder Output(int definitionId, int count, Func<int, IInventoryItem>? factory = null)
    {
        _outputs.Add(new CraftingOutput(definitionId, count, factory));
        return this;
    }

    /// <summary>出力を追加する</summary>
    public RecipeBuilder Output(ItemDefinitionId definitionId, int count, Func<int, IInventoryItem>? factory = null)
    {
        _outputs.Add(new CraftingOutput(definitionId, count, factory));
        return this;
    }

    /// <summary>クラフト時間（tick数）を設定する</summary>
    public RecipeBuilder Ticks(int ticks)
    {
        _craftingTicks = ticks;
        return this;
    }

    /// <summary>タグを追加する</summary>
    public RecipeBuilder Tag(params string[] tags)
    {
        _tags.AddRange(tags);
        return this;
    }

    /// <summary>レシピをビルドする</summary>
    public SimpleRecipe Build()
    {
        if (_outputs.Count == 0)
            throw new InvalidOperationException("Recipe must have at least one output");

        return new SimpleRecipe(
            _id,
            _name,
            _ingredients.ToArray(),
            _outputs.ToArray(),
            _craftingTicks,
            _tags.ToArray());
    }

    /// <summary>レシピをビルドして変数に出力する</summary>
    public RecipeBuilder Build(out SimpleRecipe recipe)
    {
        recipe = Build();
        return this;
    }
}
