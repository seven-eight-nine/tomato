# InventorySystem

ゲーム向けインベントリ・クラフト管理ライブラリ。

## これは何？

アイテムの格納・取得・転送・クラフトを管理する汎用システム。
ゲーム固有のロジック（容量制限、スロット構造、スタック可否、レシピ）は外部から注入可能。

```
インベントリA                      インベントリB
┌─────────────────┐               ┌─────────────────┐
│ Sword x1        │  ──Transfer──▶│ Sword x1        │
│ Potion x5       │               │                 │
│ Shield x1       │               │                 │
└─────────────────┘               └─────────────────┘
      │ TryAdd         │ TryRemove        │ Snapshot
      │ Validation     │ Events           │ Restore
      │                │                  │
      └────────────────┴────── Craft ─────┘
```

## なぜ使うのか

- **疎結合**: ストレージ構造とバリデーションロジックを分離。実装を差し替え可能
- **シリアライズ対応**: SerializationSystemと統合。セーブ/ロードが容易
- **スナップショット**: 状態のバックアップと復元。ロールバック対応
- **イベント駆動**: 追加/削除/スタック変更/クラフト完了を通知。UI更新が容易
- **クラフト統合**: レシピベースのクラフト、売買抽象化、再帰的計画、tick制クラフト

---

## クイックスタート

### 1. アイテムクラスを定義

```csharp
using System.Threading;
using Tomato.InventorySystem;
using Tomato.SerializationSystem;

public class MyItem : IInventoryItem
{
    private static long _nextId;

    public ItemDefinitionId DefinitionId { get; }
    public ItemInstanceId InstanceId { get; }
    public int StackCount { get; set; }
    public string Name { get; }

    public MyItem(int definitionId, string name, int stackCount = 1)
    {
        DefinitionId = new ItemDefinitionId(definitionId);
        InstanceId = new ItemInstanceId(Interlocked.Increment(ref _nextId));
        Name = name;
        StackCount = stackCount;
    }

    public IInventoryItem Clone() => new MyItem(DefinitionId.Value, Name, StackCount);

    public void Serialize(BinarySerializer serializer)
    {
        serializer.Write(DefinitionId.Value);
        serializer.Write(InstanceId.Value);
        serializer.Write(StackCount);
        serializer.Write(Name);
    }

    public void Deserialize(ref BinaryDeserializer deserializer) { }

    public static MyItem Create(ref BinaryDeserializer deserializer)
    {
        var defId = deserializer.ReadInt32();
        var instId = deserializer.ReadInt64();
        var stackCount = deserializer.ReadInt32();
        var name = deserializer.ReadString() ?? "";
        return new MyItem(defId, name, stackCount);
    }
}
```

### 2. インベントリを作成

```csharp
var inventory = new SimpleInventory<MyItem>(
    new InventoryId(1),
    20,  // capacity
    (ref BinaryDeserializer d) => MyItem.Create(ref d));
```

### 3. アイテムを追加・削除

```csharp
// 追加
var sword = new MyItem(definitionId: 1, name: "Sword");
var result = inventory.TryAdd(sword);
if (result.Success)
{
    Console.WriteLine($"Added: {result.ItemInstanceId}");
}

// 取得
var item = inventory.Get(sword.InstanceId);

// 削除（部分スタック）
var removeResult = inventory.TryRemove(sword.InstanceId, count: 1);
```

---

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** に以下が記載されている：

- 用語定義
- 設計哲学
- セットアップ手順
- バリデーションシステム
- スナップショットとロールバック
- インベントリ間転送
- クラフトシステム
- シリアライズ
- 派生実装の作成方法
- パフォーマンス設計

---

## 主要な概念

**インベントリ** = アイテムを格納するコンテナ

各操作は以下の順序で処理される：

| 順序 | 概念 | 説明 |
|:----:|------|------|
| 1 | **バリデーション** | 操作の可否を検証。失敗→操作中止 |
| 2 | **実行** | ストレージへの追加/削除 |
| 3 | **イベント発火** | OnItemAdded, OnItemRemoved等 |
| → | **結果** | 成功/失敗と詳細情報を返却 |

---

## よく使うパターン

### アイテムの追加

```csharp
// 追加可能かチェック
var validation = inventory.CanAdd(item);
if (validation.IsValid)
{
    inventory.TryAdd(item);
}

// 通常追加（バリデーション付き）
var result = inventory.TryAdd(item);

// コンテキスト付き追加
var result = inventory.TryAdd(item, new AddContext(AddSource.Pickup));

// バリデーションスキップ（信頼できるソースからの追加）
inventory.AddUnchecked(item);
```

### アイテムの削除

```csharp
// 削除可能かチェック
var validation = inventory.CanRemove(instanceId, count: 3);
if (validation.IsValid)
{
    inventory.TryRemove(instanceId, count: 3);
}

// 全量削除
var result = inventory.TryRemove(instanceId);

// 部分削除（スタックから3つ）
var result = inventory.TryRemove(instanceId, count: 3);

// 条件削除（期限切れアイテムを全削除）
int removed = inventory.RemoveWhere(item => item.IsExpired);

// 全クリア
inventory.Clear();
```

### アイテムの検索

```csharp
// インスタンスIDで取得
var item = inventory.Get(instanceId);

// 定義IDで取得（同種のアイテム全て）
var potions = inventory.GetByDefinition(new ItemDefinitionId(100));

// 合計スタック数を取得
int totalPotions = inventory.GetTotalStackCount(new ItemDefinitionId(100));

// 存在確認
bool exists = inventory.Contains(instanceId);
```

### イベント購読

```csharp
inventory.OnItemAdded += e =>
{
    Console.WriteLine($"Added: {e.Item} from {e.Context.Source}");
};

inventory.OnItemRemoved += e =>
{
    Console.WriteLine($"Removed: {e.Item} x{e.RemovedCount}");
};

inventory.OnItemStackChanged += e =>
{
    Console.WriteLine($"Stack: {e.Item} {e.PreviousStackCount} -> {e.NewStackCount}");
};
```

---

## バリデーション

### 組み込みバリデータ

```csharp
// 常に許可（テスト用）
AlwaysAllowValidator<MyItem>.Instance

// 容量制限
new CapacityValidator<MyItem>(maxCapacity: 20)

// 複合（AND結合）
new CompositeValidator<MyItem>()
    .Add(new CapacityValidator<MyItem>(20))
    .Add(new MyCustomValidator())
```

### カスタムバリデータ

```csharp
public class WeightValidator : IInventoryValidator<MyItem>
{
    private readonly int _maxWeight;

    public WeightValidator(int maxWeight) => _maxWeight = maxWeight;

    public IValidationResult ValidateAdd(IInventory<MyItem> inventory, MyItem item, AddContext context)
    {
        int currentWeight = inventory.GetAll().Sum(i => i.Weight);
        if (currentWeight + item.Weight > _maxWeight)
        {
            return ValidationResult.Fail(
                ValidationFailureCode.Custom,
                $"Weight limit exceeded: {currentWeight + item.Weight}/{_maxWeight}");
        }
        return ValidationResult.Success();
    }

    public IValidationResult ValidateRemove(IInventory<MyItem> inventory, MyItem item, int count)
        => ValidationResult.Success();

    public IValidationResult ValidateTransfer(IInventory<MyItem> source, IInventory<MyItem> dest, MyItem item, int count)
        => ValidationResult.Success();
}
```

---

## インベントリ間転送

```csharp
var transferManager = new TransferManager<MyItem>();

// 単一アイテム転送
var result = transferManager.TryTransfer(source, dest, itemInstanceId);

// 部分スタック転送
var result = transferManager.TryTransfer(source, dest, itemInstanceId, count: 5);

// 全量転送
var result = transferManager.TryTransferAll(source, dest, itemInstanceId);

// 転送可能かチェック
bool canTransfer = transferManager.CanTransfer(source, dest, itemInstanceId);
```

---

## スナップショット

```csharp
var snapshotManager = new SnapshotManager<MyItem>(maxSnapshotsPerInventory: 10);

// 保存
var snapshotId = snapshotManager.CreateSnapshot(inventory);

// 何か操作...
inventory.Clear();

// 復元
snapshotManager.TryRestoreSnapshot(snapshotId, inventory);

// 最新スナップショットに復元
snapshotManager.TryRestoreLatest(inventory);
```

---

## トランザクション

複数のインベントリ操作をアトミックにコミット/ロールバックする。

### InventoryTransaction

```csharp
// トランザクション開始
using var transaction = InventoryTransaction<MyItem>.Begin(sourceInventory, destInventory);

// 操作実行
sourceInventory.TryRemove(itemId);
destInventory.TryAdd(item);

// コミット（変更を確定）
transaction.Commit();

// もしくはロールバック（変更を取り消し）
// transaction.Rollback();

// Commitを呼ばずにDisposeすると自動ロールバック
```

### TransactionScope

```csharp
// usingスコープで自動管理
using (var scope = new TransactionScope<MyItem>(sourceInventory, destInventory))
{
    sourceInventory.TryRemove(itemId);
    destInventory.TryAdd(item);

    // Complete()を呼ぶと、Dispose時にコミット
    scope.Complete();
}
// Complete()を呼ばずにスコープを抜けると自動ロールバック

// 例外発生時も自動ロールバック
using (var scope = new TransactionScope<MyItem>(inventory))
{
    inventory.TryRemove(itemId);
    throw new Exception("Something went wrong");
    // ロールバックされる
}
```

### 後からインベントリを追加

```csharp
using var transaction = InventoryTransaction<MyItem>.Begin();

// 最初のインベントリを登録
transaction.Enlist(inventory1);
inventory1.TryRemove(item1.InstanceId);

// 後からインベントリを追加登録
transaction.Enlist(inventory2);
inventory2.TryAdd(item1);

transaction.Commit();
```

---

## クラフトシステム

### 基本クラフト

```csharp
// レシピ登録
var registry = new RecipeRegistry();
registry.Register(RecipeBuilder.Create(1)
    .Name("Iron Sword")
    .Ingredient(new ItemDefinitionId(1), 3)  // 鉄インゴット x3
    .Output(new ItemDefinitionId(2), 1)       // 鉄の剣 x1
    .Ticks(20)                                // 20 tick
    .Build());

// アイテムファクトリ
var factory = new DelegateItemFactory<MyItem>((defId, count) =>
    new MyItem(defId.Value, $"Item_{defId.Value}", count));

// クラフトマネージャー
var craftingManager = new CraftingManager<MyItem>(registry, factory);

// クラフト実行
var recipe = registry.GetRecipe(new RecipeId(1))!;
var result = craftingManager.TryCraft(recipe, sourceInventory, outputInventory);
if (result.Success)
{
    Console.WriteLine($"Created: {result.CreatedItems!.Count} items");
}
```

### 売買パターン（クラフトで抽象化）

```csharp
// アイテム売却 = アイテム→ゴールドへのクラフト
var sellRecipe = RecipeBuilder.Create(100)
    .Name("Sell Sword")
    .Ingredient(new ItemDefinitionId(2), 1)  // 剣 x1
    .Output(new ItemDefinitionId(999), 50)   // ゴールド x50
    .Ticks(0)  // 即時
    .Build();
registry.Register(sellRecipe);

// 売却実行（剣インベントリ → 所持金インベントリ）
craftingManager.TryCraft(sellRecipe, itemInventory, goldInventory);

// 購入は逆パターン
var buyRecipe = RecipeBuilder.Create(101)
    .Name("Buy Potion")
    .Ingredient(new ItemDefinitionId(999), 20)  // ゴールド x20
    .Output(new ItemDefinitionId(3), 1)          // ポーション x1
    .Build();
registry.Register(buyRecipe);
craftingManager.TryCraft(buyRecipe, goldInventory, itemInventory);
```

### 再帰的クラフト計画

```csharp
// 鉄の剣を作りたいが鉄インゴットがない → 自動で鉄鉱石から計画
var planner = new CraftingPlanner(registry);
var swordRecipe = registry.GetRecipe(new RecipeId(1))!;
var plan = planner.CreatePlan(swordRecipe, inventory);

if (plan.IsExecutable)
{
    // 必要な全ステップを表示
    foreach (var step in plan.Steps)
    {
        Console.WriteLine($"  {step.Recipe.Name} x{step.Count}");
    }

    // 計画を一括実行
    var result = plan.TryExecute(craftingManager, inventory);
}
else
{
    // 足りない材料を表示
    foreach (var missing in plan.MissingItems)
    {
        Console.WriteLine($"Missing: {missing.DefinitionId} x{missing.Count}");
    }
}
```

### Tick制クラフト（工業系）

```csharp
// tick制クラフター
var crafter = new TickBasedCrafter<MyItem>(craftingManager);

// キューに追加可能かチェック
var ironIngotRecipe = registry.GetRecipe(new RecipeId(1))!;
if (crafter.CanEnqueue(ironIngotRecipe, inventory))
{
    crafter.Enqueue(ironIngotRecipe, inventory);
}

// 不足材料を確認
var missing = crafter.CheckEnqueueIngredients(ironIngotRecipe, inventory);
foreach (var item in missing)
{
    Console.WriteLine($"Missing: {item.DefinitionId} x{item.Count}");
}

// ジョブをキューに追加
crafter.Enqueue(ironIngotRecipe, inventory);
crafter.Enqueue(ironIngotRecipe, inventory);

// イベント購読
crafter.OnJobStarted += e => Console.WriteLine($"Started: {e.Job.Recipe.Name}");
crafter.OnJobProgress += e => Console.WriteLine($"Progress: {e.Progress:P0}");
crafter.OnJobCompleted += e => Console.WriteLine($"Completed: {e.Job.Recipe.Name}");

// ゲームループでtickを進める
void GameUpdate()
{
    crafter.Tick();
}

// 進捗確認
Console.WriteLine($"Progress: {crafter.CurrentProgressRatio:P0}");
Console.WriteLine($"Queued: {crafter.QueuedJobCount}");
```

---

## デバッグ

```csharp
// 追加失敗の理由を確認
var result = inventory.TryAdd(item);
if (!result.Success && result.ValidationResult != null)
{
    foreach (var reason in result.ValidationResult.FailureReasons)
    {
        Console.WriteLine($"Failed: {reason.Code} - {reason.Message}");
    }
}

// 出力例:
// Failed: CapacityExceeded - Inventory is full (capacity: 20)

// クラフト失敗の理由を確認
var craftResult = craftingManager.TryCraft(recipe, inventory);
if (!craftResult.Success)
{
    Console.WriteLine($"Craft failed: {craftResult.FailureReason}");
    if (craftResult.MissingIngredients != null)
    {
        foreach (var missing in craftResult.MissingIngredients)
        {
            Console.WriteLine($"  Missing: {missing.DefinitionId} x{missing.Count}");
        }
    }
}
```

---

## ライセンス

MIT License
