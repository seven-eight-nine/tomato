# ActionExecutionSystem

ActionSelectorで決定された行動を実行し、状態機械を管理するシステム。第四更新（ExecutionPhase）を担当する。

## 設計原則

**ActionSelectorの出力を消費し、アクションの実行状態を管理する。**

- ActionSelectorが「何をするか」を決定
- ActionExecutionSystemが「どう実行するか」を担当
- カテゴリ単位で並列実行可能（Upper/Lower等）

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────┐
│                 ActionExecutionSystem                    │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌─────────────────────────────────────────────────┐   │
│  │            ActionStateMachine<TCategory>         │   │
│  │  - カテゴリ単位のアクション状態管理              │   │
│  │  - アクションの開始/更新/終了                   │   │
│  │  - Executorへのコールバック                     │   │
│  └─────────────────────────────────────────────────┘   │
│                         │                                │
│                         ▼                                │
│  ┌─────────────────────────────────────────────────┐   │
│  │    IExecutableAction<TCategory> (実行中アクション) │   │
│  │  - ActionId, Category                            │   │
│  │  - ElapsedTime/ElapsedFrames                     │   │
│  │  - IsComplete, CanCancel                         │   │
│  │  - MotionData (移動データ)                       │   │
│  │  - GetTransitionableJudgments() (コンボ遷移)     │   │
│  └─────────────────────────────────────────────────┘   │
│                         │                                │
│                         ▼                                │
│  ┌─────────────────────────────────────────────────┐   │
│  │         ActionDefinition<TCategory>              │   │
│  │  - TotalFrames                                   │   │
│  │  - CancelWindow (キャンセル可能フレーム)        │   │
│  │  - HitboxWindow (ヒットボックス発生)            │   │
│  │  - InvincibleWindow (無敵フレーム)              │   │
│  └─────────────────────────────────────────────────┘   │
│                                                          │
└─────────────────────────────────────────────────────────┘
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

// 毎フレーム更新
machine.Update(deltaTime);

// 完了チェック
if (!machine.IsRunning(ActionCategory.Upper))
{
    Console.WriteLine("Action completed");
}
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

### コンボ遷移

```csharp
// Attack1 -> Attack2 への遷移を設定
var attack2Judgment = new SimpleJudgment<ActionCategory>(
    "Attack2", ActionCategory.Upper, ActionPriority.Normal);

var attack1Action = new StandardExecutableAction<ActionCategory>(
    attack1Definition,
    transitionTargets: new[] { attack2Judgment });

machine.StartAction(ActionCategory.Upper, attack1Action);

// フレーム更新
while (!attack1Action.IsComplete)
{
    machine.Update(deltaTime);

    // CancelWindow内であれば遷移可能
    if (attack1Action.CanCancel)
    {
        var transitions = attack1Action.GetTransitionableJudgments();
        var comboResult = engine.ProcessFrame(transitions.ToArray(), state);

        if (comboResult.TryGetRequested(ActionCategory.Upper, out var combo))
        {
            // Attack2に遷移
            var nextDef = registry.Get(combo.ActionId)!;
            machine.StartAction(ActionCategory.Upper,
                new StandardExecutableAction<ActionCategory>(nextDef));
            break;
        }
    }
}
```

### Executorによるカスタム処理

```csharp
public class CharacterExecutor : IActionExecutor<ActionCategory>
{
    private readonly IAnimationController _animation;

    public void OnActionStart(IExecutableAction<ActionCategory> action)
    {
        _animation.Play(action.ActionId);
    }

    public void OnActionUpdate(IExecutableAction<ActionCategory> action, float deltaTime)
    {
        // モーションデータがあれば適用
        if (action.MotionData != null)
        {
            var frame = action.MotionData.Evaluate(action.ElapsedTime);
            ApplyRootMotion(frame.DeltaPosition);
        }
    }

    public void OnActionEnd(IExecutableAction<ActionCategory> action)
    {
        // クリーンアップ
    }
}

// Executorを登録
machine.RegisterExecutor(ActionCategory.Upper, new CharacterExecutor());
```

## 処理フロー

1. `ActionSelector.ProcessFrame()` でアクションを選択
2. `ActionDefinitionRegistry.Get()` で定義を取得
3. `StandardExecutableAction` を作成
4. `ActionStateMachine.StartAction()` でアクション開始
5. 毎フレーム `ActionStateMachine.Update()` を呼び出し
6. `IsComplete` で完了判定
7. `CanCancel` でコンボ遷移可能か判定

## テスト

```bash
dotnet test libs/ActionExecutionSystem/ActionExecutionSystem.Tests/
```

現在のテスト数: 46

## 依存関係

- ActionSelector - アクション選択システム（IRunningAction, IActionJudgment）

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
│   └── Frame/
│       └── FrameWindow.cs
└── ActionExecutionSystem.Tests/
    ├── ActionExecutionSystem.Tests.csproj
    ├── FrameWindowTests.cs
    ├── ActionDefinitionTests.cs
    ├── ExecutableActionTests.cs
    ├── ActionStateMachineTests.cs
    ├── MotionDataTests.cs
    └── ActionSelectorIntegrationTests.cs
```

## ライセンス

MIT License
