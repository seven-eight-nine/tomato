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

### 1. シンプルなツリーを定義

```csharp
using Tomato.FlowTree;

var tree = new FlowTree();
tree.Build()
    .Sequence()
        .Action(static (ref FlowContext ctx) =>
        {
            Console.WriteLine("Step 1");
            return NodeStatus.Success;
        })
        .Action(static (ref FlowContext ctx) =>
        {
            Console.WriteLine("Step 2");
            return NodeStatus.Success;
        })
    .End()
    .Complete();
```

### 2. 実行

```csharp
var context = FlowContext.Create(new Blackboard(64), 0.016f);

var status = tree.Tick(ref context);
// 出力:
// Step 1
// Step 2
```

サブツリー呼び出しを使う場合はコールスタックを設定:

```csharp
var context = FlowContext.Create(
    new Blackboard(64),
    new FlowCallStack(32),
    deltaTime: 0.016f
);
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
| **Composite** | 複数の子を持つ | Sequence, Selector, Parallel |
| **Decorator** | 1つの子を修飾 | Repeat, Retry, Timeout |
| **Leaf** | 末端ノード | Action, Condition, SubTree |

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

### 動的サブツリー

SubTreeNodeで他のツリーを呼び出せる。FlowTree参照で直接指定するため、自己再帰・相互再帰が自然に書ける。

```csharp
// 自己再帰
var countdown = new FlowTree();
countdown.Build()
    .Selector()
        .Sequence()
            .Condition((ref FlowContext ctx) => ctx.Blackboard.GetInt(counterKey) <= 0)
            .Success()
        .End()
        .Sequence()
            .Action((ref FlowContext ctx) =>
            {
                var counter = ctx.Blackboard.GetInt(counterKey);
                ctx.Blackboard.SetInt(counterKey, counter - 1);
                return NodeStatus.Success;
            })
            .SubTree(countdown) // 自己参照
        .End()
    .End()
    .Complete();

// 相互再帰
var ping = new FlowTree("ping");
var pong = new FlowTree("pong");

ping.Build()
    .Sequence()
        .Action((ref FlowContext ctx) => { /* ... */ return NodeStatus.Success; })
        .SubTree(pong) // pongを参照
    .End()
    .Complete();

pong.Build()
    .Sequence()
        .Action((ref FlowContext ctx) => { /* ... */ return NodeStatus.Success; })
        .SubTree(ping) // pingを参照
    .End()
    .Complete();
```

---

## よく使うパターン

### Sequence（順次実行）

全て成功するまで順に実行。1つでも失敗したら即Failure。

```csharp
var tree = new FlowTree();
tree.Build()
    .Sequence()
        .Action((ref FlowContext ctx) => MoveToTarget(ref ctx))
        .Action((ref FlowContext ctx) => Attack(ref ctx))
        .Action((ref FlowContext ctx) => Retreat(ref ctx))
    .End()
    .Complete();
```

### Selector（フォールバック）

最初に成功したものを採用。全て失敗したらFailure。

```csharp
var tree = new FlowTree();
tree.Build()
    .Selector()
        .Guard((ref FlowContext ctx) => HasAmmo(ref ctx),
            new ActionNode((ref FlowContext ctx) => Shoot(ref ctx)))
        .Guard((ref FlowContext ctx) => HasMeleeWeapon(ref ctx),
            new ActionNode((ref FlowContext ctx) => MeleeAttack(ref ctx)))
        .Action((ref FlowContext ctx) => Retreat(ref ctx))  // フォールバック
    .End()
    .Complete();
```

### Parallel（並列実行）

全ての子を並列に評価。

```csharp
var tree = new FlowTree();
tree.Build()
    .Parallel()
        .Action((ref FlowContext ctx) => PlayAnimation(ref ctx))
        .Action((ref FlowContext ctx) => PlaySound(ref ctx))
        .Action((ref FlowContext ctx) => SpawnParticle(ref ctx))
    .End()
    .Complete();
```

### Join（全完了待機）

全ての子が完了するまで待機（WaitAll相当）。

```csharp
var tree = new FlowTree();
tree.Build()
    .Join()
        .Action((ref FlowContext ctx) => LoadTextures(ref ctx))
        .Action((ref FlowContext ctx) => LoadSounds(ref ctx))
        .Action((ref FlowContext ctx) => LoadData(ref ctx))
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
tree.Build()
    .Race()
        .SubTree(attackTree)
        .Node(new TimeoutNode(5.0f, new SubTreeNode(patrolTree)))
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
