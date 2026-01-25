# FlowTree 設計書

コールスタック付き汎用フロー制御ライブラリの詳細設計ドキュメント。

namespace: `Tomato.FlowTree`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [状態管理](#状態管理)
5. [ノードリファレンス](#ノードリファレンス)
6. [サブツリーとコールスタック](#サブツリーとコールスタック)
7. [再帰の仕組み](#再帰の仕組み)
8. [DSL詳細](#dsl詳細)
9. [サンプル集：AI行動選択](#サンプル集ai行動選択)
10. [サンプル集：非同期処理](#サンプル集非同期処理)
11. [サンプル集：UIフロー](#サンプル集uiフロー)
12. [サンプル集：シーン遷移](#サンプル集シーン遷移)
13. [サンプル集：メニュー状態遷移](#サンプル集メニュー状態遷移)
14. [サンプル集：セーブ/ロードフロー](#サンプル集セーブロードフロー)
15. [サンプル集：マッチメイキング](#サンプル集マッチメイキング)
16. [サンプル集：クエスト/ミッション進行](#サンプル集クエストミッション進行)
17. [サンプル集：アニメーションシーケンス](#サンプル集アニメーションシーケンス)
18. [パフォーマンス](#パフォーマンス)
19. [注意事項](#注意事項)
20. [ディレクトリ構造](#ディレクトリ構造)

---

## クイックスタート

### 1. ツリーを定義

```csharp
using Tomato.FlowTree;

// 状態クラス（IFlowState必須）
public class PatrolState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int CurrentWaypoint { get; set; }
    public bool IsMoving { get; set; }
}

// DSLで定義（推奨）
var state = new PatrolState();
var patrolTree = new FlowTree("Patrol");
patrolTree.Build(state)
    .Sequence()
        .Action(s => GetNextWaypoint(s))
        .Action(s => MoveToWaypoint(s))
        .Wait(2.0f)  // 2秒待機
    .End()
    .Complete();
```

### 2. 毎フレーム実行

```csharp
void Update(float deltaTime)
{
    var status = patrolTree.Tick(deltaTime);

    if (status == NodeStatus.Success)
    {
        patrolTree.Reset();  // 完了後にリセットして再開
    }
}
```

### 3. サブツリーを使う場合

```csharp
// コールスタックを設定
patrolTree.WithCallStack(new FlowCallStack(32));

// 最大呼び出し深度を制限（オプション）
patrolTree.WithMaxCallDepth(16);
```

---

## 用語定義

### 基本概念

| 用語 | 英語 | 説明 |
|------|------|------|
| **FlowTree** | - | ツリー構造で処理フローを定義したもの。入れ物と中身が分離している |
| **ノード** | Node | ツリーを構成する要素。Tick()で評価される |
| **Tick** | - | ノードを1回評価すること |
| **状態** | State | ユーザー定義の状態オブジェクト |

### FlowTreeの構造

FlowTreeは「入れ物（コンテナ）」と「中身（ルートノード）」が分離している:

```csharp
var tree = new FlowTree();  // 入れ物を作成（中身は空）

tree.Build()
    .Sequence()
        .Action(...)
        .SubTree(tree)  // ビルド中でも自己参照可能
    .End()
    .Complete();        // 中身を設定
```

この設計により:
- ビルド中に自分自身を参照できる（自己再帰）
- 複数のツリーが互いを参照できる（相互再帰）
- 実行中に中身を差し替え可能（同一Tick中は反映されない）

### ノードの結果（NodeStatus）

| 値 | 説明 |
|----|------|
| **Success** | 処理が成功した |
| **Failure** | 処理が失敗した |
| **Running** | 処理中。次のTickで継続 |

### ノードの分類

| 分類 | 説明 |
|------|------|
| **Composite** | 複数の子ノードを持つ。子の結果を集約して返す |
| **Decorator** | 1つの子ノードを持つ。子の結果を変換して返す |
| **Leaf** | 子を持たない末端ノード。実際の処理を行う |

---

## 設計哲学

### 原則1: 汎用フロー制御

ビヘイビアツリーのパターンを採用しつつ、AI専用ではなく汎用的なフロー制御として設計。

**適用例:**
- AI行動選択
- 非同期処理のオーケストレーション
- UIダイアログフロー
- ゲームローディング
- チュートリアル進行

### 原則2: 動的構造

SubTreeNodeとコールスタックにより、実行時にツリー構造を動的に組み上げられる。

```
静的なツリー構造:
Root
├─ Sequence
│   ├─ Action1
│   └─ SubTree(PatrolTree)  ← ここで別ツリーに分岐
│       └─ PatrolTree の内容が展開される

コールスタック:
[0] RootTree
[1] PatrolTree  ← 現在の実行位置
```

### 原則3: 再帰のサポート

自己再帰・相互再帰をネイティブにサポート。各ノードは呼び出し深度ごとに独立した状態を持つ。

### 原則4: 低GC

- ノードの状態はListで管理（初期容量4、必要に応じて拡張）
- 通常使用（再帰なし）ではGCは発生しない
- 深い再帰時のみ、List拡張によるGCが発生

### 原則5: シンプルなAPI

ユーザーのラムダ式には状態オブジェクトのみを渡す。フレームワーク内部の詳細（FlowContext等）は公開しない。

```csharp
// 状態を受け取る
.Action(s => { s.Counter++; return NodeStatus.Success; })

// 状態を使わない
.Action(static () => NodeStatus.Success)
```

---

## 状態管理

### 状態オブジェクトの定義

状態クラスは `IFlowState` インターフェースを実装する必要がある:

```csharp
public class EnemyState : IFlowState
{
    public IFlowState? Parent { get; set; }  // サブツリーで親State参照に使用
    public float Health { get; set; } = 100f;
    public bool HasTarget { get; set; }
    public Vector3 TargetPosition { get; set; }
    public int AttackCount { get; set; }
}
```

### 状態の設定

`Build(state)` で状態オブジェクトを渡す:

```csharp
var state = new EnemyState();
var tree = new FlowTree();

tree.Build(state)
    .Sequence()
        .Condition(s => s.HasTarget)
        .Action(s =>
        {
            s.AttackCount++;
            return NodeStatus.Success;
        })
    .End()
    .Complete();
```

### ステートレスなツリー

状態が不要な場合は `Build()` を使用:

```csharp
var tree = new FlowTree();
tree.Build()
    .Sequence()
        .Do(static () => DoSomething())
        .Wait(1.0f)
    .End()
    .Complete();
```

### 型付きビルダーでのステートレスノード

型付きビルダー内でも状態を使わないノードを追加できる:

```csharp
tree.Build(state)
    .Sequence()
        .Action(s => ProcessState(s))           // 状態を使う
        .Action(static () => NodeStatus.Success)  // 状態を使わない
    .End()
    .Complete();
```

---

## ノードリファレンス

### Composite Nodes（複合ノード）

#### SequenceNode

全ての子ノードが成功するまで順次実行。いずれかが失敗したら即座にFailureを返す。

```csharp
new SequenceNode(
    new ActionNode<MyState>(s => Step1(s)),
    new ActionNode<MyState>(s => Step2(s)),
    new ActionNode<MyState>(s => Step3(s))
)
```

| 子の結果 | SequenceNodeの動作 |
|---------|-------------------|
| Success | 次の子へ進む。全て成功したらSuccess |
| Failure | 即座にFailureを返す |
| Running | Runningを返す。次のTickで継続 |

#### SelectorNode

最初に成功した子ノードの結果を返す。全て失敗したらFailure。

```csharp
new SelectorNode(
    new ActionNode<MyState>(s => TryOption1(s)),  // 失敗したら次へ
    new ActionNode<MyState>(s => TryOption2(s)),  // 失敗したら次へ
    new ActionNode<MyState>(s => Fallback(s))     // 最後の手段
)
```

| 子の結果 | SelectorNodeの動作 |
|---------|-------------------|
| Success | 即座にSuccessを返す |
| Failure | 次の子へ進む。全て失敗したらFailure |
| Running | Runningを返す。次のTickで継続 |

#### ParallelNode

全ての子ノードを並列に評価する。

```csharp
// RequireAll: 全成功でSuccess、1つでも失敗でFailure
new ParallelNode(ParallelPolicy.RequireAll,
    new ActionNode<MyState>(s => Task1(s)),
    new ActionNode<MyState>(s => Task2(s))
)

// RequireOne: 1つ成功でSuccess、全失敗でFailure
new ParallelNode(ParallelPolicy.RequireOne,
    new ActionNode<MyState>(s => Task1(s)),
    new ActionNode<MyState>(s => Task2(s))
)
```

#### RaceNode

最初に完了した子ノードの結果を採用する（WaitAny相当）。

```csharp
var attackTree = new FlowTree("Attack");
var patrolTree = new FlowTree("Patrol");
// ... build trees ...

new RaceNode(
    new SubTreeNode(attackTree),
    new TimeoutNode(5.0f, new SubTreeNode(patrolTree))
)
```

#### JoinNode

全ての子ノードが完了するまで待機する（WaitAll相当）。

```csharp
// RequireAll: 全成功でSuccess（デフォルト）
new JoinNode(
    new ActionNode<MyState>(s => LoadTextures(s)),
    new ActionNode<MyState>(s => LoadSounds(s)),
    new ActionNode<MyState>(s => LoadData(s))
)
```

#### RandomSelectorNode

子ノードをランダムに1つ選択して実行する。

```csharp
new RandomSelectorNode(
    new ActionNode<MyState>(s => Attack1(s)),
    new ActionNode<MyState>(s => Attack2(s)),
    new ActionNode<MyState>(s => Attack3(s))
)
```

#### ShuffledSelectorNode

全選択肢を一巡するまで同じものを選ばない（シャッフル再生）。全ての子を実行したら再シャッフル。

```csharp
// ダイアログをランダム順で再生（重複なし）
new ShuffledSelectorNode(
    new ActionNode<MyState>(s => PlayDialogue1(s)),
    new ActionNode<MyState>(s => PlayDialogue2(s)),
    new ActionNode<MyState>(s => PlayDialogue3(s))
)

// シード指定版
new ShuffledSelectorNode(42, /* children */)
```

| 動作 | 説明 |
|-----|------|
| 初回 | インデックス配列をシャッフル |
| 実行 | シャッフル順に子を実行 |
| 一巡後 | 再シャッフルして最初から |

#### WeightedRandomSelectorNode

各子ノードの重みに基づいて確率的に選択する。重みは相対値（合計で正規化）。

```csharp
// 重みとノードをタプルで指定
new WeightedRandomSelectorNode(
    (3.0f, new ActionNode<MyState>(s => CommonAction(s))),   // 75%
    (1.0f, new ActionNode<MyState>(s => RareAction(s)))      // 25%
)

// シード指定版
new WeightedRandomSelectorNode(42, /* weighted children */)
```

| 重み | 確率（4.0合計の場合） |
|------|---------------------|
| 3.0 | 75% |
| 1.0 | 25% |
| 0.0 | 選択されない |

#### RoundRobinSelectorNode

0→1→2→0→... と順番に選択する。ツリー全体で選択位置を保持。

```csharp
// 巡回パトロール
new RoundRobinSelectorNode(
    new ActionNode<MyState>(s => PatrolPointA(s)),
    new ActionNode<MyState>(s => PatrolPointB(s)),
    new ActionNode<MyState>(s => PatrolPointC(s))
)
```

| 呼び出し | 実行される子 |
|---------|------------|
| 1回目 | 子0 |
| 2回目 | 子1 |
| 3回目 | 子2 |
| 4回目 | 子0（ループ） |

**注意**: Reset()を呼んでも選択位置はリセットされない（意図的な設計）。

---

### Decorator Nodes（装飾ノード）

#### InverterNode

子の結果を反転する。

```csharp
// Success → Failure, Failure → Success
new InverterNode(
    new ConditionNode<MyState>(s => s.IsEnemyVisible)
)
```

#### SucceederNode / FailerNode

常に指定した結果を返す。

```csharp
// 子が何を返してもSuccess
new SucceederNode(
    new ActionNode<MyState>(s => TryOptionalAction(s))
)

// 子が何を返してもFailure
new FailerNode(
    new ActionNode<MyState>(s => DoSomething(s))
)
```

#### RepeatNode

子を指定回数繰り返す。

```csharp
// 3回繰り返す
new RepeatNode(3,
    new ActionNode<MyState>(s => DoSomething(s))
)
```

#### RepeatUntilFailNode / RepeatUntilSuccessNode

条件を満たすまで繰り返す。

```csharp
// 失敗するまで繰り返す
new RepeatUntilFailNode(
    new ActionNode<MyState>(s => TryAction(s))
)

// 成功するまで繰り返す
new RepeatUntilSuccessNode(
    new ActionNode<MyState>(s => TryAction(s))
)
```

#### RetryNode

失敗時に指定回数リトライする。

```csharp
// 最大3回リトライ
new RetryNode(3,
    new ActionNode<MyState>(s => TryConnect(s))
)
```

#### TimeoutNode

指定時間内に完了しなければFailure。

```csharp
// 5秒でタイムアウト
new TimeoutNode(5.0f,
    new ActionNode<MyState>(s => LongRunningTask(s))
)
```

#### DelayNode

指定時間待機してから子を実行する。

```csharp
// 1秒待ってから実行
new DelayNode(1.0f,
    new ActionNode<MyState>(s => DoAfterDelay(s))
)
```

#### GuardNode

条件を満たした場合のみ子を実行する。

```csharp
// 条件を満たさなければ即座にFailure
new GuardNode<MyState>(
    s => s.HasTarget,
    new SubTreeNode(attackTree)
)
```

#### EventNode

ノードの開始/終了時にイベントを発火する。

```csharp
// ステートレス版
new EventNode(
    new ActionNode(static () => DoSomething()),
    onEnter: () => { Console.WriteLine("処理開始"); },
    onExit: result => { Console.WriteLine($"処理終了: {result}"); }
)

// 状態付き版
new EventNode<MyState>(
    new ActionNode(static () => DoSomething()),
    onEnter: s => { s.StartTime = DateTime.Now; },
    onExit: (s, result) => { s.EndTime = DateTime.Now; }
)

// DSLで使用（Event()の次のノードに自動適用）
tree.Build(state)
    .Event(
        onEnter: s => { s.IsProcessing = true; },
        onExit: (s, result) => { s.IsProcessing = false; })
    .Sequence()
        .Action(s => DoSomething(s))
    .End()
    .Complete();
```

| イベント | 発火タイミング |
|---------|--------------|
| onEnter | 初回Tick時のみ |
| onExit | Success/Failureになった時のみ（Running中は発火しない） |

**デリゲート型**:
```csharp
// ステートレス
public delegate void FlowEventHandler();
public delegate void FlowExitEventHandler(NodeStatus result);

// 状態付き
public delegate void FlowEventHandler<in T>(T state) where T : class;
public delegate void FlowExitEventHandler<in T>(T state, NodeStatus result) where T : class;
```

---

### Leaf Nodes（葉ノード）

#### ActionNode

アクションを実行する。

```csharp
// ステートレス
new ActionNode(static () =>
{
    DoSomething();
    return NodeStatus.Success;
})

// 状態付き
new ActionNode<MyState>(s =>
{
    s.Counter++;
    return IsComplete(s) ? NodeStatus.Success : NodeStatus.Running;
})
```

#### ConditionNode

条件を評価する。trueならSuccess、falseならFailure。

```csharp
// ステートレス
new ConditionNode(static () => SomeGlobalCondition())

// 状態付き
new ConditionNode<MyState>(s => s.HasTarget)
```

#### SubTreeNode

別のツリーを呼び出す。静的参照・動的選択・State注入をサポート。

```csharp
// 静的参照
var attackTree = new FlowTree("Attack");
// ... build attackTree ...
new SubTreeNode(attackTree)

// 動的選択（ラムダで実行時にツリーを選択）
new SubTreeNode<MyState>(s => s.Difficulty > 5 ? hardTree : easyTree)

// State注入（サブツリーに専用Stateを渡す）
new SubTreeNode<ParentState, ChildState>(
    childTree,
    parentState => new ChildState { Value = parentState.SomeValue }
)
```

| 状態 | 動作 |
|-----|------|
| 初回/完了後 | プロバイダを呼び出して新しいツリーを取得 |
| Running中 | 同じツリーを継続使用 |
| null返却 | Failureを返す |

**State注入**: サブツリーに専用のStateを渡すことで、親子間で異なるStateを使える。
子のStateにはParentプロパティで親のStateへの参照が自動設定される。

```csharp
// 親State
public class ParentState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int TotalScore { get; set; }
}

// 子State
public class ChildState : IFlowState
{
    public IFlowState? Parent { get; set; }  // ParentStateへの参照が自動設定
    public int LocalScore { get; set; }
}

var childTree = new FlowTree();
childTree.Build(new ChildState())
    .Action(s =>
    {
        s.LocalScore = 100;
        var parent = (ParentState)s.Parent!;  // 親Stateにアクセス
        parent.TotalScore += s.LocalScore;
        return NodeStatus.Success;
    })
    .Complete();

var mainTree = new FlowTree();
mainTree
    .WithCallStack(new FlowCallStack(32))
    .Build(new ParentState())
    .SubTree<ChildState>(childTree, p => new ChildState())  // State注入
    .Complete();
```

**デリゲート型**:
```csharp
public delegate FlowTree? FlowTreeProvider();
public delegate FlowTree? FlowTreeProvider<in T>(T state) where T : class, IFlowState;
public delegate TChild FlowStateProvider<in TParent, out TChild>(TParent parentState)
    where TParent : class, IFlowState
    where TChild : class, IFlowState;
```

#### WaitNode

指定時間または条件で待機する。

```csharp
// 時間待機
new WaitNode(2.0f)  // 2秒待機

// 条件待機（条件がtrueになるまでRunning）
new WaitUntilNode<MyState>(s => s.IsReady)
```

#### YieldNode

1TickだけRunningを返す。

```csharp
new YieldNode()  // 次のフレームまで待機
```

#### SuccessNode / FailureNode

即座に結果を返す。

```csharp
SuccessNode.Instance  // 即座にSuccess
FailureNode.Instance  // 即座にFailure
```

---

## サブツリーとコールスタック

### 基本的なサブツリー呼び出し

FlowTree参照を直接指定してサブツリーを呼び出す。

```csharp
var patrolTree = new FlowTree("Patrol");
var attackTree = new FlowTree("Attack");

patrolTree.Build(state)
    // ... patrol logic ...
    .Complete();

attackTree.Build(state)
    // ... attack logic ...
    .Complete();

var aiTree = new FlowTree("AI");
aiTree
    .WithCallStack(new FlowCallStack(32))
    .Build(state)
    .Selector()
        .Guard(s => s.HasTarget,
            new SubTreeNode(attackTree))  // attackTreeを呼び出し
        .SubTree(patrolTree)              // patrolTreeを呼び出し
    .End()
    .Complete();
```

### コールスタック

SubTreeNodeを通じてサブツリーを呼び出すと、コールスタックに記録される。

```csharp
// コールスタックを設定
var callStack = new FlowCallStack(32);  // 最大深度32
tree.WithCallStack(callStack);

// 実行
tree.Tick(0.016f);
```

### スタックオーバーフロー保護

`WithMaxCallDepth()`で最大深度を制限できる。超えるとSubTreeNodeはFailureを返す。

```csharp
tree
    .WithCallStack(new FlowCallStack(32))
    .WithMaxCallDepth(8);  // 最大8段まで
```

---

## 再帰の仕組み

### 自己再帰

FlowTree参照を直接使うことで、自然に自己再帰が書ける。

```csharp
public class CounterState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int Counter { get; set; }
    public string Log { get; set; } = "";
}

var state = new CounterState { Counter = 3 };

// カウントダウン再帰
var countdownTree = new FlowTree("Countdown");
countdownTree
    .WithCallStack(new FlowCallStack(32))
    .Build(state)
    .Selector()
        // 終了条件: counter <= 0
        .Sequence()
            .Condition(s => s.Counter <= 0)
            .Action(s =>
            {
                s.Log += "Done!";
                return NodeStatus.Success;
            })
        .End()
        // 再帰: counter-- して自己呼び出し
        .Sequence()
            .Action(s =>
            {
                s.Log += s.Counter.ToString();
                s.Counter--;
                return NodeStatus.Success;
            })
            .SubTree(countdownTree)  // 自己呼び出し
        .End()
    .End()
    .Complete();

countdownTree.Tick(0.016f);
// state.Log == "321Done!"
```

### 相互再帰

複数のツリーが互いを参照することも可能。

```csharp
public class PingPongState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int Counter { get; set; }
    public string Log { get; set; } = "";
}

var state = new PingPongState { Counter = 6 };

var treeA = new FlowTree("A");
var treeB = new FlowTree("B");

// TreeA: "A"を記録してカウンタデクリメント、counter > 0ならTreeBを呼ぶ
treeA
    .WithCallStack(new FlowCallStack(32))
    .Build(state)
    .Sequence()
        .Action(s =>
        {
            s.Log += "A";
            s.Counter--;
            return NodeStatus.Success;
        })
        .Selector()
            .Sequence()
                .Condition(s => s.Counter > 0)
                .SubTree(treeB)  // TreeBを呼び出し
            .End()
            .Success()
        .End()
    .End()
    .Complete();

// TreeB: "B"を記録してカウンタデクリメント、counter > 0ならTreeAを呼ぶ
treeB.Build(state)
    .Sequence()
        .Action(s =>
        {
            s.Log += "B";
            s.Counter--;
            return NodeStatus.Success;
        })
        .Selector()
            .Sequence()
                .Condition(s => s.Counter > 0)
                .SubTree(treeA)  // TreeAを呼び出し
            .End()
            .Success()
        .End()
    .End()
    .Complete();

treeA.Tick(0.016f);
// state.Log == "ABABAB"
```

### 深度ごとの状態管理

再帰呼び出し時、各ノードは呼び出し深度ごとに独立した状態を保持する。
これにより、同じノードインスタンスが異なる深度で同時に正しく動作する。

```
呼び出しスタック:
[depth 0] countdownTree (Sequenceの進行状態: 子1実行中)
[depth 1] countdownTree (Sequenceの進行状態: 子0実行中)  ← 状態が独立
[depth 2] countdownTree (Sequenceの進行状態: 子1実行中)  ← 状態が独立
```

---

## DSL詳細

### ビルダーパターン

```csharp
var tree = new FlowTree("MyTree");
tree.Build(state)
    .Sequence()
        .Action(s => DoAction(s))
        .Selector()
            .Condition(s => SomeCondition(s))
            .Action(s => Alternative(s))
        .End()
    .End()
    .Complete();
```

### 短縮表記（static using）

```csharp
using static Tomato.FlowTree.Flow;

// Tree() でFlowTreeを作成
var tree = Tree("MyTree");
tree.Build()
    .Sequence()
        .SubTree(otherTree)
    .End()
    .Complete();

// ノードを直接作成
var seq = Sequence(
    Action(static () => Step1()),
    Action(static () => Step2())
);

var sel = Selector(
    Condition(static () => Check1()),
    Action(static () => Fallback())
);
```

### デコレータの連鎖

```csharp
var tree = new FlowTree();
tree.Build(state)
    .Retry(3)               // 最大3回リトライ
        .Timeout(5.0f)      // 5秒でタイムアウト
            .Sequence()
                .Do(s => TryConnect(s))
                .Do(s => FetchData(s))
            .End()
        .End()
    .End()
    .Complete();
```

### DSLメソッド一覧

#### Composite

| メソッド | 説明 |
|---------|------|
| `Sequence()` | 順次実行を開始 |
| `Selector()` | フォールバック選択を開始 |
| `Parallel(policy)` | 並列実行を開始 |
| `Race()` | 最初の完了採用を開始 |
| `Join(policy)` | 全完了待機を開始 |
| `RandomSelector()` | ランダム選択を開始 |
| `ShuffledSelector()` | シャッフル選択を開始 |
| `WeightedRandomSelector()` | 重み付きランダム選択を開始 |
| `RoundRobin()` | 順番選択を開始 |
| `End()` | Composite/Decoratorノードを終了 |

**WeightedRandomSelector専用**:
```csharp
.WeightedRandomSelector()
    .Weighted(3.0f, node1)  // 重み3
    .Weighted(1.0f, node2)  // 重み1
.End()
```

#### Decorator（スコープベース）

デコレータはCompositeと同様にスコープを開き、End()で閉じる。子ノードは1つだけ。

| メソッド | 説明 |
|---------|------|
| `Retry(maxRetries)` | 失敗時にリトライ |
| `Timeout(seconds)` | タイムアウト制限 |
| `Delay(seconds)` | 遅延実行 |
| `Repeat(count)` | 指定回数繰り返し |
| `RepeatUntilFail()` | Failureまで繰り返し |
| `RepeatUntilSuccess()` | Successまで繰り返し |
| `Inverter()` | 結果を反転 |
| `Succeeder()` | 常にSuccess |
| `Failer()` | 常にFailure |
| `Guard(condition, child)` | 条件付き実行 |
| `Event(onEnter, onExit)` | 次のノードにイベント発火を適用 |

**例**:
```csharp
// 3回リトライ、各試行は5秒でタイムアウト
.Retry(3)
    .Timeout(5.0f)
        .Sequence()
            .Do(s => TryConnect(s))
            .Wait(s => s.IsConnected)
        .End()
    .End()
.End()

// 失敗するまで繰り返し
.RepeatUntilFail()
    .Sequence()
        .SubTree(GameLoop)
        .Condition(s => s.WantsToContinue)
    .End()
.End()

// 結果を反転
.Inverter()
    .Condition(s => s.IsGameOver)
.End()
```

#### Leaf

| メソッド | 説明 |
|---------|------|
| `Action(action)` | アクションを実行（NodeStatusを返す） |
| `Do(action)` | voidアクションを実行してSuccessを返す |
| `Condition(condition)` | 条件を評価 |
| `SubTree(tree)` | サブツリーを呼び出し |
| `SubTree(provider)` | 動的にサブツリーを選択 |
| `SubTree<TChild>(tree, stateProvider)` | State注入付きサブツリー |
| `Wait(seconds)` | 指定時間待機 |
| `Wait(condition)` | 条件が満たされるまで待機 |
| `Yield()` | 1Tick待機 |
| `Success()` | 即座にSuccess |
| `Failure()` | 即座にFailure |
| `Node(node)` | 任意のノードを追加 |

---

## サンプル集：AI行動選択

### 敵AI

```csharp
public class EnemyState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool IsLowHealth { get; set; }
    public bool HasTarget { get; set; }
    public bool IsTargetInRange { get; set; }
}

public static class EnemyAI
{
    // サブツリー
    private static readonly FlowTree AttackTree = new FlowTree("Attack");
    private static readonly FlowTree ChaseTree = new FlowTree("Chase");
    private static readonly FlowTree PatrolTree = new FlowTree("Patrol");
    private static readonly FlowTree FleeTree = new FlowTree("Flee");

    public static FlowTree CreateMainTree(EnemyState state)
    {
        BuildSubTrees(state);

        var tree = new FlowTree("EnemyAI");
        tree
            .WithCallStack(new FlowCallStack(16))
            .Build(state)
            .Selector()
                // 体力が低ければ逃げる
                .Guard(s => s.IsLowHealth,
                    new SubTreeNode(FleeTree))
                // ターゲットがいる
                .Sequence()
                    .Condition(s => s.HasTarget)
                    .Selector()
                        // 射程内なら攻撃
                        .Guard(s => s.IsTargetInRange,
                            new SubTreeNode(AttackTree))
                        // 射程外なら追跡
                        .SubTree(ChaseTree)
                    .End()
                .End()
                // 何もなければパトロール
                .SubTree(PatrolTree)
            .End()
            .Complete();

        return tree;
    }

    private static void BuildSubTrees(EnemyState state)
    {
        AttackTree.Build(state)
            .Sequence()
                .Action(s => { /* aim */ return NodeStatus.Success; })
                .Action(s => { /* fire */ return NodeStatus.Success; })
            .End()
            .Complete();

        PatrolTree.Build(state)
            .Sequence()
                .Action(s => { /* get waypoint */ return NodeStatus.Success; })
                .Action(s => { /* move to */ return NodeStatus.Running; })
                .Wait(2.0f)
            .End()
            .Complete();

        // ... other subtrees ...
    }
}
```

### ボスAI（フェーズ切り替え）

```csharp
public class BossState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int Phase { get; set; } = 1;
    public float Health { get; set; } = 100f;
}

public static class BossAI
{
    private static readonly FlowTree Phase1Tree = new FlowTree("Phase1");
    private static readonly FlowTree Phase2Tree = new FlowTree("Phase2");
    private static readonly FlowTree Phase3Tree = new FlowTree("Phase3");

    public static FlowTree CreateMainTree(BossState state)
    {
        BuildSubTrees(state);

        var tree = new FlowTree("BossAI");
        tree
            .WithCallStack(new FlowCallStack(16))
            .Build(state)
            .Selector()
                // フェーズごとに分岐
                .Guard(s => s.Phase == 1,
                    new SubTreeNode(Phase1Tree))
                .Guard(s => s.Phase == 2,
                    new SubTreeNode(Phase2Tree))
                .SubTree(Phase3Tree)
            .End()
            .Complete();

        return tree;
    }

    private static void BuildSubTrees(BossState state)
    {
        Phase1Tree.Build(state)
            .Sequence()
                .Action(s => { /* phase 1 attack */ return NodeStatus.Success; })
                .Action(s =>
                {
                    // HPが70%以下でフェーズ2へ
                    if (s.Health <= 70f)
                        s.Phase = 2;
                    return NodeStatus.Success;
                })
            .End()
            .Complete();

        // ... other phases ...
    }
}
```

---

## サンプル集：非同期処理

### ゲームローディング

```csharp
public class LoadingState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool LevelLoaded { get; set; }
    public bool AssetsLoaded { get; set; }
    public bool SystemsInitialized { get; set; }
}

public static FlowTree CreateLoadingFlow(LoadingState state)
{
    var loadLevel = new FlowTree("LoadLevel");
    var loadAssets = new FlowTree("LoadAssets");
    var initializeSystems = new FlowTree("InitializeSystems");

    // 各サブツリーを構築
    loadLevel.Build(state)
        .Wait(s => s.LevelLoaded)
        .Complete();

    loadAssets.Build(state)
        .Wait(s => s.AssetsLoaded)
        .Complete();

    initializeSystems.Build(state)
        .Action(s =>
        {
            InitializeGameSystems();
            s.SystemsInitialized = true;
            return NodeStatus.Success;
        })
        .Complete();

    // メインローディングフロー
    var tree = new FlowTree("GameLoading");
    tree
        .WithCallStack(new FlowCallStack(16))
        .Build(state)
        .Sequence()
            .Do(() => ShowLoadingScreen())
            .Join()  // 全て完了まで待機
                .SubTree(loadLevel)
                .SubTree(loadAssets)
                .SubTree(initializeSystems)
            .End()
            .Do(() => HideLoadingScreen())
        .End()
        .Complete();

    return tree;
}
```

### ネットワークリクエスト with リトライ

```csharp
public class NetworkState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool RequestSent { get; set; }
    public bool ResponseReceived { get; set; }
    public string? Response { get; set; }
}

public static FlowTree CreateNetworkRequestFlow(NetworkState state)
{
    var tree = new FlowTree("NetworkRequest");
    tree.Build(state)
        .Retry(3)               // 最大3回リトライ
            .Timeout(10.0f)     // 10秒でタイムアウト
                .Sequence()
                    .Do(s =>
                    {
                        SendRequest();
                        s.RequestSent = true;
                    })
                    .Wait(s => s.ResponseReceived)
                    .Do(s => ParseResponse(s))
                .End()
            .End()
        .End()
        .Complete();

    return tree;
}
```

### 複数サーバー接続

```csharp
public static FlowTree CreateMultiServerConnectionFlow()
{
    var connectPrimary = new FlowTree("ConnectPrimary");
    var connectSecondary = new FlowTree("ConnectSecondary");
    var connectTertiary = new FlowTree("ConnectTertiary");

    // 各サーバーへの接続を構築
    // ...

    var tree = new FlowTree("MultiServerConnection");
    tree.Build()
        .Race()  // 最初に成功した接続を採用
            .SubTree(connectPrimary)
            .SubTree(connectSecondary)
            .SubTree(connectTertiary)
        .End()
        .Complete();

    return tree;
}
```

---

## サンプル集：UIフロー

### ダイアログフロー

```csharp
public class DialogState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool IsConfirmed { get; set; }
    public bool HasUserInput { get; set; }
}

public static FlowTree CreateDialogFlow(DialogState state)
{
    var confirmBranch = new FlowTree("ConfirmBranch");
    var cancelBranch = new FlowTree("CancelBranch");

    confirmBranch.Build(state)
        .Action(s =>
        {
            ExecuteConfirmAction();
            return NodeStatus.Success;
        })
        .Complete();

    cancelBranch.Build(state)
        .Action(s =>
        {
            ExecuteCancelAction();
            return NodeStatus.Success;
        })
        .Complete();

    var tree = new FlowTree("DialogFlow");
    tree
        .WithCallStack(new FlowCallStack(16))
        .Build(state)
        .Sequence()
            .Do(() => ShowDialog())
            .Wait(s => s.HasUserInput)
            .Selector()
                .Guard(s => s.IsConfirmed,
                    new SubTreeNode(confirmBranch))
                .SubTree(cancelBranch)
            .End()
        .End()
        .Complete();

    return tree;
}
```

### チュートリアルフロー

```csharp
public class TutorialState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool HasPlayerMoved { get; set; }
    public bool HasPlayerAttacked { get; set; }
}

public static FlowTree CreateTutorialFlow(TutorialState state)
{
    var tree = new FlowTree("Tutorial");
    tree.Build(state)
        .Sequence()
            // ステップ1: 移動の説明
            .Sequence()
                .Do(() => ShowTutorialMessage("WASDで移動できます"))
                .Wait(s => s.HasPlayerMoved)
            .End()
            // ステップ2: 攻撃の説明
            .Sequence()
                .Do(() => ShowTutorialMessage("クリックで攻撃できます"))
                .Wait(s => s.HasPlayerAttacked)
            .End()
            // 完了
            .Do(() => ShowTutorialMessage("チュートリアル完了！"))
        .End()
        .Complete();

    return tree;
}
```

---

## サンプル集：シーン遷移

ゲームのシーン遷移はツリー構造で表現できる。従来は状態遷移を個別に実装しがちだが、FlowTreeで一元管理することで見通しが良くなる。

### 基本的なシーン遷移

```csharp
public class SceneState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public string? NextScene { get; set; }
    public bool IsGameOver { get; set; }
    public bool IsQuitRequested { get; set; }
}

public static class SceneFlow
{
    private static readonly FlowTree TitleScene = new FlowTree("Title");
    private static readonly FlowTree GameScene = new FlowTree("Game");
    private static readonly FlowTree ResultScene = new FlowTree("Result");

    public static FlowTree CreateSceneFlow(SceneState state)
    {
        BuildScenes(state);

        var tree = new FlowTree("SceneFlow");
        tree
            .WithCallStack(new FlowCallStack(16))
            .Build(state)
            .Sequence()
                // タイトル画面
                .SubTree(TitleScene)
                // ゲームループ（リトライ可能）
                .RepeatUntilFail()
                    .Sequence()
                        .SubTree(GameScene)
                        .SubTree(ResultScene)
                        // リトライならSuccess、終了ならFailure
                        .Condition(s => !s.IsQuitRequested)
                    .End()
                .End()
                // 終了処理
                .Do(s => SaveGameData())
            .End()
            .Complete();

        return tree;
    }

    private static void BuildScenes(SceneState state)
    {
        // タイトル画面
        TitleScene.Build(state)
            .Sequence()
                .Do(() => ShowTitleScreen())
                .Wait(() => IsStartPressed())  // スタートボタン待機
                .Do(() => HideTitleScreen())
            .End()
            .Complete();

        // ゲーム画面
        GameScene.Build(state)
            .Sequence()
                .Do(() => LoadLevel())
                .Action(s =>
                {
                    // ゲームオーバーまたはクリアまで継続
                    if (s.IsGameOver)
                        return NodeStatus.Success;
                    UpdateGame();
                    return NodeStatus.Running;
                })
            .End()
            .Complete();

        // リザルト画面
        ResultScene.Build(state)
            .Sequence()
                .Do(() => ShowResultScreen())
                .Wait(() => HasResultInput())
            .End()
            .Complete();
    }
}
```

---

## サンプル集：メニュー状態遷移

階層的なメニュー構造もFlowTreeで自然に表現できる。

```csharp
public class MenuState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int SelectedIndex { get; set; }
    public bool IsBackPressed { get; set; }
}

public static class MenuFlow
{
    private static readonly FlowTree MainMenu = new FlowTree("MainMenu");
    private static readonly FlowTree OptionsMenu = new FlowTree("Options");
    private static readonly FlowTree AudioSettings = new FlowTree("Audio");

    public static FlowTree CreateMenuFlow(MenuState state)
    {
        BuildMenus(state);

        var tree = new FlowTree("MenuFlow");
        tree
            .WithCallStack(new FlowCallStack(16))
            .Build(state)
            .SubTree(MainMenu)
            .Complete();

        return tree;
    }

    private static void BuildMenus(MenuState state)
    {
        // メインメニュー
        MainMenu.Build(state)
            .Sequence()
                .Do(() => ShowMainMenu())
                // メニュー操作ループ
                .RepeatUntilFail()
                    .Sequence()
                        // 入力待機
                        .Wait(() => HasMenuInput())
                        // 選択に応じた処理
                        .Selector()
                            // ゲーム開始
                            .Sequence()
                                .Condition(s => s.SelectedIndex == 0)
                                .Action(s =>
                                {
                                    HideMainMenu();
                                    return NodeStatus.Failure; // ループ終了
                                })
                            .End()
                            // オプション
                            .Sequence()
                                .Condition(s => s.SelectedIndex == 1)
                                .SubTree(OptionsMenu)
                            .End()
                            .Success() // その他は継続
                        .End()
                    .End()
                .End()
            .End()
            .Complete();

        // オプションメニュー
        OptionsMenu.Build(state)
            .Sequence()
                .Do(s => { ShowOptionsMenu(); s.IsBackPressed = false; })
                .RepeatUntilSuccess()
                    .Selector()
                        // 戻るボタン
                        .Sequence()
                            .Condition(s => s.IsBackPressed)
                            .Success()
                        .End()
                        // 入力待ち
                        .Action(static () => NodeStatus.Running)
                    .End()
                .End()
                .Do(() => HideOptionsMenu())
            .End()
            .Complete();
    }
}
```

---

## サンプル集：セーブ/ロードフロー

```csharp
public class SaveState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool SaveSuccessful { get; set; }
    public bool LoadSuccessful { get; set; }
}

public static FlowTree CreateSaveFlow(SaveState state)
{
    var tree = new FlowTree("SaveFlow");
    tree.Build(state)
        .Sequence()
            // セーブ中表示
            .Do(() => ShowSavingIndicator())
            // データ収集
            .Do(() => CollectSaveData())
            // ディスク書き込み（リトライ付き）
            .Retry(3,
                new SequenceNode(
                    new ActionNode<SaveState>(s =>
                    {
                        return WriteSaveFile() ? NodeStatus.Success : NodeStatus.Failure;
                    }),
                    new ActionNode<SaveState>(s =>
                    {
                        return VerifySaveFile() ? NodeStatus.Success : NodeStatus.Failure;
                    })
                )
            )
            // 完了表示
            .Event(onExit: (s, result) =>
                {
                    HideSavingIndicator();
                    s.SaveSuccessful = result == NodeStatus.Success;
                })
            .Do(() => ShowSaveComplete())
        .End()
        .Complete();

    return tree;
}
```

---

## サンプル集：マッチメイキング

```csharp
public class MatchmakingState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool IsCancelled { get; set; }
    public string? MatchId { get; set; }
    public int PlayerCount { get; set; }
    public const int RequiredPlayers = 4;
}

public static FlowTree CreateMatchmakingFlow(MatchmakingState state)
{
    var tree = new FlowTree("Matchmaking");
    tree.Build(state)
        .Sequence()
            // マッチメイキングUI表示
            .Event(
                onEnter: s => { ShowMatchmakingUI(); },
                onExit: (s, result) =>
                {
                    HideMatchmakingUI();
                    if (result == NodeStatus.Failure)
                        ShowMatchmakingError();
                })
            .Sequence()
                // サーバー接続
                .Retry(3,
                    new TimeoutNode(10.0f,
                        new ActionNode<MatchmakingState>(s =>
                        {
                            return ConnectToMatchServer()
                                ? NodeStatus.Success
                                : NodeStatus.Running;
                        })
                    )
                )
                // マッチ検索開始
                .Do(() => StartMatchSearch())
                // マッチ待機（キャンセル可能）
                .Selector()
                    // キャンセル
                    .Sequence()
                        .Condition(s => s.IsCancelled)
                        .Action(s =>
                        {
                            CancelMatchSearch();
                            return NodeStatus.Failure;
                        })
                    .End()
                    // タイムアウト
                    .Timeout(60.0f,
                        new ActionNode<MatchmakingState>(s =>
                        {
                            var matchId = CheckMatchFound();
                            if (matchId != null)
                            {
                                s.MatchId = matchId;
                                return NodeStatus.Success;
                            }
                            UpdateSearchStatus();
                            return NodeStatus.Running;
                        })
                    )
                .End()
                // マッチ参加
                .Action(s =>
                {
                    return JoinMatch(s.MatchId!) ? NodeStatus.Success : NodeStatus.Failure;
                })
                // プレイヤー待機
                .Action(s =>
                {
                    s.PlayerCount = GetPlayerCount();
                    return s.PlayerCount >= MatchmakingState.RequiredPlayers
                        ? NodeStatus.Success
                        : NodeStatus.Running;
                })
            .End()
        .End()
        .Complete();

    return tree;
}
```

---

## サンプル集：クエスト/ミッション進行

```csharp
public class QuestState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int QuestPhase { get; set; }
    public bool HasKeyItem { get; set; }
    public int ChoiceResult { get; set; }
}

public static FlowTree CreateMainQuest(QuestState state)
{
    var tree = new FlowTree("MainQuest");
    tree.Build(state)
        .Sequence()
            // フェーズ1: 情報収集
            .Event(onExit: (s, _) => { s.QuestPhase = 1; })
            .Sequence()
                .Do(() => ShowObjective("村人と話す"))
                .Action(s =>
                {
                    return HasTalkedToAllVillagers()
                        ? NodeStatus.Success
                        : NodeStatus.Running;
                })
            .End()
            // フェーズ2: ダンジョン探索
            .Event(onExit: (s, _) => { s.QuestPhase = 2; })
            .Sequence()
                .Do(() => ShowObjective("古代遺跡を調査する"))
                // キーアイテム取得まで探索
                .Wait(s => s.HasKeyItem)
            .End()
            // フェーズ3: 選択肢による分岐
            .Sequence()
                .Action(s =>
                {
                    ShowChoiceDialog("アイテムをどうする？",
                        new[] { "村に渡す", "自分で使う", "破壊する" });
                    return NodeStatus.Success;
                })
                .Wait(() => HasMadeChoice())
                // 選択結果による分岐
                .SubTree(s =>
                {
                    return s.ChoiceResult switch
                    {
                        0 => CreateGoodEndingFlow(s),
                        1 => CreateNeutralEndingFlow(s),
                        2 => CreateBadEndingFlow(s),
                        _ => CreateNeutralEndingFlow(s)
                    };
                })
            .End()
        .End()
        .Complete();

    return tree;
}
```

---

## サンプル集：アニメーションシーケンス

```csharp
public class CutsceneState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool SkipRequested { get; set; }
}

public static FlowTree CreateCutscene(CutsceneState state)
{
    var tree = new FlowTree("Cutscene");
    tree.Build(state)
        .Sequence()
            // 並列で複数の演出を開始
            .Join()
                .Action(s =>
                {
                    return PlayCameraAnimation("pan_to_castle")
                        ? NodeStatus.Success : NodeStatus.Running;
                })
                .Action(s =>
                {
                    return FadeInMusic("epic_theme")
                        ? NodeStatus.Success : NodeStatus.Running;
                })
            .End()
            // セリフ1
            .Sequence()
                .Do(() => ShowDialogue("王様", "勇者よ、よく来た"))
                .Wait(() => IsDialogueComplete())
            .End()
            // キャラクターアニメーション
            .Action(s =>
            {
                return PlayCharacterAnimation("hero", "kneel")
                    ? NodeStatus.Success : NodeStatus.Running;
            })
            // セリフ2
            .Sequence()
                .Do(() => ShowDialogue("勇者", "お任せください"))
                .Wait(() => IsDialogueComplete())
            .End()
            // スキップ可能な待機
            .Race()
                .Wait(2.0f)
                .Condition(s => s.SkipRequested)
            .End()
            // フェードアウト
            .Action(s =>
            {
                return FadeToBlack() ? NodeStatus.Success : NodeStatus.Running;
            })
        .End()
        .Complete();

    return tree;
}
```

---

## パフォーマンス

### メモリ特性

| 項目 | 特性 |
|------|------|
| ノード配列 | ビルド時に固定 |
| 深度別状態 | List（初期容量4） |
| 状態オブジェクト | ユーザー管理 |
| CallStack | 固定サイズ配列 |

### GC発生条件

- **発生しない**: 通常使用（再帰深度4以下）
- **発生する**: 再帰深度が5以上で初めて到達した時のみList拡張

### 推奨設定

```csharp
// 小規模（AI数体）
new FlowCallStack(16)

// 中規模（AI数十体）
new FlowCallStack(32)

// 大規模（複雑な再帰）
new FlowCallStack(64)
```

---

## 注意事項

### ユーザー定義状態の扱い

**重要**: ActionNodeのデリゲートでキャプチャした変数は、呼び出し深度ごとに分離**されない**。

```csharp
// 悪い例：キャプチャ変数が深度間で共有される
int sharedCounter = 0;
var tree = new FlowTree();
tree.Build()
    .Selector()
        .Sequence()
            .Action(() =>
            {
                sharedCounter++;  // 全深度で共有される！
                return NodeStatus.Success;
            })
            .SubTree(tree)  // 再帰
        .End()
    .End()
    .Complete();

// 良い例：状態オブジェクトを使う
public class CounterState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int Counter { get; set; }
}

var state = new CounterState();
var tree = new FlowTree();
tree.Build(state)
    .Selector()
        .Sequence()
            .Action(s =>
            {
                // 状態オブジェクト経由でアクセス（明示的な設計）
                s.Counter++;
                return NodeStatus.Success;
            })
            .SubTree(tree)
        .End()
    .End()
    .Complete();
```

### フレームワークノードの状態

SequenceNode、SelectorNode等のフレームワーク提供ノードは、深度ごとに状態が分離される。

---

## ディレクトリ構造

```
libs/foundation/FlowTree/
  FlowTree.Core/
    FlowTree.Core.csproj

    Core/
      NodeStatus.cs              # enum: Success, Failure, Running
      IFlowNode.cs               # 基底インターフェース
      FlowTree.cs                # ツリー定義

    Nodes/
      Composite/
        SequenceNode.cs
        SelectorNode.cs
        ParallelNode.cs
        RaceNode.cs
        JoinNode.cs
        RandomSelectorNode.cs
        ShuffledSelectorNode.cs
        WeightedRandomSelectorNode.cs
        RoundRobinSelectorNode.cs
      Decorator/
        InverterNode.cs
        SucceederNode.cs
        FailerNode.cs
        RepeatNode.cs
        RepeatUntilFailNode.cs
        RepeatUntilSuccessNode.cs
        RetryNode.cs
        TimeoutNode.cs
        DelayNode.cs
        GuardNode.cs
        EventNode.cs
      Leaf/
        ActionNode.cs
        ConditionNode.cs
        SubTreeNode.cs
        WaitNode.cs
        WaitUntilNode.cs
        YieldNode.cs
        SuccessNode.cs
        FailureNode.cs

    Context/
      FlowContext.cs             # 実行コンテキスト（内部用struct）

    CallStack/
      CallFrame.cs               # readonly struct
      FlowCallStack.cs           # 固定サイズ配列

    Dsl/
      FlowTreeBuilder.cs         # ビルダーDSL
      Flow.cs                    # 短縮名

  FlowTree.Tests/
    CoreTests.cs
    CompositeNodeTests.cs
    DecoratorNodeTests.cs
    LeafNodeTests.cs
    SubTreeTests.cs
    RecursionTests.cs
    DslTests.cs
    ZeroGcTests.cs
```
