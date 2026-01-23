# Tomato Game Framework Libraries

ゲーム開発のためのライブラリ群。

## フォルダ構造

```
libs/
├── foundation/              # 基盤システム（他の多くが依存）
│   ├── HandleSystem/        # 汎用ハンドルパターン（Source Generator）
│   ├── EntityHandleSystem/  # Entity専用ハンドル（HandleSystem依存）
│   ├── CommandGenerator/    # メッセージハンドラ生成（Source Generator）
│   ├── SystemPipeline/      # ECSスタイルのシステムパイプライン（Source Generator）
│   └── FlowTree/            # コールスタック付き汎用フロー制御
│
├── systems/                 # 個別機能システム
│   ├── ActionSelector/      # 行動選択エンジン
│   ├── ActionExecutionSystem/ # 行動実行・ステートマシン
│   ├── CharacterSpawnSystem/ # キャラクタースポーン
│   ├── CollisionSystem/     # 当たり判定
│   ├── CombatSystem/        # 攻撃・ダメージ処理
│   ├── StatusEffectSystem/  # 状態異常・バフ/デバフ
│   ├── ReconciliationSystem/ # 位置調停・サーバー同期
│   ├── DiagnosticsSystem/   # フレームプロファイリング
│   ├── SchedulerSystem/     # フレームベーススケジューラ
│   ├── SpatialIndexSystem/  # 空間ハッシュグリッド
│   └── SerializationSystem/ # 高性能バイナリシリアライズ
│
└── orchestration/           # 統合・オーケストレーション
    └── GameLoop/        # 6フェーズゲームループ統合
```

## システム一覧

### foundation/ - 基盤システム

| システム | 説明 | テスト数 |
|---------|------|---------|
| **HandleSystem** | 汎用ハンドルパターン（IHandle, IArena, ArenaBase）、エンティティ以外にも適用可能（Source Generator） | 25 |
| **EntityHandleSystem** | Entity専用ハンドル、コンポーネントシステム、Query、EntityManager（HandleSystem依存） | 309 |
| **CommandGenerator** | コマンドパターンのメッセージハンドラ生成（Source Generator） | 243 |
| **SystemPipeline** | ECSスタイルのシステムパイプライン、Serial/Parallel/MessageQueue処理（Source Generator） | 51 |
| **FlowTree** | コールスタック付き汎用フロー制御、ビヘイビアツリーパターン、動的サブツリー・再帰対応 | 107 |

### systems/ - 個別機能システム

| システム | 説明 | テスト数 |
|---------|------|---------|
| **ActionSelector** | 入力からアクションを選択、優先度ベースのジャッジメント | 66 |
| **ActionExecutionSystem** | アクション実行・ステートマシン管理 | 46 |
| **CharacterSpawnSystem** | キャラクター生成・リソース管理 | 269 |
| **CollisionSystem** | 当たり判定（Hitbox/Hurtbox/Pushbox/Trigger） | 68 |
| **CombatSystem** | 攻撃・ダメージ処理（HitGroup、多段ヒット制御） | 37 |
| **StatusEffectSystem** | 状態異常・バフ/デバフ管理 | 50 |
| **ReconciliationSystem** | 依存関係を考慮した位置調停 | 31 |
| **DiagnosticsSystem** | フレームプロファイリング・計測 | 34 |
| **SchedulerSystem** | フレームベーススケジューラ・クールダウン | 32 |
| **SpatialIndexSystem** | 空間ハッシュグリッドによる高速検索 | 33 |
| **SerializationSystem** | ゼロアロケーションバイナリシリアライズ | 60 |

### orchestration/ - 統合システム

| システム | 説明 | テスト数 |
|---------|------|---------|
| **GameLoop** | 6フェーズゲームループを実現する最上位統合システム | - |

**合計: 1,461 テスト**

## 主要な使用例

### EntityHandleSystem

```csharp
// Entity定義（Source Generatorがハンドルとアリーナを生成）
[Entity]
[HasCommandQueue(typeof(GameCommandQueue))]
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

// 使用（各Entityが独自のキューを持つ）
var arena = new PlayerArena();
PlayerHandle handle = arena.Create();
handle.GameCommandQueue.Enqueue<DamageCommand>(cmd => { cmd.Amount = 50; });
```

### SystemPipeline

```csharp
// システム定義
public class MovementSystem : IParallelSystem
{
    public bool IsEnabled { get; set; } = true;
    public IEntityQuery Query => ActiveEntityQuery.Instance;

    public void ProcessEntity(AnyHandle handle, in SystemContext context)
    {
        // 並列処理
    }
}

// パイプライン実行
var group = new SystemGroup(collisionSystem, messageSystem, decisionSystem);
var pipeline = new Pipeline(registry);
pipeline.Execute(group, deltaTime);
```

### EntityManager（EntityHandleSystem内）

複数のArenaを統一的に管理し、スナップショット/復元を行う:

```csharp
// EntityManagerは複数インスタンス作成可能
var manager = new EntityManager();

// Arenaを登録
manager.Register<EnemyArena, EnemyArenaSnapshot>(enemyArena);
manager.Register<BulletArena, BulletArenaSnapshot>(bulletArena);

// スナップショット取得
var snapshot = manager.CaptureSnapshot(frameNumber);

// 状態を復元
manager.RestoreSnapshot(snapshot);
```

### FlowTree

```csharp
// ツリー定義（入れ物と中身が分離）
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

// 自己再帰も自然に書ける
var countdown = new FlowTree("Countdown");
countdown.Build()
    .Selector()
        .Sequence()
            .Condition((ref FlowContext ctx) => ctx.Blackboard.GetInt(counterKey) <= 0)
            .Success()
        .End()
        .Sequence()
            .Action((ref FlowContext ctx) => { /* decrement */ return NodeStatus.Success; })
            .SubTree(countdown)  // 自己参照
        .End()
    .End()
    .Complete();
```

## ディレクトリ構造

各システムは以下の構造を持つ:

```
{SystemName}/
├── {SystemName}.Core/          # コアライブラリ
│   ├── {SystemName}.Core.csproj
│   └── *.cs
├── {SystemName}.Tests/         # テスト
│   ├── {SystemName}.Tests.csproj
│   ├── xunit.runner.json
│   └── *Tests.cs
└── README.md                   # ドキュメント
```

Source Generatorを持つシステムはさらに:

```
├── {SystemName}.Attributes/    # 属性定義
└── {SystemName}.Generator/     # Source Generator
```

## テスト実行

```bash
# foundation
dotnet test libs/foundation/{SystemName}/{SystemName}.Tests/

# systems
dotnet test libs/systems/{SystemName}/{SystemName}.Tests/

# orchestration
dotnet test libs/orchestration/{SystemName}/{SystemName}.Tests/
```

## 依存関係

```
HandleSystem.Core (最基盤)
├── IHandle / IArena / ArenaBase
├── RefAction<T>
└── [Handleable] / [HandleableMethod]

EntityHandleSystem.Attributes (HandleSystem依存)
├── IEntityHandle : IHandle
├── IEntityArena : IArena
├── EntityArenaBase : ArenaBase
├── AnyHandle / [Entity] / [EntityMethod]
├── EntityManager（スナップショット/復元）
├── QueryExecutor / EntityQuery
└── IQueryableArena / ISnapshotableArena

SystemPipeline.Core
├── ISystem / ISerialSystem / IParallelSystem / IMessageQueueSystem
├── SystemGroup / Pipeline
├── IEntityQuery / QueryCache
└── MessageQueue / IHasQueue<T>

CommandGenerator.Core
├── MessageQueue
├── WaveProcessor
└── IMessageDispatcher

FlowTree.Core (独立・外部依存なし)
├── FlowTree / IFlowNode / NodeStatus
├── Composite: Sequence, Selector, Parallel, Race, Join
├── Decorator: Retry, Timeout, Delay, Guard, Repeat
├── Leaf: Action, Condition, SubTree, Wait
├── Blackboard / BlackboardKey<T>
└── FlowCallStack / CallFrame

その他のシステムはEntityHandleSystem.Attributesに依存:
├── SpatialIndexSystem.Core
├── SchedulerSystem.Core（EntityCooldownManager）
├── CollisionSystem.Core
├── CombatSystem.Core（HandleSystem.Coreにも依存）
└── ...
```

## 開発ガイドライン

1. **型安全**: エンティティはAnyHandle/EntityHandleを使用
2. **TDD**: 新機能はテストから書く
3. **Source Generator**: 反復的なコードは自動生成
4. **パフォーマンス**: ゲームループ内はアロケーションを避ける
5. **EntityManager**: 各ゲームワールドで独自のEntityManagerを持てる
6. **SystemPipeline**: システムはISerialSystem/IParallelSystem/IMessageQueueSystemを実装

## ライセンス

MIT License
