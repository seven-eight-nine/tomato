# GameLoop

action-game-design.mdで定義された6フェーズゲームループを実現する統合システム。
全てのシステム（EntityHandle, CommandGenerator, ActionSelector, ActionExecution, Collision, CharacterSpawn）を連携させる。

## 概要

GameLoopは以下の責務を持つ:

1. **Entity単位のコンテキスト管理** - ActionStateMachineをEntityに紐付け
2. **6フェーズゲームループの統括** - Collision→Message→Decision→Execution→Reconciliation→Cleanup
3. **CharacterSpawnSystemとの連携** - スポーン/デスポーンイベントをEntityContextに橋渡し
4. **CommandGeneratorとの連携** - MessageHandlerQueueとWaveProcessorによるメッセージ処理

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Pipeline + SystemGroup                          │
├─────────────────────────────────────────────────────────────────────┤
│  UpdateGroup:                                                       │
│    1. CollisionSystem      - 外部衝突結果をメッセージ化             │
│    2. MessageSystem        - WaveProcessorでメッセージ処理          │
│    3. DecisionSystem       - アクション選択（読み取り専用）         │
│    4. ExecutionSystem      - アクション実行                         │
│                                                                     │
│  LateUpdateGroup:                                                   │
│    5. ReconciliationSystem - 位置調停（依存順）                     │
│    6. CleanupSystem        - 消滅処理                               │
└─────────────────────────────────────────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        ▼                       ▼                       ▼
┌─────────────────┐   ┌─────────────────────┐   ┌─────────────────┐
│ EntityContext   │   │ MessageHandlerQueue │   │ WaveProcessor   │
│ Registry        │   │ [CommandQueue]      │   │                 │
│                 │   │                     │   │ Wave処理        │
│ Entity管理      │   │ ゲーム固有コマンド  │   │ 収束まで実行    │
└─────────────────┘   └─────────────────────┘   └─────────────────┘
```

## コンポーネント詳細

### Context/

Entity単位のコンテキスト管理。

| クラス | 責務 |
|--------|------|
| `EntityContext<TCategory>` | Entity単位のコンテキスト。ActionStateMachineを保持 |
| `EntityContextRegistry<TCategory>` | コンテキスト管理。IEntityRegistryを実装 |

```csharp
// EntityContext の構成
public sealed class EntityContext<TCategory>
{
    public AnyHandle Handle { get; }
    public ActionStateMachine<TCategory> ActionStateMachine { get; }
    public IActionJudgment<TCategory, InputState, GameState>[] Judgments { get; set; }
    public CharacterSpawnController? SpawnController { get; set; }
    public bool IsActive { get; set; }
    public bool IsMarkedForDeletion { get; }
}
```

### Collision/

外部からの衝突結果を受け取り、メッセージシステムに伝達する薄いレイヤー。

**重要**: 衝突検出自体はGameLoop外部（CollisionSystemを使用したゲームコード）で行う。
GameLoopは衝突結果の受け取りとメッセージ化のみを担当する。

| クラス | 責務 |
|--------|------|
| `CollisionPair` | 衝突ペア（EntityIdA, EntityIdB, Point, Normal） |
| `ICollisionSource` | 衝突結果の取得元（ゲーム側で実装） |

```csharp
// CollisionPairの構造
public readonly struct CollisionPair
{
    public readonly int EntityIdA;
    public readonly int EntityIdB;
    public readonly Vector3 Point;   // 接触点
    public readonly Vector3 Normal;  // 接触法線
}

// ICollisionSourceはゲーム側で実装
public interface ICollisionSource
{
    IReadOnlyList<CollisionPair> GetCollisions();
    void Clear();
}
```

### Phases/

6つのフェーズシステム。すべてSystemPipelineのISystem/ISerialSystem/IParallelSystemを実装。

| フェーズ | クラス | 責務 |
|----------|--------|------|
| 1. Collision | `CollisionSystem` | ICollisionSourceから衝突結果を取得、ICollisionMessageEmitterでコマンド発行 |
| 2. Message | `MessageSystem` | WaveProcessorでMessageHandlerQueue処理 |
| 3. Decision | `DecisionSystem<TCategory>` | ActionSelectorでアクション選択【読み取り専用】【並列可】 |
| 4. Execution | `ExecutionSystem<TCategory>` | ActionStateMachineでアクション実行 |
| 5. Reconciliation | `ReconciliationSystem` | 依存順計算、位置調停 |
| 6. Cleanup | `CleanupSystem<TCategory>` | 削除マーク済みEntityの削除 |

### Spawn/

CharacterSpawnSystemとの連携。

| クラス | 責務 |
|--------|------|
| `SpawnBridge<TCategory>` | CharacterSpawnControllerのStateChangedを監視し、Entity登録/削除 |
| `ISpawnCompletionHandler` | スポーン完了通知のインターフェース |
| `IEntityInitializer<TCategory>` | Entity初期化ロジック（ゲーム側で実装） |
| `IEntitySpawner` | Spawn/Despawnインターフェース |

### Providers/

外部依存の注入ポイント。

| インターフェース | 責務 |
|------------------|------|
| `IInputProvider` | Entity用入力状態の取得 |
| `ICharacterStateProvider` | キャラクター状態の取得 |
| `ICollisionMessageEmitter` | 衝突結果からコマンドを発行 |
| `IActionFactory<TCategory>` | アクションID→IExecutableAction変換 |

## 使用例

### 基本セットアップ

```csharp
using Tomato.GameLoop;
using Tomato.GameLoop.Context;
using Tomato.GameLoop.Collision;
using Tomato.GameLoop.Phases;
using Tomato.GameLoop.Providers;
using Tomato.SystemPipeline;
using Tomato.CommandGenerator;

// 1. カテゴリ定義
public enum ActionCategory { FullBody, Upper, Lower }

// 2. レジストリ作成
var registry = new EntityContextRegistry<ActionCategory>();

// 3. メッセージ処理コンポーネント
var messageQueue = new MessageHandlerQueue();
var waveProcessor = new WaveProcessor(maxWaveDepth: 100);

// 4. 衝突ソース（ゲーム側で実装）
// CollisionSystemを使用して衝突検出を行い、CollisionPairのリストを提供
var collisionSource = new MyCollisionSource();

// 5. 衝突メッセージエミッター作成（衝突をゲーム固有コマンドに変換）
var collisionEmitter = new MyCollisionMessageEmitter(messageQueue);

// 6. 依存コンポーネント作成
var inputProvider = new MyInputProvider();
var characterStateProvider = new MyCharacterStateProvider();
var actionFactory = new MyActionFactory();
var dependencyResolver = new MyDependencyResolver();
var positionReconciler = new MyPositionReconciler();
var despawner = new MyEntityDespawner();

// 7. 各システム作成
var collisionSystem = new CollisionSystem(collisionSource, collisionEmitter);

var messageSystem = new MessageSystem(waveProcessor, messageQueue);

var decisionSystem = new DecisionSystem<ActionCategory>(
    registry,
    new ActionSelector<ActionCategory, InputState, GameState>(),
    inputProvider,
    characterStateProvider);

var executionSystem = new ExecutionSystem<ActionCategory>(
    registry,
    decisionSystem.ResultBuffer,
    actionFactory);

var reconciliationSystem = new ReconciliationSystem(
    dependencyResolver,
    positionReconciler);

var cleanupSystem = new CleanupSystem<ActionCategory>(
    registry,
    despawner);

// 8. グループ構築
var updateGroup = new SystemGroup(
    collisionSystem,
    messageSystem,
    decisionSystem,
    executionSystem);

var lateUpdateGroup = new SystemGroup(
    reconciliationSystem,
    cleanupSystem);

// 9. パイプライン作成
var pipeline = new Pipeline(registry);
```

### ICollisionSource実装例

```csharp
using Tomato.GameLoop.Collision;
using Tomato.CollisionSystem;

// CollisionSystemを使用した衝突ソースの実装例
public class MyCollisionSource : ICollisionSource
{
    private readonly SpatialWorld _hitboxWorld;
    private readonly SpatialWorld _hurtboxWorld;
    private readonly List<CollisionPair> _collisions = new();

    public MyCollisionSource()
    {
        _hitboxWorld = new SpatialWorld(gridSize: 16f);
        _hurtboxWorld = new SpatialWorld(gridSize: 16f);
    }

    // フレーム開始時に各SpatialWorldを更新
    public void UpdateWorlds(/* entity positions and shapes */)
    {
        // 攻撃判定と食らい判定を別々のWorldで管理
        _hitboxWorld.Clear();
        _hurtboxWorld.Clear();
        // ... 各Entityの形状を登録
    }

    // フレーム中に衝突検出
    public void DetectCollisions()
    {
        _collisions.Clear();

        // hitbox vs hurtbox の衝突判定
        foreach (var hitbox in _hitboxWorld.GetAllEntries())
        {
            var nearbyHurtboxes = _hurtboxWorld.QuerySphere(hitbox.Position, hitbox.Radius);
            foreach (var hurtbox in nearbyHurtboxes)
            {
                if (ShapeIntersection.SphereSphere(
                    hitbox.Position, hitbox.Radius,
                    hurtbox.Position, hurtbox.Radius,
                    out var point, out var normal))
                {
                    _collisions.Add(new CollisionPair(
                        hitbox.EntityId,
                        hurtbox.EntityId,
                        point,
                        normal));
                }
            }
        }
    }

    public IReadOnlyList<CollisionPair> GetCollisions() => _collisions;
    public void Clear() => _collisions.Clear();
}
```

### ICollisionMessageEmitter実装例

```csharp
using Tomato.GameLoop.Collision;
using Tomato.GameLoop.Providers;
using Tomato.CommandGenerator;

public class MyCollisionMessageEmitter : ICollisionMessageEmitter
{
    private readonly MessageHandlerQueue _messageQueue;

    public MyCollisionMessageEmitter(MessageHandlerQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    public void EmitMessages(IReadOnlyList<CollisionPair> collisions)
    {
        foreach (var collision in collisions)
        {
            // ゲーム固有のコマンドをエンキュー
            _messageQueue.Enqueue<DamageCommand>(cmd =>
            {
                cmd.AttackerEntityId = collision.EntityIdA;
                cmd.TargetEntityId = collision.EntityIdB;
                cmd.HitPoint = collision.Point;
                cmd.HitNormal = collision.Normal;
            });
        }
    }
}
```

### CharacterSpawnSystemとの接続

```csharp
// SpawnBridge作成
var initializer = new MyEntityInitializer();
var spawnBridge = new SpawnBridge<ActionCategory>(registry, arena, initializer);

// CharacterSpawnControllerと接続
spawnBridge.Connect(characterSpawnController);

// キャラクターをスポーン
characterSpawnController.RequestState(CharacterRequestState.Active);
// → StateChangedイベントで OnCharacterActivated() が呼ばれ、EntityContextが自動登録

// キャラクターを削除
characterSpawnController.RequestState(CharacterRequestState.None);
// → OnCharacterRemoved() が呼ばれ、削除マークされる
// → 次フレームのCleanupSystemで実際に削除
```

### ゲームループ実行

```csharp
void Update(float deltaTime)
{
    // 衝突検出（CollisionSystem使用、GameLoop外部で実行）
    collisionSource.UpdateWorlds(/* ... */);
    collisionSource.DetectCollisions();

    // Update: Collision → Message → Decision → Execution
    pipeline.Execute(updateGroup, deltaTime);
}

void LateUpdate(float deltaTime)
{
    // LateUpdate: Reconciliation → Cleanup
    pipeline.Execute(lateUpdateGroup, deltaTime);
}
```

### Entity初期化の実装例

```csharp
public class MyEntityInitializer : IEntityInitializer<ActionCategory>
{
    public void Initialize(
        EntityContext<ActionCategory> context,
        string characterId,
        object? dataResource)
    {
        // データリソースからジャッジメント群を設定
        if (dataResource is CharacterData data)
        {
            context.Judgments = data.CreateJudgments();
        }

        // 初期アクション設定
        var idleAction = new IdleAction();
        context.ActionStateMachine.StartAction(ActionCategory.FullBody, idleAction);
    }
}
```

## 依存関係

```
GameLoop.Core
├── EntityHandleSystem.Attributes  (AnyHandle)
├── CommandGenerator.Attributes    (WaveProcessor, IWaveProcessable)
├── CommandGenerator.Core          (MessageHandlerQueue)
├── SystemPipeline.Core            (Pipeline, SystemGroup, ISystem)
├── ActionSelector                 (ActionSelector, IActionJudgment)
├── ActionExecutionSystem.Core     (ActionStateMachine, IExecutableAction)
├── Tomato.Math                    (Vector3)
└── CharacterSpawnSystem.Core      (CharacterSpawnController)
```

## テスト

```bash
# GameLoopのテスト実行
dotnet test libs/orchestration/GameLoop/GameLoop.Tests/
```

### テストカバレッジ

| テストファイル | テスト数 | 対象 |
|---------------|---------|------|
| EntityContextRegistryTests | 8 | コンテキスト管理 |
| SpawnBridgeTests | 27 | CharacterSpawnSystem連携 |
| CleanupPhaseProcessorTests | 10 | 削除処理 |
| CollisionPhaseProcessorTests | 6 | 衝突フェーズ |

**合計: 51テスト**

## ディレクトリ構造

```
GameLoop/
├── README.md
├── GameLoop.Core/
│   ├── GameLoop.Core.csproj
│   ├── Context/
│   │   ├── EntityContext.cs           # Entity単位コンテキスト
│   │   └── EntityContextRegistry.cs   # IEntityRegistry実装
│   ├── Collision/
│   │   ├── CollisionPair.cs           # 衝突ペア構造体
│   │   └── ICollisionSource.cs        # 衝突ソースインターフェース
│   ├── Phases/
│   │   ├── CollisionPhaseProcessor.cs # 第1フェーズ（ISerialSystem）
│   │   ├── MessagePhaseProcessor.cs   # 第2フェーズ（ISerialSystem）
│   │   ├── DecisionPhaseProcessor.cs  # 第3フェーズ（IParallelSystem）
│   │   ├── ExecutionPhaseProcessor.cs # 第4フェーズ（ISerialSystem）
│   │   ├── ReconciliationSystem.cs    # 第5フェーズ（IOrderedSerialSystem）
│   │   └── CleanupPhaseProcessor.cs   # 第6フェーズ（ISerialSystem）
│   ├── Spawn/
│   │   ├── SpawnBridge.cs             # CharacterSpawnSystem連携
│   │   ├── ISpawnCompletionHandler.cs
│   │   ├── IEntityInitializer.cs
│   │   └── IEntityArena.cs
│   └── Providers/
│       ├── IInputProvider.cs
│       ├── ICharacterStateProvider.cs
│       ├── ICollisionMessageEmitter.cs
│       └── IActionFactory.cs
└── GameLoop.Tests/
    ├── GameLoop.Tests.csproj
    ├── Context/
    │   └── EntityContextRegistryTests.cs
    ├── Spawn/
    │   └── SpawnBridgeTests.cs
    └── Phases/
        ├── CleanupPhaseProcessorTests.cs
        └── CollisionPhaseProcessorTests.cs
```

## 設計上の決定事項

### 衝突検出の外部化

**衝突検出はGameLoop外部で行う**。GameLoopは検出結果の受け取りとメッセージ化のみを担当する。

理由:
- ゲームによって衝突レイヤーの構成（hitbox/hurtbox, 環境衝突等）が異なる
- SpatialWorldの分離戦略（レイヤー別World vs 単一World）はゲーム側の判断
- GameLoopは汎用的なフレームワークとして、衝突検出の詳細に依存しない

### CommandQueueパターンの採用

メッセージ処理は`[CommandQueue]`属性で定義された`MessageHandlerQueue`を使用。
- ゲーム固有のコマンド（`DamageCommand`, `HealCommand`等）は`[Command<MessageHandlerQueue>]`でゲーム側で定義
- 優先度付きエンキューと自動プーリング
- `WaveProcessor`による収束処理

### 状態変更の一元化

Entityの論理状態はMessageSystemでのみ変更される。これにより:
- 決定論性の担保
- デバッグの容易さ
- 並列処理時の競合回避

### DecisionSystemの並列実行

アクション決定フェーズはIParallelSystemとして実装され、読み取り専用のため並列化可能。決定結果はDecisionResultBuffer（スレッドセーフ）に保存され、次のExecutionSystemで使用される。

### 遅延削除

Entity削除はCleanupSystemで行われる。これによりフレーム内での参照整合性を維持。

### SpawnBridgeパターン

CharacterSpawnSystemとの疎結合を実現。StateChangedイベントを監視し、必要なタイミングでEntityContextの登録/削除を行う。

## 関連ドキュメント

- [ARCHITECTURE.md](../../docs/ARCHITECTURE.md) - 全体アーキテクチャ
- [action-game-design.md](../../docs/plans/action-game-design.md) - ゲームループ設計
- [SystemPipeline README](../../foundation/SystemPipeline/README.md) - パイプラインシステム
- [CollisionSystem README](../../systems/CollisionSystem/README.md) - 空間インデックスと衝突検出

## ライセンス

MIT License
