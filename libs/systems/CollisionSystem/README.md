# CollisionSystem

ゲーム向け空間クエリライブラリ。

## これは何？

3D空間内の形状（Sphere、Capsule、Cylinder、Box）を登録し、高速な空間クエリを提供するシステム。
Broad Phase（空間分割）+ Narrow Phase（幾何学的判定）の2段階で効率的に衝突候補を絞り込む。

```
登録された形状群
    │
    ▼
┌────────────────────┐
│   Broad Phase      │  空間分割で候補を絞り込み
│   (6種類の戦略)    │  BVH / DBVT / Octree / GridSAP / SpatialHash / MBP
└────────────────────┘
    │
    ▼
┌────────────────────┐
│   Narrow Phase     │  幾何学的交差判定
│ (ShapeIntersection)│
└────────────────────┘
    │
    ▼
HitResult（衝突点・法線・距離）
```

## なぜ使うのか

- **高速**: 階層的空間分割で O(n) → O(log n) に絞り込み
- **ゼロアロケーション**: クエリ結果は Span で返却。毎フレームのGC負荷なし
- **多様なクエリ**: レイキャスト、球オーバーラップ、カプセルスイープ、斬撃線
- **柔軟な戦略**: 用途に応じて6種類の Broad Phase を選択可能
- **レイヤーマスク**: ビットマスクでフィルタリング。特定レイヤーのみ判定可能
- **シンプル設計**: ゲームエンジン非依存、必要な機能だけを提供

---

## クイックスタート

### 1. ワールド作成

```csharp
using Tomato.CollisionSystem;
using Tomato.Math;

// BVH戦略でワールド作成（推奨）
var world = new SpatialWorld(new BVHBroadPhase(maxShapes: 10000));

// オープンワールド向け（DBVT戦略）
var world = new SpatialWorld(new DBVTBroadPhase(maxShapes: 50000));

// 固定マップ向け（MBP戦略）
var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
var world = new SpatialWorld(new MBPBroadPhase(bounds, regionsX: 8, regionsZ: 8));

// 中規模シーン向け（GridSAP戦略）
var world = new SpatialWorld(new GridSAPBroadPhase(gridSize: 16f));
```

### 2. 形状を登録

```csharp
// レイヤーマスク定義
const uint LayerPlayer = 0x01;
const uint LayerEnemy = 0x02;
const uint LayerEnvironment = 0x04;
const uint LayerProjectile = 0x08;

// 球を追加
var sphereHandle = world.AddSphere(
    center: new Vector3(0, 1, 0),
    radius: 0.5f,
    layerMask: LayerPlayer);

// カプセルを追加
var capsuleHandle = world.AddCapsule(
    p1: new Vector3(5, 0, 0),
    p2: new Vector3(5, 2, 0),
    radius: 0.3f,
    layerMask: LayerEnemy);

// 円柱を追加
var cylinderHandle = world.AddCylinder(
    baseCenter: new Vector3(10, 0, 0),
    height: 3f,
    radius: 1f,
    layerMask: LayerEnvironment);

// ボックスを追加（Y軸回転対応）
var boxHandle = world.AddBox(
    center: new Vector3(15, 1, 0),
    halfExtents: new Vector3(1f, 1f, 2f),
    yaw: MathF.PI / 4,  // 45度回転
    layerMask: LayerEnvironment);
```

### 3. クエリを実行

```csharp
// レイキャスト（最近ヒット）
var ray = new RayQuery(
    origin: new Vector3(0, 5, 0),
    direction: Vector3.Down,
    maxDistance: 10f);

if (world.Raycast(ray, out var hit))
{
    Console.WriteLine($"Hit: {hit.Point}, Normal: {hit.Normal}");
}

// マスクフィルタリング付きレイキャスト
// 敵のみを対象（プレイヤーと環境を無視）
var enemyOnlyRay = new RayQuery(
    origin: new Vector3(0, 5, 0),
    direction: Vector3.Down,
    maxDistance: 10f,
    includeMask: LayerEnemy);

// 特定レイヤーを除外（環境以外にヒット）
var ignoreEnvRay = new RayQuery(
    origin: new Vector3(0, 5, 0),
    direction: Vector3.Down,
    maxDistance: 10f,
    excludeMask: LayerEnvironment);
```

### 4. 形状を更新・削除

```csharp
// 球の位置を更新
world.UpdateSphere(sphereHandle, newCenter: new Vector3(1, 1, 0), newRadius: 0.5f);

// ボックスの位置・回転を更新
world.UpdateBox(boxHandle,
    newCenter: new Vector3(16, 1, 0),
    newHalfExtents: new Vector3(1f, 1f, 2f),
    newYaw: MathF.PI / 2);  // 90度回転

// レイヤーマスクを取得・変更
uint currentMask = world.GetLayerMask(sphereHandle);
world.SetLayerMask(sphereHandle, LayerEnemy | LayerProjectile);

// 削除
world.Remove(sphereHandle);
```

---

## Broad Phase 戦略

用途に応じて最適な空間分割アルゴリズムを選択できる。

| 戦略 | 特徴 | 推奨用途 |
|------|------|----------|
| **BVH** | バランス良好、更新が速い | 動的シーン全般（推奨デフォルト） |
| **DBVT** | クエリ最速、増分更新 | 静的シーン、大量のペア検出 |
| **Octree** | 疎な空間に強い | 広大で疎なワールド |
| **MBP** | リージョン分割、安定性能 | 固定境界のあるマップ |
| **GridSAP** | ゾーン + SAP | 均一分布のシーン |
| **SpatialHash** | シンプル、O(1)操作 | 中規模・均一分布 |

### 戦略の選び方

```
用途別推奨:
├── ゲーム全般        → BVH（バランス良好）
├── オープンワールド   → BVH or DBVT（境界不要）
├── 物理シミュレーション → DBVT（ペア検出に強い）
├── 固定マップ + 動的NPC → MBP（リージョン分割）
└── 広大で疎なワールド  → Octree（空間スキップ）
```

### 各戦略の初期化

```csharp
// BVH（推奨）
var bvh = new BVHBroadPhase(maxShapes: 10000, useSAH: true);

// DBVT（動的BVH）
var dbvt = new DBVTBroadPhase(maxShapes: 10000, margin: 0.1f);

// Octree
var bounds = new AABB(new Vector3(-1000, -100, -1000), new Vector3(1000, 100, 1000));
var octree = new OctreeBroadPhase(bounds, maxDepth: 8, maxShapes: 10000);

// MBP（Multi-Box Pruning）
var mbp = new MBPBroadPhase(bounds, regionsX: 8, regionsZ: 8, maxShapes: 10000);

// GridSAP
var gridSap = new GridSAPBroadPhase(gridSize: 16f, axisMode: SAPAxisMode.XZ);

// SpatialHash
var spatialHash = new SpatialHashBroadPhase(cellSize: 8f, maxShapes: 10000);
```

---

## クエリタイプ

| クエリ | 構造体 | 用途 |
|--------|--------|------|
| 点クエリ | - | 点が形状内にあるか判定 |
| レイキャスト | `RayQuery` | 銃弾、視線判定、ピッキング |
| 全レイキャスト | `RayQuery` | 貫通弾、複数ヒット取得 |
| 球オーバーラップ | `SphereOverlapQuery` | 範囲内の敵検索、爆発範囲 |
| カプセルスイープ | `CapsuleSweepQuery` | キャラクター移動判定 |
| 斬撃線 | `SlashQuery` | 剣の軌跡による攻撃判定 |

すべてのクエリは `includeMask` と `excludeMask` パラメータでフィルタリング可能。

### レイキャスト

```csharp
var ray = new RayQuery(origin, direction, maxDistance);

// 最近ヒットのみ
if (world.Raycast(ray, out var hit))
{
    // hit.ShapeIndex, hit.Point, hit.Normal, hit.Distance
}

// 全ヒット（距離順）
Span<HitResult> results = stackalloc HitResult[16];
int count = world.RaycastAll(ray, results);

// マスクフィルタリング付き
var filteredRay = new RayQuery(origin, direction, maxDistance,
    includeMask: LayerEnemy | LayerEnvironment,
    excludeMask: LayerPlayer);
```

### 球オーバーラップ

```csharp
var query = new SphereOverlapQuery(center, radius: 5f);
Span<HitResult> results = stackalloc HitResult[32];
int count = world.QuerySphereOverlap(query, results);

// 敵のみ検索
var enemyQuery = new SphereOverlapQuery(center, radius: 5f,
    includeMask: LayerEnemy);
```

### カプセルスイープ

```csharp
// キャラクターの移動判定
var sweep = new CapsuleSweepQuery(
    start: currentPos,
    end: targetPos,
    radius: 0.4f);

if (world.CapsuleSweep(sweep, out var hit))
{
    // 移動経路上に障害物あり
    // hit.Distance は 0～1 の Time of Impact
    var safePos = Vector3.Lerp(currentPos, targetPos, hit.Distance);
}

// 環境のみと衝突判定（敵は通過）
var envOnlySweep = new CapsuleSweepQuery(currentPos, targetPos, 0.4f,
    includeMask: LayerEnvironment);
```

### 斬撃線クエリ

```csharp
// 剣の軌跡（前フレームと今フレームの剣の位置）
var slash = new SlashQuery(
    startBase: prevSwordBase,
    startTip: prevSwordTip,
    endBase: currSwordBase,
    endTip: currSwordTip);

Span<HitResult> results = stackalloc HitResult[8];
int count = world.QuerySlash(slash, results);

// 敵のみにダメージ（環境はスルー）
var slashEnemy = new SlashQuery(
    prevSwordBase, prevSwordTip,
    currSwordBase, currSwordTip,
    includeMask: LayerEnemy);
```

### 点クエリ

```csharp
// 基本
Span<HitResult> results = stackalloc HitResult[8];
int count = world.QueryPoint(point, results);

// マスクフィルタリング付き
int count = world.QueryPoint(point, results,
    includeMask: LayerEnemy,
    excludeMask: LayerProjectile);
```

---

## 主要な型

### ShapeHandle

```csharp
public readonly struct ShapeHandle
{
    public readonly int Index;       // 内部インデックス
    public readonly int Generation;  // 世代番号（削除後の誤参照防止）

    public static ShapeHandle Invalid => new(-1, 0);
    public bool IsValid => Index >= 0;
}
```

### HitResult

```csharp
public struct HitResult
{
    public int ShapeIndex;    // ヒットした Shape
    public float Distance;    // 距離または TOI
    public Vector3 Point;     // 接触点
    public Vector3 Normal;    // 接触面の法線

    public static HitResult None => new(-1, float.MaxValue, ...);
    public bool IsValid => ShapeIndex >= 0;
}
```

### 形状データ

```csharp
// 球
public struct SphereData { Vector3 Center; float Radius; }

// カプセル
public struct CapsuleData { Vector3 Point1; Vector3 Point2; float Radius; }

// 円柱（Y軸整列、回転なし）
public struct CylinderData { Vector3 BaseCenter; float Height; float Radius; }

// ボックス（Y軸回転対応）
public struct BoxData { Vector3 Center; Vector3 HalfExtents; float Yaw; }
```

---

## レイヤーマスク

32ビットのビットマスクで形状をフィルタリング。Narrow Phase前に適用されるため効率的。

### 判定ロジック

```csharp
bool shouldTest = (shapeMask & includeMask) != 0 && (shapeMask & excludeMask) == 0;
```

- **IncludeMask**: このビットのいずれかが Shape と一致すれば候補
- **ExcludeMask**: このビットのいずれかが Shape と一致すれば除外

### レイヤー定義例

```csharp
// ビットフラグで定義
const uint LayerDefault     = 0x01;
const uint LayerPlayer      = 0x02;
const uint LayerEnemy       = 0x04;
const uint LayerProjectile  = 0x08;
const uint LayerEnvironment = 0x10;
const uint LayerTrigger     = 0x20;

// 複合レイヤー
const uint LayerDamageable = LayerPlayer | LayerEnemy;
const uint LayerSolid = LayerEnvironment | LayerPlayer | LayerEnemy;
```

### 使用パターン

```csharp
// 形状登録時にレイヤー指定
var player = world.AddCapsule(p1, p2, radius, layerMask: LayerPlayer);
var enemy = world.AddSphere(center, radius, layerMask: LayerEnemy);
var wall = world.AddBox(center, halfExtents, layerMask: LayerEnvironment);

// プレイヤーの攻撃: 敵のみにヒット
var attackRay = new RayQuery(origin, dir, 10f, includeMask: LayerEnemy);

// 敵のAI: プレイヤーと環境を検知
var senseQuery = new SphereOverlapQuery(center, 20f,
    includeMask: LayerPlayer | LayerEnvironment);

// 移動判定: 環境のみ（他のキャラクターは通過）
var moveSweep = new CapsuleSweepQuery(start, end, 0.4f,
    includeMask: LayerEnvironment);

// 弾丸: 自分以外すべてにヒット
var bulletRay = new RayQuery(origin, dir, 100f,
    excludeMask: LayerProjectile);

// 実行時にレイヤー変更（無敵状態など）
world.SetLayerMask(playerHandle, LayerTrigger);  // 当たり判定無効化
```

### デフォルト値

- **形状の layerMask**: `0xFFFFFFFF`（全ビット ON）
- **クエリの includeMask**: `0xFFFFFFFF`（全レイヤーを含む）
- **クエリの excludeMask**: `0`（何も除外しない）

マスクを指定しない場合、従来どおり全形状が判定対象となる。

---

## パフォーマンス

### 戦略別ベンチマーク（参考値）

| パターン | BVH | DBVT | Octree | MBP | GridSAP | SpatialHash |
|----------|-----|------|--------|-----|---------|-------------|
| 均一分布（小） | 0 us | 0 us | 2 us | 0 us | 4 us | 4 us |
| 均一分布（大） | 0 us | 0 us | 4 us | 0 us | 24 us | 44 us |
| 混合サイズ | 16 us | 0 us | 8 us | 0 us | 22 us | 94 us |
| 長いレイ | 2 us | 4 us | 4 us | 2 us | 28 us | 16 us |
| 高頻度更新 | 1 ms | 39 ms | 4 ms | 3 ms | 40 ms | 4 ms |

※ 1000 shapes, 500 queries での測定

### メモリレイアウト

- ShapeRegistry は SoA（Structure of Arrays）レイアウト
- クエリ結果は Span で返却（ヒープアロケーションなし）

---

## 依存関係

```
CollisionSystem.Core
└── Tomato.Math (Vector3, AABB)
```

## ディレクトリ構造

```
CollisionSystem/
├── README.md
├── DESIGN.md
├── CollisionSystem.Core/
│   ├── CollisionSystem.Core.csproj
│   ├── SpatialWorld.cs              # メインAPI
│   ├── Data/
│   │   ├── ShapeType.cs             # 形状種別 enum
│   │   ├── ShapeData.cs             # SphereData, CapsuleData, CylinderData, BoxData
│   │   ├── ShapeHandle.cs           # ハンドル型
│   │   ├── ShapeRegistry.cs         # 形状データ管理
│   │   └── HitResult.cs             # クエリ結果
│   ├── BroadPhase/
│   │   ├── IBroadPhase.cs           # Broad Phase インターフェース
│   │   ├── BVHBroadPhase.cs         # BVH 実装
│   │   ├── DBVTBroadPhase.cs        # DBVT 実装
│   │   ├── OctreeBroadPhase.cs      # Octree 実装
│   │   ├── MBPBroadPhase.cs         # MBP 実装
│   │   ├── GridSAPBroadPhase.cs     # GridSAP 実装
│   │   ├── SpatialHashBroadPhase.cs # SpatialHash 実装
│   │   ├── SAPAxisMode.cs           # SAP 軸モード enum
│   │   └── Zone.cs                  # SAP ゾーン
│   ├── NarrowPhase/
│   │   └── ShapeIntersection.cs     # 幾何学的交差判定
│   └── Queries/
│       └── QueryTypes.cs            # RayQuery, SphereOverlapQuery 等
└── CollisionSystem.Tests/
    ├── CollisionSystem.Tests.csproj
    ├── RaycastTests.cs
    ├── SphereOverlapTests.cs
    ├── CapsuleSweepTests.cs
    ├── SlashQueryTests.cs
    ├── PointQueryTests.cs
    ├── ShapeManagementTests.cs
    ├── MaskFilteringTests.cs
    ├── PerformanceTests.cs
    └── ComprehensiveBenchmark.cs
```

## テスト

```bash
dotnet test libs/systems/CollisionSystem/CollisionSystem.Tests/
```

## ライセンス

MIT License
