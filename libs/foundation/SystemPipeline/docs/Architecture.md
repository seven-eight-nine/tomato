# SystemPipeline アーキテクチャ

## 概要

SystemPipelineは、ゲームループにおけるフェーズ処理を抽象化するECSスタイルのフレームワークです。

```
┌─────────────────────────────────────────────────────────────┐
│                        Game Loop                             │
├──────────────────────┬──────────────────────────────────────┤
│      Update()        │           LateUpdate()               │
├──────────────────────┼──────────────────────────────────────┤
│    UpdateGroup       │         LateUpdateGroup              │
│  ┌────────────────┐  │  ┌────────────────────────────────┐  │
│  │ CollisionSystem│  │  │    ReconciliationSystem        │  │
│  ├────────────────┤  │  ├────────────────────────────────┤  │
│  │ MessageSystem  │  │  │    CleanupSystem               │  │
│  ├────────────────┤  │  └────────────────────────────────┘  │
│  │ DecisionSystem │  │                                      │
│  ├────────────────┤  │                                      │
│  │ExecutionSystem │  │                                      │
│  └────────────────┘  │                                      │
└──────────────────────┴──────────────────────────────────────┘
```

## 設計原則

### 1. 単一責任原則

各システムは1つの責務のみを持つ：

- **CollisionSystem**: 衝突検出のみ
- **DamageSystem**: ダメージ計算のみ
- **MovementSystem**: 移動処理のみ

### 2. 型パラメータなし

汎用的なECSスタイルを採用。`AnyHandle`を通じてエンティティにアクセス：

```csharp
// 型安全なアクセスはArenaを通じて行う
var player = playerArena.TryGet(handle);
```

### 3. 手動配列定義

SerialSystemGroup/ParallelSystemGroupは配列で明示的に定義。実行順序が明確：

```csharp
// 直列実行: collision → damage → movement の順
var serialGroup = new SerialSystemGroup(
    collision,    // 1番目に実行
    damage,       // 2番目に実行
    movement      // 3番目に実行
);

// 並列実行: 同時に実行される可能性あり
var parallelGroup = new ParallelSystemGroup(
    aiSystem,
    animSystem,
    audioSystem
);

// 入れ子で組み合わせ可能
var mainLoop = new SerialSystemGroup(
    inputSystem,
    parallelGroup,  // ここで並列実行
    physicsSystem
);
```

マルチスレッドのタイムライン管理は、結局のところ2次元レイアウトと同じ問題になる。SerialSystemGroupが時間軸方向（横）、ParallelSystemGroupがスレッド方向（縦）を担当し、入れ子にすることで複雑な並行処理も宣言的に書ける。

### 4. Before/Afterフックなし

シンプルなインターフェースを維持。必要な場合は別システムとして追加：

```csharp
// フック代わりに専用システムを追加
var group = new SerialSystemGroup(
    prePhysicsSystem,    // "Before" の代わり
    physicsSystem,
    postPhysicsSystem    // "After" の代わり
);
```

## 処理フロー

### Serial System

```
┌─────────────────────────────────────────────────────────────┐
│                    SerialSystem.ProcessSerial                │
├─────────────────────────────────────────────────────────────┤
│   Entity[0] ─→ Entity[1] ─→ Entity[2] ─→ ... ─→ Entity[N]  │
│      ↓            ↓            ↓                   ↓        │
│   Process      Process      Process            Process      │
│   (順番に)     (順番に)     (順番に)           (順番に)    │
└─────────────────────────────────────────────────────────────┘
```

### Parallel System

```
┌─────────────────────────────────────────────────────────────┐
│                  ParallelSystem.ProcessEntity                │
├─────────────────────────────────────────────────────────────┤
│   Entity[0]     Entity[1]     Entity[2]     ...  Entity[N]  │
│      │             │             │                   │       │
│      ▼             ▼             ▼                   ▼       │
│   Process       Process       Process            Process    │
│   Thread 1      Thread 2      Thread 3          Thread M    │
│   (並列実行)                                                 │
└─────────────────────────────────────────────────────────────┘
```

### MessageQueue System (Step Processing)

```
┌─────────────────────────────────────────────────────────────┐
│              MessageQueueSystem.ProcessSteps                 │
├─────────────────────────────────────────────────────────────┤
│  Step 1:                                                     │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Entity[0].Queue → Process → (新メッセージ追加可能)   │    │
│  │ Entity[1].Queue → Process → (新メッセージ追加可能)   │    │
│  │ Entity[2].Queue → Process → (新メッセージ追加可能)   │    │
│  └─────────────────────────────────────────────────────┘    │
│                          ↓                                   │
│  Step 2: (Step 1で追加されたメッセージを処理)               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Entity[0].Queue → Process                            │    │
│  │ Entity[1].Queue → Process                            │    │
│  └─────────────────────────────────────────────────────┘    │
│                          ↓                                   │
│  Step N: (キューが空になるまで繰り返し)                      │
└─────────────────────────────────────────────────────────────┘
```

## コンポーネント構成

### プロジェクト依存関係

```
┌──────────────────────────┐
│  SystemPipeline.Tests    │
└────────────┬─────────────┘
             │ references
             ▼
┌──────────────────────────┐     ┌──────────────────────────┐
│  SystemPipeline.Core     │◄────│ SystemPipeline.Generator │
└────────────┬─────────────┘     └────────────┬─────────────┘
             │ references                      │ references
             ▼                                 ▼
┌──────────────────────────────────────────────────────────────┐
│               SystemPipeline.Attributes                       │
└──────────────────────────────────────────────────────────────┘
```

### クラス関係

```
                    ISystem
                       │
        ┌──────────────┼──────────────┐
        │              │              │
        ▼              ▼              ▼
  ISerialSystem  IParallelSystem  IMessageQueueSystem
        │
        ▼
  IOrderedSerialSystem


                  IExecutable
                       │
        ┌──────────────┴──────────────┐
        │                              │
        ▼                              ▼
     ISystem                    ISystemGroup
                                      │
                        ┌─────────────┴─────────────┐
                        │                           │
                        ▼                           ▼
               SerialSystemGroup           ParallelSystemGroup
                        │                           │
                        └────execute────▶ SystemExecutor
                                               │
                                               └────▶ Registry
                                                         │
                                                         ▼
                                                    AnyHandle[]
```

## Source Generator

### MessageQueueAttribute → System生成

```
入力:
┌────────────────────────────────┐
│ [MessageQueue]                 │
│ public partial struct DamageQ {}│
└────────────────────────────────┘
                │
                ▼ Source Generator
                │
出力:
┌────────────────────────────────────────────────────┐
│ public sealed class DamageQSystem : IMessageQueueSystem │
│ {                                                   │
│     public void ProcessSteps(...) { ... }          │
│ }                                                   │
└────────────────────────────────────────────────────┘
```

### HasQueueAttribute → Arena/Handle拡張

```
入力:
┌────────────────────────────────┐
│ [Entity]                       │
│ [HasQueue<DamageQ>]           │
│ public partial class Player {}│
└────────────────────────────────┘
                │
                ▼ EntityGenerator (拡張)
                │
出力:
┌────────────────────────────────────────────────────┐
│ public class PlayerArena : IHasQueue<DamageQ>      │
│ {                                                   │
│     internal MessageQueue[] _damageQQueues;        │
│     ref MessageQueue IHasQueue<DamageQ>.GetQueue(int i)│
│     { return ref _damageQQueues[i]; }              │
│ }                                                   │
│                                                     │
│ public struct PlayerHandle                          │
│ {                                                   │
│     public ref MessageQueue GetQueue<TQueue>()     │
│     { ... }                                         │
│ }                                                   │
└────────────────────────────────────────────────────┘
```

## パフォーマンス考慮事項

### 並列実行

- `IParallelSystem`は`Parallel.For`で実行
- エンティティ間の依存がない処理に適用
- スレッドセーフな実装が必須

### メモリ効率

- `SystemContext`は構造体（スタック割り当て）
- `AnyHandle`は構造体（ボクシング回避）
- `MessageQueue`は`List<object>`で動的拡張

### キャンセル処理

- `CancellationToken`をコンテキスト経由で伝播
- 長時間処理で定期的にチェック推奨

## 拡張ポイント

### カスタムEntityRegistry

```csharp
public class GameEntityRegistry : IEntityRegistry
{
    private readonly Dictionary<Type, List<AnyHandle>> _byType;

    public IReadOnlyList<AnyHandle> GetEntitiesOfType<TArena>()
        where TArena : class
    {
        return _byType[typeof(TArena)];
    }
}
```

### カスタムMessageHandlerRegistry

```csharp
public class TypedHandlerRegistry : IMessageHandlerRegistry
{
    private readonly Dictionary<Type, Delegate> _handlers;

    public void Register<T>(Action<AnyHandle, T, SystemContext> handler)
        where T : struct
    {
        _handlers[typeof(T)] = handler;
    }
}
```

### カスタムQuery

```csharp
public class DistanceQuery : IEntityQuery
{
    private readonly Vector3 _center;
    private readonly float _radius;

    public IEnumerable<AnyHandle> Filter(
        IEntityRegistry registry,
        IEnumerable<AnyHandle> entities)
    {
        return entities.Where(e => GetDistance(e, _center) <= _radius);
    }
}
```

## 典型的な使用パターン

### ゲームループ統合 (Unity)

```csharp
public class GameManager : MonoBehaviour
{
    private Pipeline _pipeline;
    private ISystemGroup _update, _fixedUpdate, _lateUpdate;

    void Awake()
    {
        _pipeline = new Pipeline(CreateRegistry());

        // 並列実行可能な処理をグループ化
        var parallelAI = new ParallelSystemGroup(
            new AIDecisionSystem(),
            new AnimationSystem()
        );

        // 直列実行グループに入れ子で組み込む
        _update = new SerialSystemGroup(
            new InputSystem(),
            parallelAI,
            new MovementSystem()
        );

        _fixedUpdate = new SerialSystemGroup(
            new PhysicsSystem(),
            new CollisionSystem()
        );

        _lateUpdate = new SerialSystemGroup(
            new ReconciliationSystem(),
            new CleanupSystem()
        );
    }

    void Update() => _pipeline.Execute(_update, Time.deltaTime);
    void FixedUpdate() => _pipeline.Execute(_fixedUpdate, Time.fixedDeltaTime);
    void LateUpdate() => _pipeline.Execute(_lateUpdate, Time.deltaTime);
}
```

### シーン遷移時のリセット

```csharp
void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    _pipeline.Reset();  // 時間とフレームカウントをリセット
}
```

### 条件付きシステム実行

```csharp
void Update()
{
    // 一時停止中は特定システムを無効化
    _aiSystem.IsEnabled = !isPaused;
    _physicsSystem.IsEnabled = !isPaused;

    _pipeline.Execute(_updateGroup, Time.deltaTime);
}
```
