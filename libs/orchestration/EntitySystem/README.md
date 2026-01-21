# EntitySystem

action-game-design.mdで定義された6フェーズゲームループを実現する統合システム。
全てのシステム（EntityHandle, CommandGenerator, ActionSelector, ActionExecution, Collision, CharacterSpawn）を連携させる。

## 概要

EntitySystemは以下の責務を持つ:

1. **Entity単位のコンテキスト管理** - ActionStateMachine, CollisionVolumesをEntityに紐付け
2. **6フェーズゲームループの統括** - Collision→Message→Decision→Execution→Reconciliation→Cleanup
3. **CharacterSpawnSystemとの連携** - スポーン/デスポーンイベントをEntityContextに橋渡し
4. **CommandGeneratorとの連携** - MessageHandlerQueueとWaveProcessorによるメッセージ処理

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Pipeline + SystemGroup                          │
├─────────────────────────────────────────────────────────────────────┤
│  UpdateGroup:                                                       │
│    1. CollisionSystem      - 衝突判定・コマンド発行                 │
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
| `EntityContext<TCategory>` | Entity単位のコンテキスト。ActionStateMachine, CollisionVolumesを保持 |
| `EntityContextRegistry<TCategory>` | コンテキスト管理。IEntityRegistryを実装 |

```csharp
// EntityContext の構成
public sealed class EntityContext<TCategory>
{
    public VoidHandle Handle { get; }
    public ActionStateMachine<TCategory> ActionStateMachine { get; }
    public List<CollisionVolume> CollisionVolumes { get; }
    public IActionJudgment<TCategory, InputState, GameState>[] Judgments { get; set; }
    public CharacterSpawnController? SpawnController { get; set; }
    public bool IsActive { get; set; }
    public bool IsMarkedForDeletion { get; }
}
```

### Phases/

6つのフェーズシステム。すべてSystemPipelineのISystem/ISerialSystem/IParallelSystemを実装。

| フェーズ | クラス | 責務 |
|----------|--------|------|
| 1. Collision | `CollisionSystem<TCategory>` | 衝突ボリューム収集、衝突検出、ICollisionMessageEmitterでコマンド発行 |
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
| `IEntityPositionProvider` | Entity位置の取得 |
| `IActionFactory<TCategory>` | アクションID→IExecutableAction変換 |

## 使用例

### 基本セットアップ

```csharp
using EntitySystem.Context;
using EntitySystem.Phases;
using EntitySystem.Providers;
using SystemPipeline;
using CollisionSystem;
using CommandGenerator;

// 1. カテゴリ定義
public enum ActionCategory { FullBody, Upper, Lower }

// 2. レジストリ作成
var registry = new EntityContextRegistry<ActionCategory>();

// 3. メッセージ処理コンポーネント
var messageQueue = new MessageHandlerQueue();
var waveProcessor = new WaveProcessor(maxWaveDepth: 100);

// 4. 依存コンポーネント作成
var positionProvider = new MyPositionProvider();
var inputProvider = new MyInputProvider();
var characterStateProvider = new MyCharacterStateProvider();
var actionFactory = new MyActionFactory();
var dependencyResolver = new MyDependencyResolver();
var positionReconciler = new MyPositionReconciler();
var despawner = new MyEntityDespawner();

// 5. 衝突メッセージエミッター作成（ゲーム固有のコマンドをエンキュー）
var collisionEmitter = new CallbackCollisionMessageEmitter(info =>
{
    // 衝突情報からゲーム固有のコマンドをエンキュー
    messageQueue.Enqueue<DamageCommand>(cmd =>
    {
        cmd.Target = info.Target;
        cmd.Source = info.Source;
        cmd.Amount = info.Amount;
    });
});

// 6. 各システム作成
var collisionSystem = new CollisionSystem<ActionCategory>(
    registry,
    new CollisionDetector(),
    positionProvider,
    collisionEmitter);

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

// 7. グループ構築
var updateGroup = new SystemGroup(
    collisionSystem,
    messageSystem,
    decisionSystem,
    executionSystem);

var lateUpdateGroup = new SystemGroup(
    reconciliationSystem,
    cleanupSystem);

// 8. パイプライン作成
var pipeline = new Pipeline(registry);
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
EntitySystem.Core
├── EntityHandleSystem.Attributes  (VoidHandle)
├── CommandGenerator.Attributes    (WaveProcessor, IWaveProcessable)
├── CommandGenerator.Core          (MessageHandlerQueue)
├── SystemPipeline.Core            (Pipeline, SystemGroup, ISystem)
├── ActionSelector                 (ActionSelector, IActionJudgment)
├── ActionExecutionSystem.Core     (ActionStateMachine, IExecutableAction)
├── CollisionSystem.Core           (CollisionDetector, CollisionVolume)
└── CharacterSpawnSystem.Core      (CharacterSpawnController)
```

## テスト

```bash
# EntitySystemのテスト実行
dotnet test libs/orchestration/EntitySystem/EntitySystem.Tests/
```

### テストカバレッジ

| テストファイル | テスト数 | 対象 |
|---------------|---------|------|
| EntityContextRegistryTests | 8 | コンテキスト管理 |
| SpawnBridgeTests | 28 | CharacterSpawnSystem連携 |
| CleanupPhaseProcessorTests | 6 | 削除処理 |
| CollisionPhaseProcessorTests | 10 | 衝突判定フェーズ |

**合計: 52テスト**

## ディレクトリ構造

```
EntitySystem/
├── README.md
├── EntitySystem.Core/
│   ├── EntitySystem.Core.csproj
│   ├── Context/
│   │   ├── EntityContext.cs           # Entity単位コンテキスト
│   │   └── EntityContextRegistry.cs   # IEntityRegistry実装
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
│       ├── IEntityPositionProvider.cs
│       └── IActionFactory.cs
└── EntitySystem.Tests/
    ├── EntitySystem.Tests.csproj
    ├── Context/
    │   └── EntityContextRegistryTests.cs
    ├── Spawn/
    │   └── SpawnBridgeTests.cs
    └── Phases/
        ├── CleanupPhaseProcessorTests.cs
        └── CollisionPhaseProcessorTests.cs
```

## 設計上の決定事項

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

## ライセンス

MIT License
