# はじめに

このガイドでは、Tomatoフレームワークを使ってゲームを構築する方法を説明する。

---

## 前提条件

- .NET 8.0 SDK
- C# の基本的な知識
- ゲームループの概念の理解

---

## プロジェクト構成

```
YourGame/
├── YourGame.Core/           # ゲームロジック
│   └── YourGame.Core.csproj
├── YourGame.Tests/          # テスト
│   └── YourGame.Tests.csproj
└── libs/                    # Tomatoフレームワーク（サブモジュール等）
    └── tomato/
```

### csproj参照設定

```xml
<ItemGroup>
  <!-- 基盤システム -->
  <ProjectReference Include="../libs/tomato/libs/foundation/EntityHandleSystem/EntityHandleSystem.Attributes/EntityHandleSystem.Attributes.csproj" />
  <ProjectReference Include="../libs/tomato/libs/foundation/EntityHandleSystem/EntityHandleSystem.Generator/EntityHandleSystem.Generator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
  <ProjectReference Include="../libs/tomato/libs/foundation/SystemPipeline/SystemPipeline.Core/SystemPipeline.Core.csproj" />
  <ProjectReference Include="../libs/tomato/libs/foundation/SystemPipeline/SystemPipeline.Generator/SystemPipeline.Generator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="true" />
  <ProjectReference Include="../libs/tomato/libs/foundation/CommandGenerator/CommandGenerator.Core/CommandGenerator.Core.csproj" />

  <!-- 機能システム -->
  <ProjectReference Include="../libs/tomato/libs/systems/CollisionSystem/CollisionSystem.Core/CollisionSystem.Core.csproj" />
  <ProjectReference Include="../libs/tomato/libs/systems/ActionSelector/ActionSelector.Core/ActionSelector.Core.csproj" />
  <ProjectReference Include="../libs/tomato/libs/systems/ActionExecutionSystem/ActionExecutionSystem.Core/ActionExecutionSystem.Core.csproj" />
  <ProjectReference Include="../libs/tomato/libs/systems/ReconciliationSystem/ReconciliationSystem.Core/ReconciliationSystem.Core.csproj" />

  <!-- 統合システム -->
  <ProjectReference Include="../libs/tomato/libs/orchestration/GameLoop/GameLoop.Core/GameLoop.Core.csproj" />

  <!-- オプション：追加システム -->
  <ProjectReference Include="../libs/tomato/libs/systems/InventorySystem/InventorySystem.Core/InventorySystem.Core.csproj" />
  <ProjectReference Include="../libs/tomato/libs/systems/TimelineSystem/TimelineSystem.Core/TimelineSystem.Core.csproj" />
  <ProjectReference Include="../libs/tomato/libs/systems/HierarchicalStateMachine/HierarchicalStateMachine.Core/HierarchicalStateMachine.Core.csproj" />
  <ProjectReference Include="../libs/tomato/libs/foundation/FlowTree/FlowTree.Core/FlowTree.Core.csproj" />
</ItemGroup>
```

---

## 基本的なセットアップ

### 1. Entity定義

EntityHandleSystemを使用してEntityを定義する。Source Generatorによりハンドルとアリーナが自動生成される。

```csharp
using Tomato.EntityHandleSystem;

// Entity定義
[Entity(InitialCapacity = 100)]
public partial class Player
{
    public int Health;
    public int Defense;
    public bool IsAlive;

    [EntityMethod]
    public void TakeDamage(int damage)
    {
        Health -= Math.Max(0, damage - Defense);
        if (Health <= 0) IsAlive = false;
    }

    [EntityMethod]
    public void Heal(int amount)
    {
        Health = Math.Min(Health + amount, 100);
    }
}

[Entity(InitialCapacity = 200)]
public partial class Enemy
{
    public int Health;
    public bool IsAlive;

    [EntityMethod]
    public void TakeDamage(int damage)
    {
        Health -= damage;
        if (Health <= 0) IsAlive = false;
    }
}
```

### 2. Entity使用例

```csharp
// Arenaを作成（自動生成される）
var arena = new PlayerArena(
    onSpawn: p => { p.Health = 100; p.IsAlive = true; },
    onDespawn: p => { p.IsAlive = false; }
);

// エンティティを作成
PlayerHandle handle = arena.Create();

// メソッド呼び出し（安全版）
if (handle.TryTakeDamage(30))
{
    Console.WriteLine("ダメージを与えました");
}

// 値を取得
if (handle.TryGetHealth(out int health))
{
    Console.WriteLine($"残りHP: {health}");
}
```

### 3. コマンドキューの使用

Tomatoでは状態変更を`CommandQueue`を通じて行う。コマンドキューはEntity単位で管理され、各Entityが独自のキューを持つ。

```csharp
using Tomato.CommandGenerator;
using Tomato.EntityHandleSystem;

// CommandQueueの定義
[CommandQueue]
public partial class GameCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(VoidHandle handle);
}

// Entity定義（CommandQueueを追加）
[Entity(InitialCapacity = 100)]
[HasCommandQueue(typeof(GameCommandQueue))]
public partial class Player
{
    public int Health;
    public int Defense;
}

// ゲーム固有のダメージコマンド
[Command<GameCommandQueue>(Priority = 50)]
public partial class DamageCommand
{
    public int Amount;

    public void ExecuteCommand(VoidHandle handle)
    {
        // ダメージ処理ロジック
    }
}

// ゲーム固有の回復コマンド
[Command<GameCommandQueue>(Priority = 80)]
public partial class HealCommand
{
    public int Amount;

    public void ExecuteCommand(VoidHandle handle)
    {
        // 回復処理ロジック
    }
}
```

使用例:
```csharp
var arena = new PlayerArena();
var handle = arena.Create();

// プロパティでキューにアクセス（各Entityが独自のキューを持つ）
handle.GameCommandQueue.Enqueue<DamageCommand>(cmd => {
    cmd.Amount = 50;
});

// 別のEntityは別のキューを持つ
var handle2 = arena.Create();
handle2.GameCommandQueue.Enqueue<HealCommand>(cmd => {
    cmd.Amount = 30;
});
```

Source Generatorにより、各コマンドに以下の機能が自動生成される:
- `Handle.{QueueName}.Enqueue<T>(Action<T> configure)` - コマンドをキューに追加
- オブジェクトプーリング（コマンド生成のGC負荷を軽減）
- 優先度順のソート処理

**重要**: コマンドの実行は `StepProcessor` によって行われます。`MessageSystem`（自動生成される`GameCommandQueueSystem`）がゲームループ内で自動的に処理します。

### 4. ゲームループのセットアップ

Tomatoでは`Pipeline`と`SystemGroup`を使ってゲームループを構築する。

```csharp
using Tomato.GameLoop.Context;
using Tomato.GameLoop.Phases;
using Tomato.GameLoop.Providers;
using Tomato.SystemPipeline;
using Tomato.CommandGenerator;
using Tomato.CollisionSystem;

// アクションカテゴリの定義
public enum ActionCategory { FullBody, Upper, Lower }

public class Game
{
    private readonly Pipeline _pipeline;
    private readonly SystemGroup _updateGroup;
    private readonly SystemGroup _lateUpdateGroup;
    private readonly EntityContextRegistry<ActionCategory> _registry;

    public Game()
    {
        // 1. レジストリ作成
        _registry = new EntityContextRegistry<ActionCategory>();

        // 2. メッセージ処理コンポーネント作成
        var handlerRegistry = new GameMessageHandlerRegistry();
        var stepProcessor = new StepProcessor(maxStepDepth: 100);

        // 3. 依存オブジェクト作成
        var positionProvider = new GamePositionProvider();
        var inputProvider = new GameInputProvider();
        var characterStateProvider = new GameCharacterStateProvider();
        var actionFactory = new GameActionFactory();
        var dependencyResolver = new GameDependencyResolver();
        var positionReconciler = new GamePositionReconciler();
        var despawner = new GameEntityDespawner();

        // 4. 衝突メッセージエミッター作成（ゲーム固有のコマンドをエンキュー）
        var collisionEmitter = new CallbackCollisionMessageEmitter((info, registry) =>
        {
            // 衝突情報からゲーム固有のコマンドをエンキュー
            // 各Entityのキューに直接Enqueue
            if (registry.TryGetHandle(info.Target, out var targetHandle))
            {
                targetHandle.GameCommandQueue.Enqueue<DamageCommand>(cmd =>
                {
                    cmd.Amount = info.Amount;
                });
            }
        });

        // 5. 各システム作成
        var collisionSystem = new CollisionSystem<ActionCategory>(
            _registry,
            new CollisionDetector(),
            positionProvider,
            collisionEmitter);

        // GameCommandQueueSystemはSource Generatorが自動生成
        var messageSystem = new GameCommandQueueSystem(handlerRegistry);

        var decisionSystem = new DecisionSystem<ActionCategory>(
            _registry,
            new ActionSelector<ActionCategory, InputState, GameState>(),
            inputProvider,
            characterStateProvider);

        var executionSystem = new ExecutionSystem<ActionCategory>(
            _registry,
            decisionSystem.ResultBuffer,
            actionFactory);

        var reconciliationSystem = new ReconciliationSystem(
            dependencyResolver,
            positionReconciler);

        var cleanupSystem = new CleanupSystem<ActionCategory>(
            _registry,
            despawner);

        // 6. グループ構築（実行順序は配列の順番）
        _updateGroup = new SystemGroup(
            collisionSystem,
            messageSystem,
            decisionSystem,
            executionSystem);

        _lateUpdateGroup = new SystemGroup(
            reconciliationSystem,
            cleanupSystem);

        // 7. パイプライン作成
        _pipeline = new Pipeline(_registry);
    }

    public void Update(float deltaTime)
    {
        // Update: Collision → Message → Decision → Execution
        _pipeline.Execute(_updateGroup, deltaTime);
    }

    public void LateUpdate(float deltaTime)
    {
        // LateUpdate: Reconciliation → Cleanup
        _pipeline.Execute(_lateUpdateGroup, deltaTime);
    }

    public EntityContextRegistry<ActionCategory> Registry => _registry;
}
```

---

## メッセージシステム

Tomatoでは、Entityの状態変更は`CommandQueue`を通じて行います。各EntityがCommandQueueを持ち、キューは独立して管理されます。

**重要**: コマンドは即時処理されません。`Enqueue<T>()`はキューに追加するだけで、実際の処理は`MessageSystem`の実行タイミング（ゲームループのMessagePhase）で行われます。また、キュー内のコマンドは**優先度順にソート**されて処理されるため、Enqueueした順序で処理されるとは限りません。優先度は`[Command<T>(Priority = N)]`で指定します。

```
handle.GameCommandQueue.Enqueue<T>() → キューに蓄積 → MessagePhaseでStep処理 → Command.ExecuteCommand()
```

`CollisionSystem`は衝突検出時にコマンドを発行するシステムの一例です。Hitbox vs Hurtboxの衝突が検出されると、`ICollisionMessageEmitter`を通じてターゲットEntityのCommandQueueにダメージコマンドがEnqueueされます。

---

## 行動システムの統合

### 1. カテゴリの定義

```csharp
public enum ActionCategory
{
    FullBody,  // 全身アクション（ジャンプ等）
    Upper,     // 上半身アクション（攻撃等）
    Lower,     // 下半身アクション（移動等）
}
```

### 2. ActionSelectorの使用

```csharp
using Tomato.ActionSelector;
using Tomato.ActionExecutionSystem;

// ActionSelector作成
var selector = new ActionSelector<ActionCategory, InputState, GameState>();

// JudgmentListを構築
var judgmentList = new JudgmentList<ActionCategory, InputState, GameState>();
judgmentList.Add(attackJudgment);
judgmentList.Add(jumpJudgment);

// 毎フレーム処理
var inputState = inputProvider.GetInputState(handle);
var gameState = new GameState(
    inputState,
    characterState,
    CombatState.OutOfCombat,
    EmptyResourceState.Instance,
    deltaTime,
    totalTime,
    frameCount);

var frameState = new FrameState<InputState, GameState>(
    inputState,
    gameState,
    deltaTime,
    totalTime,
    frameCount);

var result = selector.ProcessFrame(judgmentList, in frameState);

// カテゴリごとの結果を取得
if (result.TryGetRequested(ActionCategory.Upper, out var judgment))
{
    // 選択されたアクションを開始
    var action = actionFactory.Create(judgment.ActionId, ActionCategory.Upper);
    stateMachine.StartAction(ActionCategory.Upper, action);
}
```

### 3. ActionStateMachineの使用

```csharp
var machine = new ActionStateMachine<ActionCategory>();

// アクション開始
machine.StartAction(ActionCategory.Upper, action);

// 毎フレーム更新
machine.Update(deltaTime);

// 状態確認
if (machine.IsRunning(ActionCategory.Upper))
{
    var current = machine.GetCurrentAction(ActionCategory.Upper);
    if (current is IRunningAction<ActionCategory> running && running.CanCancel)
    {
        // コンボ遷移可能
    }
}
```

### 4. MotionGraphの使用

MotionGraphはHierarchicalStateMachineを基盤としたフレームベースのモーション状態管理。フレーム経過やモーション完了などの情報を提供する。

**責務分担**: アクション選択はActionSelectorの責務。MotionGraphはキャンセル可能かどうかの情報（ElapsedFrames等）を提供するだけ。

```csharp
using Tomato.ActionExecutionSystem.MotionGraph;
using Tomato.HierarchicalStateMachine;
using Tomato.TimelineSystem;

// モーション定義を作成
var idleDefinition = new MotionDefinition("Idle", totalFrames: 60, new Sequence());
var walkDefinition = new MotionDefinition("Walk", totalFrames: 30, new Sequence());

// 状態を作成
var idleState = new MotionState(idleDefinition);
var walkState = new MotionState(walkDefinition);

// 状態グラフを構築
var graph = new StateGraph<MotionContext>()
    .AddState(idleState)
    .AddState(walkState)
    .AddTransition(new Transition<MotionContext>(
        "Idle", "Walk", 1f,
        MotionTransitionCondition.Always()))
    .AddTransition(new Transition<MotionContext>(
        "Walk", "Idle", 1f,
        MotionTransitionCondition.IsComplete()));

// ステートマシンを作成・初期化
var machine = new MotionStateMachine(graph);
machine.Initialize("Idle");

// ゲームループでの使用例（6フェーズに従う）
void DecisionPhase(float deltaTime)
{
    // 1. ジャッジメントリストを構築
    judgmentList.Clear();

    // 2. キャンセル可能ならコンボ系ジャッジメントを追加
    bool canCancel = machine.ElapsedFrames >= 15;
    if (canCancel)
    {
        judgmentList.Add(attack2Judgment);
        judgmentList.Add(attack3Judgment);
    }

    // 3. 他の遷移（ダメージ、ガード等）は常に追加
    judgmentList.Add(damageReactionJudgment);
    judgmentList.Add(guardJudgment);

    // 4. ActionSelectorは常に回る
    selectionResult = actionSelector.ProcessFrame(judgmentList, frameState);
}

void ExecutionPhase(float deltaTime)
{
    // 5. 選択されたアクションに遷移
    if (selectionResult.TryGetRequested(category, out var selected))
    {
        machine.ForceTransitionTo(selected.ActionId);
    }

    // 6. モーションを更新
    machine.Update(deltaTime);
}
```

---

## 衝突システム

### 衝突ボリュームの追加

```csharp
// ボリュームタイプはゲーム側で定義（int値）
const int Hurtbox = 1;

// EntityContextに衝突ボリュームを追加
if (_registry.TryGetContext(handle, out var context))
{
    var volume = new CollisionVolume(
        owner: handle,
        shape: new SphereShape(1.0f),
        filter: new CollisionFilter(layer: 1, mask: 2),
        volumeType: Hurtbox);

    context.CollisionVolumes.Add(volume);
}
```

### ボリュームタイプ

ボリュームタイプはゲーム側で`int`定数として定義します。一般的な用途：

| 値 | 推奨用途 |
|---|------|
| 0 | Hitbox（攻撃判定） |
| 1 | Hurtbox（被ダメージ判定） |
| 2 | Pushbox（押し出し判定） |
| 3 | Trigger（イベントトリガー） |

---

## テストの書き方

### Entityのテスト

```csharp
[Fact]
public void TakeDamage_ShouldReduceHealth()
{
    // Arrange
    var arena = new PlayerArena(
        onSpawn: p => { p.Health = 100; p.Defense = 10; });
    var handle = arena.Create();

    // Act
    handle.TryTakeDamage(30);

    // Assert
    handle.TryGet(out var player);
    Assert.Equal(80, player.Health); // 100 - (30 - 10) = 80
}
```

---

## 次のステップ

1. **アクション定義の追加** - 攻撃、防御、移動などのアクションを定義
2. **衝突フィルターの設計** - ゲームに必要な衝突マトリクスを設計
3. **カスタムダメージ計算** - IDamageCalculatorを実装
4. **エフェクトシステム** - メッセージハンドラと連携したエフェクト生成
5. **ネットワーク対応** - DecisionPhaseの同期設計

詳細は[アーキテクチャ概要](ARCHITECTURE.md)を参照。
