# ActionExecutionSystem

ActionSelectorで決定された行動を実行し、状態機械を管理するシステム。

## 設計原則

**ActionSelectorの出力を消費し、アクションの実行状態を管理する。**

- ActionSelectorが「何をするか」を決定
- ActionExecutionSystemが「どう実行するか」を担当
- カテゴリ単位で並列実行可能（Upper/Lower等）

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────────────┐
│                   ActionExecutionSystem                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │           ActionStateMachine<TCategory>                  │   │
│  │  - カテゴリ単位のアクション状態管理                      │   │
│  │  - アクションの開始/更新/終了                            │   │
│  │  - Executorへのコールバック                              │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    MotionGraph                           │   │
│  │  - HierarchicalStateMachineベースのモーション管理        │   │
│  │  - フレームベースの状態遷移                              │   │
│  │  - TimelineSystemによるフレームイベント                  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## コンポーネント

### コア型

- `FrameWindow` - フレーム範囲（開始/終了）
- `ActionDefinition<TCategory>` - アクション定義データ
- `ActionDefinitionRegistry<TCategory>` - 定義の登録/取得

### 実行

- `IExecutableAction<TCategory>` - 実行中アクションインターフェース
- `StandardExecutableAction<TCategory>` - 標準実装
- `ActionStateMachine<TCategory>` - Entity単位の状態機械

### Executor

- `IActionExecutor<TCategory>` - アクション実行ロジック

### Motion

- `IMotionData` - モーションデータインターフェース
- `MotionFrame` - 1フレームのモーション
- `LinearMotionData` - 線形補間モーション
- `ConstantMotionData` - 一定値モーション

### MotionGraph

HierarchicalStateMachineをベースにしたフレームベースのモーション状態管理。

- `MotionStateMachine` - モーション専用のステートマシン
- `MotionState` - モーション状態（IState<MotionContext>実装）
- `MotionContext` - モーション状態管理用コンテキスト
- `MotionDefinition` - モーション定義（ID、フレーム数、タイムライン）
- `MotionTransitionCondition` - 遷移条件ユーティリティ
- `IMotionExecutor` - モーション実行コールバック

## 使用例

### 基本的なアクション実行

```csharp
// カテゴリ定義
public enum ActionCategory
{
    Upper,
    Lower,
    Movement
}

// アクション定義を作成
var attackDefinition = new ActionDefinition<ActionCategory>(
    actionId: "Attack1",
    category: ActionCategory.Upper,
    totalFrames: 30,
    cancelWindow: new FrameWindow(15, 25),
    hitboxWindow: new FrameWindow(5, 10));

// レジストリに登録
var registry = new ActionDefinitionRegistry<ActionCategory>();
registry.Register(attackDefinition);

// 状態機械を作成
var machine = new ActionStateMachine<ActionCategory>();

// アクションを開始
var action = new StandardExecutableAction<ActionCategory>(attackDefinition);
machine.StartAction(ActionCategory.Upper, action);

// 毎tick更新
machine.Tick(deltaTicks);

// 完了チェック
if (!machine.IsRunning(ActionCategory.Upper))
{
    Console.WriteLine("Action completed");
}
```

### MotionGraphによるモーション管理

MotionGraphはフレームベースのモーション状態を管理する。アクション選択の判断はActionSelectorの責務であり、MotionGraphはキャンセル可能かどうかの情報（ElapsedFrames等）を提供するだけ。ActionSelectorは常に回り、キャンセル可能な場合はコンボ系ジャッジメントを追加、ダメージ遷移等は常に追加という形でジャッジメントリストを構築する。

```csharp
using Tomato.ActionExecutionSystem.MotionGraph;
using Tomato.HierarchicalStateMachine;
using Tomato.TimelineSystem;

// モーション定義を作成
var idleDefinition = new MotionDefinition(
    motionId: "Idle",
    totalFrames: 60,
    timeline: new Sequence());

var walkDefinition = new MotionDefinition(
    motionId: "Walk",
    totalFrames: 30,
    timeline: new Sequence());

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

// 毎tick更新
machine.Tick(deltaTicks);

// 遷移を試行
if (machine.TryTransitionTo("Walk"))
{
    Console.WriteLine("Transitioned to Walk");
}

// 現在のモーション完了チェック
if (machine.IsCurrentMotionComplete())
{
    Console.WriteLine("Motion completed");
}
```

### 遷移条件の組み合わせ

```csharp
// フレーム条件
var afterFrame30 = MotionTransitionCondition.AfterFrame(30);

// フレーム範囲条件
var inRange = MotionTransitionCondition.InFrameRange(10, 50);

// モーション完了条件
var isComplete = MotionTransitionCondition.IsComplete();

// 条件の組み合わせ
var combined = MotionTransitionCondition.And(
    MotionTransitionCondition.AfterFrame(20),
    MotionTransitionCondition.InFrameRange(20, 40));

var either = MotionTransitionCondition.Or(
    MotionTransitionCondition.IsComplete(),
    MotionTransitionCondition.AfterFrame(60));
```

### IMotionExecutorによるコールバック

```csharp
public class CharacterMotionExecutor : IMotionExecutor
{
    private readonly IAnimationController _animation;

    public CharacterMotionExecutor(IAnimationController animation)
    {
        _animation = animation;
    }

    public void OnMotionStart(string motionId)
    {
        _animation.Play(motionId);
    }

    public void OnMotionTick(string motionId, int elapsedTicks, int deltaTicks)
    {
        // tickごとの処理
    }

    public void OnMotionEnd(string motionId)
    {
        // クリーンアップ
    }
}

// Executorを設定
var executor = new CharacterMotionExecutor(animationController);
var machine = new MotionStateMachine(graph, executor);
// または
machine.SetExecutor(executor);
```

### ActionSelectorとの統合

```csharp
// ActionSelectorで選択
var engine = new SelectionEngine<ActionCategory>();
var result = engine.ProcessFrame(judgments, state);

if (result.TryGetRequested(ActionCategory.Upper, out var requested))
{
    // 選択されたアクションの定義を取得
    var definition = registry.Get(requested.ActionId);
    if (definition != null)
    {
        // アクションを実行
        var action = new StandardExecutableAction<ActionCategory>(definition);
        machine.StartAction(ActionCategory.Upper, action);
    }
}
```

## 処理フロー

1. `ActionSelector.ProcessFrame()` でアクションを選択
2. `ActionDefinitionRegistry.Get()` で定義を取得
3. `StandardExecutableAction` を作成
4. `ActionStateMachine.StartAction()` でアクション開始
5. 毎tick `ActionStateMachine.Tick()` を呼び出し
6. `IsComplete` で完了判定
7. `CanCancel` でコンボ遷移可能か判定

## テスト

```bash
dotnet test libs/systems/ActionExecutionSystem/ActionExecutionSystem.Tests/
```

現在のテスト数: 81

## 依存関係

- ActionSelector - アクション選択システム
- HierarchicalStateMachine - 階層型ステートマシン（MotionGraph用）
- TimelineSystem - フレームベースのイベント管理（MotionGraph用）

## ディレクトリ構造

```
ActionExecutionSystem/
├── README.md
├── ActionExecutionSystem.Core/
│   ├── ActionExecutionSystem.Core.csproj
│   ├── StateMachine/
│   │   └── ActionStateMachine.cs
│   ├── Action/
│   │   ├── IExecutableAction.cs
│   │   ├── StandardExecutableAction.cs
│   │   ├── ActionDefinition.cs
│   │   └── ActionDefinitionRegistry.cs
│   ├── Executor/
│   │   └── IActionExecutor.cs
│   ├── Motion/
│   │   ├── IMotionData.cs
│   │   ├── MotionFrame.cs
│   │   ├── LinearMotionData.cs
│   │   └── ConstantMotionData.cs
│   ├── Frame/
│   │   └── FrameWindow.cs
│   └── MotionGraph/
│       ├── IMotionExecutor.cs
│       ├── MotionContext.cs
│       ├── MotionDefinition.cs
│       ├── MotionState.cs
│       ├── MotionStateMachine.cs
│       └── MotionTransitionCondition.cs
└── ActionExecutionSystem.Tests/
    ├── ActionExecutionSystem.Tests.csproj
    ├── FrameWindowTests.cs
    ├── ActionDefinitionTests.cs
    ├── ExecutableActionTests.cs
    ├── ActionStateMachineTests.cs
    ├── MotionDataTests.cs
    ├── ActionSelectorIntegrationTests.cs
    └── MotionGraph/
        ├── MotionContextTests.cs
        ├── MotionStateTests.cs
        ├── MotionStateMachineTests.cs
        └── MotionTransitionConditionTests.cs
```

## ライセンス

MIT License
