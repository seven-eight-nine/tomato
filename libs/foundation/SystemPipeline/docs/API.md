# SystemPipeline API リファレンス

## 目次

- [Core インターフェース](#core-インターフェース)
- [システム実装](#システム実装)
- [属性](#属性)
- [クエリ](#クエリ)
- [ユーティリティ](#ユーティリティ)

---

## Core インターフェース

### ISystem

すべてのシステムの基底インターフェース。

```csharp
public interface ISystem
{
    bool IsEnabled { get; set; }
    IEntityQuery Query { get; }
}
```

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `IsEnabled` | `bool` | システムの有効/無効。`false`の場合、実行がスキップされる |
| `Query` | `IEntityQuery` | エンティティフィルタリング用クエリ。`null`の場合は全エンティティが対象。同一フレーム内で結果はキャッシュされる |

---

### ISerialSystem

直列処理システムのインターフェース。

```csharp
public interface ISerialSystem : ISystem
{
    void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<VoidHandle> entities,
        in SystemContext context);
}
```

| メソッド | 説明 |
|---------|------|
| `ProcessSerial` | エンティティを順番に処理する |

**パラメータ:**

| 名前 | 型 | 説明 |
|------|-----|------|
| `registry` | `IEntityRegistry` | エンティティレジストリ |
| `entities` | `IReadOnlyList<VoidHandle>` | 処理対象エンティティ |
| `context` | `in SystemContext` | 実行コンテキスト |

---

### IParallelSystem

並列処理システムのインターフェース。

```csharp
public interface IParallelSystem : ISystem
{
    void ProcessEntity(VoidHandle handle, in SystemContext context);
}
```

| メソッド | 説明 |
|---------|------|
| `ProcessEntity` | 単一エンティティを処理（並列実行される） |

**注意事項:**
- スレッドセーフな実装が必須
- 共有状態への書き込みは避ける
- 読み取り専用の操作が推奨

---

### IOrderedSerialSystem

順序制御付き直列処理システムのインターフェース。

```csharp
public interface IOrderedSerialSystem : ISerialSystem
{
    void OrderEntities(IReadOnlyList<VoidHandle> input, List<VoidHandle> output);
}
```

| メソッド | 説明 |
|---------|------|
| `OrderEntities` | エンティティの処理順序を決定 |

**使用例: 優先度ソート**

```csharp
public void OrderEntities(IReadOnlyList<VoidHandle> input, List<VoidHandle> output)
{
    var sorted = input.OrderByDescending(h => GetPriority(h));
    output.AddRange(sorted);
}
```

**使用例: トポロジカルソート**

```csharp
public void OrderEntities(IReadOnlyList<VoidHandle> input, List<VoidHandle> output)
{
    // 依存関係を考慮してソート
    foreach (var handle in TopologicalSort(input))
    {
        output.Add(handle);
    }
}
```

---

### IMessageQueueSystem

Wave処理システムのインターフェース。

```csharp
public interface IMessageQueueSystem : ISystem
{
    int MaxWaveDepth { get; }
    IMessageHandlerRegistry HandlerRegistry { get; }
    void ProcessWaves(IEntityRegistry registry, in SystemContext context);
}
```

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `MaxWaveDepth` | `int` | 最大Wave数（無限ループ防止） |
| `HandlerRegistry` | `IMessageHandlerRegistry` | メッセージハンドラ |

| メソッド | 説明 |
|---------|------|
| `ProcessWaves` | Wave単位でメッセージを処理 |

---

### IMessageHandlerRegistry

メッセージハンドラのレジストリインターフェース。

```csharp
public interface IMessageHandlerRegistry
{
    bool HasHandler<TMessage>() where TMessage : struct;
    void Handle<TMessage>(VoidHandle handle, in TMessage message, in SystemContext context)
        where TMessage : struct;
}
```

| メソッド | 説明 |
|---------|------|
| `HasHandler<T>` | 指定型のハンドラが存在するか |
| `Handle<T>` | メッセージを処理 |

---

### IEntityRegistry

エンティティレジストリインターフェース。

```csharp
public interface IEntityRegistry
{
    IReadOnlyList<VoidHandle> GetAllEntities();
    IReadOnlyList<VoidHandle> GetEntitiesOfType<TArena>() where TArena : class;
}
```

| メソッド | 説明 |
|---------|------|
| `GetAllEntities` | すべてのアクティブエンティティを取得 |
| `GetEntitiesOfType<T>` | 指定した型のエンティティを取得 |

---

## システム実装

### SystemContext

実行コンテキスト（構造体）。

```csharp
public readonly struct SystemContext
{
    public readonly float DeltaTime;
    public readonly float TotalTime;
    public readonly int FrameCount;
    public readonly CancellationToken CancellationToken;
    public readonly QueryCache QueryCache;
}
```

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `DeltaTime` | `float` | 前フレームからの経過時間（秒） |
| `TotalTime` | `float` | パイプライン開始からの累積時間（秒） |
| `FrameCount` | `int` | フレームカウント |
| `CancellationToken` | `CancellationToken` | キャンセル制御用トークン |
| `QueryCache` | `QueryCache` | クエリ結果キャッシュ。同一フレーム内の同一クエリは結果をキャッシュ |

---

### SystemGroup

システムのグループクラス。

```csharp
public sealed class SystemGroup
{
    public bool IsEnabled { get; set; }
    public int Count { get; }

    public SystemGroup(params ISystem[] systems);

    public void Execute(IEntityRegistry registry, in SystemContext context);
    public void Add(ISystem system);
    public void Insert(int index, ISystem system);
    public bool Remove(ISystem system);
    public ISystem this[int index] { get; }
}
```

| コンストラクタ | 説明 |
|---------------|------|
| `SystemGroup(params ISystem[])` | システム配列でグループを作成 |

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `IsEnabled` | `bool` | グループの有効/無効 |
| `Count` | `int` | システム数 |

| メソッド | 説明 |
|---------|------|
| `Execute` | グループ内の全システムを順番に実行 |
| `Add` | システムを末尾に追加 |
| `Insert` | 指定位置にシステムを挿入 |
| `Remove` | システムを削除 |

---

### Pipeline

パイプライン管理クラス。

```csharp
public sealed class Pipeline
{
    public float TotalTime { get; }
    public int FrameCount { get; }

    public Pipeline(IEntityRegistry registry);

    public void Execute(SystemGroup group, float deltaTime);
    public void Reset();
    public void Cancel();
}
```

| コンストラクタ | 説明 |
|---------------|------|
| `Pipeline(IEntityRegistry)` | レジストリを指定してパイプラインを作成 |

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `TotalTime` | `float` | 累積時間（秒） |
| `FrameCount` | `int` | フレームカウント |

| メソッド | 説明 |
|---------|------|
| `Execute` | SystemGroupを実行 |
| `Reset` | 時間とフレームカウントをリセット |
| `Cancel` | 実行中の処理をキャンセル |

---

### SystemExecutor

システム実行を担当する静的クラス。

```csharp
public static class SystemExecutor
{
    public static void Execute(ISystem system, IEntityRegistry registry, in SystemContext context);
}
```

| メソッド | 説明 |
|---------|------|
| `Execute` | システムの型に応じた適切な実行メソッドを呼び出す |

**実行フロー:**
1. `IMessageQueueSystem` → `ProcessWaves`
2. `IOrderedSerialSystem` → `OrderEntities` + `ProcessSerial`
3. `ISerialSystem` → `ProcessSerial`
4. `IParallelSystem` → `Parallel.For` + `ProcessEntity`

---

### MessageQueue

エンティティごとのメッセージキュー（構造体）。

```csharp
public struct MessageQueue
{
    public bool HasMessages { get; }
    public int Count { get; }

    public void Enqueue<TMessage>(in TMessage message) where TMessage : struct;
    public void ProcessMessages(VoidHandle handle, IMessageHandlerRegistry registry, in SystemContext context);
    public void Clear();
}
```

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `HasMessages` | `bool` | キューにメッセージがあるか |
| `Count` | `int` | メッセージ数 |

| メソッド | 説明 |
|---------|------|
| `Enqueue<T>` | メッセージを追加 |
| `ProcessMessages` | 全メッセージを処理（処理後クリア） |
| `Clear` | キューをクリア |

---

### VoidHandle

エンティティハンドル（構造体）。

```csharp
public readonly struct VoidHandle
{
    public object Arena { get; }
    public int Index { get; }
    public int Generation { get; }
    public bool IsValid { get; }

    public static VoidHandle Invalid { get; }

    public VoidHandle(object arena, int index, int generation);
}
```

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Arena` | `object` | Arenaへの参照 |
| `Index` | `int` | エンティティインデックス |
| `Generation` | `int` | 世代番号 |
| `IsValid` | `bool` | ハンドルが有効か |

---

## 属性

### MessageQueueAttribute

構造体をMessageQueueとしてマーク。Source Generatorがシステム実装を自動生成。

```csharp
[AttributeUsage(AttributeTargets.Struct)]
public class MessageQueueAttribute : Attribute
{
    public int MaxWaveDepth { get; set; } = 100;
}
```

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|----------|------|
| `MaxWaveDepth` | `int` | `100` | 最大Wave深度 |

**使用例:**

```csharp
[MessageQueue]
public partial struct DamageQueue { }

[MessageQueue(MaxWaveDepth = 50)]
public partial struct EventQueue { }
```

**生成されるコード:**
- `{TypeName}System : IMessageQueueSystem`

---

### EntityMessageQueueAttribute

エンティティにMessageQueueを追加。EntityHandleSystem.Generatorと連携。
この属性は`EntityHandleSystem`名前空間で定義されています。

```csharp
// EntityHandleSystem.Attributes で定義
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class EntityMessageQueueAttribute : Attribute
{
    public Type QueueType { get; }
    public EntityMessageQueueAttribute(Type queueType);
}
```

**使用例:**

```csharp
using EntityHandleSystem;

[Entity]
[EntityMessageQueue(typeof(DamageQueue))]
[EntityMessageQueue(typeof(EventQueue))]
public partial class Player { }
```

**生成されるコード:**
- Arena側: `MessageQueue[] _{queueName}Queues` 配列
- Handle側: `{QueueName}` プロパティ（例: `playerHandle.DamageQueue`）
- Arena側: `IHasQueue<TQueue>` インターフェース実装

---

## クエリ

### IEntityQuery

エンティティフィルタリング用インターフェース。

```csharp
public interface IEntityQuery
{
    IEnumerable<VoidHandle> Filter(
        IEntityRegistry registry,
        IEnumerable<VoidHandle> entities);
}
```

---

### ActiveEntityQuery

アクティブなエンティティのみをフィルタリング。

```csharp
public sealed class ActiveEntityQuery : IEntityQuery
{
    public static readonly ActiveEntityQuery Instance;
}
```

**使用例:**

```csharp
var active = ActiveEntityQuery.Instance.Filter(registry, entities);
```

---

### HasComponentQuery\<TComponent\>

特定のコンポーネントを持つエンティティをフィルタリング。

```csharp
public sealed class HasComponentQuery<TComponent> : IEntityQuery
{
    public HasComponentQuery(Func<VoidHandle, bool> hasComponentCheck);
}
```

**使用例:**

```csharp
var query = new HasComponentQuery<Health>(h => HasHealth(h));
var withHealth = query.Filter(registry, entities);
```

---

### CompositeQuery

複数のクエリを組み合わせ（AND条件）。

```csharp
public sealed class CompositeQuery : IEntityQuery
{
    public CompositeQuery(params IEntityQuery[] queries);
}
```

**使用例:**

```csharp
var query = new CompositeQuery(
    ActiveEntityQuery.Instance,
    new HasComponentQuery<Health>(h => HasHealth(h))
);
var filtered = query.Filter(registry, entities);
```

---

## ユーティリティ

### IHasQueue\<TQueue\>

キューを持つArenaを表すインターフェース。EntityHandleSystem.Generatorが実装を生成。

```csharp
public interface IHasQueue<TQueue> where TQueue : struct
{
    ref MessageQueue GetQueue(int index);
}
```

このインターフェースは、MessageQueueSystemで特定のキュー型を持つArenaを検索するために使用されます。

---

### Queue プロパティアクセス

`[EntityMessageQueue]`属性を使用すると、Handle上にキュープロパティが生成されます:

```csharp
// 生成されるプロパティ
public ref MessageQueue DamageQueue
{
    get { return ref _arena._damageQueueQueues[_index]; }
}
```

**使用例:**

```csharp
ref var queue = ref playerHandle.DamageQueue;
queue.Enqueue(new DamageMessage { Amount = 50 });
```

---

## エラーハンドリング

### SystemExecutor.Execute

不明なシステム型の場合、`InvalidOperationException`をスロー:

```
Unknown system type: {TypeName}. System must implement ISerialSystem, IParallelSystem, or IMessageQueueSystem.
```

### IMessageQueueSystem.ProcessWaves

最大Wave深度を超えた場合、`InvalidOperationException`をスロー:

```
Maximum wave depth ({MaxWaveDepth}) exceeded. Possible infinite loop.
```
