# SystemPipeline

ECS (Entity Component System) スタイルのシステムパイプラインフレームワーク。ゲームループにおけるフェーズ処理を抽象化し、3種類の処理パターン（Serial, Parallel, MessageQueue）をサポートします。

## 特徴

- **3種類の処理パターン**: Serial（直列）、Parallel（並列）、MessageQueue（Step処理）
- **Source Generator対応**: `[CommandQueue]`属性でシステム実装を自動生成
- **EntityHandleSystem連携**: `[HasCommandQueue(typeof(T))]`属性でエンティティにキューを追加
- **シンプルなAPI**: SerialSystemGroup/ParallelSystemGroupで構造を定義し、Pipelineで実行
- **並列実行対応**: 直列/並列グループを入れ子にして組み合わせ可能
- **型安全**: コンパイル時の型チェックによる安全なキューアクセス

## インストール

プロジェクト参照を追加:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/SystemPipeline.Attributes/SystemPipeline.Attributes.csproj" />
  <ProjectReference Include="path/to/SystemPipeline.Core/SystemPipeline.Core.csproj" />
  <ProjectReference Include="path/to/SystemPipeline.Generator/SystemPipeline.Generator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
</ItemGroup>
```

## クイックスタート

### 1. システムの定義

```csharp
// 直列処理システム（Reconciliation, Cleanupなど）
public class ReconciliationSystem : ISerialSystem
{
    public bool IsEnabled { get; set; } = true;
    public IEntityQuery Query => null;  // nullの場合は全エンティティ対象

    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context)
    {
        foreach (var handle in entities)
        {
            // エンティティを順番に処理
        }
    }
}

// 並列処理システム（Decision, Executionなど）
public class DecisionSystem : IParallelSystem
{
    public bool IsEnabled { get; set; } = true;
    public IEntityQuery Query => ActiveEntityQuery.Instance;  // アクティブエンティティのみ

    public void ProcessEntity(AnyHandle handle, in SystemContext context)
    {
        // 各エンティティを並列に処理（スレッドセーフに実装）
    }
}
```

### 2. CommandQueueの定義（Source Generator使用）

```csharp
using CommandGenerator;

// キューの定義（Source Generatorが〇〇QueueSystemを自動生成）
[CommandQueue]
public partial class GameCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(AnyHandle handle);
}

// Commandの定義
[Command<GameCommandQueue>(Priority = 50)]
public partial class DamageCommand
{
    public int Amount;

    public void ExecuteCommand(AnyHandle handle)
    {
        // ダメージ処理
    }
}
```

### 3. エンティティへのキュー追加

```csharp
using EntityHandleSystem;
using CommandGenerator;

[Entity]
[HasCommandQueue(typeof(GameCommandQueue))]
public partial class Player
{
    public int Health;
    public float Speed;
}
```

### 4. パイプラインの構築と実行

```csharp
// システムを作成
var collision = new CollisionSystem();
var messageSystem = new GameCommandQueueSystem(handlerRegistry);  // 自動生成
var decision = new DecisionSystem();
var execution = new ExecutionSystem();
var reconciliation = new ReconciliationSystem();
var cleanup = new CleanupSystem();

// SerialSystemGroupで直列実行グループを定義（実行順序は配列順）
var updateGroup = new SerialSystemGroup(
    collision,
    messageSystem,
    decision,
    execution
);

var lateUpdateGroup = new SerialSystemGroup(
    reconciliation,
    cleanup
);

// ParallelSystemGroupで並列実行も可能
var parallelGroup = new ParallelSystemGroup(
    new AIDecisionSystem(),
    new AnimationSystem(),
    new AudioSystem()
);

// グループは入れ子にできる
var mainLoop = new SerialSystemGroup(
    new InputSystem(),
    parallelGroup,  // 並列実行
    new PhysicsSystem()
);

// パイプライン作成
var registry = new GameEntityRegistry();
var pipeline = new Pipeline(registry);

// ゲームループで実行（tickベース）
void Tick(int deltaTicks)
{
    pipeline.Execute(updateGroup, deltaTicks);
}

void LateTick(int deltaTicks)
{
    pipeline.Execute(lateUpdateGroup, deltaTicks);
}
```

## 処理パターン

### Serial（直列処理）

全エンティティを順番に処理します。状態の整合性が重要な場合に使用。

```csharp
public interface ISystem
{
    bool IsEnabled { get; set; }
    IEntityQuery Query { get; }  // エンティティフィルタリング用クエリ
}

public interface ISerialSystem : ISystem
{
    void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context);
}
```

**用途例**: Reconciliation, Cleanup, 依存関係のある処理

### Parallel（並列処理）

各エンティティを独立して並列に処理します。読み取り専用や独立した処理に最適。

```csharp
public interface IParallelSystem : ISystem
{
    void ProcessEntity(AnyHandle handle, in SystemContext context);
}
```

**用途例**: Decision（AI判断）, Physics計算, Rendering準備

**注意**: スレッドセーフな実装が必要です。共有状態への書き込みは避けてください。

### MessageQueue（Step処理）

メッセージキューをStep単位で処理します。メッセージ処理中に新たなメッセージが追加されると次のStepで処理されます。

```csharp
public interface IMessageQueueSystem : ISystem
{
    void ProcessMessages(IEntityRegistry registry, in SystemContext context);
}
```

**用途例**: イベント処理, ダメージ計算, 連鎖反応

## 順序付き直列処理

エンティティの処理順序をカスタマイズする場合は`IOrderedSerialSystem`を使用:

```csharp
public class PrioritySystem : IOrderedSerialSystem
{
    public bool IsEnabled { get; set; } = true;

    public void OrderEntities(IReadOnlyList<AnyHandle> input, List<AnyHandle> output)
    {
        // 優先度順にソート
        var sorted = input.OrderByDescending(h => GetPriority(h));
        output.AddRange(sorted);
    }

    public void ProcessSerial(
        IEntityRegistry registry,
        IReadOnlyList<AnyHandle> entities,
        in SystemContext context)
    {
        // 順序付けられたエンティティを処理
    }
}
```

## エンティティクエリ

特定の条件でエンティティをフィルタリング:

```csharp
using SystemPipeline.Query;

// アクティブなエンティティのみ
var activeQuery = ActiveEntityQuery.Instance;

// 特定のコンポーネントを持つエンティティ
var healthQuery = new HasComponentQuery<Health>(
    handle => HasHealthComponent(handle));

// 複合クエリ（AND条件）
var compositeQuery = new CompositeQuery(
    ActiveEntityQuery.Instance,
    healthQuery
);

var filtered = compositeQuery.Filter(registry, entities);
```

## SystemContext

各システムに渡される実行コンテキスト（tickベース）:

```csharp
public readonly struct SystemContext
{
    public readonly int DeltaTicks;           // 前フレームからの経過tick数
    public readonly GameTick CurrentTick;     // 現在のtick（累積）
    public readonly CancellationToken CancellationToken;  // キャンセル制御
}
```

tick単位はアプリケーション側で定義します（例: 1ms, 1/60秒など）。一度に複数tick進む場合もあります（例: `Execute(group, 16)`）。

## CommandQueueの使用

### コマンドの送信

```csharp
// エンティティのキューにコマンドを追加（プロパティベースアクセス）
// 各Entityが独自のキューを持つ
playerHandle.GameCommandQueue.Enqueue<DamageCommand>(cmd => {
    cmd.Amount = 50;
});
```

### メッセージハンドラの実装

```csharp
public class GameMessageHandlerRegistry : IMessageHandlerRegistry
{
    private readonly Dictionary<Type, object> _handlers = new();

    public void RegisterHandler<TMessage>(Action<AnyHandle, TMessage, SystemContext> handler)
        where TMessage : struct
    {
        _handlers[typeof(TMessage)] = handler;
    }

    public bool HasHandler<TMessage>() where TMessage : struct
    {
        return _handlers.ContainsKey(typeof(TMessage));
    }

    public void Handle<TMessage>(AnyHandle handle, in TMessage message, in SystemContext context)
        where TMessage : struct
    {
        if (_handlers.TryGetValue(typeof(TMessage), out var handler))
        {
            ((Action<AnyHandle, TMessage, SystemContext>)handler)(handle, message, context);
        }
    }
}
```

## プロジェクト構成

```
SystemPipeline/
├── SystemPipeline.Attributes/    # 属性定義
│   └── MessageQueueAttribute.cs
├── SystemPipeline.Core/          # ランタイム
│   ├── ISystem.cs               # Queryプロパティ含む
│   ├── ISerialSystem.cs
│   ├── IParallelSystem.cs
│   ├── IMessageQueueSystem.cs
│   ├── SystemContext.cs         # QueryCache含む
│   ├── SystemExecutor.cs
│   ├── IExecutable.cs           # 実行可能要素インターフェース
│   ├── ISystemGroup.cs          # グループインターフェース
│   ├── SerialSystemGroup.cs     # 直列実行グループ
│   ├── ParallelSystemGroup.cs   # 並列実行グループ
│   ├── Pipeline.cs
│   ├── IEntityRegistry.cs
│   ├── IHasQueue.cs
│   ├── MessageQueue.cs
│   └── Query/
│       ├── IEntityQuery.cs
│       ├── QueryCache.cs        # フレーム内クエリキャッシュ
│       ├── ActiveEntityQuery.cs
│       ├── HasComponentQuery.cs
│       └── CompositeQuery.cs
├── SystemPipeline.Generator/     # Source Generator
│   └── MessageQueueGenerator.cs
└── SystemPipeline.Tests/         # テスト
```

## Unity連携例

```csharp
public class GameBootstrap : MonoBehaviour
{
    private Pipeline _pipeline;
    private ISystemGroup _updateGroup;
    private ISystemGroup _fixedUpdateGroup;
    private ISystemGroup _lateUpdateGroup;

    void Awake()
    {
        var registry = new GameEntityRegistry();
        var handlerRegistry = new GameMessageHandlerRegistry();

        // 並列実行可能なシステムをグループ化
        var parallelAI = new ParallelSystemGroup(
            new AIDecisionSystem(),
            new AnimationSystem()
        );

        // SerialSystemGroup で直列実行（順序が重要な処理）
        _updateGroup = new SerialSystemGroup(
            new InputSystem(),
            new UpdateBeginQueueSystem(handlerRegistry),
            parallelAI,  // 入れ子で並列実行
            new MovementSystem()
        );

        _fixedUpdateGroup = new SerialSystemGroup(
            new PhysicsSystem(),
            new CollisionSystem(),
            new DamageQueueSystem(handlerRegistry)
        );

        _lateUpdateGroup = new SerialSystemGroup(
            new ReconciliationSystem(),
            new CleanupSystem()
        );

        _pipeline = new Pipeline(registry);
    }

    // tickベースで実行（deltaTicks = 経過tick数）
    void Tick(int deltaTicks)
    {
        _pipeline.Execute(_updateGroup, deltaTicks);
        _pipeline.Execute(_fixedUpdateGroup, deltaTicks);
    }

    void LateTick(int deltaTicks)
    {
        _pipeline.Execute(_lateUpdateGroup, deltaTicks);
    }
}
```

## ライセンス

MIT License
