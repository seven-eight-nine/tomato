# HierarchicalStateMachine

階層型ステートマシン + A*パス探索ライブラリ。

## これは何？

AIキャラクターの状態管理と目標達成のための経路計画を行うシステム。
階層構造を持つ状態グラフ上でA*アルゴリズムを使用し、最適な状態遷移パスを探索する。

```
現在: Idle → 目標: Attack

探索結果:
  Idle → Ready → Approach → Attack（コスト: 3）
```

## なぜ使うのか

- **最適経路**: A*アルゴリズムで最小コストの遷移パスを計算
- **動的コスト**: コンテキストに応じて遷移コストを動的に計算
- **階層構造**: 複雑な状態を階層的に整理（サブステートマシン）
- **条件付き遷移**: 状態やコンテキストに基づく遷移制御

---

## クイックスタート

### 1. コンテキストを定義

```csharp
public class AIContext : PathfindingContextBase
{
    public float Health { get; set; }
    public bool HasTarget { get; set; }
}
```

### 2. 状態を定義

```csharp
using Tomato.HierarchicalStateMachine;

public class IdleState : StateBase<AIContext>
{
    public IdleState() : base("Idle") { }

    public override void OnEnter(AIContext ctx)
    {
        Console.WriteLine("Entering Idle");
    }
}

public class AttackState : StateBase<AIContext>
{
    public AttackState() : base("Attack") { }
}
```

### 3. グラフを構築

```csharp
var graph = new StateGraph<AIContext>()
    .AddState(new IdleState())
    .AddState(new StateBase<AIContext>("Ready"))
    .AddState(new StateBase<AIContext>("Approach"))
    .AddState(new AttackState())
    .AddTransition(new Transition<AIContext>("Idle", "Ready", 1f))
    .AddTransition(new Transition<AIContext>("Ready", "Approach", 1f))
    .AddTransition(new Transition<AIContext>("Approach", "Attack", 1f,
        ctx => ctx.HasTarget));  // ターゲットがいる場合のみ遷移可能
```

### 4. ステートマシンで実行

```csharp
var sm = new HierarchicalStateMachine<AIContext>(graph);
var context = new AIContext { HasTarget = true };

// 初期化
sm.Initialize("Idle", context);

// パスを計画
var path = sm.PlanPath("Attack", context);
if (path.IsValid)
{
    Console.WriteLine($"パス発見: コスト={path.TotalCost}");

    // パスを実行
    sm.ExecuteAllSteps(context);
}
```

---

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** に以下が記載されている：

- 用語定義
- 設計哲学
- 状態とグラフ構築
- 遷移の詳細（静的/動的コスト、条件）
- A*探索アルゴリズム
- 階層状態（サブグラフ）
- ヒューリスティック関数
- デバッグ方法

---

## 主要な概念

**状態グラフ** = 状態と遷移のネットワーク

```
┌───────┐     ┌───────┐     ┌───────┐
│ Idle  │────▶│ Ready │────▶│Attack │
└───────┘     └───────┘     └───────┘
     │                           ▲
     └───────────────────────────┘
           Any State遷移
```

**探索フロー**:

| 順序 | 概念 | 説明 |
|:----:|------|------|
| 1 | **開始状態** | 現在の状態 |
| 2 | **ゴール状態** | 目標の状態 |
| 3 | **A*探索** | 最小コストパスを計算 |
| 4 | **パス実行** | 遷移を順次実行 |

---

## よく使うパターン

### 静的コスト遷移

```csharp
// コスト1で遷移
new Transition<AIContext>("A", "B", 1f)

// デフォルトコストは1
new Transition<AIContext>("A", "B")
```

### 条件付き遷移

```csharp
// HPが50%以上の場合のみ遷移可能
new Transition<AIContext>("Idle", "Attack", 1f,
    ctx => ctx.Health > 0.5f)
```

### 動的コスト遷移

```csharp
// 距離に応じてコストを計算
new Transition<AIContext>("Idle", "Approach",
    ctx => ctx.DistanceToTarget * 0.1f)
```

### 条件 + 動的コスト

```csharp
new Transition<AIContext>("Ready", "Attack",
    ctx => ctx.HasTarget,              // 条件
    ctx => ctx.DistanceToTarget)       // コスト
```

### Any State遷移

どの状態からでも遷移可能な「緊急遷移」を定義：

```csharp
// どの状態からでもDeadに遷移可能
graph.AddTransition(new Transition<AIContext>(StateId.Any, "Dead", 0f,
    ctx => ctx.Health <= 0));
```

---

## 探索オプション

```csharp
var options = new PathfindingOptions
{
    MaxIterations = 10000,       // 最大反復回数
    TimeoutMilliseconds = 100,   // タイムアウト（ms）
    AllowPartialPath = true,     // 部分パスを許可
    MaxDepth = 100               // 最大探索深度
};

var path = sm.PlanPath("Goal", context, options);

if (path.Result == PathfindingResult.Timeout)
{
    // タイムアウト時は部分パスを使用可能
    Console.WriteLine($"部分パス: {path.States.Count}ステップ");
}
```

---

## 階層状態（サブグラフ）

状態内に別のステートマシンを持つことができる：

```csharp
// サブグラフを構築
var combatSubGraph = new StateGraph<AIContext>()
    .AddState(new StateBase<AIContext>("WindUp"))
    .AddState(new StateBase<AIContext>("Strike"))
    .AddState(new StateBase<AIContext>("Recover"))
    .AddTransition(new Transition<AIContext>("WindUp", "Strike", 0.5f))
    .AddTransition(new Transition<AIContext>("Strike", "Recover", 0.5f));

// 階層状態として追加
var combatState = new HierarchicalState<AIContext>(
    "Combat", combatSubGraph, "WindUp");

graph.AddState(combatState);
```

---

## デバッグ

```csharp
var path = sm.PlanPath("Goal", context);

Console.WriteLine($"結果: {path.Result}");
Console.WriteLine($"訪問ノード数: {path.NodesVisited}");
Console.WriteLine($"探索時間: {path.ElapsedMilliseconds}ms");
Console.WriteLine($"総コスト: {path.TotalCost}");

// パスの詳細
Console.WriteLine("パス:");
foreach (var state in path.States)
{
    Console.WriteLine($"  -> {state}");
}
```

---

## ライセンス

MIT License
