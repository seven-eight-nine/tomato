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
│   Broad Phase      │  ゾーン分割 + SAP で候補を絞り込み
│   (WorldPartition) │
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

- **高速**: SAP + ゾーン分割で O(n) → O(k) に絞り込み
- **ゼロアロケーション**: クエリ結果は Span で返却。毎フレームのGC負荷なし
- **多様なクエリ**: レイキャスト、球オーバーラップ、カプセルスイープ、斬撃線
- **シンプル設計**: ゲームエンジン非依存、必要な機能だけを提供

---

## クイックスタート

### 1. ワールド作成

```csharp
using Tomato.CollisionSystem;
using Tomato.Math;

// グリッドサイズ16mでワールド作成
var world = new SpatialWorld(gridSize: 16f);

// SAP軸モードを指定する場合
var world = new SpatialWorld(gridSize: 16f, axisMode: SAPAxisMode.XZ);
```

> **グリッドサイズ**: 空間を分割するグリッドの1辺の長さ（メートル）。
> 登録する形状の典型的なサイズの5～10倍程度が目安。詳細は[グリッドサイズの選択](#グリッドサイズの選択)を参照。
>
> **SAP軸モード**: Broad Phaseでのソート軸を選択する。詳細は[SAP軸モード](#sap軸モード)を参照。

### 2. 形状を登録

```csharp
// 球を追加
var sphereHandle = world.AddSphere(
    center: new Vector3(0, 1, 0),
    radius: 0.5f);

// カプセルを追加
var capsuleHandle = world.AddCapsule(
    p1: new Vector3(5, 0, 0),
    p2: new Vector3(5, 2, 0),
    radius: 0.3f);

// 円柱を追加
var cylinderHandle = world.AddCylinder(
    baseCenter: new Vector3(10, 0, 0),
    height: 3f,
    radius: 1f);

// ボックスを追加（Y軸回転対応）
var boxHandle = world.AddBox(
    center: new Vector3(15, 1, 0),
    halfExtents: new Vector3(1f, 1f, 2f),
    yaw: MathF.PI / 4);  // 45度回転
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

// 削除
world.Remove(sphereHandle);
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
```

### 球オーバーラップ

```csharp
var query = new SphereOverlapQuery(center, radius: 5f);
Span<HitResult> results = stackalloc HitResult[32];
int count = world.QuerySphereOverlap(query, results);
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
```

---

## アーキテクチャ

```
SpatialWorld
├── ShapeRegistry          Shape データ管理（SoA レイアウト）
├── WorldPartition         空間分割（ゾーンベース）
│   └── Zone[]             ゾーン単位の SAP 配列
└── ShapeIntersection      幾何学的交差判定（static メソッド群）
```

### Broad Phase

**WorldPartition**: 3D空間を固定サイズのゾーンに分割。

- 各ゾーンはSAP（Sweep and Prune）配列を保持
- SAP軸モードで主軸（X軸 or Z軸）を選択可能
- クエリ時はAABBが重なるゾーンのみを検索
- 32個以下の場合は愚直検索にフォールバック

### Narrow Phase

**ShapeIntersection**: 球・カプセル・円柱・ボックス間の交差判定。

- すべて static メソッドでゼロアロケーション
- 結果は out パラメータで返却
- ボックスはY軸回転に対応

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

**BoxData の Yaw**:
- Y軸周りの回転角（ラジアン）
- 0 で回転なし、正の値で上から見て反時計回り
- AABB は回転を考慮して自動計算される

---

## パフォーマンス

### グリッドサイズの選択

グリッドサイズは空間を分割するグリッドの1辺の長さ。クエリ性能に直接影響する重要なパラメータ。

```
ワールド空間をグリッドサイズで分割
┌─────┬─────┬─────┬─────┐
│     │     │     │     │
│  Z0 │  Z1 │  Z2 │  Z3 │  ← クエリ時は関係するゾーンのみ検索
│     │     │     │     │
├─────┼─────┼─────┼─────┤
│     │     │ ●●  │     │  ● = 登録された形状
│  Z4 │  Z5 │  Z6 │  Z7 │
│     │     │     │     │
└─────┴─────┴─────┴─────┘
       ◀───▶
      gridSize
```

**選び方の原則**:
- 登録する形状の典型的なサイズの **5～10倍** が目安
- 1ゾーンあたり10～100個程度の形状が入るのが理想

| 条件 | 問題 |
|------|------|
| 小さすぎる | 形状が複数ゾーンにまたがり、登録・更新コストが増加 |
| 大きすぎる | 1ゾーンに形状が集中し、絞り込み効果が薄れる |

**ゲームタイプ別の目安**:

| ゲームタイプ | 典型的な形状サイズ | 推奨グリッドサイズ |
|-------------|------------------|-----------------|
| 格闘ゲーム | 0.5～2m（キャラ） | 8～16m |
| アクションRPG | 1～3m（キャラ・敵） | 16～32m |
| オープンワールド | 1～10m（様々） | 32～64m |
| RTS・シミュレーション | 1～50m（建物含む） | 64～128m |

**コンストラクタ**:

```csharp
// 方法1: 固定サイズで指定（推奨）
var world = new SpatialWorld(gridSize: 16f);

// 方法2: 推定ワールドサイズから自動計算
// ワールドサイズ ÷ 64 で計算（8m～512mにクランプ）
var world = new SpatialWorld(estimatedWorldSize: 1000f, _: true);

// 方法3: SAP軸モードを指定
var world = new SpatialWorld(gridSize: 16f, axisMode: SAPAxisMode.XZ);

// 方法4: 後から再構築
world.RebuildWithOptimalGridSize();  // 現在の形状分布から最適化
world.RebuildPartition(newGridSize: 32f);  // 指定サイズで再構築
```

**ワールドサイズ別の早見表**:

| ワールドサイズ | 推奨グリッドサイズ | 備考 |
|---------------|-----------------|------|
| < 100m | 8～16m | 室内、アリーナ |
| 100m～500m | 16～32m | 中規模マップ |
| 500m～2km | 32～64m | 大規模マップ |
| > 2km | 64～256m | オープンワールド |

### SAP軸モード

SAP（Sweep and Prune）のソート軸を選択する。ゲームの形状分布に応じて選択することで、Broad Phaseの絞り込み効率を向上できる。

| モード | 説明 | 用途 |
|--------|------|------|
| `SAPAxisMode.X` | X軸でソート・判定（デフォルト） | 横スクロール、Z軸方向に長いマップ |
| `SAPAxisMode.Z` | Z軸でソート・判定 | X軸方向に長いマップ |
| `SAPAxisMode.XZ` | X軸でソート、Z軸も追加判定 | 均等に分布するマップ、オープンワールド |

```
SAPAxisMode.X（デフォルト）:
┌─────────────────────────────────────────┐
│  ●   ●   ●   ●   ●   ●   ●   ●   ●     │  X軸でソート
│  ◀──────────────────────────────────▶   │  → X軸範囲で絞り込み
└─────────────────────────────────────────┘

SAPAxisMode.XZ:
┌─────────────────────────────────────────┐
│  ●       ●       ●       ●       ●     │  X軸でソート
│      ●       ●       ●       ●         │  → X軸範囲で絞り込み
│  ●       ●       ●       ●       ●     │  → さらにZ軸範囲でフィルタ
└─────────────────────────────────────────┘
```

**選び方の目安**:

| ゲームタイプ | 推奨モード | 理由 |
|-------------|-----------|------|
| 横スクロールアクション | `X` | X軸方向に形状が広がる |
| サイドビューゲーム | `X` | X軸方向に形状が広がる |
| 縦スクロールシューター | `Z` | Z軸方向に形状が広がる |
| オープンワールド | `XZ` | XZ平面に均等に分布 |
| MMORPG | `XZ` | 広いフィールドに均等に分布 |
| 室内・ダンジョン | `X` or `XZ` | マップ構造に依存 |

**使用例**:

```csharp
// 横スクロールアクション（デフォルト）
var world = new SpatialWorld(gridSize: 16f, axisMode: SAPAxisMode.X);

// オープンワールドRPG
var world = new SpatialWorld(gridSize: 32f, axisMode: SAPAxisMode.XZ);

// 縦スクロールシューター
var world = new SpatialWorld(gridSize: 16f, axisMode: SAPAxisMode.Z);
```

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
├── CollisionSystem.Core/
│   ├── CollisionSystem.Core.csproj
│   ├── SpatialWorld.cs           # メインAPI
│   ├── Data/
│   │   ├── ShapeType.cs          # 形状種別 enum
│   │   ├── ShapeData.cs          # SphereData, CapsuleData, CylinderData, BoxData
│   │   ├── ShapeHandle.cs        # ハンドル型
│   │   ├── ShapeRegistry.cs      # 形状データ管理
│   │   └── HitResult.cs          # クエリ結果
│   ├── BroadPhase/
│   │   ├── SAPAxisMode.cs        # SAP 軸モード enum
│   │   ├── SAPEntry.cs           # SAP エントリ
│   │   ├── Zone.cs               # SAP ゾーン
│   │   └── WorldPartition.cs     # 空間分割管理
│   ├── NarrowPhase/
│   │   └── ShapeIntersection.cs  # 幾何学的交差判定
│   └── Queries/
│       └── QueryTypes.cs         # RayQuery, SphereOverlapQuery 等
└── CollisionSystem.Tests/
    ├── CollisionSystem.Tests.csproj
    ├── PointQueryTests.cs
    ├── RaycastTests.cs
    ├── SphereOverlapTests.cs
    ├── CapsuleSweepTests.cs
    ├── SlashQueryTests.cs
    ├── ShapeManagementTests.cs
    └── PerformanceTests.cs
```

## テスト

```bash
dotnet test libs/systems/CollisionSystem/CollisionSystem.Tests/
```

## ライセンス

MIT License
