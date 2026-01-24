# ReconciliationSystem

LateUpdateでのEntity位置の調停を担うシステム。依存順の計算と押し出し処理を行う。

## 設計原則

**依存関係を尊重し、物理的整合性を保証する。**

- Entity間の依存関係（騎乗者→馬など）を正しい順序で処理
- 衝突したPushbox同士の押し出しを優先度に基づいて実行
- 循環依存を検出し、安全に処理をスキップ

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────┐
│                 ReconciliationSystem                     │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌─────────────────────────────────────────────────┐   │
│  │              PositionReconciler                  │   │
│  │  - LateUpdate処理を統括                          │   │
│  │  - 依存順の計算                                  │   │
│  │  - 押し出し処理                                  │   │
│  └─────────────────────────────────────────────────┘   │
│                         │                                │
│          ┌──────────────┼──────────────┐                │
│          ▼              ▼              ▼                │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────────┐  │
│  │ Dependency  │ │ Topological │ │ Reconciliation  │  │
│  │   Graph     │ │   Sorter    │ │     Rule        │  │
│  │(DependSort) │ │(DependSort) │ │ 押し出し計算   │  │
│  │             │ │             │ │                 │  │
│  └─────────────┘ └─────────────┘ └─────────────────┘  │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

## コンポーネント

### 依存関係（DependencySortSystem使用）

- `DependencyGraph<AnyHandle>` - Entity間の依存関係を管理するDAG
- `TopologicalSorter<AnyHandle>` - トポロジカルソートで処理順序を計算

### ルール

- `EntityType` - Entity種別（Player, Enemy, Wall等）
- `ReconciliationRule` - 押し出しルール基底クラス
- `PriorityBasedReconciliationRule` - 優先度ベースの調停ルール

### Reconciler

- `PositionReconciler` - 位置調停を統括

### Accessor

- `IEntityTransformAccessor` - Entity位置へのアクセス
- `IEntityTypeAccessor` - Entity種別へのアクセス

## 使用例

### 基本的な使用法

```csharp
using Tomato.DependencySortSystem;
using Tomato.ReconciliationSystem;

// コンポーネントを作成
var dependencyGraph = new DependencyGraph<AnyHandle>();
var rule = new PriorityBasedReconciliationRule();
var transforms = new MyTransformAccessor(); // IEntityTransformAccessor実装
var entityTypes = new MyEntityTypeAccessor(); // IEntityTypeAccessor実装

var reconciler = new PositionReconciler(
    dependencyGraph,
    rule,
    transforms,
    entityTypes);

// 依存関係を登録（騎乗者→馬）
dependencyGraph.AddDependency(rider, horse);

// LateUpdateで実行
reconciler.Process(entities, pushboxCollisions);
```

### 優先度のカスタマイズ

```csharp
var rule = new PriorityBasedReconciliationRule();

// デフォルト優先度:
// Wall: 1000 (絶対に動かない)
// Obstacle: 500
// Enemy: 100
// Player: 50
// NPC: 30
// Projectile: 0

// カスタマイズ
rule.SetPriority(EntityType.Enemy, 200);  // 大型敵を押し出しにくく
```

### 依存順の計算

```csharp
var graph = new DependencyGraph<AnyHandle>();
var sorter = new TopologicalSorter<AnyHandle>();

// A -> B -> C の依存関係
graph.AddDependency(a, b);
graph.AddDependency(b, c);

// トポロジカルソート: C, B, A の順序（依存先が先）
var result = sorter.Sort(new[] { a, b, c }, graph);
if (result.Success)
{
    // result.SortedOrder[0] == c, [1] == b, [2] == a
}
```

### 押し出し処理

```csharp
// Playerが壁に衝突した場合
var contact = new CollisionContact(
    point: Vector3.Zero,
    normal: new Vector3(1, 0, 0),  // 法線
    penetration: 0.5f);            // 貫通深度

rule.ComputePushout(
    player, EntityType.Player,
    wall, EntityType.Wall,
    in contact,
    out var pushoutPlayer,
    out var pushoutWall);

// pushoutPlayer = (-0.5, 0, 0)  プレイヤーは押し出される
// pushoutWall = (0, 0, 0)       壁は動かない
```

## 処理フロー

1. `Process()` 呼び出し
2. Pushbox衝突を収集
3. `TopologicalSorter.Sort()` で処理順序を計算
4. 依存順に従って各Entityを調停
5. 押し出し量を計算し蓄積
6. 押し出しを一括適用

## テスト

```bash
dotnet test libs/systems/ReconciliationSystem/ReconciliationSystem.Tests/
```

## 依存関係

- **DependencySortSystem** - 汎用トポロジカルソートライブラリ
- CommandGenerator - AnyHandle
- CollisionSystem - Vector3, CollisionContact, CollisionResult

## ディレクトリ構造

```
ReconciliationSystem/
├── README.md
├── ReconciliationSystem.Core/
│   ├── ReconciliationSystem.Core.csproj
│   ├── VolumeType.cs
│   ├── Rule/
│   │   ├── EntityType.cs
│   │   ├── ReconciliationRule.cs
│   │   └── PriorityBasedReconciliationRule.cs
│   ├── Reconciler/
│   │   └── PositionReconciler.cs
│   └── Accessor/
│       ├── IEntityTransformAccessor.cs
│       └── IEntityTypeAccessor.cs
└── ReconciliationSystem.Tests/
    ├── ReconciliationSystem.Tests.csproj
    ├── ReconciliationRuleTests.cs
    └── PositionReconcilerTests.cs
```

## ライセンス

MIT License
