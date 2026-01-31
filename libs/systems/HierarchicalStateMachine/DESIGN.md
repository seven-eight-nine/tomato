# HierarchicalStateMachine 設計書

階層型ステートマシン + A*パス探索ライブラリの詳細設計ドキュメント。

namespace: `Tomato.HierarchicalStateMachine`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [状態（State）詳細](#状態state詳細)
5. [遷移（Transition）詳細](#遷移transition詳細)
6. [状態グラフ（StateGraph）詳細](#状態グラフstategraph詳細)
7. [A*探索アルゴリズム](#a探索アルゴリズム)
8. [階層状態（HierarchicalState）詳細](#階層状態hierarchicalstate詳細)
9. [ヒューリスティック関数](#ヒューリスティック関数)
10. [コンテキスト](#コンテキスト)
11. [ステートマシン操作](#ステートマシン操作)
12. [可視化インターフェース](#可視化インターフェース)
13. [パフォーマンス](#パフォーマンス)
14. [実践パターン集](#実践パターン集)
15. [トラブルシューティング](#トラブルシューティング)

---

## クイックスタート

### 1. コンテキストを定義

```csharp
using Tomato.HierarchicalStateMachine;

public class AIContext : PathfindingContextBase
{
    public float Health { get; set; } = 1.0f;
    public bool HasTarget { get; set; }
    public float DistanceToTarget { get; set; }
}
```

### 2. 状態を定義

```csharp
public class PatrolState : StateBase<AIContext>
{
    public PatrolState() : base("Patrol") { }

    public override void OnEnter(AIContext ctx)
    {
        // パトロール開始処理
    }

    public override void OnTick(AIContext ctx, int deltaTicks)
    {
        // パトロール中の更新処理
    }

    public override void OnExit(AIContext ctx)
    {
        // パトロール終了処理
    }
}
```

### 3. グラフを構築

```csharp
var graph = new StateGraph<AIContext>()
    .AddState(new PatrolState())
    .AddState(new StateBase<AIContext>("Chase"))
    .AddState(new StateBase<AIContext>("Attack"))
    .AddState(new StateBase<AIContext>("Flee"))
    .AddTransition(new Transition<AIContext>("Patrol", "Chase", 1f,
        ctx => ctx.HasTarget))
    .AddTransition(new Transition<AIContext>("Chase", "Attack", 1f,
        ctx => ctx.DistanceToTarget < 2f))
    .AddTransition(new Transition<AIContext>("Attack", "Chase", 0.5f))
    .AddTransition(new Transition<AIContext>(StateId.Any, "Flee", 0f,
        ctx => ctx.Health < 0.2f));
```

### 4. ステートマシンを使用

```csharp
var sm = new HierarchicalStateMachine<AIContext>(graph);
var context = new AIContext();

sm.Initialize("Patrol", context);

// ゲームループ
void Tick(int deltaTicks)
{
    // 状態を更新
    sm.Tick(context, deltaTicks);

    // 条件が変わったらパスを再計画
    if (context.HasTarget && sm.CurrentStateId == "Patrol")
    {
        var path = sm.PlanPath("Attack", context);
        if (path.IsValid)
        {
            sm.ExecuteAllSteps(context);
        }
    }
}
```

---

## 用語定義

### 中核概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **状態** | State | エージェントの振る舞いを定義するノード。OnEnter/OnExit/OnTickを持つ。 |
| **遷移** | Transition | 状態間の接続。コストと条件を持つ。 |
| **状態グラフ** | StateGraph | 状態と遷移のネットワーク。有向グラフ構造。 |
| **パス** | Path | 開始状態からゴール状態への遷移の列。 |

### 探索関連

| 用語 | 英語 | 定義 |
|------|------|------|
| **コスト** | Cost | 遷移の「重み」。低いほど優先される。 |
| **ヒューリスティック** | Heuristic | ゴールまでの推定コスト。A*の効率を向上させる。 |
| **オープンリスト** | Open List | 探索候補のノード群。優先度キューで管理。 |
| **クローズドリスト** | Closed List | 探索済みのノード群。 |

### 階層関連

| 用語 | 英語 | 定義 |
|------|------|------|
| **階層状態** | Hierarchical State | サブグラフを持つ状態。入れ子構造を実現。 |
| **サブグラフ** | SubGraph | 階層状態内部の状態グラフ。 |
| **Any State** | Any State | どの状態からでも遷移可能な仮想状態。 |

---

## 設計哲学

### 原則1: 宣言的グラフ構築

状態と遷移を宣言的に定義する。手続き的な遷移ロジックを排除。

```csharp
// ✓ 良い例（宣言的）
var graph = new StateGraph<AIContext>()
    .AddState(new IdleState())
    .AddState(new AttackState())
    .AddTransition(new Transition<AIContext>("Idle", "Attack", 1f,
        ctx => ctx.HasTarget));

// ❌ 悪い例（手続き的）
void Update()
{
    if (currentState == "Idle" && context.HasTarget)
        TransitionTo("Attack");  // 遷移ロジックが分散
}
```

### 原則2: コスト最適化

A*アルゴリズムにより、常に最小コストのパスを選択する。

```csharp
// 距離が近いほど低コスト → 近い敵を優先
new Transition<AIContext>("Patrol", "Chase",
    ctx => ctx.DistanceToTarget)

// HPが低いほど高コスト → 安全を優先
new Transition<AIContext>("Chase", "Attack",
    ctx => 10f / ctx.Health)
```

### 原則3: 条件による遷移制御

遷移の可否を条件関数で制御する。グラフ構造を変更せずに動的制御が可能。

```csharp
// スタミナがある場合のみ攻撃可能
new Transition<AIContext>("Ready", "Attack", 1f,
    ctx => ctx.Stamina > 0)

// クールダウン中は遷移不可
new Transition<AIContext>("Attack", "SpecialAttack", 0.5f,
    ctx => !ctx.IsOnCooldown)
```

### 原則4: 階層による複雑性管理

複雑な状態を階層構造で整理する。

```csharp
// Combat状態の中にサブ状態を持つ
var combatGraph = new StateGraph<AIContext>()
    .AddState(new StateBase<AIContext>("WindUp"))
    .AddState(new StateBase<AIContext>("Strike"))
    .AddState(new StateBase<AIContext>("Recover"));

var combat = new HierarchicalState<AIContext>("Combat", combatGraph, "WindUp");
mainGraph.AddState(combat);
```

---

## 状態（State）詳細

### インターフェース

```csharp
public interface IState<TContext>
{
    /// <summary>状態の識別子。</summary>
    StateId Id { get; }

    /// <summary>状態に入った時に呼び出される。</summary>
    void OnEnter(TContext context);

    /// <summary>状態から出る時に呼び出される。</summary>
    void OnExit(TContext context);

    /// <summary>状態の更新処理。</summary>
    void OnTick(TContext context, int deltaTicks);
}
```

### StateBase基底クラス

```csharp
public abstract class StateBase<TContext> : IState<TContext>
{
    public StateId Id { get; }

    protected StateBase(StateId id)
    {
        Id = id;
    }

    // デフォルトは空実装
    public virtual void OnEnter(TContext context) { }
    public virtual void OnExit(TContext context) { }
    public virtual void OnTick(TContext context, int deltaTicks) { }
}
```

### 状態の実装例

```csharp
public class PatrolState : StateBase<AIContext>
{
    private readonly Vector3[] _waypoints;
    private int _currentWaypoint;

    public PatrolState(Vector3[] waypoints) : base("Patrol")
    {
        _waypoints = waypoints;
    }

    public override void OnEnter(AIContext ctx)
    {
        _currentWaypoint = 0;
        ctx.Agent.SetDestination(_waypoints[0]);
    }

    public override void OnTick(AIContext ctx, int deltaTicks)
    {
        if (ctx.Agent.HasReachedDestination)
        {
            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
            ctx.Agent.SetDestination(_waypoints[_currentWaypoint]);
        }
    }

    public override void OnExit(AIContext ctx)
    {
        ctx.Agent.Stop();
    }
}
```

### StateId

```csharp
public readonly struct StateId : IEquatable<StateId>
{
    public string Value { get; }

    // 暗黙的変換
    public static implicit operator StateId(string value) => new StateId(value);
    public static implicit operator string(StateId id) => id.Value;

    // Any State
    public static readonly StateId Any = new StateId("__ANY__");
    public bool IsAny => Value == Any.Value;
}
```

**使用例:**

```csharp
StateId idle = "Idle";           // 暗黙的変換
StateId attack = new StateId("Attack");  // 明示的構築

if (currentState == StateId.Any)
{
    // Any State判定
}
```

---

## 遷移（Transition）詳細

### コンストラクタ

```csharp
public class Transition<TContext>
{
    // 1. 静的コストのみ
    public Transition(StateId from, StateId to, float cost = 1f);

    // 2. 静的コスト + 条件
    public Transition(StateId from, StateId to, float cost,
        Func<TContext, bool> condition);

    // 3. 動的コストのみ
    public Transition(StateId from, StateId to,
        Func<TContext, float> dynamicCost);

    // 4. 条件 + 動的コスト
    public Transition(StateId from, StateId to,
        Func<TContext, bool> condition,
        Func<TContext, float> dynamicCost);
}
```

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|---|------|
| `From` | `StateId` | 遷移元の状態ID |
| `To` | `StateId` | 遷移先の状態ID |
| `BaseCost` | `float` | 静的コスト（固定値） |
| `HasCondition` | `bool` | 条件が設定されているか |
| `HasDynamicCost` | `bool` | 動的コストが設定されているか |

### メソッド

| メソッド | 戻り値 | 説明 |
|---------|--------|------|
| `CanTransition(context)` | `bool` | 遷移条件を評価 |
| `GetCost(context)` | `float` | 遷移コストを計算 |

### 遷移パターン

**1. 固定コスト:**

```csharp
new Transition<AIContext>("A", "B", 5f)
// コストは常に5
```

**2. 条件付き固定コスト:**

```csharp
new Transition<AIContext>("Idle", "Attack", 1f,
    ctx => ctx.HasTarget && ctx.Stamina > 10)
// ターゲットがいてスタミナがある場合のみ遷移可能
```

**3. 動的コスト:**

```csharp
new Transition<AIContext>("Patrol", "Chase",
    ctx => ctx.DistanceToTarget * 0.5f)
// 距離に比例したコスト
```

**4. 条件 + 動的コスト:**

```csharp
new Transition<AIContext>("Chase", "Attack",
    ctx => ctx.DistanceToTarget < 5f,   // 5m以内なら遷移可能
    ctx => ctx.DistanceToTarget)         // 距離がコスト
```

### Any State遷移

どの状態からでも遷移可能な特殊遷移：

```csharp
// 死亡遷移（どこからでも）
graph.AddTransition(new Transition<AIContext>(StateId.Any, "Dead", 0f,
    ctx => ctx.Health <= 0));

// 緊急回避（どこからでも）
graph.AddTransition(new Transition<AIContext>(StateId.Any, "Dodge", 0f,
    ctx => ctx.IsInDanger));
```

**注意:** Any State遷移は自分自身への遷移を除外する。
（例: Dead状態からDeadへのAny State遷移は適用されない）

---

## 状態グラフ（StateGraph）詳細

### 構築

```csharp
var graph = new StateGraph<AIContext>();

// 状態を追加
graph.AddState(new IdleState());
graph.AddState(new ChaseState());
graph.AddState(new AttackState());

// 遷移を追加
graph.AddTransition(new Transition<AIContext>("Idle", "Chase", 1f));
graph.AddTransition(new Transition<AIContext>("Chase", "Attack", 1f));
graph.AddTransition(new Transition<AIContext>("Attack", "Idle", 2f));
```

### Fluent API

```csharp
var graph = new StateGraph<AIContext>()
    .AddState(new IdleState())
    .AddState(new ChaseState())
    .AddState(new AttackState())
    .AddTransition(new Transition<AIContext>("Idle", "Chase", 1f))
    .AddTransition(new Transition<AIContext>("Chase", "Attack", 1f))
    .AddTransition(new Transition<AIContext>("Attack", "Idle", 2f));
```

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|---|------|
| `States` | `IReadOnlyDictionary<StateId, IState>` | 登録済み状態 |
| `Transitions` | `IReadOnlyDictionary<StateId, List<Transition>>` | 状態ごとの遷移 |
| `AnyStateTransitions` | `IReadOnlyList<Transition>` | Any State遷移 |
| `StateCount` | `int` | 状態数 |

### メソッド

| メソッド | 説明 |
|---------|------|
| `AddState(state)` | 状態を追加 |
| `AddTransition(transition)` | 遷移を追加 |
| `GetState(id)` | 状態を取得（なければnull） |
| `HasState(id)` | 状態の存在確認 |
| `GetTransitionsFrom(id)` | 指定状態からの遷移を取得（Any State含む） |

### エラー処理

```csharp
// 重複状態
graph.AddState(new StateBase<AIContext>("A"));
graph.AddState(new StateBase<AIContext>("A"));  // InvalidOperationException

// 存在しない状態への遷移
graph.AddTransition(new Transition<AIContext>("X", "Y", 1f));
// InvalidOperationException: Source/Target state not found
```

---

## A*探索アルゴリズム

### 概要

A*は「現在までのコスト (g) + ゴールまでの推定コスト (h)」を最小化するパスを探索する。

```
f(n) = g(n) + h(n)

f(n): ノードnを通るパスの推定総コスト
g(n): 開始からノードnまでの実コスト
h(n): ノードnからゴールまでの推定コスト（ヒューリスティック）
```

### 探索フロー

```
FindPath(start, goal, context)
│
├─1. 検証
│   ├─ start存在確認 → InvalidStart
│   ├─ goal存在確認 → InvalidGoal
│   └─ start == goal → 即座にFound返却
│
├─2. 初期化
│   ├─ openSet = { start }
│   ├─ gScore[start] = 0
│   └─ fScore[start] = h(start, goal)
│
├─3. メインループ
│   │
│   │  ┌─────────────────────────────────────────────┐
│   └─►│ WHILE openSet is not empty                  │
│      │   │                                         │
│      │   ├─ タイムアウト確認                         │
│      │   │   └─ Timeout → 部分パス or Timeout返却    │
│      │   │                                         │
│      │   ├─ 最大反復回数確認                         │
│      │   │   └─ 超過 → 部分パス or Timeout返却       │
│      │   │                                         │
│      │   ├─ current = openSet.Dequeue()            │
│      │   │                                         │
│      │   ├─ current == goal?                       │
│      │   │   └─ YES → パス復元、Found返却            │
│      │   │                                         │
│      │   └─ FOR each transition from current       │
│      │       │                                     │
│      │       ├─ CanTransition(context)?            │
│      │       │   └─ NO → スキップ                   │
│      │       │                                     │
│      │       ├─ tentativeG = gScore[current]       │
│      │       │             + transition.GetCost()  │
│      │       │                                     │
│      │       └─ tentativeG < gScore[neighbor]?     │
│      │           └─ YES → 更新してopenSetに追加     │
│      └─────────────────────────────────────────────┘
│
└─4. パス未発見
    └─ NotFound返却
```

### PathfindingResult

| 値 | 説明 |
|----|------|
| `Found` | パスが見つかった |
| `NotFound` | パスが存在しない |
| `Timeout` | タイムアウトまたは反復上限 |
| `Cancelled` | 探索がキャンセルされた |
| `InvalidStart` | 開始状態が存在しない |
| `InvalidGoal` | ゴール状態が存在しない |

### TransitionPath

```csharp
public class TransitionPath<TContext>
{
    public PathfindingResult Result { get; }
    public IReadOnlyList<Transition<TContext>> Transitions { get; }
    public IReadOnlyList<StateId> States { get; }
    public float TotalCost { get; }
    public int NodesVisited { get; }
    public double ElapsedMilliseconds { get; }

    public bool IsValid => Result == PathfindingResult.Found;
    public bool IsEmpty => Transitions.Count == 0;
}
```

### PathfindingOptions

```csharp
public class PathfindingOptions
{
    /// <summary>最大反復回数（デフォルト: 10000）</summary>
    public int MaxIterations { get; set; } = 10000;

    /// <summary>タイムアウト（ms）。0で無制限。</summary>
    public double TimeoutMilliseconds { get; set; } = 0;

    /// <summary>タイムアウト時に部分パスを返すか</summary>
    public bool AllowPartialPath { get; set; } = false;

    /// <summary>階層探索の最大深度</summary>
    public int MaxDepth { get; set; } = 100;

    public static readonly PathfindingOptions Default = new();
}
```

### 使用例

```csharp
var finder = new HierarchicalPathFinder<AIContext>(graph);

// 基本探索
var path = finder.FindPath("Idle", "Attack", context);

// オプション付き探索
var options = new PathfindingOptions
{
    TimeoutMilliseconds = 50,
    AllowPartialPath = true
};
var path = finder.FindPath("Idle", "Attack", context, options);

// 複数ゴールへの探索
var path = finder.FindPathToAny("Patrol",
    new[] { new StateId("Rest"), new StateId("Base") }, context);
```

---

## 階層状態（HierarchicalState）詳細

### インターフェース

```csharp
public interface IHierarchicalState<TContext> : IState<TContext>
{
    StateGraph<TContext>? SubGraph { get; }
    StateId? InitialSubStateId { get; }
    StateId? CurrentSubStateId { get; }
    bool HasSubGraph { get; }

    void EnterSubState(StateId subStateId, TContext context);
    void ExitSubState(TContext context);
}
```

### 構築

```csharp
// サブグラフなし（通常状態として動作）
var simpleState = new HierarchicalState<AIContext>("Simple");

// サブグラフあり
var combatSubGraph = new StateGraph<AIContext>()
    .AddState(new StateBase<AIContext>("WindUp"))
    .AddState(new StateBase<AIContext>("Strike"))
    .AddState(new StateBase<AIContext>("Recover"))
    .AddTransition(new Transition<AIContext>("WindUp", "Strike", 0.5f))
    .AddTransition(new Transition<AIContext>("Strike", "Recover", 0.5f));

var combatState = new HierarchicalState<AIContext>(
    "Combat",
    combatSubGraph,
    "WindUp"  // 初期サブ状態
);
```

### ライフサイクル

```
CombatState.OnEnter(context)
│
├─ 親状態の初期化処理
│
└─ サブグラフあり?
    └─ YES → WindUp.OnEnter(context)
              CurrentSubStateId = "WindUp"

CombatState.OnTick(context, deltaTicks)
│
├─ 親状態の更新処理
│
└─ CurrentSubState?.OnTick(context, deltaTicks)

CombatState.OnExit(context)
│
├─ CurrentSubState?.OnExit(context)
│   CurrentSubStateId = null
│
└─ 親状態の終了処理
```

### サブ状態の遷移

```csharp
// 階層状態を取得
var combat = graph.GetState("Combat") as IHierarchicalState<AIContext>;

// サブ状態を切り替え
combat.EnterSubState("Strike", context);
// WindUp.OnExit → Strike.OnEnter

// サブ状態から離脱
combat.ExitSubState(context);
// CurrentSubStateId = null
```

---

## ヒューリスティック関数

### インターフェース

```csharp
public interface IHeuristic<TContext>
{
    /// <summary>
    /// 現在状態からゴールまでの推定コストを返す。
    /// 0以上かつ実コスト以下であること（許容的ヒューリスティック）。
    /// </summary>
    float Estimate(StateId current, StateId goal, TContext context);
}
```

### ZeroHeuristic（デフォルト）

常に0を返す。A*はダイクストラ法と等価になる。

```csharp
public class ZeroHeuristic<TContext> : IHeuristic<TContext>
{
    public static readonly ZeroHeuristic<TContext> Instance = new();

    public float Estimate(StateId current, StateId goal, TContext context)
        => 0f;
}
```

### DelegateHeuristic

ラムダ式でヒューリスティックを定義：

```csharp
var heuristic = new DelegateHeuristic<AIContext>(
    (current, goal, ctx) =>
    {
        // 状態名の距離をヒューリスティックとして使用（例）
        var currentPos = GetStatePosition(current);
        var goalPos = GetStatePosition(goal);
        return Vector3.Distance(currentPos, goalPos);
    });

var finder = new HierarchicalPathFinder<AIContext>(graph, heuristic);
```

### カスタムヒューリスティック

```csharp
public class GameStateHeuristic : IHeuristic<AIContext>
{
    private readonly Dictionary<(StateId, StateId), float> _estimates;

    public GameStateHeuristic()
    {
        // 事前計算されたヒューリスティック値
        _estimates = new Dictionary<(StateId, StateId), float>
        {
            { ("Idle", "Attack"), 2f },
            { ("Patrol", "Attack"), 3f },
            { ("Chase", "Attack"), 1f },
        };
    }

    public float Estimate(StateId current, StateId goal, AIContext context)
    {
        if (_estimates.TryGetValue((current, goal), out var estimate))
            return estimate;
        return 0f;  // 不明な場合は0（安全側）
    }
}
```

### ヒューリスティックの選択

| ヒューリスティック | 特性 | 用途 |
|-------------------|------|------|
| `ZeroHeuristic` | 常に最適解、探索量多い | 小規模グラフ、正確性重視 |
| カスタム | 探索量削減、実装コスト | 大規模グラフ、性能重視 |

---

## コンテキスト

### PathfindingContextBase

```csharp
public abstract class PathfindingContextBase
{
    /// <summary>現在の探索深度</summary>
    public int CurrentDepth { get; set; }

    /// <summary>最大探索深度</summary>
    public int MaxDepth { get; set; } = 100;

    /// <summary>探索がキャンセルされたか</summary>
    public bool IsCancelled { get; private set; }

    public void Cancel() => IsCancelled = true;

    public virtual void Reset()
    {
        CurrentDepth = 0;
        IsCancelled = false;
    }
}
```

### カスタムコンテキスト

```csharp
public class AIContext : PathfindingContextBase
{
    // キャラクター状態
    public float Health { get; set; } = 1.0f;
    public float Stamina { get; set; } = 100f;
    public bool IsStunned { get; set; }

    // 戦闘情報
    public bool HasTarget { get; set; }
    public float DistanceToTarget { get; set; }
    public bool IsInCombat { get; set; }

    // 環境情報
    public Vector3 Position { get; set; }
    public bool IsInSafeZone { get; set; }

    // ゲームオブジェクト参照
    public NavMeshAgent Agent { get; set; }
    public Animator Animator { get; set; }

    public override void Reset()
    {
        base.Reset();
        // カスタムリセット処理
    }
}
```

---

## ステートマシン操作

### 初期化

```csharp
var sm = new HierarchicalStateMachine<AIContext>(graph);

// ヒューリスティック付き
var sm = new HierarchicalStateMachine<AIContext>(graph, myHeuristic);

// 初期状態設定
sm.Initialize("Idle", context);
// → Idle.OnEnter(context) が呼ばれる
```

### パス計画と実行

```csharp
// パスを計画
var path = sm.PlanPath("Attack", context);

if (path.IsValid)
{
    // ステップ単位で実行
    while (!sm.IsPathComplete)
    {
        if (sm.ExecuteNextStep(context))
        {
            Console.WriteLine($"遷移: {sm.CurrentStateId}");
        }
        else
        {
            Console.WriteLine("遷移失敗（条件未満足）");
            break;
        }
    }

    // または一括実行
    sm.ExecuteAllSteps(context);
}
```

### 直接遷移

```csharp
// 遷移チェックあり
if (sm.TransitionTo("Attack", context))
{
    Console.WriteLine("遷移成功");
}
else
{
    Console.WriteLine("遷移失敗（遷移が存在しないか条件未満足）");
}

// 強制遷移（遷移チェックなし）
sm.ForceTransitionTo("Dead", context);
```

### 更新

```csharp
void Tick(int deltaTicks)
{
    sm.Tick(context, deltaTicks);
    // → CurrentState.OnTick(context, deltaTicks)
}
```

### パス管理

```csharp
// パスをクリア
sm.ClearPath();

// 現在のパスを再計画（同じゴールへ）
var newPath = sm.ReplanPath(context);
```

### プロパティ

| プロパティ | 型 | 説明 |
|-----------|---|------|
| `CurrentStateId` | `StateId?` | 現在の状態ID |
| `CurrentState` | `IState?` | 現在の状態 |
| `CurrentPath` | `TransitionPath?` | 計画されたパス |
| `CurrentPathIndex` | `int` | パス内の現在位置 |
| `IsPathComplete` | `bool` | パス実行完了か |
| `Graph` | `StateGraph` | 状態グラフ |

---

## 可視化インターフェース

### IGraphVisualizer

```csharp
public interface IGraphVisualizer<TContext>
{
    void BeginDraw();
    void DrawState(IState<TContext> state, bool isCurrent, bool isInPath);
    void DrawTransition(Transition<TContext> transition, bool isInPath, float cost);
    void EndDraw();

    void DrawGraph(
        StateGraph<TContext> graph,
        StateId? currentState = null,
        TransitionPath<TContext>? currentPath = null,
        TContext? context = default);
}
```

### IPathVisualizerData

探索過程のデバッグ用：

```csharp
public interface IPathVisualizerData
{
    IReadOnlyList<StateId> VisitedNodes { get; }
    IReadOnlyList<StateId> OpenNodes { get; }
    IReadOnlyList<StateId> ClosedNodes { get; }
    IReadOnlyDictionary<StateId, float> GScores { get; }
    IReadOnlyDictionary<StateId, float> FScores { get; }
    IReadOnlyDictionary<StateId, StateId> CameFrom { get; }
    StateId Start { get; }
    StateId Goal { get; }
    int CurrentIteration { get; }
}
```

---

## パフォーマンス

### 計算量

| 操作 | 計算量 |
|-----|--------|
| 状態追加 | O(1) |
| 遷移追加 | O(1) |
| A*探索 | O(E log V) |
| 状態取得 | O(1) |

V = 状態数, E = 遷移数

### 最適化のヒント

**1. ヒューリスティックの使用**

```csharp
// 良い: カスタムヒューリスティックで探索を高速化
var heuristic = new MyHeuristic();
var finder = new HierarchicalPathFinder<AIContext>(graph, heuristic);
```

**2. 探索オプションの調整**

```csharp
var options = new PathfindingOptions
{
    MaxIterations = 1000,        // 反復上限を設定
    TimeoutMilliseconds = 10,    // タイムアウトを設定
    AllowPartialPath = true      // 部分パスで妥協
};
```

**3. グラフ設計の最適化**

```csharp
// 良い: 必要な遷移のみを定義
graph.AddTransition(new Transition<AIContext>("A", "B", 1f));

// 悪い: 不要な遷移が多いと探索が遅くなる
```

**4. 条件評価の軽量化**

```csharp
// 良い: シンプルな条件
new Transition<AIContext>("A", "B", 1f, ctx => ctx.HasTarget)

// 悪い: 重い条件（毎回計算される）
new Transition<AIContext>("A", "B", 1f, ctx =>
    Physics.OverlapSphere(ctx.Position, 10f).Length > 0)
```

---

## 実践パターン集

### AI敵キャラクター

```csharp
public class EnemyAI
{
    private readonly HierarchicalStateMachine<AIContext> _sm;
    private readonly AIContext _context;

    public EnemyAI()
    {
        var graph = new StateGraph<AIContext>()
            // 基本状態
            .AddState(new PatrolState())
            .AddState(new ChaseState())
            .AddState(new AttackState())
            .AddState(new FleeState())
            .AddState(new DeadState())

            // 通常遷移
            .AddTransition(new Transition<AIContext>("Patrol", "Chase", 1f,
                ctx => ctx.HasTarget && ctx.DistanceToTarget < 20f))
            .AddTransition(new Transition<AIContext>("Chase", "Attack", 1f,
                ctx => ctx.DistanceToTarget < 2f))
            .AddTransition(new Transition<AIContext>("Attack", "Chase", 0.5f,
                ctx => ctx.DistanceToTarget > 3f))
            .AddTransition(new Transition<AIContext>("Chase", "Patrol", 2f,
                ctx => !ctx.HasTarget))

            // 緊急遷移（Any State）
            .AddTransition(new Transition<AIContext>(StateId.Any, "Flee", 0f,
                ctx => ctx.Health < 0.2f && !ctx.IsInSafeZone))
            .AddTransition(new Transition<AIContext>(StateId.Any, "Dead", 0f,
                ctx => ctx.Health <= 0));

        _sm = new HierarchicalStateMachine<AIContext>(graph);
        _context = new AIContext();
        _sm.Initialize("Patrol", _context);
    }

    public void Tick(int deltaTicks)
    {
        _sm.Tick(_context, deltaTicks);

        // 状態に応じた行動計画
        if (ShouldReplan())
        {
            var path = _sm.PlanPath(GetBestGoal(), _context);
            if (path.IsValid)
            {
                _sm.ExecuteNextStep(_context);
            }
        }
    }

    private StateId GetBestGoal()
    {
        if (_context.Health < 0.2f) return "Flee";
        if (_context.HasTarget) return "Attack";
        return "Patrol";
    }

    private bool ShouldReplan()
    {
        return _sm.IsPathComplete ||
               _context.HasTarget != (_sm.CurrentStateId != "Patrol");
    }
}
```

### ボス戦フェーズ管理

```csharp
public class BossAI
{
    public BossAI()
    {
        // フェーズ1のサブグラフ
        var phase1 = new StateGraph<BossContext>()
            .AddState(new StateBase<BossContext>("Idle"))
            .AddState(new StateBase<BossContext>("Slash"))
            .AddState(new StateBase<BossContext>("Slam"))
            .AddTransition(new Transition<BossContext>("Idle", "Slash", 1f))
            .AddTransition(new Transition<BossContext>("Idle", "Slam", 2f))
            .AddTransition(new Transition<BossContext>("Slash", "Idle", 1f))
            .AddTransition(new Transition<BossContext>("Slam", "Idle", 1.5f));

        // フェーズ2のサブグラフ
        var phase2 = new StateGraph<BossContext>()
            .AddState(new StateBase<BossContext>("Idle"))
            .AddState(new StateBase<BossContext>("Barrage"))
            .AddState(new StateBase<BossContext>("Laser"))
            .AddTransition(new Transition<BossContext>("Idle", "Barrage", 1f))
            .AddTransition(new Transition<BossContext>("Idle", "Laser", 1.5f))
            .AddTransition(new Transition<BossContext>("Barrage", "Idle", 1f))
            .AddTransition(new Transition<BossContext>("Laser", "Idle", 2f));

        // メイングラフ
        var mainGraph = new StateGraph<BossContext>()
            .AddState(new HierarchicalState<BossContext>("Phase1", phase1, "Idle"))
            .AddState(new HierarchicalState<BossContext>("Phase2", phase2, "Idle"))
            .AddState(new StateBase<BossContext>("Transition"))
            .AddState(new StateBase<BossContext>("Dead"))
            .AddTransition(new Transition<BossContext>("Phase1", "Transition", 0f,
                ctx => ctx.Health < 0.5f))
            .AddTransition(new Transition<BossContext>("Transition", "Phase2", 1f))
            .AddTransition(new Transition<BossContext>(StateId.Any, "Dead", 0f,
                ctx => ctx.Health <= 0));
    }
}
```

### NPCの日常行動

```csharp
public class NPCAI
{
    public NPCAI()
    {
        var graph = new StateGraph<NPCContext>()
            // 時間帯ベースの状態
            .AddState(new SleepState())
            .AddState(new WakeUpState())
            .AddState(new WorkState())
            .AddState(new LunchState())
            .AddState(new RestState())
            .AddState(new GoHomeState())

            // 時間に応じた動的コスト
            .AddTransition(new Transition<NPCContext>("Sleep", "WakeUp",
                ctx => ctx.Hour < 7 ? 100f : 0f))  // 7時前は起きにくい
            .AddTransition(new Transition<NPCContext>("WakeUp", "Work",
                ctx => Math.Abs(ctx.Hour - 9) * 0.5f))  // 9時に近いほど低コスト
            .AddTransition(new Transition<NPCContext>("Work", "Lunch",
                ctx => Math.Abs(ctx.Hour - 12) * 0.5f))
            .AddTransition(new Transition<NPCContext>("Lunch", "Work", 1f))
            .AddTransition(new Transition<NPCContext>("Work", "GoHome",
                ctx => ctx.Hour >= 18 ? 0f : 10f))
            .AddTransition(new Transition<NPCContext>("GoHome", "Rest", 1f))
            .AddTransition(new Transition<NPCContext>("Rest", "Sleep",
                ctx => ctx.Hour >= 22 ? 0f : 5f));
    }
}
```

---

## トラブルシューティング

### パスが見つからない

**1. 遷移の存在確認**

```csharp
var transitions = graph.GetTransitionsFrom(currentState).ToList();
Console.WriteLine($"遷移数: {transitions.Count}");
foreach (var t in transitions)
{
    Console.WriteLine($"  -> {t.To} (コスト: {t.GetCost(context)})");
}
```

**2. 条件の確認**

```csharp
foreach (var t in graph.GetTransitionsFrom(currentState))
{
    var canTransition = t.CanTransition(context);
    Console.WriteLine($"{t.From} -> {t.To}: {canTransition}");
}
```

**3. 状態の存在確認**

```csharp
Console.WriteLine($"開始状態存在: {graph.HasState(start)}");
Console.WriteLine($"ゴール状態存在: {graph.HasState(goal)}");
```

### 意図しないパスが選択される

**1. コストの確認**

```csharp
var path = sm.PlanPath(goal, context);
Console.WriteLine($"総コスト: {path.TotalCost}");
foreach (var t in path.Transitions)
{
    Console.WriteLine($"  {t.From} -> {t.To}: {t.GetCost(context)}");
}
```

**2. コストの調整**

```csharp
// 望ましくない遷移のコストを上げる
new Transition<AIContext>("A", "B", 10f)  // 高コスト = 避けられる

// 望ましい遷移のコストを下げる
new Transition<AIContext>("A", "C", 0.1f)  // 低コスト = 優先される
```

### パフォーマンスが悪い

**1. 探索統計の確認**

```csharp
var path = sm.PlanPath(goal, context);
Console.WriteLine($"訪問ノード数: {path.NodesVisited}");
Console.WriteLine($"探索時間: {path.ElapsedMilliseconds}ms");
```

**2. オプションの調整**

```csharp
var options = new PathfindingOptions
{
    MaxIterations = 100,
    TimeoutMilliseconds = 5,
    AllowPartialPath = true
};
```

**3. ヒューリスティックの導入**

```csharp
var heuristic = new DelegateHeuristic<AIContext>(
    (current, goal, ctx) => EstimateCost(current, goal));
var sm = new HierarchicalStateMachine<AIContext>(graph, heuristic);
```

---

## ディレクトリ構造

```
HierarchicalStateMachine/
├── DESIGN.md                           # 本ドキュメント
├── README.md                           # クイックスタート
│
├── HierarchicalStateMachine.Core/
│   ├── HierarchicalStateMachine.Core.csproj
│   │
│   ├── Core/
│   │   ├── StateId.cs                  # 状態識別子
│   │   ├── IState.cs                   # 状態インターフェース
│   │   ├── StateBase.cs                # 状態基底クラス
│   │   ├── Transition.cs               # 遷移クラス
│   │   ├── StateGraph.cs               # 状態グラフ
│   │   ├── IHierarchicalState.cs       # 階層状態インターフェース
│   │   └── HierarchicalState.cs        # 階層状態実装
│   │
│   ├── Context/
│   │   └── PathfindingContextBase.cs   # コンテキスト基底クラス
│   │
│   ├── Pathfinding/
│   │   ├── PathfindingResult.cs        # 探索結果enum
│   │   ├── TransitionPath.cs           # パス結果クラス
│   │   ├── IHeuristic.cs               # ヒューリスティックインターフェース
│   │   ├── ZeroHeuristic.cs            # ゼロヒューリスティック
│   │   ├── DelegateHeuristic.cs        # デリゲートヒューリスティック
│   │   ├── PathfindingOptions.cs       # 探索オプション
│   │   ├── PriorityQueue.cs            # 優先度キュー
│   │   └── HierarchicalPathFinder.cs   # A*探索エンジン
│   │
│   ├── StateMachine/
│   │   └── HierarchicalStateMachine.cs # 統合ステートマシン
│   │
│   └── Visualization/
│       ├── IGraphVisualizer.cs         # グラフ可視化インターフェース
│       └── IPathVisualizerData.cs      # パス可視化データ
│
└── HierarchicalStateMachine.Tests/
    ├── HierarchicalStateMachine.Tests.csproj
    ├── StateIdTests.cs
    ├── TransitionTests.cs
    ├── StateGraphTests.cs
    ├── HierarchicalStateTests.cs
    ├── PriorityQueueTests.cs
    ├── HierarchicalPathFinderTests.cs
    └── HierarchicalStateMachineTests.cs
```
