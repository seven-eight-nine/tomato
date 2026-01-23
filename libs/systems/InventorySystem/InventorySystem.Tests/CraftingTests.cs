using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.InventorySystem;
using Tomato.SerializationSystem;

namespace Tomato.InventorySystem.Tests;

public class CraftingTests
{
    private readonly RecipeRegistry _registry;
    private readonly IItemFactory<TestItem> _itemFactory;
    private readonly CraftingManager<TestItem> _craftingManager;

    // アイテム定義ID
    private readonly ItemDefinitionId _ironOre = new(1);
    private readonly ItemDefinitionId _ironIngot = new(2);
    private readonly ItemDefinitionId _ironSword = new(3);
    private readonly ItemDefinitionId _coal = new(4);
    private readonly ItemDefinitionId _gold = new(100);

    private static int _nextInventoryId;
    private static long _nextInstanceId;

    public CraftingTests()
    {
        _registry = new RecipeRegistry();
        _itemFactory = new DelegateItemFactory<TestItem>((defId, count) =>
            new TestItem(defId, new ItemInstanceId(System.Threading.Interlocked.Increment(ref _nextInstanceId)), count));
        _craftingManager = new CraftingManager<TestItem>(_registry, _itemFactory);

        // レシピ登録
        SetupRecipes();
    }

    private void SetupRecipes()
    {
        // 鉄鉱石 x2 + 石炭 x1 → 鉄インゴット x1 (10 ticks)
        _registry.Register(RecipeBuilder.Create(1)
            .Name("Smelt Iron")
            .Ingredient(_ironOre, 2)
            .Ingredient(_coal, 1)
            .Output(_ironIngot, 1)
            .Ticks(10)
            .Build());

        // 鉄インゴット x3 → 鉄の剣 x1 (20 ticks)
        _registry.Register(RecipeBuilder.Create(2)
            .Name("Forge Iron Sword")
            .Ingredient(_ironIngot, 3)
            .Output(_ironSword, 1)
            .Ticks(20)
            .Build());

        // アイテム売却: 鉄の剣 x1 → ゴールド x50 (即時)
        _registry.Register(RecipeBuilder.Create(3)
            .Name("Sell Iron Sword")
            .Ingredient(_ironSword, 1)
            .Output(_gold, 50)
            .Ticks(0)
            .Build());
    }

    #region CraftingManager Tests

    [Fact]
    public void Craft_WithSufficientMaterials_Succeeds()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 4));
        inventory.TryAdd(CreateItem(_coal, 2));

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var result = _craftingManager.TryCraft(recipe, inventory);

        Assert.True(result.Success);
        Assert.Single(result.CreatedItems!);
        Assert.Equal(_ironIngot, result.CreatedItems![0].DefinitionId);
        Assert.Equal(2, inventory.GetTotalStackCount(_ironOre)); // 4-2=2
        Assert.Equal(1, inventory.GetTotalStackCount(_coal));    // 2-1=1
        Assert.Equal(1, inventory.GetTotalStackCount(_ironIngot));
    }

    [Fact]
    public void Craft_WithInsufficientMaterials_Fails()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 1)); // 2必要なのに1
        inventory.TryAdd(CreateItem(_coal, 1));    // 石炭は足りている

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var result = _craftingManager.TryCraft(recipe, inventory);

        Assert.False(result.Success);
        Assert.Equal(CraftingFailureReason.InsufficientMaterials, result.FailureReason);
        Assert.NotNull(result.MissingIngredients);
        Assert.Single(result.MissingIngredients);
        Assert.Equal(_ironOre, result.MissingIngredients![0].DefinitionId);
        Assert.Equal(1, result.MissingIngredients![0].Count); // 2-1=1不足
    }

    [Fact]
    public void Craft_MultipleCount_ConsumesCorrectAmount()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 10));
        inventory.TryAdd(CreateItem(_coal, 5));

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var result = _craftingManager.TryCraft(recipe, inventory, count: 3);

        Assert.True(result.Success);
        Assert.Equal(3, result.CreatedItems!.Count);
        Assert.Equal(4, inventory.GetTotalStackCount(_ironOre));  // 10-6=4
        Assert.Equal(2, inventory.GetTotalStackCount(_coal));     // 5-3=2
        Assert.Equal(3, inventory.GetTotalStackCount(_ironIngot));
    }

    [Fact]
    public void Craft_ToSeparateOutputInventory_Works()
    {
        var source = CreateInventory();
        var output = CreateInventory();
        source.TryAdd(CreateItem(_ironOre, 2));
        source.TryAdd(CreateItem(_coal, 1));

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var result = _craftingManager.TryCraft(recipe, source, output);

        Assert.True(result.Success);
        Assert.Equal(0, source.GetTotalStackCount(_ironOre));
        Assert.Equal(0, source.GetTotalStackCount(_coal));
        Assert.Equal(0, source.GetTotalStackCount(_ironIngot));
        Assert.Equal(1, output.GetTotalStackCount(_ironIngot));
    }

    [Fact]
    public void Craft_RecipeNotFound_Fails()
    {
        var inventory = CreateInventory();
        var result = _craftingManager.TryCraft(new RecipeId(999), inventory);

        Assert.False(result.Success);
        Assert.Equal(CraftingFailureReason.RecipeNotFound, result.FailureReason);
    }

    [Fact]
    public void CanCraft_ReturnsCorrectResult()
    {
        var inventory = CreateInventory();
        var recipe = _registry.GetRecipe(new RecipeId(1))!;

        Assert.False(_craftingManager.CanCraft(recipe, inventory));

        inventory.TryAdd(CreateItem(_ironOre, 2));
        inventory.TryAdd(CreateItem(_coal, 1));

        Assert.True(_craftingManager.CanCraft(recipe, inventory));
    }

    [Fact]
    public void GetMaxCraftCount_ReturnsCorrectValue()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 10));
        inventory.TryAdd(CreateItem(_coal, 3));

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        // 鉄鉱石: 10/2=5回, 石炭: 3/1=3回 → 最小の3
        Assert.Equal(3, _craftingManager.GetMaxCraftCount(recipe, inventory));
    }

    [Fact]
    public void Craft_SellPattern_ItemToGold()
    {
        var inventory = CreateInventory();
        var goldInventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironSword, 2));

        var sellRecipe = _registry.GetRecipe(new RecipeId(3))!;
        var result = _craftingManager.TryCraft(sellRecipe, inventory, goldInventory, 2);

        Assert.True(result.Success);
        Assert.Equal(0, inventory.GetTotalStackCount(_ironSword));
        Assert.Equal(100, goldInventory.GetTotalStackCount(_gold)); // 50*2
    }

    [Fact]
    public void Craft_FiresCompletedEvent()
    {
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 2));
        inventory.TryAdd(CreateItem(_coal, 1));

        CraftingCompletedEvent<TestItem>? capturedEvent = null;
        _craftingManager.OnCraftingCompleted += e => capturedEvent = e;

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        _craftingManager.TryCraft(recipe, inventory);

        Assert.NotNull(capturedEvent);
        Assert.Equal(recipe, capturedEvent.Value.Recipe);
        Assert.Equal(1, capturedEvent.Value.CraftCount);
    }

    #endregion

    #region TickBasedCrafter Tests

    [Fact]
    public void TickBasedCrafter_ProcessesJobOverTicks()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 2));
        inventory.TryAdd(CreateItem(_coal, 1));

        var recipe = _registry.GetRecipe(new RecipeId(1))!; // 10 ticks
        crafter.Enqueue(recipe, inventory);

        // 最初はジョブなし
        Assert.False(crafter.IsBusy);

        // 最初のTickでジョブ開始
        Assert.False(crafter.Tick()); // 完了していない
        Assert.True(crafter.IsBusy);
        Assert.Equal(1, crafter.CurrentProgress);

        // 9 tick進める (合計10)
        for (int i = 0; i < 8; i++)
        {
            Assert.False(crafter.Tick());
        }

        // 10 tick目で完了
        Assert.True(crafter.Tick());
        Assert.False(crafter.IsBusy);
        Assert.Equal(1, inventory.GetTotalStackCount(_ironIngot));
    }

    [Fact]
    public void TickBasedCrafter_InstantCrafting_CompletesImmediately()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironSword, 1));

        var sellRecipe = _registry.GetRecipe(new RecipeId(3))!; // 0 ticks
        crafter.Enqueue(sellRecipe, inventory);

        // 即時完了
        Assert.True(crafter.Tick());
        Assert.False(crafter.IsBusy);
        Assert.Equal(50, inventory.GetTotalStackCount(_gold));
    }

    [Fact]
    public void TickBasedCrafter_MultipleJobs_ProcessedInOrder()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironSword, 3));

        var sellRecipe = _registry.GetRecipe(new RecipeId(3))!;
        crafter.Enqueue(sellRecipe, inventory, count: 1);
        crafter.Enqueue(sellRecipe, inventory, count: 1);
        crafter.Enqueue(sellRecipe, inventory, count: 1);

        // キューに全3ジョブが入っている（Tick前なのでDequeueされていない）
        Assert.Equal(3, crafter.QueuedJobCount);

        // 3回のTickで全完了
        Assert.True(crafter.Tick()); // job1完了、queue=2
        Assert.Equal(2, crafter.QueuedJobCount);
        Assert.True(crafter.Tick()); // job2完了、queue=1
        Assert.True(crafter.Tick()); // job3完了、queue=0
        Assert.False(crafter.IsBusy);
        Assert.Equal(150, inventory.GetTotalStackCount(_gold));
    }

    [Fact]
    public void TickBasedCrafter_TickMultiple_ReturnsCompletedCount()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironSword, 5));

        var sellRecipe = _registry.GetRecipe(new RecipeId(3))!;
        crafter.EnqueueMultiple(sellRecipe, inventory, count: 5);

        int completed = crafter.TickMultiple(10);
        Assert.Equal(5, completed);
        Assert.Equal(250, inventory.GetTotalStackCount(_gold));
    }

    [Fact]
    public void TickBasedCrafter_CancelCurrent_StopsJob()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 2));
        inventory.TryAdd(CreateItem(_coal, 1));

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var job = crafter.Enqueue(recipe, inventory);

        crafter.Tick(); // ジョブ開始
        Assert.True(crafter.IsBusy);

        var cancelled = crafter.CancelCurrent();
        Assert.Equal(job.Id, cancelled!.Id);
        Assert.False(crafter.IsBusy);
    }

    [Fact]
    public void TickBasedCrafter_ProgressRatio_CalculatedCorrectly()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 2));
        inventory.TryAdd(CreateItem(_coal, 1));

        var recipe = _registry.GetRecipe(new RecipeId(1))!; // 10 ticks
        crafter.Enqueue(recipe, inventory);

        Assert.Equal(0f, crafter.CurrentProgressRatio);

        crafter.Tick();
        Assert.Equal(0.1f, crafter.CurrentProgressRatio, 0.001);

        crafter.TickMultiple(4);
        Assert.Equal(0.5f, crafter.CurrentProgressRatio, 0.001);
    }

    [Fact]
    public void TickBasedCrafter_InsufficientMaterials_SkipsJob()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        // 材料なし

        CraftingJobFailedEvent<TestItem>? failedEvent = null;
        crafter.OnJobFailed += e => failedEvent = e;

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        crafter.Enqueue(recipe, inventory);

        crafter.Tick();

        Assert.NotNull(failedEvent);
        Assert.Equal(CraftingFailureReason.InsufficientMaterials, failedEvent.Value.Result.FailureReason);
        Assert.False(crafter.IsBusy);
    }

    [Fact]
    public void TickBasedCrafter_CanEnqueue_WhenSufficientMaterials_ReturnsTrue()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 2));
        inventory.TryAdd(CreateItem(_coal, 1));

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var canEnqueue = crafter.CanEnqueue(recipe, inventory);

        Assert.True(canEnqueue);
    }

    [Fact]
    public void TickBasedCrafter_CanEnqueue_WhenInsufficientMaterials_ReturnsFalse()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        // 材料なし

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var canEnqueue = crafter.CanEnqueue(recipe, inventory);

        Assert.False(canEnqueue);
    }

    [Fact]
    public void TickBasedCrafter_CheckEnqueueIngredients_ReturnsMissingItems()
    {
        var crafter = new TickBasedCrafter<TestItem>(_craftingManager);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 1)); // 2必要なのに1
        // 石炭なし

        var recipe = _registry.GetRecipe(new RecipeId(1))!;
        var missing = crafter.CheckEnqueueIngredients(recipe, inventory);

        Assert.Equal(2, missing.Count);
        Assert.Contains(missing, m => m.DefinitionId == _ironOre && m.Count == 1);
        Assert.Contains(missing, m => m.DefinitionId == _coal && m.Count == 1);
    }

    #endregion

    #region CraftingPlanner Tests

    [Fact]
    public void CraftingPlanner_CreatePlan_SimpleCraft()
    {
        var planner = new CraftingPlanner(_registry);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironIngot, 3));

        var swordRecipe = _registry.GetRecipe(new RecipeId(2))!;
        var plan = planner.CreatePlan(swordRecipe, inventory);

        Assert.True(plan.IsExecutable);
        Assert.Single(plan.Steps);
        Assert.Equal(swordRecipe, plan.Steps[0].Recipe);
    }

    [Fact]
    public void CraftingPlanner_CreatePlan_RecursiveCraft()
    {
        var planner = new CraftingPlanner(_registry);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 6));
        inventory.TryAdd(CreateItem(_coal, 3));

        // 鉄の剣を作るには鉄インゴットが3必要
        // 鉄インゴット1を作るには鉄鉱石2+石炭1が必要
        var swordRecipe = _registry.GetRecipe(new RecipeId(2))!;
        var plan = planner.CreatePlan(swordRecipe, inventory);

        Assert.True(plan.IsExecutable);
        Assert.Equal(2, plan.Steps.Count);
        // 先にインゴット精錬、次に剣鍛造
        Assert.Equal("Smelt Iron", plan.Steps[0].Recipe.Name);
        Assert.Equal(3, plan.Steps[0].Count);
        Assert.Equal("Forge Iron Sword", plan.Steps[1].Recipe.Name);
    }

    [Fact]
    public void CraftingPlanner_CreatePlan_MissingMaterials()
    {
        var planner = new CraftingPlanner(_registry);
        var inventory = CreateInventory();
        // 何も入れない

        var swordRecipe = _registry.GetRecipe(new RecipeId(2))!;
        var plan = planner.CreatePlan(swordRecipe, inventory);

        Assert.False(plan.IsExecutable);
        Assert.NotEmpty(plan.MissingItems);
    }

    [Fact]
    public void CraftingPlanner_ExecutePlan_Succeeds()
    {
        var planner = new CraftingPlanner(_registry);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironOre, 6));
        inventory.TryAdd(CreateItem(_coal, 3));

        var swordRecipe = _registry.GetRecipe(new RecipeId(2))!;
        var plan = planner.CreatePlan(swordRecipe, inventory);

        var result = plan.TryExecute(_craftingManager, inventory);

        Assert.True(result.Success);
        Assert.Equal(1, inventory.GetTotalStackCount(_ironSword));
        Assert.Equal(0, inventory.GetTotalStackCount(_ironOre));
        Assert.Equal(0, inventory.GetTotalStackCount(_coal));
    }

    [Fact]
    public void CraftingPlanner_CreatePlanForOutput_FindsRecipe()
    {
        var planner = new CraftingPlanner(_registry);
        var inventory = CreateInventory();
        inventory.TryAdd(CreateItem(_ironIngot, 6));

        var plan = planner.CreatePlanForOutput(_ironSword, inventory, 2);

        Assert.True(plan.IsExecutable);
        Assert.Equal(2, plan.TotalCraftOperations);
    }

    #endregion

    #region RecipeRegistry Tests

    [Fact]
    public void RecipeRegistry_GetRecipesForOutput_FindsCorrectRecipes()
    {
        var recipes = _registry.GetRecipesForOutput(_ironIngot).ToList();

        Assert.Single(recipes);
        Assert.Equal("Smelt Iron", recipes[0].Name);
    }

    [Fact]
    public void RecipeRegistry_GetRecipesByTag_FindsCorrectRecipes()
    {
        // タグ付きレシピを追加
        _registry.Register(RecipeBuilder.Create(100)
            .Name("Tagged Recipe")
            .Output(_ironIngot, 1)
            .Tag("smelting", "basic")
            .Build());

        var recipes = _registry.GetRecipesByTag("smelting").ToList();

        Assert.Single(recipes);
        Assert.Equal("Tagged Recipe", recipes[0].Name);
    }

    #endregion

    #region RecipeBuilder Tests

    [Fact]
    public void RecipeBuilder_CreatesRecipeCorrectly()
    {
        var recipe = RecipeBuilder.Create(200)
            .Name("Test Recipe")
            .Ingredient(_ironOre, 5)
            .Output(_ironIngot, 2)
            .Ticks(15)
            .Tag("test")
            .Build();

        Assert.Equal(new RecipeId(200), recipe.Id);
        Assert.Equal("Test Recipe", recipe.Name);
        Assert.Single(recipe.Ingredients);
        Assert.Equal(5, recipe.Ingredients[0].Count);
        Assert.Single(recipe.Outputs);
        Assert.Equal(2, recipe.Outputs[0].Count);
        Assert.Equal(15, recipe.CraftingTicks);
        Assert.Contains("test", recipe.Tags);
    }

    #endregion

    #region Helper Methods

    private SimpleInventory<TestItem> CreateInventory(int capacity = 100)
    {
        return new SimpleInventory<TestItem>(
            new InventoryId(System.Threading.Interlocked.Increment(ref _nextInventoryId)),
            capacity,
            (ref BinaryDeserializer d) => TestItem.Deserialize(ref d, true));
    }

    private TestItem CreateItem(ItemDefinitionId definitionId, int stackCount)
    {
        return new TestItem(
            definitionId,
            new ItemInstanceId(System.Threading.Interlocked.Increment(ref _nextInstanceId)),
            stackCount);
    }

    #endregion
}
