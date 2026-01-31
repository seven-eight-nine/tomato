# FlowTree

コールスタック付き汎用フロー制御ライブラリ。

## これは何？

ツリー構造で処理フローを定義・実行するシステム。
ビヘイビアツリーのパターンを基盤としつつ、AIに限らず非同期処理やワークフロー全般に適用可能。

```
ゲームローディング
├─ ShowLoadingScreen
├─ Join (全完了まで待機)
│   ├─ LoadLevel
│   ├─ LoadAssets
│   └─ InitializeSystems
└─ HideLoadingScreen
```

## なぜ使うのか

- **動的構造**: サブツリーとコールスタックでツリーを動的に組み上げ
- **再帰対応**: 自己再帰・相互再帰をフルサポート
- **汎用性**: AI行動選択、非同期処理、UIフローなど用途を選ばない
- **低GC**: 通常使用ではGCなし（深い再帰時のみリスト拡張）

---

## クイックスタート

### 1. シンプルなツリーを定義（ステートレス）

```csharp
using Tomato.FlowTree;
using static Tomato.FlowTree.Flow;

var tree = new FlowTree();
tree.Build(
    Sequence(
        Do(static () => Console.WriteLine("Step 1")),
        Do(static () => Console.WriteLine("Step 2"))
    )
);
```

### 2. 状態を使うツリーを定義

```csharp
// 状態クラス（IFlowState必須）
public class GameState : IFlowState
{
    public IFlowState? Parent { get; set; }  // サブツリーで親参照に使用
    public int Score { get; set; }
    public bool IsAlive { get; set; } = true;
}

var state = new GameState();
var tree = new FlowTree();

// FlowBuilder を使った型推論パターン（推奨）
tree.Build(state, b => b.Sequence(
    b.Do(s => s.Score += 100),
    b.Condition(s => s.IsAlive)
));

// 明示的な型パラメータ指定も可能
tree.Build(state,
    Sequence(
        Do<GameState>(s => s.Score += 100),
        Condition<GameState>(s => s.IsAlive)
    )
);
```

### 3. 実行（tickベース）

```csharp
var status = tree.Tick(deltaTicks);  // 経過tick数を渡す
// 出力:
// Step 1
// Step 2
```

---

## 主要な概念

### ノードの3つの結果

| 結果 | 説明 |
|------|------|
| **Success** | 処理成功。次へ進む |
| **Failure** | 処理失敗。親ノードが判断 |
| **Running** | 処理中。次のTickで継続 |

### ノードの分類

| 分類 | 説明 | 例 |
|------|------|-----|
| **Composite** | 複数の子を持つ | Sequence, Selector, Race, Join, ShuffledSelector |
| **Decorator** | 1つの子を修飾 | Repeat, Retry, Timeout, Scope |
| **Leaf** | 末端ノード | Action, Condition, SubTree, Wait |

### FlowTreeの構造

FlowTreeは「入れ物」と「中身」が分離している:

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
- 実行中に中身を差し替えることも可能

### サブツリー

SubTreeで他のツリーを呼び出せる。静的・動的ツリー選択、State注入をサポート。

```csharp
// 自己再帰
public class CounterState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int Counter { get; set; }
}

var state = new CounterState { Counter = 5 };
var countdown = new FlowTree();
countdown.Build(state, b => b.Selector(
        b.Sequence(
            b.Condition(s => s.Counter <= 0),
            b.Success
        ),
        b.Sequence(
            b.Do(s => s.Counter--),
            b.SubTree(countdown) // 自己参照
        )
    ));

// 動的サブツリー（実行時にツリーを選択）
public class DifficultyState : IFlowState
{
    public IFlowState? Parent { get; set; }
    public int Difficulty { get; set; }
}

var easyTree = new FlowTree("Easy");
var hardTree = new FlowTree("Hard");
// ... build trees ...

var state = new DifficultyState { Difficulty = 3 };
var tree = new FlowTree();
tree.Build(state, b => b.SubTree(s => s.Difficulty > 5 ? hardTree : easyTree));
```

### State注入

サブツリーに専用のStateを渡すことで、親子間で異なるStateを使える。
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
    public IFlowState? Parent { get; set; }
    public int LocalScore { get; set; }
}

var childTree = new FlowTree();
childTree.Build(new ChildState(), b => b.Do(s =>
{
    s.LocalScore = 100;
    // 親Stateにアクセス
    var parent = (ParentState)s.Parent!;
    parent.TotalScore += s.LocalScore;
}));

var mainTree = new FlowTree();
mainTree.Build(new ParentState(), b =>
    b.SubTree<ParentState, ChildState>(childTree, p => new ChildState())
);
```

---

## よく使うパターン

### Sequence（順次実行）

全て成功するまで順に実行。1つでも失敗したら即Failure。

```csharp
var tree = new FlowTree();
tree.Build(state, b => b.Sequence(
    b.Action(s => MoveToTarget(s)),
    b.Action(s => Attack(s)),
    b.Action(s => Retreat(s))
));
```

### Selector（フォールバック）

最初に成功したものを採用。全て失敗したらFailure。

```csharp
var tree = new FlowTree();
tree.Build(state, b => b.Selector(
    b.Guard(s => s.HasAmmo, b.Action(s => Shoot(s))),
    b.Guard(s => s.HasMeleeWeapon, b.Action(s => MeleeAttack(s))),
    b.Action(s => Retreat(s))  // フォールバック
));
```

### Join（全完了待機）

全ての子が完了するまで待機（WaitAll相当）。

```csharp
var tree = new FlowTree();
tree.Build(state, b => b.Join(
    b.Action(s => LoadTextures(s)),
    b.Action(s => LoadSounds(s)),
    b.Action(s => LoadData(s))
));
```

### Race（最初の完了を採用）

最初に完了した子の結果を採用（WaitAny相当）。

```csharp
var attackTree = new FlowTree("Attack");
var patrolTree = new FlowTree("Patrol");
// ... build trees ...

var tree = new FlowTree();
tree.Build(
    Race(
        SubTree(attackTree),
        Timeout(new TickDuration(300), SubTree(patrolTree))  // 300 tick後にタイムアウト
    )
);
```

### ShuffledSelector（シャッフル選択）

全選択肢を一巡するまで同じものを選ばない（シャッフル再生）。

```csharp
var tree = new FlowTree();
tree.Build(state, b => b.ShuffledSelector(
    b.Action(s => PlayDialogue1(s)),
    b.Action(s => PlayDialogue2(s)),
    b.Action(s => PlayDialogue3(s))
));
```

### WeightedRandomSelector（重み付きランダム）

各子ノードの重みに基づいて確率的に選択。

```csharp
var tree = new FlowTree();
tree.Build(
    WeightedRandomSelector(
        (3.0f, Action(static () => CommonAction())),   // 75%
        (1.0f, Action(static () => RareAction()))      // 25%
    )
);
```

### RoundRobin（順番選択）

0→1→2→0→... と順番に選択。

```csharp
var tree = new FlowTree();
tree.Build(state, b => b.RoundRobin(
    b.Action(s => PatrolPointA(s)),
    b.Action(s => PatrolPointB(s)),
    b.Action(s => PatrolPointC(s))
));
```

### Scope（スコープ）

ノードの開始/終了時にコールバックを発火。

```csharp
// ステートレス版
var tree = new FlowTree();
tree.Build(
    Scope(
        () => Console.WriteLine("開始"),
        result => Console.WriteLine($"終了: {result}"),
        Sequence(
            Action(static () => DoSomething()),
            Wait(new TickDuration(60))  // 60 tick待機
        )
    )
);

// 状態付き版（FlowBuilder）
tree.Build(state, b => b.Scope(
    s => s.StartTime = DateTime.Now,
    (s, result) => s.EndTime = DateTime.Now,
    b.Action(s => Process(s))
));
```

### Wait（待機）

tick数または条件で待機。

```csharp
// tick待機
var tree = new FlowTree();
tree.Build(
    Sequence(
        Action(static () => ShowMessage()),
        Wait(new TickDuration(120)),  // 120 tick待機
        Action(static () => HideMessage())
    )
);

// 条件待機（条件がtrueになるまでRunning）
tree.Build(state, b => b.Sequence(
    b.Action(s => StartLoading(s)),
    b.WaitUntil(s => s.LevelLoaded),  // ロード完了まで待機
    b.Action(s => ShowLevel(s))
));
```

---

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** に以下が記載されている：

- 用語定義
- 全ノードリファレンス
- サブツリーとコールスタック
- 再帰の仕組み
- DSL詳細
- サンプル集（AI、非同期処理、UIフロー）

---

## ライセンス

MIT License
