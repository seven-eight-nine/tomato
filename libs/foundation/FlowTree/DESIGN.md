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
11. [サンプル集：並行処理パターン](#サンプル集並行処理パターン)
12. [サンプル集：UIフロー](#サンプル集uiフロー)
13. [サンプル集：シーン遷移](#サンプル集シーン遷移)
14. [サンプル集：メニュー状態遷移](#サンプル集メニュー状態遷移)
15. [サンプル集：セーブ/ロードフロー](#サンプル集セーブロードフロー)
16. [サンプル集：マッチメイキング](#サンプル集マッチメイキング)
17. [サンプル集：クエスト/ミッション進行](#サンプル集クエストミッション進行)
18. [サンプル集：アニメーションシーケンス](#サンプル集アニメーションシーケンス)
19. [パフォーマンス](#パフォーマンス)
20. [注意事項](#注意事項)
21. [ディレクトリ構造](#ディレクトリ構造)

---

## クイックスタート

### 1. ツリーを定義

```csharp
using Tomato.FlowTree;
using static Tomato.FlowTree.Flow;

// 状態クラス（IFlowState必須）
public class PatrolState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int CurrentWaypoint { get; set; }
    public bool IsMoving { get; set; }
}

// FlowBuilder を使った型推論パターン（推奨）
var state = new PatrolState();
var patrolTree = new FlowTree("Patrol");
patrolTree.Build(state, b => b.Sequence(
    b.Action(s => GetNextWaypoint(s)),
    b.Action(s => MoveToWaypoint(s)),
    b.Wait(2.0f)  // 2秒待機
));
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

tree.Build(
    Sequence(
        Action(() => NodeStatus.Success),
        SubTree(tree)  // ビルド中でも自己参照可能
    )
);
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
// 状態を受け取る（FlowBuilder使用）
b.Action(s => { s.Counter++; return NodeStatus.Success; })

// 状態を使わない
Action(static () => NodeStatus.Success)
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

`Build(state, ...)` で状態オブジェクトを渡す:

```csharp
var state = new EnemyState();
var tree = new FlowTree();

// FlowBuilder パターン（型推論が効く）
tree.Build(state, b => b.Sequence(
    b.Condition(s => s.HasTarget),
    b.Action(s =>
    {
        s.AttackCount++;
        return NodeStatus.Success;
    })
));

// 明示的型パラメータ指定
tree.Build(state,
    Sequence(
        Condition<EnemyState>(s => s.HasTarget),
        Action<EnemyState>(s =>
        {
            s.AttackCount++;
            return NodeStatus.Success;
        })
    )
);
```

### ステートレスなツリー

状態が不要な場合は `Build(rootNode)` を使用:

```csharp
var tree = new FlowTree();
tree.Build(
    Sequence(
        Do(static () => DoSomething()),
        Wait(1.0f)
    )
);
```

### 型付きビルダーでのステートレスノード

FlowBuilder内でも状態を使わないノードを追加できる:

```csharp
tree.Build(state, b => b.Sequence(
    b.Action(s => ProcessState(s)),           // 状態を使う
    b.Action(static () => NodeStatus.Success),  // 状態を使わない
    b.Wait(1.0f)                              // ステートレスデコレータ
));
```

---

## ノードリファレンス

### Composite Nodes（複合ノード）

Composite Nodesは複数の子ノードを持つノード。大きく2種類に分類される：

| 種類 | ノード | 説明 |
|------|--------|------|
| **順次実行系** | Sequence, Selector | 子を1つずつ順番に実行 |
| **並列実行系** | Join, Race | 全ての子を同時に実行 |

#### SequenceNode

全ての子ノードが成功するまで順次実行。いずれかが失敗したら即座にFailureを返す。

```csharp
Sequence(
    Action<MyState>(s => Step1(s)),
    Action<MyState>(s => Step2(s)),
    Action<MyState>(s => Step3(s))
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
Selector(
    Action<MyState>(s => TryOption1(s)),  // 失敗したら次へ
    Action<MyState>(s => TryOption2(s)),  // 失敗したら次へ
    Action<MyState>(s => Fallback(s))     // 最後の手段
)
```

| 子の結果 | SelectorNodeの動作 |
|---------|-------------------|
| Success | 即座にSuccessを返す |
| Failure | 次の子へ進む。全て失敗したらFailure |
| Running | Runningを返す。次のTickで継続 |

---

**並列実行系 (Join / Race)**

これらは全ての子ノードを同時に評価する。違いは「いつ完了するか」。

| ノード | 完了条件 | 用途 |
|--------|---------|------|
| Join | 全員完了 | WaitAll相当 |
| Race | 最初の1つ完了 | WaitAny相当、キャンセルパターン |

#### RaceNode

最初に完了した子ノードの結果を採用する（WaitAny相当）。

```csharp
var attackTree = new FlowTree("Attack");
var patrolTree = new FlowTree("Patrol");
// ... build trees ...

Race(
    SubTree(attackTree),
    Timeout(5.0f, SubTree(patrolTree))
)
```

#### JoinNode

全ての子ノードが完了するまで待機する（WaitAll相当）。

```csharp
// RequireAll: 全成功でSuccess（デフォルト）
Join(
    Action<MyState>(s => LoadTextures(s)),
    Action<MyState>(s => LoadSounds(s)),
    Action<MyState>(s => LoadData(s))
)
```

#### RandomSelectorNode

子ノードをランダムに1つ選択して実行する。

```csharp
RandomSelector(
    Action<MyState>(s => Attack1(s)),
    Action<MyState>(s => Attack2(s)),
    Action<MyState>(s => Attack3(s))
)
```

#### ShuffledSelectorNode

全選択肢を一巡するまで同じものを選ばない（シャッフル再生）。全ての子を実行したら再シャッフル。

```csharp
// ダイアログをランダム順で再生（重複なし）
ShuffledSelector(
    Action<MyState>(s => PlayDialogue1(s)),
    Action<MyState>(s => PlayDialogue2(s)),
    Action<MyState>(s => PlayDialogue3(s))
)
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
WeightedRandomSelector(
    (3.0f, Action<MyState>(s => CommonAction(s))),   // 75%
    (1.0f, Action<MyState>(s => RareAction(s)))      // 25%
)
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
RoundRobin(
    Action<MyState>(s => PatrolPointA(s)),
    Action<MyState>(s => PatrolPointB(s)),
    Action<MyState>(s => PatrolPointC(s))
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
Inverter(
    Condition<MyState>(s => s.IsEnemyVisible)
)
```

#### SucceederNode / FailerNode

常に指定した結果を返す。

```csharp
// 子が何を返してもSuccess
Succeeder(
    Action<MyState>(s => TryOptionalAction(s))
)

// 子が何を返してもFailure
Failer(
    Action<MyState>(s => DoSomething(s))
)
```

#### RepeatNode

子を指定回数繰り返す。

```csharp
// 3回繰り返す
Repeat(3,
    Action<MyState>(s => DoSomething(s))
)
```

#### RepeatUntilFailNode / RepeatUntilSuccessNode

条件を満たすまで繰り返す。

```csharp
// 失敗するまで繰り返す
RepeatUntilFail(
    Action<MyState>(s => TryAction(s))
)

// 成功するまで繰り返す
RepeatUntilSuccess(
    Action<MyState>(s => TryAction(s))
)
```

#### RetryNode

失敗時に指定回数リトライする。

```csharp
// 最大3回リトライ
Retry(3,
    Action<MyState>(s => TryConnect(s))
)
```

#### TimeoutNode

指定時間内に完了しなければFailure。

```csharp
// 5秒でタイムアウト
Timeout(5.0f,
    Action<MyState>(s => LongRunningTask(s))
)
```

#### DelayNode

指定時間待機してから子を実行する。

```csharp
// 1秒待ってから実行
Delay(1.0f,
    Action<MyState>(s => DoAfterDelay(s))
)
```

#### GuardNode

条件を満たした場合のみ子を実行する。

```csharp
// 条件を満たさなければ即座にFailure
Guard<MyState>(s => s.HasTarget, SubTree(attackTree))

// FlowBuilder使用時
b.Guard(s => s.HasTarget, b.SubTree(attackTree))
```

#### ScopeNode

ノードの開始/終了時にコールバックを発火する。

```csharp
// ステートレス版
Scope(
    () => Console.WriteLine("処理開始"),
    result => Console.WriteLine($"処理終了: {result}"),
    Action(static () => DoSomething())
)

// 状態付き版（FlowBuilder使用）
b.Scope(
    s => s.StartTime = DateTime.Now,
    (s, result) => s.EndTime = DateTime.Now,
    b.Action(s => DoSomething(s))
)
```

| コールバック | 発火タイミング |
|---------|--------------|
| onEnter | 初回Tick時のみ |
| onExit | Success/Failureになった時のみ（Running中は発火しない） |

**デリゲート型**:
```csharp
// ステートレス
public delegate void FlowScopeEnterHandler();
public delegate void FlowScopeExitHandler(NodeStatus result);

// 状態付き
public delegate void FlowScopeEnterHandler<in T>(T state) where T : class;
public delegate void FlowScopeExitHandler<in T>(T state, NodeStatus result) where T : class;
```

---

### Leaf Nodes（葉ノード）

#### ActionNode

アクションを実行する。

```csharp
// ステートレス
Action(static () =>
{
    DoSomething();
    return NodeStatus.Success;
})

// 状態付き（FlowBuilder使用）
b.Action(s =>
{
    s.Counter++;
    return IsComplete(s) ? NodeStatus.Success : NodeStatus.Running;
})
```

#### ConditionNode

条件を評価する。trueならSuccess、falseならFailure。

```csharp
// ステートレス
Condition(static () => SomeGlobalCondition())

// 状態付き（FlowBuilder使用）
b.Condition(s => s.HasTarget)
```

#### SubTreeNode

別のツリーを呼び出す。静的参照・動的選択・State注入をサポート。

```csharp
// 静的参照
var attackTree = new FlowTree("Attack");
// ... build attackTree ...
SubTree(attackTree)

// 動的選択（ラムダで実行時にツリーを選択）
b.SubTree(s => s.Difficulty > 5 ? hardTree : easyTree)

// State注入（サブツリーに専用Stateを渡す）
b.SubTree<ParentState, ChildState>(
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
childTree.Build(new ChildState(), b => b.Action(s =>
{
    s.LocalScore = 100;
    var parent = (ParentState)s.Parent!;  // 親Stateにアクセス
    parent.TotalScore += s.LocalScore;
    return NodeStatus.Success;
}));

var mainTree = new FlowTree();
mainTree.Build(new ParentState(), b =>
    b.SubTree<ParentState, ChildState>(childTree, p => new ChildState())
);
```

**デリゲート型**:
```csharp
public delegate FlowTree? FlowTreeProvider();
public delegate FlowTree? FlowTreeProvider<in T>(T state) where T : class, IFlowState;
public delegate TChild FlowStateProvider<in TParent, out TChild>(TParent parentState)
    where TParent : class, IFlowState
    where TChild : class, IFlowState;
```

#### WaitNode / WaitUntilNode

指定時間または条件で待機する。

```csharp
// 時間待機
Wait(2.0f)  // 2秒待機

// 条件待機（条件がtrueになるまでRunning）
b.WaitUntil(s => s.IsReady)
```

#### YieldNode

1TickだけRunningを返す。

```csharp
Yield()  // 次のフレームまで待機
```

#### SuccessNode / FailureNode

即座に結果を返す。

```csharp
Success  // 即座にSuccess
Failure  // 即座にFailure
```

#### ReturnNode

ツリーの早期終了を要求する。実行されると、現在のツリー（またはサブツリー）をリセットし、ScopeNodeのonExitを発火させる。

```csharp
// 成功で早期終了
ReturnSuccess()

// 失敗で早期終了
ReturnFailure()

// ステータスを指定
Return(NodeStatus.Success)
```

**使用例：サブツリーからの早期脱出**

```csharp
var subTree = new FlowTree();
subTree.Build(state, b => b.Scope(
    s => InitializeResources(s),
    (s, _) => CleanupResources(s),  // Returnでも発火される
    b.Sequence(
        b.Action(s => Step1(s)),
        b.Selector(
            b.Guard(s => s.ShouldAbort, b.ReturnFailure()),  // 条件で早期終了
            b.Action(s => Step2(s))
        ),
        b.Action(s => Step3(s))
    )
));
```

**Race + WaitUntil との組み合わせ**

```csharp
// キャンセル可能な長時間処理
tree.Build(state, b => b.Scope(
    s => OnStart(s),
    (s, _) => OnEnd(s),
    b.Race(
        b.Sequence(
            b.WaitUntil(s => s.IsCancelled),
            b.ReturnFailure()  // キャンセル時はFailureで終了
        ),
        b.Action(s => LongRunningTask(s))
    )
));
```

---

## サブツリーとコールスタック

### コールスタックの仕組み

SubTreeNodeでサブツリーを呼び出すと、内部でコールスタックが管理される：

1. **呼び出し時**: 現在のツリーをスタックにプッシュ
2. **サブツリー実行**: サブツリーのルートからTick開始
3. **完了時**: スタックからポップして呼び出し元に戻る

```
メインツリー
├─ Sequence
│   ├─ Action1
│   └─ SubTree(PatrolTree) ← 呼び出し
│       ├─ [Push: MainTree]
│       ├─ PatrolTree を実行
│       └─ [Pop: MainTree に戻る]
└─ Action2
```

スタックは自動的に拡張されるため、サイズを気にする必要はない。再帰呼び出しも自然にサポートされる。

### 基本的なサブツリー呼び出し

FlowTree参照を直接指定してサブツリーを呼び出す。

```csharp
var patrolTree = new FlowTree("Patrol");
var attackTree = new FlowTree("Attack");

patrolTree.Build(state, b => b.Sequence(
    // ... patrol logic ...
));

attackTree.Build(state, b => b.Sequence(
    // ... attack logic ...
));

var aiTree = new FlowTree("AI");
aiTree.Build(state, b => b.Selector(
    b.Guard(s => s.HasTarget, b.SubTree(attackTree)),
    b.SubTree(patrolTree)
));
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
countdownTree.Build(state, b => b.Selector(
        // 終了条件: counter <= 0
        b.Sequence(
            b.Condition(s => s.Counter <= 0),
            b.Action(s =>
            {
                s.Log += "Done!";
                return NodeStatus.Success;
            })
        ),
        // 再帰: counter-- して自己呼び出し
        b.Sequence(
            b.Action(s =>
            {
                s.Log += s.Counter.ToString();
                s.Counter--;
                return NodeStatus.Success;
            }),
            b.SubTree(countdownTree)  // 自己呼び出し
        )
    ));

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
treeA.Build(state, b => b.Sequence(
        b.Action(s =>
        {
            s.Log += "A";
            s.Counter--;
            return NodeStatus.Success;
        }),
        b.Selector(
            b.Sequence(
                b.Condition(s => s.Counter > 0),
                b.SubTree(treeB)  // TreeBを呼び出し
            ),
            b.Success
        )
    ));

// TreeB: "B"を記録してカウンタデクリメント、counter > 0ならTreeAを呼ぶ
treeB.Build(state, b => b.Sequence(
    b.Action(s =>
    {
        s.Log += "B";
        s.Counter--;
        return NodeStatus.Success;
    }),
    b.Selector(
        b.Sequence(
            b.Condition(s => s.Counter > 0),
            b.SubTree(treeA)  // TreeAを呼び出し
        ),
        b.Success
    )
));

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

### Build パターン

```csharp
var tree = new FlowTree("MyTree");

// ステートレス
tree.Build(
    Sequence(
        Action(() => DoAction()),
        Selector(
            Condition(() => SomeCondition()),
            Action(() => Alternative())
        )
    )
);

// 状態付き（FlowBuilder 型推論パターン - 推奨）
tree.Build(state, b => b.Sequence(
    b.Action(s => DoAction(s)),
    b.Selector(
        b.Condition(s => SomeCondition(s)),
        b.Action(s => Alternative(s))
    )
));

// 状態付き（明示的型パラメータ）
tree.Build(state,
    Sequence(
        Action<MyState>(s => DoAction(s)),
        Selector(
            Condition<MyState>(s => SomeCondition(s)),
            Action<MyState>(s => Alternative(s))
        )
    )
);
```

### 短縮表記（static using）

```csharp
using static Tomato.FlowTree.Flow;

// Tree() でFlowTreeを作成
var tree = Tree("MyTree");
tree.Build(
    Sequence(
        SubTree(otherTree),
        Action(() => DoSomething())
    )
);

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

### DSLメソッド一覧

#### Composite

| メソッド | 説明 |
|---------|------|
| `Sequence(...)` | 順次実行 |
| `Selector(...)` | フォールバック選択 |
| `Race(...)` | 最初の完了採用 |
| `Join(...)` | 全完了待機 |
| `Join(policy, ...)` | ポリシー付き全完了待機 |
| `RandomSelector(...)` | ランダム選択 |
| `ShuffledSelector(...)` | シャッフル選択 |
| `WeightedRandomSelector(...)` | 重み付きランダム選択 |
| `RoundRobin(...)` | 順番選択 |

#### Decorator

| メソッド | 説明 |
|---------|------|
| `Retry(maxRetries, child)` | 失敗時にリトライ |
| `Timeout(seconds, child)` | タイムアウト制限 |
| `Delay(seconds, child)` | 遅延実行 |
| `Repeat(count, child)` | 指定回数繰り返し |
| `RepeatUntilFail(child)` | Failureまで繰り返し |
| `RepeatUntilSuccess(child)` | Successまで繰り返し |
| `Inverter(child)` | 結果を反転 |
| `Succeeder(child)` | 常にSuccess |
| `Failer(child)` | 常にFailure |
| `Guard(condition, child)` | 条件付き実行 |
| `Guard<T>(condition, child)` | 型付き条件付き実行 |
| `Scope(onEnter, onExit, child)` | スコープ（開始/終了コールバック） |
| `Scope<T>(onEnter, onExit, child)` | 型付きスコープ |

#### Leaf

| メソッド | 説明 |
|---------|------|
| `Action(action)` | アクションを実行（NodeStatusを返す） |
| `Action<T>(action)` | 型付きアクション |
| `Do(action)` | voidアクションを実行してSuccessを返す |
| `Do<T>(action)` | 型付きvoidアクション |
| `Condition(condition)` | 条件を評価 |
| `Condition<T>(condition)` | 型付き条件 |
| `SubTree(tree)` | サブツリーを呼び出し |
| `SubTree(provider)` | 動的にサブツリーを選択 |
| `SubTree<T>(provider)` | 型付き動的サブツリー |
| `SubTree<TParent, TChild>(tree, stateProvider)` | State注入付きサブツリー |
| `Wait(seconds)` | 指定時間待機 |
| `WaitUntil(condition)` | 条件が満たされるまで待機 |
| `WaitUntil(condition, interval)` | 間隔評価で条件待機 |
| `WaitUntil<T>(condition)` | 型付き条件待機 |
| `WaitUntil<T>(condition, interval)` | 型付き間隔評価で条件待機 |
| `Yield()` | 1Tick待機 |
| `Success` | 即座にSuccess |
| `Failure` | 即座にFailure |
| `ReturnSuccess()` | ツリーの早期終了（Success） |
| `ReturnFailure()` | ツリーの早期終了（Failure） |
| `Return(status)` | ツリーの早期終了（指定ステータス） |

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
        tree.Build(state, b => b.Selector(
                // 体力が低ければ逃げる
                b.Guard(s => s.IsLowHealth, b.SubTree(FleeTree)),
                // ターゲットがいる
                b.Sequence(
                    b.Condition(s => s.HasTarget),
                    b.Selector(
                        // 射程内なら攻撃
                        b.Guard(s => s.IsTargetInRange, b.SubTree(AttackTree)),
                        // 射程外なら追跡
                        b.SubTree(ChaseTree)
                    )
                ),
                // 何もなければパトロール
                b.SubTree(PatrolTree)
            ));

        return tree;
    }

    private static void BuildSubTrees(EnemyState state)
    {
        AttackTree.Build(state, b => b.Sequence(
            b.Action(s => { /* aim */ return NodeStatus.Success; }),
            b.Action(s => { /* fire */ return NodeStatus.Success; })
        ));

        PatrolTree.Build(state, b => b.Sequence(
            b.Action(s => { /* get waypoint */ return NodeStatus.Success; }),
            b.Action(s => { /* move to */ return NodeStatus.Running; }),
            b.Wait(2.0f)
        ));

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
        tree.Build(state, b => b.Selector(
                // フェーズごとに分岐
                b.Guard(s => s.Phase == 1, b.SubTree(Phase1Tree)),
                b.Guard(s => s.Phase == 2, b.SubTree(Phase2Tree)),
                b.SubTree(Phase3Tree)
            ));

        return tree;
    }

    private static void BuildSubTrees(BossState state)
    {
        Phase1Tree.Build(state, b => b.Sequence(
            b.Action(s => { /* phase 1 attack */ return NodeStatus.Success; }),
            b.Action(s =>
            {
                // HPが70%以下でフェーズ2へ
                if (s.Health <= 70f)
                    s.Phase = 2;
                return NodeStatus.Success;
            })
        ));

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
    loadLevel.Build(state, b => b.WaitUntil(s => s.LevelLoaded));
    loadAssets.Build(state, b => b.WaitUntil(s => s.AssetsLoaded));
    initializeSystems.Build(state, b => b.Action(s =>
    {
        InitializeGameSystems();
        s.SystemsInitialized = true;
        return NodeStatus.Success;
    }));

    // メインローディングフロー
    var tree = new FlowTree("GameLoading");
    tree.Build(state, b => b.Sequence(
            b.Do(s => ShowLoadingScreen()),
            b.Join(  // 全て完了まで待機
                b.SubTree(loadLevel),
                b.SubTree(loadAssets),
                b.SubTree(initializeSystems)
            ),
            b.Do(s => HideLoadingScreen())
        ));

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
    tree.Build(state, b => b.Retry(3,  // 最大3回リトライ
        b.Timeout(10.0f,  // 10秒でタイムアウト
            b.Sequence(
                b.Do(s =>
                {
                    SendRequest();
                    s.RequestSent = true;
                }),
                b.WaitUntil(s => s.ResponseReceived),
                b.Do(s => ParseResponse(s))
            )
        )
    ));

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
    tree.Build(
        Race(  // 最初に成功した接続を採用
                SubTree(connectPrimary),
                SubTree(connectSecondary),
                SubTree(connectTertiary)
            )
        );

    return tree;
}
```

---

## サンプル集：並行処理パターン

Race + WaitUntil を組み合わせることで、Verse言語の構造化並行処理に似たパターンを実現できる。

### キャンセル可能な長時間処理

```csharp
public class TaskState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool IsCancelled { get; set; }
    public float Progress { get; set; }
}

// 処理中にキャンセルフラグが立ったら即座に中断
tree.Build(state, b => b.Race(
    // メインの長時間処理
    b.Action(s =>
    {
        s.Progress += 0.01f;
        return s.Progress >= 1.0f ? NodeStatus.Success : NodeStatus.Running;
    }),
    // キャンセル監視（キャンセルされたらSuccess→Raceが終了）
    b.WaitUntil(s => s.IsCancelled)
));
```

### タイムアウト付き待機

```csharp
// ユーザー入力を待つが、10秒でタイムアウト
tree.Build(state, b => b.Race(
    b.WaitUntil(s => s.HasUserInput),
    b.Sequence(
        b.Wait(10.0f),
        b.Do(s => s.TimedOut = true)
    )
));
```

### 複数条件の監視（最初に成立した条件で分岐）

```csharp
public class BattleState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool EnemyDefeated { get; set; }
    public bool PlayerDefeated { get; set; }
    public bool TimeUp { get; set; }
    public string Result { get; set; } = "";
}

// 勝利・敗北・時間切れのいずれかで終了
tree.Build(state, b => b.Race(
    b.Sequence(
        b.WaitUntil(s => s.EnemyDefeated),
        b.Do(s => s.Result = "Victory")
    ),
    b.Sequence(
        b.WaitUntil(s => s.PlayerDefeated),
        b.Do(s => s.Result = "Defeat")
    ),
    b.Sequence(
        b.WaitUntil(s => s.TimeUp),
        b.Do(s => s.Result = "TimeUp")
    )
));
```

### 並列ダウンロード with 進捗追跡

```csharp
public class DownloadState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public bool File1Done { get; set; }
    public bool File2Done { get; set; }
    public bool File3Done { get; set; }
    public int CompletedCount { get; set; }
}

// 全ファイルのダウンロード完了を待つ（進捗コールバック付き）
tree.Build(state, b => b.Join(
    b.Scope(
        null,
        (s, _) => s.CompletedCount++,
        b.WaitUntil(s => s.File1Done)
    ),
    b.Scope(
        null,
        (s, _) => s.CompletedCount++,
        b.WaitUntil(s => s.File2Done)
    ),
    b.Scope(
        null,
        (s, _) => s.CompletedCount++,
        b.WaitUntil(s => s.File3Done)
    )
));
```

### 中断可能なシーケンス

```csharp
// 3ステップの処理を実行。各ステップ間でキャンセルチェック
tree.Build(state, b => b.Sequence(
    b.Race(
        b.Action(s => Step1(s)),
        b.WaitUntil(s => s.IsCancelled)
    ),
    b.Condition(s => !s.IsCancelled),  // キャンセルされていたらFailure
    b.Race(
        b.Action(s => Step2(s)),
        b.WaitUntil(s => s.IsCancelled)
    ),
    b.Condition(s => !s.IsCancelled),
    b.Race(
        b.Action(s => Step3(s)),
        b.WaitUntil(s => s.IsCancelled)
    )
));
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

    confirmBranch.Build(state, b => b.Action(s =>
    {
        ExecuteConfirmAction();
        return NodeStatus.Success;
    }));

    cancelBranch.Build(state, b => b.Action(s =>
    {
        ExecuteCancelAction();
        return NodeStatus.Success;
    }));

    var tree = new FlowTree("DialogFlow");
    tree.Build(state, b => b.Sequence(
            b.Do(s => ShowDialog()),
            b.WaitUntil(s => s.HasUserInput),
            b.Selector(
                b.Guard(s => s.IsConfirmed, b.SubTree(confirmBranch)),
                b.SubTree(cancelBranch)
            )
        ));

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
    tree.Build(state, b => b.Sequence(
        // ステップ1: 移動の説明
        b.Sequence(
            b.Do(s => ShowTutorialMessage("WASDで移動できます")),
            b.WaitUntil(s => s.HasPlayerMoved)
        ),
        // ステップ2: 攻撃の説明
        b.Sequence(
            b.Do(s => ShowTutorialMessage("クリックで攻撃できます")),
            b.WaitUntil(s => s.HasPlayerAttacked)
        ),
        // 完了
        b.Do(s => ShowTutorialMessage("チュートリアル完了！"))
    ));

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
        tree.Build(state, b => b.Sequence(
                // タイトル画面
                b.SubTree(TitleScene),
                // ゲームループ（リトライ可能）
                b.RepeatUntilFail(
                    b.Sequence(
                        b.SubTree(GameScene),
                        b.SubTree(ResultScene),
                        // リトライならSuccess、終了ならFailure
                        b.Condition(s => !s.IsQuitRequested)
                    )
                ),
                // 終了処理
                b.Do(s => SaveGameData())
            ));

        return tree;
    }

    private static void BuildScenes(SceneState state)
    {
        // タイトル画面
        TitleScene.Build(state, b => b.Sequence(
            b.Do(s => ShowTitleScreen()),
            b.WaitUntil(s => IsStartPressed()),  // スタートボタン待機
            b.Do(s => HideTitleScreen())
        ));

        // ゲーム画面
        GameScene.Build(state, b => b.Sequence(
            b.Do(s => LoadLevel()),
            b.Action(s =>
            {
                // ゲームオーバーまたはクリアまで継続
                if (s.IsGameOver)
                    return NodeStatus.Success;
                UpdateGame();
                return NodeStatus.Running;
            })
        ));

        // リザルト画面
        ResultScene.Build(state, b => b.Sequence(
            b.Do(s => ShowResultScreen()),
            b.WaitUntil(s => HasResultInput())
        ));
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
        tree.Build(state, b => b.SubTree(MainMenu));

        return tree;
    }

    private static void BuildMenus(MenuState state)
    {
        // メインメニュー
        MainMenu.Build(state, b => b.Sequence(
            b.Do(s => ShowMainMenu()),
            // メニュー操作ループ
            b.RepeatUntilFail(
                b.Sequence(
                    // 入力待機
                    b.WaitUntil(s => HasMenuInput()),
                    // 選択に応じた処理
                    b.Selector(
                        // ゲーム開始
                        b.Sequence(
                            b.Condition(s => s.SelectedIndex == 0),
                            b.Action(s =>
                            {
                                HideMainMenu();
                                return NodeStatus.Failure; // ループ終了
                            })
                        ),
                        // オプション
                        b.Sequence(
                            b.Condition(s => s.SelectedIndex == 1),
                            b.SubTree(OptionsMenu)
                        ),
                        b.Success // その他は継続
                    )
                )
            )
        ));

        // オプションメニュー
        OptionsMenu.Build(state, b => b.Sequence(
            b.Do(s => { ShowOptionsMenu(); s.IsBackPressed = false; }),
            b.RepeatUntilSuccess(
                b.Selector(
                    // 戻るボタン
                    b.Sequence(
                        b.Condition(s => s.IsBackPressed),
                        b.Success
                    ),
                    // 入力待ち
                    b.Action(s => NodeStatus.Running)
                )
            ),
            b.Do(s => HideOptionsMenu())
        ));
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
    tree.Build(state, b => b.Sequence(
        // セーブ中表示
        b.Do(s => ShowSavingIndicator()),
        // データ収集
        b.Do(s => CollectSaveData()),
        // ディスク書き込み（リトライ付き）
        b.Retry(3,
            b.Sequence(
                b.Action(s =>
                {
                    return WriteSaveFile() ? NodeStatus.Success : NodeStatus.Failure;
                }),
                b.Action(s =>
                {
                    return VerifySaveFile() ? NodeStatus.Success : NodeStatus.Failure;
                })
            )
        ),
        // 完了表示
        b.Scope(
            null,
            (s, result) =>
            {
                HideSavingIndicator();
                s.SaveSuccessful = result == NodeStatus.Success;
            },
            b.Do(s => ShowSaveComplete())
        )
    ));

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
    tree.Build(state, b => b.Sequence(
        // マッチメイキングUI表示
        b.Scope(
            s => ShowMatchmakingUI(),
            (s, result) =>
            {
                HideMatchmakingUI();
                if (result == NodeStatus.Failure)
                    ShowMatchmakingError();
            },
            b.Sequence(
                // サーバー接続（3回リトライ、各10秒タイムアウト）
                b.Retry(3,
                    b.Timeout(10.0f,
                        b.Action(s =>
                        {
                            return ConnectToMatchServer()
                                ? NodeStatus.Success
                                : NodeStatus.Running;
                        })
                    )
                ),
                // マッチ検索開始
                b.Do(s => StartMatchSearch()),
                // マッチ待機（キャンセル可能）
                b.Selector(
                    // キャンセル
                    b.Sequence(
                        b.Condition(s => s.IsCancelled),
                        b.Action(s =>
                        {
                            CancelMatchSearch();
                            return NodeStatus.Failure;
                        })
                    ),
                    // タイムアウト（60秒）
                    b.Timeout(60.0f,
                        b.Action(s =>
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
                ),
                // マッチ参加
                b.Action(s =>
                {
                    return JoinMatch(s.MatchId!) ? NodeStatus.Success : NodeStatus.Failure;
                }),
                // プレイヤー待機
                b.Action(s =>
                {
                    s.PlayerCount = GetPlayerCount();
                    return s.PlayerCount >= MatchmakingState.RequiredPlayers
                        ? NodeStatus.Success
                        : NodeStatus.Running;
                })
            )
        )
    ));

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
    tree.Build(state, b => b.Sequence(
        // フェーズ1: 情報収集
        b.Scope(
            null,
            (s, _) => s.QuestPhase = 1,
            b.Sequence(
                b.Do(s => ShowObjective("村人と話す")),
                b.Action(s =>
                {
                    return HasTalkedToAllVillagers()
                        ? NodeStatus.Success
                        : NodeStatus.Running;
                })
            )
        ),
        // フェーズ2: ダンジョン探索
        b.Scope(
            null,
            (s, _) => s.QuestPhase = 2,
            b.Sequence(
                b.Do(s => ShowObjective("古代遺跡を調査する")),
                // キーアイテム取得まで探索
                b.WaitUntil(s => s.HasKeyItem)
            )
        ),
        // フェーズ3: 選択肢による分岐
        b.Sequence(
            b.Action(s =>
            {
                ShowChoiceDialog("アイテムをどうする？",
                    new[] { "村に渡す", "自分で使う", "破壊する" });
                return NodeStatus.Success;
            }),
            b.WaitUntil(s => HasMadeChoice()),
            // 選択結果による分岐
            b.SubTree(s =>
            {
                return s.ChoiceResult switch
                {
                    0 => CreateGoodEndingFlow(s),
                    1 => CreateNeutralEndingFlow(s),
                    2 => CreateBadEndingFlow(s),
                    _ => CreateNeutralEndingFlow(s)
                };
            })
        )
    ));

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
    tree.Build(state, b => b.Sequence(
        // 並列で複数の演出を開始
        b.Join(
            b.Action(s =>
            {
                return PlayCameraAnimation("pan_to_castle")
                    ? NodeStatus.Success : NodeStatus.Running;
            }),
            b.Action(s =>
            {
                return FadeInMusic("epic_theme")
                    ? NodeStatus.Success : NodeStatus.Running;
            })
        ),
        // セリフ1
        b.Sequence(
            b.Do(s => ShowDialogue("王様", "勇者よ、よく来た")),
            b.WaitUntil(s => IsDialogueComplete())
        ),
        // キャラクターアニメーション
        b.Action(s =>
        {
            return PlayCharacterAnimation("hero", "kneel")
                ? NodeStatus.Success : NodeStatus.Running;
        }),
        // セリフ2
        b.Sequence(
            b.Do(s => ShowDialogue("勇者", "お任せください")),
            b.WaitUntil(s => IsDialogueComplete())
        ),
        // スキップ可能な待機
        b.Race(
            b.Wait(2.0f),
            b.Condition(s => s.SkipRequested)
        ),
        // フェードアウト
        b.Action(s =>
        {
            return FadeToBlack() ? NodeStatus.Success : NodeStatus.Running;
        })
    ));

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

### GC発生条件

- **発生しない**: 通常使用（再帰深度4以下）
- **発生する**: 再帰深度が5以上で初めて到達した時のみList拡張

---

## 注意事項

### ユーザー定義状態の扱い

**重要**: ActionNodeのデリゲートでキャプチャした変数は、呼び出し深度ごとに分離**されない**。

```csharp
// 悪い例：キャプチャ変数が深度間で共有される
int sharedCounter = 0;
var tree = new FlowTree();
tree.Build(
    Selector(
        Sequence(
            Action(() =>
            {
                sharedCounter++;  // 全深度で共有される！
                return NodeStatus.Success;
            }),
            SubTree(tree)  // 再帰
        )
    )
);

// 良い例：状態オブジェクトを使う
public class CounterState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int Counter { get; set; }
}

var state = new CounterState();
var tree = new FlowTree();
tree.Build(state, b => b.Selector(
    b.Sequence(
        b.Action(s =>
        {
            // 状態オブジェクト経由でアクセス（明示的な設計）
            s.Counter++;
            return NodeStatus.Success;
        }),
        b.SubTree(tree)
    )
));
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
        ScopeNode.cs
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

    Dsl/
      Flow.cs                    # ファクトリメソッド
      FlowBuilder.cs             # 型付きビルダー（型推論用）

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
