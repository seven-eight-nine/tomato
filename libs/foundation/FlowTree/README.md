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

var tree = new FlowTree();
tree.Build()
    .Sequence()
        .Do(static () => Console.WriteLine("Step 1"))
        .Do(static () => Console.WriteLine("Step 2"))
    .End()
    .Complete();
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

tree.Build(state)
    .Sequence()
        .Do(s => s.Score += 100)
        .Condition(s => s.IsAlive)
    .End()
    .Complete();
```

### 3. 実行

```csharp
var status = tree.Tick(0.016f);
// 出力:
// Step 1
// Step 2
```

サブツリー呼び出しを使う場合はコールスタックを設定:

```csharp
tree.WithCallStack(new FlowCallStack(32));
var status = tree.Tick(0.016f);
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
| **Composite** | 複数の子を持つ | Sequence, Selector, Parallel, ShuffledSelector |
| **Decorator** | 1つの子を修飾 | Repeat, Retry, Timeout, Event |
| **Leaf** | 末端ノード | Action, Condition, SubTree, DynamicSubTree, Wait |

### FlowTreeの構造

FlowTreeは「入れ物」と「中身」が分離している:

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
countdown
    .WithCallStack(new FlowCallStack(32))
    .Build(state)
    .Selector()
        .Sequence()
            .Condition(s => s.Counter <= 0)
            .Success()
        .End()
        .Sequence()
            .Do(s => s.Counter--)
            .SubTree(countdown) // 自己参照
        .End()
    .End()
    .Complete();

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
tree.Build(state)
    .SubTree(s => s.Difficulty > 5 ? hardTree : easyTree)  // 動的選択
    .Complete();
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
childTree.Build(new ChildState())
    .Do(s =>
    {
        s.LocalScore = 100;
        // 親Stateにアクセス
        var parent = (ParentState)s.Parent!;
        parent.TotalScore += s.LocalScore;
    })
    .Complete();

var mainTree = new FlowTree();
mainTree
    .WithCallStack(new FlowCallStack(32))
    .Build(new ParentState())
    .SubTree<ChildState>(childTree, p => new ChildState())  // State注入
    .Complete();
```

---

## よく使うパターン

### Sequence（順次実行）

全て成功するまで順に実行。1つでも失敗したら即Failure。

```csharp
var tree = new FlowTree();
tree.Build(state)
    .Sequence()
        .Action(s => MoveToTarget(s))
        .Action(s => Attack(s))
        .Action(s => Retreat(s))
    .End()
    .Complete();
```

### Selector（フォールバック）

最初に成功したものを採用。全て失敗したらFailure。

```csharp
var tree = new FlowTree();
tree.Build(state)
    .Selector()
        .Guard(s => s.HasAmmo,
            new ActionNode<GameState>(s => Shoot(s)))
        .Guard(s => s.HasMeleeWeapon,
            new ActionNode<GameState>(s => MeleeAttack(s)))
        .Action(s => Retreat(s))  // フォールバック
    .End()
    .Complete();
```

### Parallel（並列実行）

全ての子を並列に評価。

```csharp
var tree = new FlowTree();
tree.Build(state)
    .Parallel()
        .Action(s => PlayAnimation(s))
        .Action(s => PlaySound(s))
        .Action(s => SpawnParticle(s))
    .End()
    .Complete();
```

### Join（全完了待機）

全ての子が完了するまで待機（WaitAll相当）。

```csharp
var tree = new FlowTree();
tree.Build(state)
    .Join()
        .Action(s => LoadTextures(s))
        .Action(s => LoadSounds(s))
        .Action(s => LoadData(s))
    .End()
    .Complete();
```

### Race（最初の完了を採用）

最初に完了した子の結果を採用（WaitAny相当）。

```csharp
var attackTree = new FlowTree("Attack");
var patrolTree = new FlowTree("Patrol");
// ... build trees ...

var tree = new FlowTree();
tree
    .WithCallStack(new FlowCallStack(16))
    .Build()
    .Race()
        .SubTree(attackTree)
        .Timeout(5.0f)
            .SubTree(patrolTree)
        .End()
    .End()
    .Complete();
```

### ShuffledSelector（シャッフル選択）

全選択肢を一巡するまで同じものを選ばない（シャッフル再生）。

```csharp
var tree = new FlowTree();
tree.Build(state)
    .ShuffledSelector()
        .Action(s => PlayDialogue1(s))
        .Action(s => PlayDialogue2(s))
        .Action(s => PlayDialogue3(s))
    .End()
    .Complete();
```

### WeightedRandomSelector（重み付きランダム）

各子ノードの重みに基づいて確率的に選択。

```csharp
var tree = new FlowTree();
tree.Build()
    .WeightedRandomSelector()
        .Weighted(3.0f, new ActionNode(static () => CommonAction()))   // 75%
        .Weighted(1.0f, new ActionNode(static () => RareAction()))     // 25%
    .End()
    .Complete();
```

### RoundRobin（順番選択）

0→1→2→0→... と順番に選択。

```csharp
var tree = new FlowTree();
tree.Build(state)
    .RoundRobin()
        .Action(s => PatrolPointA(s))
        .Action(s => PatrolPointB(s))
        .Action(s => PatrolPointC(s))
    .End()
    .Complete();
```

### Event（イベント発火）

ノードの開始/終了時にイベントを発火。

```csharp
// ステートレス版
var tree = new FlowTree();
tree.Build()
    .Event(
        onEnter: () => { Console.WriteLine("開始"); },
        onExit: result => { Console.WriteLine($"終了: {result}"); })
    .Sequence()
        .Action(static () => DoSomething())
        .Wait(1.0f)
    .End()
    .Complete();

// 状態付き版
tree.Build(state)
    .Event(
        onEnter: s => { s.StartTime = DateTime.Now; },
        onExit: (s, result) => { s.EndTime = DateTime.Now; })
    .Action(s => Process(s))
    .Complete();
```

### Wait（待機）

時間または条件で待機。

```csharp
// 時間待機
var tree = new FlowTree();
tree.Build()
    .Sequence()
        .Action(static () => ShowMessage())
        .Wait(2.0f)  // 2秒待機
        .Action(static () => HideMessage())
    .End()
    .Complete();

// 条件待機（条件がtrueになるまでRunning）
tree.Build(state)
    .Sequence()
        .Action(s => StartLoading(s))
        .Wait(s => s.LevelLoaded)  // ロード完了まで待機
        .Action(s => ShowLevel(s))
    .End()
    .Complete();
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
