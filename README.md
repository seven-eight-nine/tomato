# Tomato - アクションゲームフレームワーク

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-1%2C603%20passing-brightgreen)]()

3次元空間上でEntityが相互作用するアクションゲームのためのコアフレームワーク。
TDD（テスト駆動開発）で構築された、決定論的で拡張可能なゲームループシステム。

## 特徴

- **決定論的なゲームループ**: 同じ入力に対して常に同じ結果を保証
- **Step型メッセージシステム**: Entity間の状態変更をStep単位で処理
- **ECSスタイルのシステムパイプライン**: Serial/Parallel/MessageQueueの3種類の処理パターン
- **Source Generator活用**: EntityHandle、MessageHandler、MessageQueueSystem、DeepCloneを自動生成
- **モジュラーアーキテクチャ**: 各システムが独立してテスト可能
- **1,603テスト**: 高いテストカバレッジによる信頼性

## プロジェクト構成

```
tomato/
├── libs/
│   ├── foundation/              # 基盤システム
│   │   ├── EntityHandleSystem/  # 型安全なEntityハンドル、Arena、Query（Source Generator）
│   │   ├── CommandGenerator/    # コマンドキュー・メッセージハンドラ生成（Source Generator）
│   │   ├── SystemPipeline/      # ECSスタイルのシステムパイプライン（Source Generator）
│   │   ├── FlowTree/            # コールスタック付き汎用フロー制御（ビヘイビアツリー）
│   │   ├── DeepCloneGenerator/  # ディープクローン自動生成（Source Generator）
│   │   └── DependencySortSystem/ # 汎用トポロジカルソート
│   │
│   ├── systems/                 # 個別機能システム
│   │   ├── ActionSelector/      # 行動選択エンジン
│   │   ├── ActionExecutionSystem/  # 行動実行・ステートマシン
│   │   ├── CharacterSpawnSystem/   # キャラクタースポーン管理
│   │   ├── SpatialSystem/       # 空間システム（衝突判定・空間検索統合）
│   │   ├── CombatSystem/        # 攻撃・ダメージ処理
│   │   ├── StatusEffectSystem/  # 状態異常・バフ/デバフ管理
│   │   ├── InventorySystem/     # アイテム・インベントリ管理
│   │   ├── ReconciliationSystem/   # 位置調停・依存順処理
│   │   └── SerializationSystem/ # 高性能バイナリシリアライズ
│   │
│   └── orchestration/           # 統合・オーケストレーション
│       └── GameLoop/        # 6フェーズゲームループ統合
│
└── docs/
    ├── ARCHITECTURE.md          # アーキテクチャ概要
    ├── GETTING_STARTED.md       # 入門ガイド
    └── plans/                   # 設計ドキュメント
```

## フレーム処理フロー

```
GameLoopOrchestrator.Tick(deltaTime)
│
├─ Update:
│   ├─ CollisionPhase      衝突判定・メッセージ発行
│   ├─ MessagePhase        Step処理（状態変更はここでのみ）
│   ├─ DecisionPhase       行動決定（読み取り専用）
│   └─ ExecutionPhase      行動実行
│
└─ LateUpdate:
    ├─ ReconciliationPhase 位置調停（依存順）
    └─ CleanupPhase        消滅Entity削除
```

## クイックスタート

### 必要環境

- .NET 8.0 SDK

### ビルド

```bash
# 全システムをビルド
dotnet build libs/orchestration/GameLoop/GameLoop.Core/

# 個別ビルド
dotnet build libs/foundation/SystemPipeline/SystemPipeline.Core/
dotnet build libs/foundation/EntityHandleSystem/EntityHandleSystem.Attributes/
```

### テスト実行

```bash
# foundation
dotnet test libs/foundation/HandleSystem/HandleSystem.Tests/
dotnet test libs/foundation/EntityHandleSystem/EntityHandleSystem.Tests/
dotnet test libs/foundation/CommandGenerator/CommandGenerator.Tests/
dotnet test libs/foundation/SystemPipeline/SystemPipeline.Tests/
dotnet test libs/foundation/FlowTree/FlowTree.Tests/
dotnet test libs/foundation/DeepCloneGenerator/DeepCloneGenerator.Tests/
dotnet test libs/foundation/DependencySortSystem/DependencySortSystem.Tests/

# systems
dotnet test libs/systems/CharacterSpawnSystem/CharacterSpawnSystem.Tests/
dotnet test libs/systems/ActionSelector/ActionSelector.Tests/
dotnet test libs/systems/ActionExecutionSystem/ActionExecutionSystem.Tests/
dotnet test libs/systems/CollisionSystem/CollisionSystem.Tests/
dotnet test libs/systems/CombatSystem/CombatSystem.Tests/
dotnet test libs/systems/StatusEffectSystem/StatusEffectSystem.Tests/
dotnet test libs/systems/InventorySystem/InventorySystem.Tests/
dotnet test libs/systems/SerializationSystem/SerializationSystem.Tests/
```

## 基盤システム（Foundation）

### EntityHandleSystem

型安全なエンティティ管理システム。Source Generatorによりハンドルとアリーナを自動生成。

```csharp
// Entity定義（Source Generatorがハンドルとアリーナを生成）
[Entity]
[HasCommandQueue(typeof(GameCommandQueue))]  // CommandQueue追加
public partial class Player
{
    public int Health;
    public float Speed;
}

// CommandQueue定義
[CommandQueue]
public partial class GameCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(AnyHandle handle);
}

// 使用例
var arena = new PlayerArena();
PlayerHandle handle = arena.Create();

// プロパティベースのキューアクセス（Entity単位で独立したキューを持つ）
handle.GameCommandQueue.Enqueue<MoveCommand>(cmd => {
    cmd.Direction = new Vector3(1, 0, 0);
});
```

主な機能:
- `[Entity]`属性によるHandle/Arena自動生成
- `[HasCommandQueue]`属性によるEntity単位のキュー管理
- `[EntityComponent]`属性によるコンポーネント合成
- `AnyHandle`による型消去されたハンドル
- `EntityManager`によるスナップショット/復元
- `QueryExecutor`によるエンティティクエリ

### CommandGenerator

コマンドパターンを自動生成。優先度ベースの実行とオブジェクトプーリングを備えています。

```csharp
// CommandQueue定義
[CommandQueue]
public partial class GameCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(AnyHandle handle);
}

// Command定義（優先度付き）
[Command<GameCommandQueue>(Priority = 50)]
public partial class MoveCommand
{
    public Vector3 Direction;

    public void ExecuteCommand(AnyHandle handle)
    {
        // 移動処理
    }
}

// 使用例
queue.Enqueue<MoveCommand>(cmd => {
    cmd.Direction = new Vector3(1, 0, 0);
});
```

### SystemPipeline

ECSスタイルのシステムパイプライン。3種類の処理パターンをサポート。

```csharp
// システム定義
public class MovementSystem : IParallelSystem
{
    public bool IsEnabled { get; set; } = true;
    public IEntityQuery? Query => ActiveEntityQuery.Instance;  // フィルタリング

    public void ProcessEntity(AnyHandle handle, in SystemContext context)
    {
        // 並列処理（スレッドセーフに実装）
    }
}

// パイプライン構築
var updateGroup = new SystemGroup(
    new CollisionSystem(),
    new GameCommandQueueSystem(handlerRegistry),  // Source Generator生成
    new DecisionSystem(),
    new ExecutionSystem()
);

var pipeline = new Pipeline(registry);
pipeline.Execute(updateGroup, deltaTime);
```

処理パターン:
| パターン | インターフェース | 説明 |
|---------|-----------------|------|
| **Serial** | `ISerialSystem` | 全エンティティを順番に処理 |
| **Parallel** | `IParallelSystem` | 各エンティティを並列に処理 |
| **MessageQueue** | `IMessageQueueSystem` | メッセージをStep単位で処理 |

### FlowTree

コールスタック付き汎用フロー制御ライブラリ。ビヘイビアツリーのパターンを基盤としつつ、AI行動選択に限らず非同期処理やワークフロー全般に適用可能。

```csharp
// ツリー定義
var tree = new FlowTree("Patrol");
tree.Build()
    .Sequence()
        .Action(static (ref FlowContext ctx) => GetNextWaypoint(ref ctx))
        .Action(static (ref FlowContext ctx) => MoveToWaypoint(ref ctx))
        .Wait(2.0f)
    .End()
    .Complete();

// 実行
var context = FlowContext.Create(new Blackboard(64), 0.016f);
var status = tree.Tick(ref context);
```

主な機能:
- **動的サブツリー**: SubTreeNodeで別ツリーを呼び出し、コールスタックで追跡
- **自己再帰・相互再帰**: ツリー参照による自然な再帰記述
- **低GC**: 通常使用ではヒープアロケーションなし
- **豊富なノード**: Sequence, Selector, Parallel, Race, Join, Retry, Timeout等

### DeepCloneGenerator

ディープクローンを自動生成するSource Generator。

```csharp
[DeepClonable]
public partial class GameState
{
    public int Score;
    public List<Enemy> Enemies;  // 自動でディープコピー
}

// 使用例
var clone = original.DeepClone();
```

### DependencySortSystem

汎用的なトポロジカルソートライブラリ。依存グラフから処理順序を計算。

```csharp
var graph = new DependencyGraph<string>();
graph.AddDependency("app", "database");
graph.AddDependency("database", "config");

var sorter = new TopologicalSorter<string>();
var result = sorter.Sort(graph.GetAllNodes(), graph);
// result.SortedOrder: config -> database -> app
```

## 個別機能システム（Systems）

### CollisionSystem

空間的な衝突判定。レイヤーベースのフィルタリングをサポート。

```csharp
// ボリュームタイプはゲーム側で定義（int値）
const int Hitbox = 0;
const int Hurtbox = 1;

var volume = new CollisionVolume(
    owner: handle,
    shape: new SphereShape(1.0f),
    filter: new CollisionFilter(layer: 1, mask: 2),
    volumeType: Hurtbox);

collisionDetector.AddVolume(volume, position);
collisionDetector.DetectCollisions(results);
```

形状: `SphereShape`, `BoxShape`, `CapsuleShape`

### ActionSelector

入力やAIからの行動選択。優先度ベースのジャッジメント評価。

```csharp
var engine = new SelectionEngine<ActionCategory>();
var result = engine.ProcessFrame(judgments, gameState);

if (result.TryGetRequested(ActionCategory.Upper, out var requested))
{
    // 選択されたアクションを実行
}
```

### ActionExecutionSystem

選択されたアクションの実行とステートマシン管理。

```csharp
var machine = new ActionStateMachine<ActionCategory>();
var action = new StandardExecutableAction<ActionCategory>(definition);
machine.StartAction(ActionCategory.Upper, action);
machine.Update(deltaTime);
```

### CharacterSpawnSystem

キャラクターのリソース管理とスポーン。

```csharp
var controller = new CharacterSpawnController(characterId, loader, factory);
var spawnBridge = new SpawnBridge<ActionCategory>(registry, arena, initializer);
spawnBridge.Connect(controller);

// スポーン
controller.RequestState(CharacterRequestState.Active);
```

### ReconciliationSystem

依存関係を考慮した位置調停。DependencySortSystemを使用。

```csharp
var graph = new DependencyGraph<AnyHandle>();
graph.AddDependency(rider, horse);  // 騎乗者は馬に依存

var reconciler = new PositionReconciler(graph, rule, transforms, entityTypes);
reconciler.Process(entities, pushboxCollisions);
```

### その他のシステム

| システム | 説明 |
|---------|------|
| **StatusEffectSystem** | 状態異常・バフ/デバフの管理 |
| **InventorySystem** | アイテム・インベントリ管理 |
| **SpatialSystem** | 衝突判定・空間検索統合 |
| **SerializationSystem** | ゼロアロケーションのバイナリシリアライズ |

## 統合システム（Orchestration）

### GameLoop

6フェーズのゲームループを統合する最上位システム。

```csharp
// カテゴリ定義
public enum ActionCategory { FullBody, Upper, Lower }

// セットアップ
var registry = new EntityContextRegistry<ActionCategory>();
var orchestrator = new GameLoopOrchestrator<ActionCategory>(
    registry,
    collisionProcessor,
    messageProcessor,
    decisionProcessor,
    executionProcessor,
    reconciliationProcessor,
    cleanupProcessor);

// ゲームループ
void Update(float deltaTime) => orchestrator.Update(deltaTime);
void LateUpdate(float deltaTime) => orchestrator.LateUpdate(deltaTime);
```

## 設計原則

### 状態変更の一元化

Entityの論理状態（HP等）はメッセージ処理（MessagePhase）でのみ変更される。
これにより決定論性とデバッグの容易さを実現。

### Step処理

```
Step 0: 全Entityの今Stepのメッセージを処理
        ↓ 新規メッセージは次Stepへ
Step 1: 全Entityの次Stepのメッセージを処理
        ↓
      収束まで繰り返し
```

### Entity消滅タイミング

フレーム末尾（CleanupPhase）でのみ消滅。フレーム内での参照整合性を維持。

### Source Generator活用

反復的なボイラープレートコードは自動生成:
- `[Entity]` → Handle構造体 + Arena管理クラス
- `[HasCommandQueue]` → Entity単位のキュー配列 + プロパティアクセサ
- `[CommandQueue]` + `[Command<T>]` → コマンドキューとハンドラ
- `[CommandQueue]` → MessageQueueSystem実装（SystemPipeline連携）
- `[DeepClonable]` → ディープクローンメソッド

## テスト数

| カテゴリ | システム | テスト数 |
|---------|---------|---------|
| foundation | EntityHandleSystem | 309 |
| foundation | CommandGenerator | 243 |
| foundation | FlowTree | 107 |
| foundation | DeepCloneGenerator | 82 |
| foundation | SystemPipeline | 51 |
| foundation | DependencySortSystem | 28 |
| foundation | HandleSystem | 25 |
| systems | CharacterSpawnSystem | 269 |
| systems | InventorySystem | 101 |
| systems | ActionSelector | 66 |
| systems | SpatialSystem | 50+ |
| systems | StatusEffectSystem | 50 |
| systems | ActionExecutionSystem | 46 |
| systems | CombatSystem | 37 |
| systems | SerializationSystem | 21 |
| systems | ReconciliationSystem | 11 |
| orchestration | GameLoop | 56 |
| | **合計** | **1,603** |

## ドキュメント

- [アーキテクチャ概要](docs/ARCHITECTURE.md)
- [はじめに](docs/GETTING_STARTED.md)
- [設計ドキュメント](docs/plans/action-game-design.md)
- [SystemPipeline詳細](libs/foundation/SystemPipeline/README.md)
- [EntityHandleSystem詳細](libs/foundation/EntityHandleSystem/README.md)
- [FlowTree詳細](libs/foundation/FlowTree/README.md)
- [DeepCloneGenerator詳細](libs/foundation/DeepCloneGenerator/README.md)
- [DependencySortSystem詳細](libs/foundation/DependencySortSystem/README.md)
- [GameLoop詳細](libs/orchestration/GameLoop/README.md)
- [ライブラリ一覧](libs/README.md)

## ライセンス

MIT License - 詳細は[LICENSE](LICENSE)を参照してください。
