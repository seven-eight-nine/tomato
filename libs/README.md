# Tomato Game Framework Libraries

ゲーム開発のためのライブラリ群。

## フォルダ構造

```
libs/
├── foundation/              # 基盤システム（他の多くが依存）
│   ├── HandleSystem/        # 汎用ハンドルパターン（Source Generator）
│   ├── EntityHandleSystem/  # Entity専用ハンドル（HandleSystem依存）
│   ├── CommandGenerator/    # メッセージハンドラ生成（Source Generator）
│   └── SystemPipeline/      # ECSスタイルのシステムパイプライン（Source Generator）
│
├── systems/                 # 個別機能システム
│   ├── ActionSelector/      # 行動選択エンジン
│   ├── ActionExecutionSystem/ # 行動実行・ステートマシン
│   ├── CharacterSpawnSystem/ # キャラクタースポーン
│   ├── CollisionSystem/     # 当たり判定
│   ├── ReconciliationSystem/ # 位置調停・サーバー同期
│   ├── DiagnosticsSystem/   # フレームプロファイリング
│   ├── SchedulerSystem/     # フレームベーススケジューラ
│   ├── SpatialIndexSystem/  # 空間ハッシュグリッド
│   └── SerializationSystem/ # 高性能バイナリシリアライズ
│
└── orchestration/           # 統合・オーケストレーション
    └── EntitySystem/        # 6フェーズゲームループ統合
```

## システム一覧

### foundation/ - 基盤システム

| システム | 説明 | テスト数 |
|---------|------|---------|
| **HandleSystem** | 汎用ハンドルパターン（IHandle, IArena, ArenaBase）、エンティティ以外にも適用可能（Source Generator） | 25 |
| **EntityHandleSystem** | Entity専用ハンドル、コンポーネントシステム、Query、EntityManager（HandleSystem依存） | 309 |
| **CommandGenerator** | コマンドパターンのメッセージハンドラ生成（Source Generator） | 243 |
| **SystemPipeline** | ECSスタイルのシステムパイプライン、Serial/Parallel/MessageQueue処理（Source Generator） | 53 |

### systems/ - 個別機能システム

| システム | 説明 | テスト数 |
|---------|------|---------|
| **ActionSelector** | 入力からアクションを選択、優先度ベースのジャッジメント | 125 |
| **ActionExecutionSystem** | アクション実行・ステートマシン管理 | 46 |
| **CharacterSpawnSystem** | キャラクター生成・リソース管理 | 269 |
| **CollisionSystem** | 当たり判定（Hitbox/Hurtbox/Pushbox/Trigger） | 74 |
| **ReconciliationSystem** | 依存関係を考慮した位置調停 | 31 |
| **DiagnosticsSystem** | フレームプロファイリング・計測 | 34 |
| **SchedulerSystem** | フレームベーススケジューラ・クールダウン | 32 |
| **SpatialIndexSystem** | 空間ハッシュグリッドによる高速検索 | 33 |
| **SerializationSystem** | ゼロアロケーションバイナリシリアライズ | 33 |

### orchestration/ - 統合システム

| システム | 説明 | テスト数 |
|---------|------|---------|
| **EntitySystem** | 6フェーズゲームループを実現する最上位統合システム | 57 |

**合計: 1,357+ テスト**

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

その他のシステムはEntityHandleSystem.Attributesに依存:
├── SpatialIndexSystem.Core
├── SchedulerSystem.Core（EntityCooldownManager）
├── CollisionSystem.Core
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
