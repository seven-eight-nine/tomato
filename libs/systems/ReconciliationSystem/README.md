# ReconciliationSystem

LateUpdateでのEntity位置の調停を担うシステム。依存順の計算と押し出し処理を行う。

## これは何？

Entity間の衝突時に「どちらをどれだけ押し出すか」を決定するシステム。

```
衝突検出結果（押し出し衝突リスト）
    │
    ▼
┌────────────────────────────────────────┐
│          PositionReconciler             │
│                                         │
│  1. 依存順でEntityをソート              │
│  2. 優先度ルールで押し出し量を計算      │
│  3. 押し出しを位置に適用                │
└────────────────────────────────────────┘
    │
    ▼
各Entityの位置が更新される
```

## なぜ使うのか

- **優先度ベースの押し出し**: 壁は動かない、プレイヤーは押し出される
- **依存関係の尊重**: 騎乗者は馬に追従してから調整される
- **循環依存の安全処理**: 循環検出時はスキップ

---

## クイックスタート

### 1. コンポーネントを作成

```csharp
using Tomato.DependencySortSystem;
using Tomato.ReconciliationSystem;
using Tomato.Math;

// 依存グラフとルールを作成
var dependencyGraph = new DependencyGraph<AnyHandle>();
var rule = new PriorityBasedReconciliationRule();

// ゲーム側で実装するアクセサ
var transforms = new MyTransformAccessor();   // IEntityTransformAccessor実装
var entityTypes = new MyEntityTypeAccessor(); // IEntityTypeAccessor実装

var reconciler = new PositionReconciler(
    dependencyGraph,
    rule,
    transforms,
    entityTypes);
```

### 2. 衝突情報を作成

```csharp
// 衝突検出システムから得られた押し出し衝突
var collisions = new List<PushCollision>
{
    new PushCollision(
        entityA: player,
        entityB: wall,
        normal: new Vector3(1, 0, 0),  // playerからwallへの方向
        penetration: 0.5f)             // 貫通深度（メートル）
};
```

### 3. 調停を実行

```csharp
// LateUpdateで実行
reconciler.Process(entities, collisions);
// → playerは押し出され、wallは動かない
```

---

## 主要な型

### PushCollision

押し出し衝突情報。2つのEntityの衝突を表す。

```csharp
public readonly struct PushCollision
{
    public readonly AnyHandle EntityA;   // 衝突したEntity A
    public readonly AnyHandle EntityB;   // 衝突したEntity B
    public readonly Vector3 Normal;      // 衝突法線（AからBへの方向）
    public readonly float Penetration;   // 貫通深度
}
```

### EntityType

Entity種別。優先度ベースの押し出しルールで使用。

```csharp
public enum EntityType
{
    Player,      // プレイヤー（優先度: 50）
    Enemy,       // 敵（優先度: 100）
    NPC,         // NPC（優先度: 30）
    Wall,        // 壁（優先度: 1000、絶対に動かない）
    Obstacle,    // 障害物（優先度: 500）
    Projectile   // 飛び道具（優先度: 0）
}
```

### PriorityBasedReconciliationRule

優先度ベースの調停ルール。高優先度のEntityほど押し出されにくい。

```csharp
var rule = new PriorityBasedReconciliationRule();

// デフォルト優先度:
// Wall: 1000      絶対に動かない
// Obstacle: 500   基本動かない
// Enemy: 100      大型敵
// Player: 50      プレイヤー
// NPC: 30         NPC
// Projectile: 0   飛び道具

// 優先度をカスタマイズ
rule.SetPriority(EntityType.Enemy, 200);  // 敵をより押し出しにくく
rule.SetPriority(EntityType.NPC, 100);    // NPCを強化
```

### PositionReconciler

位置調停を統括するクラス。

```csharp
public sealed class PositionReconciler
{
    // 依存グラフへのアクセス
    public DependencyGraph<AnyHandle> DependencyGraph { get; }

    // LateUpdate処理を実行
    public void Process(IEnumerable<AnyHandle> entities, IReadOnlyList<PushCollision> pushCollisions);
}
```

---

## 押し出しルール

### 同優先度の場合

両者が半分ずつ押し出される。

```
Player(50) vs Player(50)
    ├─ penetration: 0.2m
    ├─ pushoutA: -0.1m（反対方向）
    └─ pushoutB: +0.1m
```

### 異なる優先度の場合

低優先度側のみが押し出される。

```
Player(50) vs Wall(1000)
    ├─ penetration: 0.5m
    ├─ pushoutPlayer: -0.5m（全量押し出し）
    └─ pushoutWall: 0m（動かない）
```

---

## 依存関係の処理

Entity間に依存関係がある場合（例：騎乗者→馬）、依存先を先に処理する。

```csharp
// 騎乗者は馬に依存
reconciler.DependencyGraph.AddDependency(rider, horse);

// Process()内で:
// 1. horse を先に処理（調停）
// 2. rider を後に処理（馬の位置を基準に調整）
```

### 循環依存

循環依存を検出した場合、安全に処理をスキップする。

```csharp
graph.AddDependency(a, b);
graph.AddDependency(b, a);  // 循環

// Process()内で循環検出 → 調停処理をスキップ
```

---

## アクセサインターフェース

ゲーム側で実装する必要があるインターフェース。

### IEntityTransformAccessor

Entity位置へのアクセス。

```csharp
public interface IEntityTransformAccessor
{
    Vector3 GetPosition(AnyHandle handle);
    void SetPosition(AnyHandle handle, Vector3 position);
}
```

### IEntityTypeAccessor

Entity種別へのアクセス。

```csharp
public interface IEntityTypeAccessor
{
    EntityType GetEntityType(AnyHandle handle);
}
```

---

## 処理フロー

```
Process(entities, pushCollisions)
    │
    ├─1. 依存グラフからトポロジカルソート
    │    └─ 循環検出時は return（スキップ）
    │
    ├─2. 依存順に各Entityを調停
    │    └─ 依存先との相対位置を維持
    │
    └─3. 押し出し処理
         ├─ 各衝突の押し出し量を計算
         ├─ Entity毎に押し出しを蓄積
         └─ 一括で位置に適用
```

---

## 依存関係

```
ReconciliationSystem.Core
├── EntityHandleSystem.Attributes  (AnyHandle)
├── CommandGenerator.Core          (IEntityArena)
├── DependencySortSystem.Core      (DependencyGraph, TopologicalSorter)
└── Tomato.Math                    (Vector3)
```

## ディレクトリ構造

```
ReconciliationSystem/
├── README.md
├── ReconciliationSystem.Core/
│   ├── ReconciliationSystem.Core.csproj
│   ├── PushCollision.cs              # 押し出し衝突情報
│   ├── VolumeType.cs
│   ├── Rule/
│   │   ├── EntityType.cs             # Entity種別
│   │   ├── ReconciliationRule.cs     # ルール基底クラス
│   │   └── PriorityBasedReconciliationRule.cs  # 優先度ルール
│   ├── Reconciler/
│   │   └── PositionReconciler.cs     # 位置調停
│   └── Accessor/
│       ├── IEntityTransformAccessor.cs
│       └── IEntityTypeAccessor.cs
└── ReconciliationSystem.Tests/
    ├── ReconciliationSystem.Tests.csproj
    ├── ReconciliationRuleTests.cs
    └── PositionReconcilerTests.cs
```

## テスト

```bash
dotnet test libs/systems/ReconciliationSystem/ReconciliationSystem.Tests/
```

## ライセンス

MIT License
