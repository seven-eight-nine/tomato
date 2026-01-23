# FlowTree 設計書

コールスタック付き汎用フロー制御ライブラリの詳細設計ドキュメント。

namespace: `Tomato.FlowTree`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [ノードリファレンス](#ノードリファレンス)
5. [サブツリーとコールスタック](#サブツリーとコールスタック)
6. [再帰の仕組み](#再帰の仕組み)
7. [Blackboard](#blackboard)
8. [DSL詳細](#dsl詳細)
9. [サンプル集：AI行動選択](#サンプル集ai行動選択)
10. [サンプル集：非同期処理](#サンプル集非同期処理)
11. [サンプル集：UIフロー](#サンプル集uiフロー)
12. [パフォーマンス](#パフォーマンス)
13. [注意事項](#注意事項)
14. [ディレクトリ構造](#ディレクトリ構造)

---

## クイックスタート

### 1. ツリーを定義

```csharp
using Tomato.FlowTree;

// DSLで定義（推奨）
var patrolTree = new FlowTree("Patrol");
patrolTree.Build()
    .Sequence()
        .Action(static (ref FlowContext ctx) => GetNextWaypoint(ref ctx))
        .Action(static (ref FlowContext ctx) => MoveToWaypoint(ref ctx))
        .Wait(2.0f)  // 2秒待機
    .End()
    .Complete();
```

### 2. コンテキストを作成

```csharp
var context = FlowContext.Create(
    new Blackboard(64),
    new FlowCallStack(32),
    0.016f
);
```

### 3. 毎フレーム実行

```csharp
void Update(float deltaTime)
{
    context.DeltaTime = deltaTime;

    var status = patrolTree.Tick(ref context);

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
| **コンテキスト** | Context | 実行に必要な情報をまとめた構造体 |

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

---

## ノードリファレンス

### Composite Nodes（複合ノード）

#### SequenceNode

全ての子ノードが成功するまで順次実行。いずれかが失敗したら即座にFailureを返す。

```csharp
new SequenceNode(
    new ActionNode((ref FlowContext ctx) => Step1(ref ctx)),
    new ActionNode((ref FlowContext ctx) => Step2(ref ctx)),
    new ActionNode((ref FlowContext ctx) => Step3(ref ctx))
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
    new ActionNode((ref FlowContext ctx) => TryOption1(ref ctx)),  // 失敗したら次へ
    new ActionNode((ref FlowContext ctx) => TryOption2(ref ctx)),  // 失敗したら次へ
    new ActionNode((ref FlowContext ctx) => Fallback(ref ctx))     // 最後の手段
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
    new ActionNode((ref FlowContext ctx) => Task1(ref ctx)),
    new ActionNode((ref FlowContext ctx) => Task2(ref ctx))
)

// RequireOne: 1つ成功でSuccess、全失敗でFailure
new ParallelNode(ParallelPolicy.RequireOne,
    new ActionNode((ref FlowContext ctx) => Task1(ref ctx)),
    new ActionNode((ref FlowContext ctx) => Task2(ref ctx))
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
    new ActionNode((ref FlowContext ctx) => LoadTextures(ref ctx)),
    new ActionNode((ref FlowContext ctx) => LoadSounds(ref ctx)),
    new ActionNode((ref FlowContext ctx) => LoadData(ref ctx))
)
```

#### RandomSelectorNode

子ノードをランダムな順序で試行する。

```csharp
new RandomSelectorNode(
    new ActionNode((ref FlowContext ctx) => Attack1(ref ctx)),
    new ActionNode((ref FlowContext ctx) => Attack2(ref ctx)),
    new ActionNode((ref FlowContext ctx) => Attack3(ref ctx))
)
```

---

### Decorator Nodes（装飾ノード）

#### InverterNode

子の結果を反転する。

```csharp
// Success → Failure, Failure → Success
new InverterNode(
    new ConditionNode((ref FlowContext ctx) => IsEnemyVisible(ref ctx))
)
```

#### SucceederNode / FailerNode

常に指定した結果を返す。

```csharp
// 子が何を返してもSuccess
new SucceederNode(
    new ActionNode((ref FlowContext ctx) => TryOptionalAction(ref ctx))
)

// 子が何を返してもFailure
new FailerNode(
    new ActionNode((ref FlowContext ctx) => DoSomething(ref ctx))
)
```

#### RepeatNode

子を指定回数繰り返す。

```csharp
// 3回繰り返す
new RepeatNode(3,
    new ActionNode((ref FlowContext ctx) => DoSomething(ref ctx))
)
```

#### RepeatUntilFailNode / RepeatUntilSuccessNode

条件を満たすまで繰り返す。

```csharp
// 失敗するまで繰り返す
new RepeatUntilFailNode(
    new ActionNode((ref FlowContext ctx) => TryAction(ref ctx))
)

// 成功するまで繰り返す
new RepeatUntilSuccessNode(
    new ActionNode((ref FlowContext ctx) => TryAction(ref ctx))
)
```

#### RetryNode

失敗時に指定回数リトライする。

```csharp
// 最大3回リトライ
new RetryNode(3,
    new ActionNode((ref FlowContext ctx) => TryConnect(ref ctx))
)
```

#### TimeoutNode

指定時間内に完了しなければFailure。

```csharp
// 5秒でタイムアウト
new TimeoutNode(5.0f,
    new ActionNode((ref FlowContext ctx) => LongRunningTask(ref ctx))
)
```

#### DelayNode

指定時間待機してから子を実行する。

```csharp
// 1秒待ってから実行
new DelayNode(1.0f,
    new ActionNode((ref FlowContext ctx) => DoAfterDelay(ref ctx))
)
```

#### GuardNode

条件を満たした場合のみ子を実行する。

```csharp
// 条件を満たさなければ即座にFailure
new GuardNode(
    (ref FlowContext ctx) => HasTarget(ref ctx),
    new SubTreeNode(attackTree)
)
```

---

### Leaf Nodes（葉ノード）

#### ActionNode

アクションを実行する。

```csharp
new ActionNode(static (ref FlowContext ctx) =>
{
    // 処理を実行
    var target = ctx.Blackboard.GetObject<Entity>(targetKey);
    if (target == null) return NodeStatus.Failure;

    MoveToward(target);
    return IsAtTarget(target) ? NodeStatus.Success : NodeStatus.Running;
})
```

#### ConditionNode

条件を評価する。trueならSuccess、falseならFailure。

```csharp
new ConditionNode(static (ref FlowContext ctx) =>
{
    return ctx.Blackboard.GetBool(hasTargetKey);
})
```

#### SubTreeNode

別のツリーを呼び出す。FlowTree参照を直接指定。

```csharp
// 別ツリーを参照
var attackTree = new FlowTree("Attack");
// ... build attackTree ...

new SubTreeNode(attackTree)
```

#### WaitNode

指定時間待機する。

```csharp
new WaitNode(2.0f)  // 2秒待機
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

patrolTree.Build()
    // ... patrol logic ...
    .Complete();

attackTree.Build()
    // ... attack logic ...
    .Complete();

var aiTree = new FlowTree("AI");
aiTree.Build()
    .Selector()
        .Guard((ref FlowContext ctx) => HasTarget(ref ctx),
            new SubTreeNode(attackTree))  // attackTreeを呼び出し
        .SubTree(patrolTree)              // patrolTreeを呼び出し
    .End()
    .Complete();
```

### コールスタック

SubTreeNodeを通じてサブツリーを呼び出すと、コールスタックに記録される。

```csharp
// コンテキストにコールスタックを設定
var callStack = new FlowCallStack(32);  // 最大深度32
var context = FlowContext.Create(
    new Blackboard(64),
    callStack,
    0.016f
);

// 実行中のコールスタックを表示
for (int i = 0; i < callStack.Count; i++)
{
    var frame = callStack[i];
    Console.WriteLine($"[{i}] Tree: {frame.Tree?.Name ?? "(anonymous)"}");
}
```

### スタックオーバーフロー保護

`maxCallDepth`を超えるとSubTreeNodeはFailureを返す。

```csharp
var context = FlowContext.Create(
    new Blackboard(),
    new FlowCallStack(8),
    deltaTime: 0f,
    maxCallDepth: 8  // 最大8段まで
);
```

---

## 再帰の仕組み

### 自己再帰

FlowTree参照を直接使うことで、自然に自己再帰が書ける。

```csharp
var counterKey = new BlackboardKey<int>(1);

// カウントダウン再帰
var countdownTree = new FlowTree("Countdown");
countdownTree.Build()
    .Selector()
        // 終了条件: counter <= 0
        .Sequence()
            .Condition((ref FlowContext ctx) =>
                ctx.Blackboard.GetInt(counterKey) <= 0)
            .Action((ref FlowContext ctx) =>
            {
                Console.WriteLine("Done!");
                return NodeStatus.Success;
            })
        .End()
        // 再帰: counter-- して自己呼び出し
        .Sequence()
            .Action((ref FlowContext ctx) =>
            {
                var counter = ctx.Blackboard.GetInt(counterKey);
                Console.WriteLine(counter);
                ctx.Blackboard.SetInt(counterKey, counter - 1);
                return NodeStatus.Success;
            })
            .SubTree(countdownTree)  // 自己呼び出し
        .End()
    .End()
    .Complete();
```

### 相互再帰

複数のツリーが互いを参照することも可能。

```csharp
var counterKey = new BlackboardKey<int>(1);
var logKey = new BlackboardKey<string>(2);

var treeA = new FlowTree("A");
var treeB = new FlowTree("B");

// TreeA: "A"を記録してカウンタデクリメント、counter > 0ならTreeBを呼ぶ
treeA.Build()
    .Sequence()
        .Action((ref FlowContext ctx) =>
        {
            var log = ctx.Blackboard.GetString(logKey, "") ?? "";
            ctx.Blackboard.SetString(logKey, log + "A");
            var counter = ctx.Blackboard.GetInt(counterKey);
            ctx.Blackboard.SetInt(counterKey, counter - 1);
            return NodeStatus.Success;
        })
        .Selector()
            .Sequence()
                .Condition((ref FlowContext ctx) =>
                    ctx.Blackboard.GetInt(counterKey) > 0)
                .SubTree(treeB)  // TreeBを呼び出し
            .End()
            .Success()
        .End()
    .End()
    .Complete();

// TreeB: "B"を記録してカウンタデクリメント、counter > 0ならTreeAを呼ぶ
treeB.Build()
    .Sequence()
        .Action((ref FlowContext ctx) =>
        {
            var log = ctx.Blackboard.GetString(logKey, "") ?? "";
            ctx.Blackboard.SetString(logKey, log + "B");
            var counter = ctx.Blackboard.GetInt(counterKey);
            ctx.Blackboard.SetInt(counterKey, counter - 1);
            return NodeStatus.Success;
        })
        .Selector()
            .Sequence()
                .Condition((ref FlowContext ctx) =>
                    ctx.Blackboard.GetInt(counterKey) > 0)
                .SubTree(treeA)  // TreeAを呼び出し
            .End()
            .Success()
        .End()
    .End()
    .Complete();
```

### 深度ごとの状態管理

再帰呼び出し時、各ノードは呼び出し深度ごとに独立した状態を保持する。
これにより、同じノードインスタンスが異なる深度で同時に正しく動作する。

```
呼び出しスタック:
[depth 0] countdownTree (counter=3)
[depth 1] countdownTree (counter=2)  ← 状態が独立
[depth 2] countdownTree (counter=1)  ← 状態が独立
```

---

## Blackboard

### 型安全なキー

```csharp
// キーは静的に定義（ゼロGC）
public static class GameKeys
{
    public static readonly BlackboardKey<int> Score = new(1);
    public static readonly BlackboardKey<float> Health = new(2);
    public static readonly BlackboardKey<bool> IsAlive = new(3);
    public static readonly BlackboardKey<string> PlayerName = new(4);
}
```

### 基本操作

```csharp
var bb = new Blackboard(64);  // 初期容量

// 設定
bb.SetInt(GameKeys.Score, 100);
bb.SetFloat(GameKeys.Health, 75.5f);
bb.SetBool(GameKeys.IsAlive, true);
bb.SetString(GameKeys.PlayerName, "Player1");

// 取得（デフォルト値付き）
int score = bb.GetInt(GameKeys.Score, 0);
float health = bb.GetFloat(GameKeys.Health, 100f);
bool alive = bb.GetBool(GameKeys.IsAlive);
string name = bb.GetString(GameKeys.PlayerName, "Unknown");

// 存在確認
if (bb.TryGetInt(GameKeys.Score, out var s))
{
    Console.WriteLine($"Score: {s}");
}
```

### ScopedBlackboard

サブツリー内でのみ有効なスコープ付きBlackboard。

```csharp
var parent = new Blackboard(64);
parent.SetInt(scoreKey, 100);

using (var scoped = new ScopedBlackboard(parent))
{
    // 親の値を参照可能
    int score = scoped.GetInt(scoreKey);  // 100

    // ローカルで上書き（親には影響しない）
    scoped.SetInt(scoreKey, 200);

    // スコープ内では200
    Console.WriteLine(scoped.GetInt(scoreKey));  // 200
}

// スコープ外では元の値
Console.WriteLine(parent.GetInt(scoreKey));  // 100
```

---

## DSL詳細

### ビルダーパターン

```csharp
var tree = new FlowTree("MyTree");
tree.Build()
    .Sequence()
        .Action((ref FlowContext ctx) => DoAction(ref ctx))
        .Selector()
            .Condition((ref FlowContext ctx) => SomeCondition(ref ctx))
            .Action((ref FlowContext ctx) => Alternative(ref ctx))
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
    Action((ref FlowContext ctx) => Step1(ref ctx)),
    Action((ref FlowContext ctx) => Step2(ref ctx))
);

var sel = Selector(
    Condition((ref FlowContext ctx) => Check1(ref ctx)),
    Action((ref FlowContext ctx) => Fallback(ref ctx))
);
```

### デコレータの連鎖

```csharp
var tree = new FlowTree();
tree.Build()
    .Retry(3,
        new TimeoutNode(5.0f,
            new SequenceNode(
                new ActionNode((ref FlowContext ctx) => TryConnect(ref ctx)),
                new ActionNode((ref FlowContext ctx) => FetchData(ref ctx))
            )
        )
    )
    .Complete();
```

---

## サンプル集：AI行動選択

### 敵AI

```csharp
public static class EnemyAI
{
    // キー定義
    public static readonly BlackboardKey<bool> IsLowHealth = new(1);
    public static readonly BlackboardKey<bool> HasTarget = new(2);
    public static readonly BlackboardKey<bool> IsTargetInRange = new(3);

    // サブツリー
    private static readonly FlowTree AttackTree = new FlowTree("Attack");
    private static readonly FlowTree ChaseTree = new FlowTree("Chase");
    private static readonly FlowTree PatrolTree = new FlowTree("Patrol");
    private static readonly FlowTree FleeTree = new FlowTree("Flee");

    public static FlowTree CreateMainTree()
    {
        BuildSubTrees();

        var tree = new FlowTree("EnemyAI");
        tree.Build()
            .Selector()
                // 体力が低ければ逃げる
                .Guard((ref FlowContext ctx) => ctx.Blackboard.GetBool(IsLowHealth),
                    new SubTreeNode(FleeTree))
                // ターゲットがいる
                .Sequence()
                    .Condition((ref FlowContext ctx) => ctx.Blackboard.GetBool(HasTarget))
                    .Selector()
                        // 射程内なら攻撃
                        .Guard((ref FlowContext ctx) => ctx.Blackboard.GetBool(IsTargetInRange),
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

    private static void BuildSubTrees()
    {
        AttackTree.Build()
            .Sequence()
                .Action((ref FlowContext ctx) => { /* aim */ return NodeStatus.Success; })
                .Action((ref FlowContext ctx) => { /* fire */ return NodeStatus.Success; })
            .End()
            .Complete();

        PatrolTree.Build()
            .Sequence()
                .Action((ref FlowContext ctx) => { /* get waypoint */ return NodeStatus.Success; })
                .Action((ref FlowContext ctx) => { /* move to */ return NodeStatus.Running; })
                .Wait(2.0f)
            .End()
            .Complete();

        // ... other subtrees ...
    }
}
```

### ボスAI（フェーズ切り替え）

```csharp
public static class BossAI
{
    public static readonly BlackboardKey<int> Phase = new(1);
    public static readonly BlackboardKey<float> Health = new(2);

    private static readonly FlowTree Phase1Tree = new FlowTree("Phase1");
    private static readonly FlowTree Phase2Tree = new FlowTree("Phase2");
    private static readonly FlowTree Phase3Tree = new FlowTree("Phase3");

    public static FlowTree CreateMainTree()
    {
        BuildSubTrees();

        var tree = new FlowTree("BossAI");
        tree.Build()
            .Selector()
                // フェーズごとに分岐
                .Guard((ref FlowContext ctx) => ctx.Blackboard.GetInt(Phase) == 1,
                    new SubTreeNode(Phase1Tree))
                .Guard((ref FlowContext ctx) => ctx.Blackboard.GetInt(Phase) == 2,
                    new SubTreeNode(Phase2Tree))
                .SubTree(Phase3Tree)
            .End()
            .Complete();

        return tree;
    }

    private static void BuildSubTrees()
    {
        Phase1Tree.Build()
            .Sequence()
                .Action((ref FlowContext ctx) => { /* phase 1 attack */ return NodeStatus.Success; })
                .Action((ref FlowContext ctx) =>
                {
                    // HPが70%以下でフェーズ2へ
                    if (ctx.Blackboard.GetFloat(Health) <= 70f)
                        ctx.Blackboard.SetInt(Phase, 2);
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
public static FlowTree CreateLoadingFlow()
{
    var loadLevel = new FlowTree("LoadLevel");
    var loadAssets = new FlowTree("LoadAssets");
    var initializeSystems = new FlowTree("InitializeSystems");

    // 各サブツリーを構築
    loadLevel.Build()
        .Action((ref FlowContext ctx) =>
        {
            // レベルローディングのシミュレーション
            return IsLevelLoaded() ? NodeStatus.Success : NodeStatus.Running;
        })
        .Complete();

    loadAssets.Build()
        .Action((ref FlowContext ctx) =>
        {
            return AreAssetsLoaded() ? NodeStatus.Success : NodeStatus.Running;
        })
        .Complete();

    initializeSystems.Build()
        .Action((ref FlowContext ctx) =>
        {
            InitializeGameSystems();
            return NodeStatus.Success;
        })
        .Complete();

    // メインローディングフロー
    var tree = new FlowTree("GameLoading");
    tree.Build()
        .Sequence()
            .Action((ref FlowContext ctx) =>
            {
                ShowLoadingScreen();
                return NodeStatus.Success;
            })
            .Join()  // 全て完了まで待機
                .SubTree(loadLevel)
                .SubTree(loadAssets)
                .SubTree(initializeSystems)
            .End()
            .Action((ref FlowContext ctx) =>
            {
                HideLoadingScreen();
                return NodeStatus.Success;
            })
        .End()
        .Complete();

    return tree;
}
```

### ネットワークリクエスト with リトライ

```csharp
public static FlowTree CreateNetworkRequestFlow()
{
    var tree = new FlowTree("NetworkRequest");
    tree.Build()
        .Node(new RetryNode(3,  // 最大3回リトライ
            new TimeoutNode(10.0f,  // 10秒でタイムアウト
                new SequenceNode(
                    new ActionNode((ref FlowContext ctx) =>
                    {
                        SendRequest();
                        return NodeStatus.Success;
                    }),
                    new ActionNode((ref FlowContext ctx) =>
                    {
                        return IsResponseReceived()
                            ? NodeStatus.Success
                            : NodeStatus.Running;
                    }),
                    new ActionNode((ref FlowContext ctx) =>
                    {
                        ParseResponse();
                        return NodeStatus.Success;
                    })
                )
            )
        ))
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
public static readonly BlackboardKey<bool> IsConfirmed = new(1);

public static FlowTree CreateDialogFlow()
{
    var confirmBranch = new FlowTree("ConfirmBranch");
    var cancelBranch = new FlowTree("CancelBranch");

    confirmBranch.Build()
        .Action((ref FlowContext ctx) =>
        {
            ExecuteConfirmAction();
            return NodeStatus.Success;
        })
        .Complete();

    cancelBranch.Build()
        .Action((ref FlowContext ctx) =>
        {
            ExecuteCancelAction();
            return NodeStatus.Success;
        })
        .Complete();

    var tree = new FlowTree("DialogFlow");
    tree.Build()
        .Sequence()
            .Action((ref FlowContext ctx) =>
            {
                ShowDialog();
                return NodeStatus.Success;
            })
            .Action((ref FlowContext ctx) =>
            {
                return HasUserInput() ? NodeStatus.Success : NodeStatus.Running;
            })
            .Selector()
                .Guard((ref FlowContext ctx) => ctx.Blackboard.GetBool(IsConfirmed),
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
public static FlowTree CreateTutorialFlow()
{
    var tree = new FlowTree("Tutorial");
    tree.Build()
        .Sequence()
            // ステップ1: 移動の説明
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    ShowTutorialMessage("WASDで移動できます");
                    return NodeStatus.Success;
                })
                .Action((ref FlowContext ctx) =>
                {
                    return HasPlayerMoved() ? NodeStatus.Success : NodeStatus.Running;
                })
            .End()
            // ステップ2: 攻撃の説明
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    ShowTutorialMessage("クリックで攻撃できます");
                    return NodeStatus.Success;
                })
                .Action((ref FlowContext ctx) =>
                {
                    return HasPlayerAttacked() ? NodeStatus.Success : NodeStatus.Running;
                })
            .End()
            // 完了
            .Action((ref FlowContext ctx) =>
            {
                ShowTutorialMessage("チュートリアル完了！");
                return NodeStatus.Success;
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
| Blackboard | Dictionary事前確保 |
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
            .Action((ref FlowContext ctx) =>
            {
                sharedCounter++;  // 全深度で共有される！
                return NodeStatus.Success;
            })
            .SubTree(tree)  // 再帰
        .End()
    .End()
    .Complete();

// 良い例：Blackboardを使う
var counterKey = new BlackboardKey<int>(1);
var tree = new FlowTree();
tree.Build()
    .Selector()
        .Sequence()
            .Action((ref FlowContext ctx) =>
            {
                // Blackboardは深度間で共有されるが、明示的な設計
                var counter = ctx.Blackboard.GetInt(counterKey);
                ctx.Blackboard.SetInt(counterKey, counter + 1);
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
      Leaf/
        ActionNode.cs
        ConditionNode.cs
        SubTreeNode.cs
        WaitNode.cs
        YieldNode.cs
        SuccessNode.cs
        FailureNode.cs

    Context/
      FlowContext.cs             # 実行コンテキスト（struct）

    Blackboard/
      BlackboardKey.cs           # readonly struct キー
      Blackboard.cs              # 型別ストレージ
      ScopedBlackboard.cs        # スコープ付き

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
