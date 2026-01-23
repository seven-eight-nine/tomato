# InventorySystem 設計書

ゲーム向けインベントリ管理ライブラリの詳細設計ドキュメント。

namespace: `Tomato.InventorySystem`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [セットアップ](#セットアップ)
5. [識別子詳細](#識別子詳細)
6. [IInventoryItem詳細](#iinventoryitem詳細)
7. [IInventory詳細](#iinventory詳細)
8. [バリデーション詳細](#バリデーション詳細)
9. [コンテキストとイベント](#コンテキストとイベント)
10. [スナップショット詳細](#スナップショット詳細)
11. [転送システム詳細](#転送システム詳細)
12. [トランザクション](#トランザクション)
13. [クラフトシステム](#クラフトシステム)
14. [シリアライズ詳細](#シリアライズ詳細)
15. [派生実装の作成](#派生実装の作成)
16. [パフォーマンス](#パフォーマンス)
17. [実践パターン集](#実践パターン集)
18. [トラブルシューティング](#トラブルシューティング)

---

## クイックスタート

### 1. アイテムクラスを定義

```csharp
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

    public void Serialize(BinarySerializer serializer) { /* ... */ }
    public void Deserialize(ref BinaryDeserializer deserializer) { /* ... */ }
}
```

### 2. インベントリを作成

```csharp
var inventory = new SimpleInventory<MyItem>(
    new InventoryId(1),
    20,  // capacity
    (ref BinaryDeserializer d) => MyItem.Create(ref d));
```

### 3. アイテムを追加・削除・検索

```csharp
// 追加
var sword = new MyItem(1, "Sword");
var result = inventory.TryAdd(sword);

// 取得
var item = inventory.Get(sword.InstanceId);

// 削除
var removeResult = inventory.TryRemove(sword.InstanceId);
```

---

## 用語定義

### 中核概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **インベントリ** | Inventory | アイテムを格納するコンテナ。`IInventory<TItem>` を実装する。 |
| **アイテム** | Item | インベントリに格納される単位。`IInventoryItem` を実装する。 |
| **スタック** | Stack | 同じ種類のアイテムをまとめた数量。`StackCount` で表現。 |

### 識別子

| 用語 | 英語 | 定義 |
|------|------|------|
| **インベントリID** | InventoryId | インベントリを一意に識別する。`int` ベース。 |
| **アイテム定義ID** | ItemDefinitionId | アイテムの種類を識別する。同じ種類のアイテムは同じID。 |
| **アイテムインスタンスID** | ItemInstanceId | アイテムの個体を識別する。同じ種類でも各インスタンスは異なるID。 |
| **スナップショットID** | SnapshotId | スナップショットを一意に識別する。 |

### 操作フロー

| 順序 | 操作 | 説明 |
|:----:|------|------|
| 1 | **バリデーション** | `IInventoryValidator` で操作可否を検証 |
| 2 | **実行** | ストレージへの追加/削除を実行 |
| 3 | **イベント** | `OnItemAdded` 等のイベントを発火 |
| → | **結果** | `AddResult`, `RemoveResult` 等を返却 |

---

## 設計哲学

### 原則1: インターフェース抽出（Interface Extraction）

システムは「仕組み」のみを提供し、ゲーム固有のロジックは外部から注入する。

```csharp
// ❌ 悪い例: ロジックがハードコード
public class Inventory
{
    public bool TryAdd(Item item)
    {
        if (_items.Count >= 20) return false;  // 容量20固定
        if (item.Weight > 100) return false;   // 重量100固定
        // ...
    }
}

// ✓ 良い例: バリデータで注入
public class Inventory<TItem>
{
    private readonly IInventoryValidator<TItem> _validator;

    public AddResult TryAdd(TItem item, AddContext context)
    {
        var validation = _validator.ValidateAdd(this, item, context);
        if (!validation.IsValid) return AddResult.Failed(validation);
        // ...
    }
}
```

**メリット:**
- 容量制限、重量制限、アイテム種別制限などを自由に組み合わせ可能
- 同じインベントリ実装を異なるゲームルールで再利用可能
- テスト時にバリデータをモックに差し替え可能

### 原則2: 結果オブジェクト（Result Object）

操作の成功/失敗は例外ではなく結果オブジェクトで表現する。

```csharp
// ❌ 悪い例: 例外で失敗を表現
public void Add(Item item)
{
    if (IsFull()) throw new InventoryFullException();
    // ...
}

// ✓ 良い例: 結果オブジェクトで表現
public AddResult TryAdd(Item item)
{
    if (IsFull()) return AddResult.Failed(ValidationResult.Fail(CapacityExceeded));
    // ...
    return AddResult.Succeeded(item.InstanceId);
}
```

**メリット:**
- 失敗が予期されるケースで例外のオーバーヘッドを回避
- 複数の失敗理由を返却可能
- 呼び出し側で失敗処理を強制（nullチェック忘れを防ぐ）

### 原則3: イベント駆動（Event-Driven）

状態変更は同期的なイベントで通知する。

```csharp
public interface IInventory<TItem>
{
    event Action<ItemAddedEvent<TItem>>? OnItemAdded;
    event Action<ItemRemovedEvent<TItem>>? OnItemRemoved;
    event Action<ItemStackChangedEvent<TItem>>? OnItemStackChanged;
}
```

**メリット:**
- UI更新ロジックをインベントリから分離
- 複数のリスナーが同時に購読可能
- ログ記録、統計収集などを後付け可能

### 原則4: スナップショットによる状態管理（Snapshot-Based State）

状態のバックアップと復元をシリアライズベースで実現する。

```csharp
// スナップショット作成
var snapshot = inventory.CreateSnapshot();

// 何か操作...
inventory.Clear();

// ロールバック
inventory.RestoreFromSnapshot(snapshot);
```

**メリット:**
- トランザクション的なロールバックが可能
- ネットワーク同期時の状態再構築が容易
- デバッグ時の状態保存/復元が簡単

### 原則5: 2種類のIDによる明確な識別（Dual-ID System）

アイテムは「種類」と「個体」の2つのIDで識別する。

| ID | 目的 | 例 |
|----|------|-----|
| `ItemDefinitionId` | 種類の識別 | 「ポーション」「剣」 |
| `ItemInstanceId` | 個体の識別 | 「このポーション」「あの剣」 |

```csharp
// 同じ種類のアイテム
var potion1 = new Potion();  // DefinitionId=100, InstanceId=1
var potion2 = new Potion();  // DefinitionId=100, InstanceId=2

// 種類で検索（全てのポーション）
var allPotions = inventory.GetByDefinition(new ItemDefinitionId(100));

// 個体で検索（特定のポーション）
var specific = inventory.Get(new ItemInstanceId(1));
```

---

## セットアップ

### ステップ1: アイテムクラスを定義

`IInventoryItem` インターフェースを実装する。

```csharp
public interface IInventoryItem : ISerializable
{
    ItemDefinitionId DefinitionId { get; }  // 種類ID
    ItemInstanceId InstanceId { get; }       // 個体ID
    int StackCount { get; set; }             // スタック数
    IInventoryItem Clone();                  // 複製
}
```

**実装例:**

```csharp
public class GameItem : IInventoryItem
{
    private static long _nextInstanceId;

    // 必須プロパティ
    public ItemDefinitionId DefinitionId { get; }
    public ItemInstanceId InstanceId { get; }
    public int StackCount { get; set; }

    // ゲーム固有プロパティ
    public string Name { get; }
    public int MaxStack { get; }
    public ItemRarity Rarity { get; }

    public GameItem(int definitionId, string name, int maxStack = 99)
    {
        DefinitionId = new ItemDefinitionId(definitionId);
        InstanceId = new ItemInstanceId(Interlocked.Increment(ref _nextInstanceId));
        Name = name;
        MaxStack = maxStack;
        StackCount = 1;
    }

    private GameItem(ItemDefinitionId defId, ItemInstanceId instId, string name, int maxStack, int stackCount)
    {
        DefinitionId = defId;
        InstanceId = instId;
        Name = name;
        MaxStack = maxStack;
        StackCount = stackCount;
    }

    public IInventoryItem Clone()
    {
        // 新しいインスタンスIDで複製
        return new GameItem(
            DefinitionId,
            new ItemInstanceId(Interlocked.Increment(ref _nextInstanceId)),
            Name,
            MaxStack,
            StackCount);
    }

    public void Serialize(BinarySerializer serializer)
    {
        serializer.Write(DefinitionId.Value);
        serializer.Write(InstanceId.Value);
        serializer.Write(StackCount);
        serializer.Write(Name);
        serializer.Write(MaxStack);
    }

    public void Deserialize(ref BinaryDeserializer deserializer)
    {
        // 通常は使わない（ファクトリメソッドでデシリアライズ）
    }

    public static GameItem Deserialize(ref BinaryDeserializer deserializer)
    {
        var defId = new ItemDefinitionId(deserializer.ReadInt32());
        var instId = new ItemInstanceId(deserializer.ReadInt64());
        var stackCount = deserializer.ReadInt32();
        var name = deserializer.ReadString() ?? "";
        var maxStack = deserializer.ReadInt32();
        return new GameItem(defId, instId, name, maxStack, stackCount);
    }
}
```

### ステップ2: インベントリを作成

```csharp
// SimpleInventory（参考実装）を使用
var playerInventory = new SimpleInventory<GameItem>(
    new InventoryId(1),  // インベントリID
    30,                  // 容量
    (ref BinaryDeserializer d) => GameItem.Create(ref d)
);

// カスタムバリデータ付き
var limitedInventory = new SimpleInventory<GameItem>(
    new InventoryId(2),
    10,  // 容量
    (ref BinaryDeserializer d) => GameItem.Create(ref d),
    new CompositeValidator<GameItem>()
        .Add(new CapacityValidator<GameItem>(10))
        .Add(new WeightValidator<GameItem>(100))
);
```

### ステップ3: イベントを購読

```csharp
playerInventory.OnItemAdded += e =>
{
    Debug.Log($"[Inventory] Added: {e.Item.Name} x{e.Item.StackCount}");
    UpdateUI();
};

playerInventory.OnItemRemoved += e =>
{
    Debug.Log($"[Inventory] Removed: {e.Item.Name} x{e.RemovedCount}");
    UpdateUI();
};

playerInventory.OnItemStackChanged += e =>
{
    Debug.Log($"[Inventory] Stack changed: {e.Item.Name} {e.PreviousStackCount} -> {e.NewStackCount}");
    UpdateUI();
};
```

---

## 識別子詳細

### 構造

すべての識別子は `readonly struct` で実装され、値型セマンティクスを持つ。

```csharp
public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>, IComparable<ItemInstanceId>
{
    public readonly long Value;

    public ItemInstanceId(long value) => Value = value;

    public bool Equals(ItemInstanceId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ItemInstanceId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(ItemInstanceId other) => Value.CompareTo(other.Value);

    public static bool operator ==(ItemInstanceId left, ItemInstanceId right) => left.Equals(right);
    public static bool operator !=(ItemInstanceId left, ItemInstanceId right) => !left.Equals(right);

    public override string ToString() => $"ItemInstanceId({Value})";

    public static readonly ItemInstanceId Invalid = new(-1);
    public bool IsValid => Value >= 0;
}
```

### ID一覧

| 識別子 | 内部型 | 用途 |
|--------|--------|------|
| `InventoryId` | `int` | インベントリの識別 |
| `ItemDefinitionId` | `int` | アイテム種類の識別 |
| `ItemInstanceId` | `long` | アイテム個体の識別 |
| `SnapshotId` | `long` | スナップショットの識別 |

### 無効値

各識別子は `Invalid` 定数と `IsValid` プロパティを持つ。

```csharp
var id = ItemInstanceId.Invalid;
if (!id.IsValid)
{
    Console.WriteLine("Invalid ID");
}
```

---

## IInventoryItem詳細

### インターフェース

```csharp
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
```

### Clone()の実装ガイド

`Clone()` は部分スタック転送時に呼ばれる。以下の点に注意：

1. **新しいインスタンスIDを発行する**
2. **プロパティをコピーする**
3. **参照型プロパティはディープコピーする**

```csharp
public IInventoryItem Clone()
{
    // ❌ 悪い例: 同じインスタンスIDを使いまわし
    return new MyItem(DefinitionId, InstanceId, Name, StackCount);

    // ✓ 良い例: 新しいインスタンスIDを発行
    return new MyItem(
        DefinitionId,
        new ItemInstanceId(GenerateNewId()),
        Name,
        StackCount);
}
```

### シリアライズの実装

`ISerializable` を実装してセーブ/ロードに対応する。

```csharp
public void Serialize(BinarySerializer serializer)
{
    // 必須フィールド
    serializer.Write(DefinitionId.Value);
    serializer.Write(InstanceId.Value);
    serializer.Write(StackCount);

    // ゲーム固有フィールド
    serializer.Write(Name);
    serializer.Write((int)Rarity);
    serializer.Write(Durability);
}

// 注: Deserializeはref structを引数に取るため、staticファクトリを推奨
public static MyItem CreateFromDeserializer(ref BinaryDeserializer deserializer)
{
    var defId = new ItemDefinitionId(deserializer.ReadInt32());
    var instId = new ItemInstanceId(deserializer.ReadInt64());
    var stackCount = deserializer.ReadInt32();
    var name = deserializer.ReadString() ?? "";
    var rarity = (ItemRarity)deserializer.ReadInt32();
    var durability = deserializer.ReadInt32();

    return new MyItem(defId, instId, name, stackCount, rarity, durability);
}
```

---

## IInventory詳細

### インターフェース

```csharp
public interface IInventory<TItem> : ISerializable, ISnapshotable<IInventory<TItem>>
    where TItem : class, IInventoryItem
{
    // === プロパティ ===
    InventoryId Id { get; }
    int Count { get; }
    bool HasSpace { get; }

    // === 追加 ===
    IValidationResult CanAdd(TItem item, AddContext? context = null);
    AddResult TryAdd(TItem item, AddContext? context = null);
    void AddUnchecked(TItem item);

    // === 削除 ===
    IValidationResult CanRemove(ItemInstanceId instanceId, int count = 1);
    RemoveResult TryRemove(ItemInstanceId instanceId, int count = 1);
    int RemoveWhere(Func<TItem, bool> predicate);
    void Clear();

    // === クエリ ===
    TItem? Get(ItemInstanceId instanceId);
    IEnumerable<TItem> GetByDefinition(ItemDefinitionId definitionId);
    IEnumerable<TItem> GetAll();
    bool Contains(ItemInstanceId instanceId);
    int GetTotalStackCount(ItemDefinitionId definitionId);

    // === イベント ===
    event Action<ItemAddedEvent<TItem>>? OnItemAdded;
    event Action<ItemRemovedEvent<TItem>>? OnItemRemoved;
    event Action<ItemStackChangedEvent<TItem>>? OnItemStackChanged;
}
```

### CanAdd / CanRemove

操作を実行する前に、成功するかどうかを確認するための事前チェックメソッド。

```csharp
// 追加可能かチェック（UIのボタン有効化などに使用）
var validation = inventory.CanAdd(item);
if (validation.IsValid)
{
    inventory.TryAdd(item);
}
else
{
    foreach (var reason in validation.FailureReasons)
    {
        Console.WriteLine($"Cannot add: {reason.Code} - {reason.Message}");
    }
}

// 削除可能かチェック
var validation = inventory.CanRemove(itemId, count: 5);
if (!validation.IsValid)
{
    ShowMessage("数量が足りません");
}
```

### TryAdd vs AddUnchecked

| メソッド | バリデーション | 用途 |
|----------|:--------------:|------|
| `CanAdd` | バリデーションのみ | 事前チェック（変更なし） |
| `TryAdd` | あり | 通常のゲームプレイ |
| `AddUnchecked` | なし | デシリアライズ、システム付与 |

```csharp
// プレイヤーがアイテムを拾う → バリデーション必要
var result = inventory.TryAdd(item, new AddContext(AddSource.Pickup));
if (!result.Success)
{
    ShowMessage("インベントリがいっぱいです");
}

// セーブデータから復元 → バリデーション不要
inventory.AddUnchecked(loadedItem);
```

### TryRemoveの部分削除

`TryRemove` は `count` パラメータで部分削除が可能。

```csharp
// 全量削除（デフォルト: count=1だがスタック全体を削除）
var result = inventory.TryRemove(potionId);

// 部分削除（スタックから3つだけ）
var result = inventory.TryRemove(potionId, count: 3);
```

**部分削除時の挙動:**

1. 指定数量分を `StackCount` から減算
2. `OnItemStackChanged` イベントを発火
3. `RemoveResult.RemovedItem` には削除分のクローンを格納

```csharp
// 10個のポーションから3個削除
var potion = inventory.Get(potionId);  // StackCount = 10
var result = inventory.TryRemove(potionId, count: 3);

// 結果
potion.StackCount;              // 7（元のアイテムは残る）
result.RemovedCount;            // 3
result.RemovedItem.StackCount;  // 3（削除分のクローン）
```

---

## バリデーション詳細

### インターフェース

```csharp
public interface IInventoryValidator<TItem>
    where TItem : class, IInventoryItem
{
    IValidationResult ValidateAdd(IInventory<TItem> inventory, TItem item, AddContext context);
    IValidationResult ValidateRemove(IInventory<TItem> inventory, TItem item, int count);
    IValidationResult ValidateTransfer(IInventory<TItem> source, IInventory<TItem> dest, TItem item, int count);
}

public interface IValidationResult
{
    bool IsValid { get; }
    IReadOnlyList<ValidationFailureReason> FailureReasons { get; }
}
```

### 組み込みバリデータ

| バリデータ | 説明 |
|------------|------|
| `AlwaysAllowValidator<T>` | 常に許可（テスト用） |
| `CapacityValidator<T>` | 容量制限 |
| `CompositeValidator<T>` | 複数のバリデータをAND結合 |

### CapacityValidator

```csharp
public sealed class CapacityValidator<TItem> : IInventoryValidator<TItem>
{
    private readonly int _maxCapacity;

    public CapacityValidator(int maxCapacity) => _maxCapacity = maxCapacity;

    public IValidationResult ValidateAdd(IInventory<TItem> inventory, TItem item, AddContext context)
    {
        if (inventory.Count >= _maxCapacity)
        {
            return ValidationResult.Fail(
                ValidationFailureCode.CapacityExceeded,
                $"Inventory is full (capacity: {_maxCapacity})");
        }
        return ValidationResult.Success();
    }
    // ...
}
```

### CompositeValidator

複数のバリデータを組み合わせる。すべてが成功した場合のみ成功。

```csharp
var validator = new CompositeValidator<GameItem>()
    .Add(new CapacityValidator<GameItem>(30))
    .Add(new WeightValidator<GameItem>(100))
    .Add(new ItemTypeValidator<GameItem>(allowedTypes));

// すべてのバリデータがチェックされ、失敗理由はすべて収集される
var result = inventory.TryAdd(item);
if (!result.Success)
{
    foreach (var reason in result.ValidationResult.FailureReasons)
    {
        Console.WriteLine($"{reason.Code}: {reason.Message}");
    }
}
// 出力例:
// CapacityExceeded: Inventory is full (capacity: 30)
// Custom: Weight limit exceeded (current: 95, item: 10, max: 100)
```

### カスタムバリデータの実装

```csharp
// スタック上限バリデータ
public class MaxStackValidator<TItem> : IInventoryValidator<TItem>
    where TItem : class, IInventoryItem, IStackable
{
    public IValidationResult ValidateAdd(IInventory<TItem> inventory, TItem item, AddContext context)
    {
        // 同じ定義IDのアイテムを探す
        var existing = inventory.GetByDefinition(item.DefinitionId).FirstOrDefault();
        if (existing != null)
        {
            if (existing.StackCount + item.StackCount > item.MaxStack)
            {
                return ValidationResult.Fail(
                    ValidationFailureCode.InvalidStackCount,
                    $"Stack would exceed maximum ({item.MaxStack})");
            }
        }
        return ValidationResult.Success();
    }

    public IValidationResult ValidateRemove(...) => ValidationResult.Success();
    public IValidationResult ValidateTransfer(...) => ValidationResult.Success();
}

// 装備スロット専用バリデータ
public class EquipmentSlotValidator : IInventoryValidator<Equipment>
{
    private readonly EquipmentSlot _allowedSlot;

    public EquipmentSlotValidator(EquipmentSlot slot) => _allowedSlot = slot;

    public IValidationResult ValidateAdd(IInventory<Equipment> inventory, Equipment item, AddContext context)
    {
        if (item.Slot != _allowedSlot)
        {
            return ValidationResult.Fail(
                ValidationFailureCode.InvalidItemType,
                $"This inventory only accepts {_allowedSlot} items");
        }
        return ValidationResult.Success();
    }
    // ...
}
```

### ValidationFailureCode一覧

| コード | 説明 |
|--------|------|
| `CapacityExceeded` | 容量超過 |
| `InvalidItemType` | アイテム種類が無効 |
| `InvalidStackCount` | スタック数が無効 |
| `ItemNotFound` | アイテムが見つからない |
| `InsufficientQuantity` | 数量不足 |
| `DestinationFull` | 転送先が満杯 |
| `TransferNotAllowed` | 転送不可 |
| `Custom` | カスタムバリデーション失敗 |

---

## コンテキストとイベント

### AddContext

追加操作の追加情報を提供する。

```csharp
public readonly struct AddContext
{
    public readonly AddSource Source;      // 入手経路
    public readonly bool AllowStacking;    // スタック許可
    public readonly object? CustomData;    // カスタムデータ

    public static readonly AddContext Default = new(AddSource.Unknown, true, null);
}

public enum AddSource
{
    Unknown,    // 不明
    Pickup,     // 拾得
    Purchase,   // 購入
    Craft,      // クラフト
    Reward,     // 報酬
    Transfer,   // 転送
    System      // システム付与
}
```

**使用例:**

```csharp
// 拾得
inventory.TryAdd(item, new AddContext(AddSource.Pickup));

// 購入（スタック禁止）
inventory.TryAdd(item, new AddContext(AddSource.Purchase, allowStacking: false));

// カスタムデータ付き
inventory.TryAdd(item, new AddContext(AddSource.Reward, customData: questId));
```

### イベント構造体

```csharp
// 追加イベント
public readonly struct ItemAddedEvent<TItem>
{
    public readonly InventoryId InventoryId;
    public readonly TItem Item;
    public readonly AddContext Context;
}

// 削除イベント
public readonly struct ItemRemovedEvent<TItem>
{
    public readonly InventoryId InventoryId;
    public readonly TItem Item;
    public readonly int RemovedCount;
    public readonly RemoveReason Reason;
}

// スタック変更イベント
public readonly struct ItemStackChangedEvent<TItem>
{
    public readonly InventoryId InventoryId;
    public readonly TItem Item;
    public readonly int PreviousStackCount;
    public readonly int NewStackCount;
    public int Delta => NewStackCount - PreviousStackCount;
}
```

### RemoveReason

```csharp
public enum RemoveReason
{
    Manual,     // 手動削除
    Used,       // 使用
    Transfer,   // 転送
    Discard,    // 破棄
    Clear,      // クリア
    System      // システム削除
}
```

---

## スナップショット詳細

### InventorySnapshot

```csharp
public sealed class InventorySnapshot
{
    public SnapshotId Id { get; }
    public DateTime CreatedAt { get; }
    public InventoryId InventoryId { get; }
    public byte[] Data { get; }
}
```

### 直接スナップショット

インベントリから直接スナップショットを作成/復元できる。

```csharp
// 作成
var snapshot = inventory.CreateSnapshot();

// 何か操作...
inventory.TryRemove(itemId);

// 復元
inventory.RestoreFromSnapshot(snapshot);
```

### SnapshotManager

複数のスナップショットを管理する場合は `SnapshotManager` を使用。

```csharp
var manager = new SnapshotManager<MyItem>(maxSnapshotsPerInventory: 10);

// 保存（自動ID発行）
var id1 = manager.CreateSnapshot(inventory);
// ... 操作 ...
var id2 = manager.CreateSnapshot(inventory);

// 復元（特定のスナップショット）
manager.TryRestoreSnapshot(id1, inventory);

// 復元（最新）
manager.TryRestoreLatest(inventory);

// 削除
manager.TryRemoveSnapshot(id1);

// 存在確認
bool exists = manager.HasSnapshot(id1);
bool hasAny = manager.HasSnapshotFor(inventory.Id);

// インベントリの全スナップショット削除
manager.ClearSnapshots(inventory.Id);

// 全削除
manager.ClearAll();
```

### maxSnapshotsPerInventory

インベントリごとのスナップショット数を制限。古いものから自動削除。

```csharp
var manager = new SnapshotManager<MyItem>(maxSnapshotsPerInventory: 5);

// 6つ目のスナップショットを作成すると、最も古いものが削除される
for (int i = 0; i < 6; i++)
{
    manager.CreateSnapshot(inventory);
}
Assert.Equal(5, manager.GetSnapshotCount(inventory.Id));
```

---

## 転送システム詳細

### TransferManager

インベントリ間のアイテム移動を管理する。

```csharp
public sealed class TransferManager<TItem>
{
    // グローバルバリデータ（すべての転送に適用）
    public TransferManager(IInventoryValidator<TItem>? globalValidator = null);

    // 転送
    public TransferResult TryTransfer(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        ItemInstanceId itemInstanceId,
        int count = 1);

    // コンテキスト付き転送
    public TransferResult TryTransfer(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        TransferContext context);

    // 全量転送
    public TransferResult TryTransferAll(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        ItemInstanceId itemInstanceId);

    // 転送可能チェック（変更なし）
    public bool CanTransfer(
        IInventory<TItem> source,
        IInventory<TItem> destination,
        ItemInstanceId itemInstanceId,
        int count = 1);
}
```

### 転送フロー

```
TryTransfer(source, dest, itemId, count)
│
├─1. ソースにアイテムがあるか確認
│   └─ なし → SourceItemNotFound
│
├─2. 数量チェック
│   └─ 不足 → InsufficientQuantity
│
├─3. 転送先に空きがあるか確認
│   └─ なし → DestinationFull
│
├─4. グローバルバリデータでチェック
│   └─ 失敗 → ValidationFailed
│
├─5. ソースから削除
│   └─ 失敗 → SourceItemNotFound
│
├─6. 転送先に追加
│   ├─ 成功 → TransferResult.Succeeded
│   └─ 失敗 → ソースに戻して失敗を返す
│
└─→ TransferResult
```

### TransferResult

```csharp
public readonly struct TransferResult
{
    public readonly bool Success;
    public readonly ItemInstanceId TransferredItemId;
    public readonly int TransferredCount;
    public readonly TransferFailureReason FailureReason;
    public readonly IValidationResult? ValidationResult;
}

public enum TransferFailureReason
{
    None,
    SourceItemNotFound,
    InsufficientQuantity,
    ValidationFailed,
    DestinationFull
}
```

### グローバルバリデータ

すべての転送に適用されるバリデーションルール。

```csharp
// プレイヤー間トレード禁止アイテムのチェック
public class NoTradeValidator : IInventoryValidator<GameItem>
{
    public IValidationResult ValidateTransfer(
        IInventory<GameItem> source,
        IInventory<GameItem> dest,
        GameItem item,
        int count)
    {
        if (item.IsBindOnPickup)
        {
            return ValidationResult.Fail(
                ValidationFailureCode.TransferNotAllowed,
                "This item cannot be traded");
        }
        return ValidationResult.Success();
    }
    // ...
}

var tradeManager = new TransferManager<GameItem>(new NoTradeValidator());
```

---

## シリアライズ詳細

### ISerializable実装

`InventoryBase` は `ISerializable` を実装しており、SerializationSystemと統合されている。

```csharp
public virtual void Serialize(BinarySerializer serializer)
{
    serializer.Write(Id.Value);
    var items = new List<TItem>(GetAllCore());
    serializer.Write(items.Count);
    foreach (var item in items)
    {
        item.Serialize(serializer);
    }
}

public virtual void Deserialize(ref BinaryDeserializer deserializer)
{
    ClearCore();
    var inventoryId = deserializer.ReadInt32();
    var count = deserializer.ReadInt32();
    for (int i = 0; i < count; i++)
    {
        var item = DeserializeItem(ref deserializer);
        AddCore(item, AddContext.Default);
    }
}
```

### セーブ/ロード例

```csharp
// セーブ
var serializer = new BinarySerializer();
playerInventory.Serialize(serializer);
File.WriteAllBytes("inventory.dat", serializer.ToArray());

// ロード
var data = File.ReadAllBytes("inventory.dat");
var deserializer = new BinaryDeserializer(data);
playerInventory.Deserialize(ref deserializer);
```

### ItemDeserializerDelegate

`BinaryDeserializer` は `ref struct` のため、`Func<>` の型引数に使えない。
そのため専用のデリゲート型を使用する。

```csharp
public delegate TItem ItemDeserializerDelegate<TItem>(ref BinaryDeserializer deserializer)
    where TItem : class, IInventoryItem;

// 使用例
var inventory = new SimpleInventory<GameItem>(
    new InventoryId(1),
    20,  // capacity
    (ref BinaryDeserializer d) => GameItem.Create(ref d));
```

---

## 派生実装の作成

### InventoryBase

独自のストレージ構造を持つインベントリを作成するには `InventoryBase<TItem>` を継承する。

```csharp
public abstract class InventoryBase<TItem> : IInventory<TItem>
{
    // === 実装必須 ===
    protected abstract AddResult AddCore(TItem item, AddContext context);
    protected abstract RemoveResult RemoveCore(ItemInstanceId instanceId, int count);
    protected abstract TItem? GetCore(ItemInstanceId instanceId);
    protected abstract IEnumerable<TItem> GetAllCore();
    protected abstract int GetCountCore();
    protected abstract bool HasSpaceCore { get; }
    protected abstract TItem DeserializeItem(ref BinaryDeserializer deserializer);
    protected abstract void ClearCore();
}
```

### 実装例: スロット型インベントリ

```csharp
public class SlotInventory<TItem> : InventoryBase<TItem>
    where TItem : class, IInventoryItem
{
    private readonly TItem?[] _slots;
    private readonly ItemDeserializerDelegate<TItem> _itemFactory;

    public SlotInventory(
        InventoryId id,
        int slotCount,
        ItemDeserializerDelegate<TItem> itemFactory,
        IInventoryValidator<TItem>? validator = null)
        : base(id, validator)
    {
        _slots = new TItem?[slotCount];
        _itemFactory = itemFactory;
    }

    protected override bool HasSpaceCore => Array.Exists(_slots, s => s == null);

    protected override int GetCountCore() => _slots.Count(s => s != null);

    protected override AddResult AddCore(TItem item, AddContext context)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = item;
                return AddResult.Succeeded(item.InstanceId);
            }
        }
        return AddResult.Failed(ValidationResult.Fail(ValidationFailureCode.CapacityExceeded));
    }

    protected override RemoveResult RemoveCore(ItemInstanceId instanceId, int count)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i]?.InstanceId == instanceId)
            {
                var item = _slots[i]!;
                _slots[i] = null;
                return RemoveResult.Succeeded(item.StackCount, item);
            }
        }
        return RemoveResult.NotFound();
    }

    protected override TItem? GetCore(ItemInstanceId instanceId)
    {
        return Array.Find(_slots, s => s?.InstanceId == instanceId);
    }

    protected override IEnumerable<TItem> GetAllCore()
    {
        return _slots.Where(s => s != null).Cast<TItem>();
    }

    protected override void ClearCore()
    {
        Array.Clear(_slots, 0, _slots.Length);
    }

    protected override TItem DeserializeItem(ref BinaryDeserializer deserializer)
    {
        return _itemFactory(ref deserializer);
    }

    // スロット固有のメソッド
    public TItem? GetSlot(int index) => _slots[index];

    public bool SetSlot(int index, TItem? item)
    {
        if (index < 0 || index >= _slots.Length) return false;
        _slots[index] = item;
        return true;
    }
}
```

### 派生実装例

| 実装名 | 説明 | 用途 |
|--------|------|------|
| **SimpleInventory** | リスト型、容量制限のみ | 汎用インベントリ |
| **SlotInventory** | 固定スロット型 | マイクラ式インベントリ |
| **SpatialInventory** | 2Dグリッド型 | バイオハザード式アタッシュケース |
| **EquipmentInventory** | 装備スロット型 | キャラクター装備 |
| **StackingInventory** | 自動スタック型 | 素材インベントリ |

---

## パフォーマンス

### 設計方針

| 指標 | 方針 |
|------|------|
| アロケーション | 操作ごとの new を最小化 |
| 検索 | Dictionary による O(1) ルックアップ |
| イベント | delegate invoke のオーバーヘッドのみ |

### SimpleInventoryの計算量

| 操作 | 計算量 |
|------|--------|
| `TryAdd` | O(1) |
| `TryRemove` | O(1) |
| `Get` | O(1) |
| `GetByDefinition` | O(n) |
| `GetAll` | O(n) |
| `Contains` | O(1) |
| `GetTotalStackCount` | O(n) |
| `RemoveWhere` | O(n) |
| `Clear` | O(n)（イベント発火のため）|

### 最適化のヒント

```csharp
// ❌ 毎フレームGetByDefinitionを呼ぶ
void Update()
{
    var potions = inventory.GetByDefinition(potionDefId);
    UpdatePotionCount(potions.Count());
}

// ✓ イベントで更新をキャッチ
int _potionCount;

void Start()
{
    inventory.OnItemAdded += e =>
    {
        if (e.Item.DefinitionId == potionDefId)
            _potionCount += e.Item.StackCount;
    };
    inventory.OnItemRemoved += e =>
    {
        if (e.Item.DefinitionId == potionDefId)
            _potionCount -= e.RemovedCount;
    };
    inventory.OnItemStackChanged += e =>
    {
        if (e.Item.DefinitionId == potionDefId)
            _potionCount += e.Delta;
    };
}
```

---

## 実践パターン集

### RPGキャラクターのインベントリ

```csharp
// インベントリ定義
public class CharacterInventorySystem
{
    public SimpleInventory<GameItem> Backpack { get; }
    public SlotInventory<Equipment> Equipment { get; }
    public SimpleInventory<GameItem> Hotbar { get; }

    public CharacterInventorySystem(int characterId)
    {
        Backpack = new SimpleInventory<GameItem>(
            new InventoryId(characterId * 100 + 1),
            30,  // capacity
            GameItem.Create);

        Equipment = new SlotInventory<Equipment>(
            new InventoryId(characterId * 100 + 2),
            6,  // slotCount: Head, Body, Hands, Legs, Feet, Accessory
            Equipment.Create);

        Hotbar = new SimpleInventory<GameItem>(
            new InventoryId(characterId * 100 + 3),
            8,  // capacity
            GameItem.Create);
    }
}
```

### ショップシステム

```csharp
public class ShopSystem
{
    private readonly TransferManager<GameItem> _transferManager;
    private readonly SimpleInventory<GameItem> _shopInventory;
    private readonly SimpleInventory<GameItem> _playerInventory;

    public ShopSystem(SimpleInventory<GameItem> playerInventory)
    {
        _playerInventory = playerInventory;
        _shopInventory = CreateShopInventory();
        _transferManager = new TransferManager<GameItem>();
    }

    public PurchaseResult Purchase(ItemInstanceId itemId, int count)
    {
        var item = _shopInventory.Get(itemId);
        if (item == null)
            return PurchaseResult.ItemNotFound;

        int totalCost = item.Price * count;
        if (!_playerInventory.CanAfford(totalCost))
            return PurchaseResult.InsufficientFunds;

        var context = new AddContext(AddSource.Purchase);
        var result = _transferManager.TryTransfer(
            _shopInventory,
            _playerInventory,
            itemId,
            count);

        if (!result.Success)
            return PurchaseResult.InventoryFull;

        _playerInventory.DeductGold(totalCost);
        return PurchaseResult.Success;
    }
}
```

### クラフトシステム

```csharp
public class CraftingSystem
{
    private readonly SnapshotManager<GameItem> _snapshotManager;

    public CraftingResult Craft(
        IInventory<GameItem> inventory,
        CraftingRecipe recipe)
    {
        // ロールバック用にスナップショット作成
        var snapshotId = _snapshotManager.CreateSnapshot(inventory);

        try
        {
            // 素材を消費
            foreach (var material in recipe.Materials)
            {
                int remaining = material.Count;
                foreach (var item in inventory.GetByDefinition(material.DefinitionId))
                {
                    int toRemove = Math.Min(item.StackCount, remaining);
                    var result = inventory.TryRemove(item.InstanceId, toRemove);
                    if (!result.Success)
                    {
                        // 失敗したらロールバック
                        _snapshotManager.TryRestoreSnapshot(snapshotId, inventory);
                        return CraftingResult.MaterialError;
                    }
                    remaining -= toRemove;
                    if (remaining <= 0) break;
                }
                if (remaining > 0)
                {
                    _snapshotManager.TryRestoreSnapshot(snapshotId, inventory);
                    return CraftingResult.InsufficientMaterials;
                }
            }

            // 成果物を追加
            var output = recipe.CreateOutput();
            var addResult = inventory.TryAdd(output, new AddContext(AddSource.Craft));
            if (!addResult.Success)
            {
                _snapshotManager.TryRestoreSnapshot(snapshotId, inventory);
                return CraftingResult.InventoryFull;
            }

            return CraftingResult.Success;
        }
        finally
        {
            _snapshotManager.TryRemoveSnapshot(snapshotId);
        }
    }
}
```

---

## トラブルシューティング

### アイテムが追加できない

**1. バリデーション結果を確認**
```csharp
var result = inventory.TryAdd(item);
if (!result.Success)
{
    Console.WriteLine($"Failed: {result.ValidationResult?.FailureReasons[0].Code}");
}
```

**2. よくある原因**

| 原因 | 対処 |
|------|------|
| `CapacityExceeded` | 容量を増やす or アイテムを削除 |
| `InvalidItemType` | バリデータの設定を確認 |
| `InvalidStackCount` | スタック数を確認 |

### イベントが発火しない

**1. イベント購読タイミングを確認**
```csharp
// ❌ アイテム追加後に購読
inventory.TryAdd(item);
inventory.OnItemAdded += handler;  // 遅い！

// ✓ 先に購読
inventory.OnItemAdded += handler;
inventory.TryAdd(item);
```

**2. AddUncheckedはイベントを発火する**
```csharp
// AddUncheckedでもイベントは発火する（バリデーションをスキップするだけ）
inventory.AddUnchecked(item);  // OnItemAddedが発火する
```

### スナップショットの復元後に不整合

**1. インスタンスIDの重複に注意**
```csharp
// スナップショット復元後、古いアイテム参照は無効になる
var item = inventory.Get(itemId);
inventory.RestoreFromSnapshot(snapshot);
// itemは復元前の参照。復元後は別のインスタンス
var restoredItem = inventory.Get(itemId);  // 復元後の正しい参照
```

### 転送が失敗する

**1. TransferResultを確認**
```csharp
var result = transferManager.TryTransfer(source, dest, itemId);
switch (result.FailureReason)
{
    case TransferFailureReason.SourceItemNotFound:
        Console.WriteLine("Source doesn't have the item");
        break;
    case TransferFailureReason.DestinationFull:
        Console.WriteLine("Destination is full");
        break;
    case TransferFailureReason.ValidationFailed:
        Console.WriteLine($"Validation: {result.ValidationResult?.FailureReasons[0]}");
        break;
}
```

**2. CanTransferで事前チェック**
```csharp
if (!transferManager.CanTransfer(source, dest, itemId))
{
    ShowMessage("Cannot transfer this item");
    return;
}
// 実際の転送
transferManager.TryTransfer(source, dest, itemId);
```

---

## トランザクション

複数のインベントリ操作をアトミックにコミットまたはロールバックする機能。
マルチスレッドやネットワーク同期などで操作が中断される可能性がある場合に有用。

### InventoryTransaction

低レベルのトランザクションAPI。`Begin()`/`Commit()`/`Rollback()` で明示的に制御。

```csharp
public sealed class InventoryTransaction<TItem> : IDisposable
{
    // トランザクション開始
    public static InventoryTransaction<TItem> Begin();
    public static InventoryTransaction<TItem> Begin(params IInventory<TItem>[] inventories);

    // インベントリ登録（スナップショット取得）
    public void Enlist(IInventory<TItem> inventory);

    // コミット（変更確定）
    public void Commit();

    // ロールバック（変更取消）
    public void Rollback();

    // プロパティ
    public bool IsCommitted { get; }
    public bool IsDisposed { get; }
}
```

**使用例:**

```csharp
// 複数インベントリのアトミック操作
using var transaction = InventoryTransaction<MyItem>.Begin(sourceInventory, destInventory);

try
{
    var removeResult = sourceInventory.TryRemove(itemId);
    if (!removeResult.Success)
    {
        // 自動ロールバック（Commitなしでusing終了）
        return;
    }

    var addResult = destInventory.TryAdd(removeResult.RemovedItem!);
    if (!addResult.Success)
    {
        // 自動ロールバック
        return;
    }

    // すべて成功したらコミット
    transaction.Commit();
}
catch
{
    // 例外時も自動ロールバック
    throw;
}
```

### TransactionScope

高レベルのトランザクションAPI。`Complete()` を呼ぶとコミット、呼ばずにDisposeするとロールバック。

```csharp
public sealed class TransactionScope<TItem> : IDisposable
{
    public TransactionScope(params IInventory<TItem>[] inventories);

    // 追加登録
    public void Enlist(IInventory<TItem> inventory);

    // 完了マーク（Dispose時にコミット）
    public void Complete();

    // 内部トランザクションへのアクセス
    public InventoryTransaction<TItem> Transaction { get; }
}
```

**使用例:**

```csharp
// usingスコープで自動管理
using (var scope = new TransactionScope<MyItem>(inventory1, inventory2))
{
    inventory1.TryRemove(item1.InstanceId);
    inventory2.TryRemove(item2.InstanceId);

    if (SomeCondition())
    {
        scope.Complete();  // Dispose時にコミット
    }
    // Complete()なしでスコープを抜けるとロールバック
}

// 例外発生時も自動ロールバック
using (var scope = new TransactionScope<MyItem>(inventory))
{
    inventory.TryRemove(itemId);
    ValidateOrThrow();  // 例外発生時はロールバック
    scope.Complete();
}
```

### 後からインベントリを登録

操作の途中で追加のインベントリをトランザクションに参加させることができる。

```csharp
using var transaction = InventoryTransaction<MyItem>.Begin();

// 最初のインベントリを登録
transaction.Enlist(inventory1);
inventory1.TryRemove(item1.InstanceId);

// 条件に応じて追加のインベントリを登録
if (transferToAnotherInventory)
{
    transaction.Enlist(inventory2);
    inventory2.TryAdd(item1);
}

transaction.Commit();
```

### 二重登録の扱い

同じインベントリを複数回 `Enlist()` しても、最初のスナップショットが維持される。

```csharp
using var transaction = InventoryTransaction<MyItem>.Begin(inventory);

// 操作1
inventory.TryRemove(item1.InstanceId);

// 操作2の前に再度登録（無視される）
inventory.TryAdd(new MyItem(2, "New Item"));
transaction.Enlist(inventory);  // 最初のスナップショットが維持

transaction.Rollback();

// inventory は最初の Enlist 時点の状態に戻る
```

### 注意事項

1. **Commitなしでの破棄は自動ロールバック**: 安全側に倒す設計
2. **Commit/Rollback後の操作は例外**: `InvalidOperationException` がスローされる
3. **スナップショットベース**: 内部的には `CreateSnapshot()` / `RestoreFromSnapshot()` を使用

---

## クラフトシステム

インベントリシステムに統合されたクラフト機能。レシピ管理、即時クラフト、再帰的計画、tick制クラフトをサポート。

### クラス構成

```
┌─────────────────┐
│  RecipeRegistry │ ← レシピの登録・検索
└────────┬────────┘
         │ 参照
         ▼
┌─────────────────┐     ┌─────────────────┐
│ CraftingManager │────▶│  IItemFactory   │ ← アイテム生成
└────────┬────────┘     └─────────────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌─────────────┐  ┌──────────────────┐
│ CraftingPlan│  │ TickBasedCrafter │
│   ner       │  │                  │
└─────────────┘  └──────────────────┘
  再帰的計画       tick制クラフト
```

### レシピ定義

```csharp
// ビルダーパターンでレシピを定義
var recipe = RecipeBuilder.Create(1)
    .Name("Iron Sword")
    .Ingredient(new ItemDefinitionId(1), 3)   // 鉄インゴット x3
    .Ingredient(new ItemDefinitionId(2), 1)   // 木材 x1
    .Output(new ItemDefinitionId(10), 1)      // 鉄の剣 x1
    .Ticks(20)                                // 20 tick所要
    .Tag("smithing", "weapon")                // タグ付け
    .Build();

// レジストリに登録
var registry = new RecipeRegistry();
registry.Register(recipe);

// レシピ検索
var weaponRecipes = registry.GetRecipesByTag("weapon");
var swordRecipes = registry.GetRecipesForOutput(new ItemDefinitionId(10));
```

### 即時クラフト（CraftingManager）

```csharp
// アイテムファクトリを用意
var factory = new DelegateItemFactory<MyItem>((defId, count) =>
    new MyItem(defId.Value, $"Item_{defId.Value}", count));

// マネージャー作成
var craftingManager = new CraftingManager<MyItem>(registry, factory);

// クラフト実行
var result = craftingManager.TryCraft(recipe, sourceInventory, outputInventory, count: 2);

// 結果確認
if (result.Success)
{
    foreach (var item in result.CreatedItems!)
    {
        Console.WriteLine($"Created: {item.DefinitionId}");
    }
}
else
{
    switch (result.FailureReason)
    {
        case CraftingFailureReason.InsufficientMaterials:
            foreach (var missing in result.MissingIngredients!)
            {
                Console.WriteLine($"Missing: {missing.DefinitionId} x{missing.Count}");
            }
            break;
        case CraftingFailureReason.OutputInventoryFull:
            Console.WriteLine("Output inventory is full");
            break;
    }
}
```

### 再帰的クラフト計画（CraftingPlanner）

材料が不足している場合、許可されたレシピから自動で中間素材を生産する計画を立てる。

```csharp
// 鉄の剣を作りたいが鉄インゴットがない場合：
// 1. 鉄鉱石→鉄インゴット（精錬）
// 2. 鉄インゴット→鉄の剣（鍛冶）
// の順でクラフトする計画を自動生成

var planner = new CraftingPlanner(
    registry,
    allowedRecipes: new[] { smeltingRecipeId, smithingRecipeId },
    maxDepth: 10);

var plan = planner.CreatePlan(swordRecipe, inventory);

if (plan.IsExecutable)
{
    Console.WriteLine($"Total steps: {plan.TotalSteps}");
    Console.WriteLine($"Total operations: {plan.TotalCraftOperations}");

    foreach (var step in plan.Steps)
    {
        Console.WriteLine($"  {step.Recipe.Name} x{step.Count}");
    }

    // 計画を実行
    var result = plan.TryExecute(craftingManager, inventory);
}
```

### Tick制クラフト（TickBasedCrafter）

工業系ゲーム向けの時間ベースクラフト。ジョブキューを管理し、tickごとに進行。

```csharp
var crafter = new TickBasedCrafter<MyItem>(craftingManager);

// ジョブをキューに追加
var job1 = crafter.Enqueue(smeltingRecipe, inventory, count: 5);
var job2 = crafter.Enqueue(smithingRecipe, inventory);

// イベント購読
crafter.OnJobStarted += e =>
    Console.WriteLine($"Started: {e.Job.Recipe.Name}");

crafter.OnJobProgress += e =>
    Console.WriteLine($"Progress: {e.CurrentTick}/{e.TotalTicks}");

crafter.OnJobCompleted += e =>
    Console.WriteLine($"Completed: {e.Job.Recipe.Name}");

crafter.OnJobFailed += e =>
    Console.WriteLine($"Failed: {e.Result.FailureReason}");

// ゲームループでtick
void Update()
{
    bool completed = crafter.Tick();
    if (completed)
    {
        PlayCompletionSound();
    }
}

// 状態確認
Console.WriteLine($"Busy: {crafter.IsBusy}");
Console.WriteLine($"Progress: {crafter.CurrentProgressRatio:P0}");
Console.WriteLine($"Queued: {crafter.QueuedJobCount}");

// キャンセル
crafter.CancelCurrent();  // 現在のジョブをキャンセル
crafter.ClearQueue();     // キューをクリア
crafter.ClearAll();       // 全てクリア
```

### 売買の抽象化

クラフトシステムで売買を抽象化できる。

```csharp
// 売却 = アイテム→ゴールドへのクラフト
var sellRecipe = RecipeBuilder.Create()
    .Name("Sell Iron Sword")
    .Ingredient(swordId, 1)
    .Output(goldId, 50)
    .Ticks(0)  // 即時
    .Build();

// 購入 = ゴールド→アイテムへのクラフト
var buyRecipe = RecipeBuilder.Create()
    .Name("Buy Potion")
    .Ingredient(goldId, 20)
    .Output(potionId, 1)
    .Build();

// 売却実行
craftingManager.TryCraft(sellRecipe, itemInventory, goldInventory);

// 購入実行
craftingManager.TryCraft(buyRecipe, goldInventory, itemInventory);
```

---

## ディレクトリ構造

```
InventorySystem/
├── README.md                           # クイックスタート
├── DESIGN.md                           # 本ドキュメント
│
├── InventorySystem.Core/
│   ├── InventorySystem.Core.csproj
│   │
│   ├── Identifiers/                    # 識別子
│   │   ├── InventoryId.cs
│   │   ├── ItemDefinitionId.cs
│   │   ├── ItemInstanceId.cs
│   │   └── SnapshotId.cs
│   │
│   ├── Interfaces/                     # インターフェース
│   │   ├── IInventoryItem.cs
│   │   ├── IInventory.cs
│   │   ├── IInventoryValidator.cs
│   │   ├── ISnapshotable.cs
│   │   ├── ITransferContext.cs
│   │   └── ItemDeserializerDelegate.cs
│   │
│   ├── Validation/                     # バリデーション
│   │   ├── IValidationResult.cs
│   │   ├── ValidationResult.cs
│   │   ├── ValidationFailureReason.cs
│   │   ├── AlwaysAllowValidator.cs
│   │   ├── CompositeValidator.cs
│   │   └── CapacityValidator.cs
│   │
│   ├── Results/                        # 結果型
│   │   ├── AddResult.cs
│   │   ├── RemoveResult.cs
│   │   └── TransferResult.cs
│   │
│   ├── Context/                        # コンテキスト
│   │   ├── AddContext.cs
│   │   └── TransferContext.cs
│   │
│   ├── Events/                         # イベント
│   │   ├── ItemAddedEvent.cs
│   │   ├── ItemRemovedEvent.cs
│   │   └── ItemStackChangedEvent.cs
│   │
│   ├── Snapshot/                       # スナップショット
│   │   ├── InventorySnapshot.cs
│   │   └── SnapshotManager.cs
│   │
│   ├── Transfer/                       # 転送
│   │   └── TransferManager.cs
│   │
│   ├── Transaction/                    # トランザクション
│   │   └── InventoryTransaction.cs
│   │
│   ├── Base/                           # 基底クラス
│   │   └── InventoryBase.cs
│   │
│   ├── Implementations/                # 参考実装
│   │   └── SimpleInventory.cs
│   │
│   └── Crafting/                       # クラフトシステム
│       ├── CraftingIngredient.cs       # 材料・出力定義
│       ├── ICraftingRecipe.cs          # レシピインターフェース
│       ├── SimpleRecipe.cs             # レシピ実装・ビルダー
│       ├── IRecipeRegistry.cs          # レジストリ
│       ├── CraftingResult.cs           # クラフト結果
│       ├── IItemFactory.cs             # アイテムファクトリ
│       ├── CraftingManager.cs          # 即時クラフト
│       ├── CraftingPlanner.cs          # 再帰的計画
│       └── TickBasedCrafter.cs         # tick制クラフト
│
└── InventorySystem.Tests/
    ├── InventorySystem.Tests.csproj
    ├── xunit.runner.json
    ├── TestItem.cs                     # テスト用アイテム
    ├── SimpleInventoryTests.cs
    ├── ValidationTests.cs
    ├── TransferTests.cs
    ├── SnapshotTests.cs
    ├── SerializationTests.cs
    ├── CraftingTests.cs                # クラフトテスト
    └── TransactionTests.cs             # トランザクションテスト
```
