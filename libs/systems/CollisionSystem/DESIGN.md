# CollisionSystem 設計書

ゲーム向け空間クエリライブラリの詳細設計ドキュメント。

namespace: `Tomato.CollisionSystem`

---

## 目次

1. [用語定義](#用語定義)
2. [設計哲学](#設計哲学)
3. [アーキテクチャ概要](#アーキテクチャ概要)
4. [Broad Phase詳細](#broad-phase詳細)
5. [Narrow Phase詳細](#narrow-phase詳細)
6. [クエリ詳細](#クエリ詳細)
7. [パフォーマンス設計](#パフォーマンス設計)
8. [実践パターン集](#実践パターン集)
9. [トラブルシューティング](#トラブルシューティング)

---

## 用語定義

### 中核概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **Shape** | Shape | 3D空間内の幾何学的形状。Sphere、Capsule、Cylinderがある。 |
| **クエリ** | Query | 空間に対する問い合わせ。レイキャスト、オーバーラップなど。 |
| **ヒット** | Hit | クエリの結果、形状との交差が検出されたこと。 |
| **AABB** | Axis-Aligned Bounding Box | 軸平行境界ボックス。形状を囲む最小の直方体。 |

### 2段階判定

| 用語 | 英語 | 定義 |
|------|------|------|
| **Broad Phase** | Broad Phase | 空間分割によるおおまかな候補絞り込み。O(n) → O(k)。 |
| **Narrow Phase** | Narrow Phase | 幾何学的な詳細判定。候補に対してのみ実行。 |
| **候補** | Candidate | Broad Phaseで絞り込まれた形状。Narrow Phaseで判定される。 |

### ゾーン分割

| 用語 | 英語 | 定義 |
|------|------|------|
| **ゾーン** | Zone | ワールドを固定サイズで区切った領域。 |
| **グリッドサイズ** | Grid Size | ゾーン1辺の長さ（メートル）。 |
| **SAP** | Sweep and Prune | 1軸ソートによる高速オーバーラップ判定。 |
| **SAP軸モード** | SAP Axis Mode | SAPのソート軸の選択。X軸、Z軸、XZ軸（2軸）。 |
| **主軸** | Primary Axis | SAPでソート・絞り込みを行う軸。 |
| **副軸** | Secondary Axis | XZモード時にフィルタリングに使用する追加軸。 |

---

## 設計哲学

### 原則1: 2段階判定（Broad → Narrow）

Broad Phaseでおおまかな候補を絞り込み、Narrow Phaseで詳細判定を行う。

```
全Shape（数千個）
    │
    ▼ Broad Phase（空間分割 + SAP）
候補Shape（数十個）
    │
    ▼ Narrow Phase（幾何学的判定）
ヒット結果（数個）
```

**メリット:**
- N個のShapeに対してN²回の判定を避けられる
- 候補数kに対してk回の詳細判定のみ
- 大規模ワールドでもスケールする

### 原則2: ゼロアロケーション（Zero Allocation）

毎フレームのクエリでヒープアロケーションを発生させない。

```csharp
// ✓ 良い: スタックアロケーション
Span<HitResult> results = stackalloc HitResult[32];
int count = world.QuerySphereOverlap(query, results);

// ✗ 悪い: ヒープアロケーション（このライブラリでは使わない）
List<HitResult> results = world.QuerySphereOverlap(query);
```

**実現方法:**
- クエリ結果は Span<HitResult> で返却
- 内部バッファは事前確保・再利用
- データ型は struct を使用

### 原則3: ハンドルによる安全な参照

削除後の誤参照を Generation 番号で検出する。

```csharp
var handle = world.AddSphere(center, radius);
world.Remove(handle);

// 削除後のハンドルは無効
bool valid = world.IsValid(handle);  // false
```

**ShapeHandle構造:**
- Index: 内部配列のインデックス
- Generation: 削除・再利用の追跡番号

### 原則4: 動的ワールドへの対応

形状の追加・削除・移動が毎フレーム発生しても効率的に動作する。

```csharp
// 毎フレーム位置更新（差分ソートで高速）
world.UpdateSphere(handle, newCenter, radius);
```

**最適化:**
- ゾーン移動は AABB 変化時のみ
- SAP配列はインサーションソートで差分更新
- 32個以下は愚直検索にフォールバック

---

## アーキテクチャ概要

```
┌─────────────────────────────────────────────────────────────────┐
│                        SpatialWorld                             │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                    Public API                              │  │
│  │  AddSphere/AddCapsule/AddCylinder                         │  │
│  │  UpdateSphere/UpdateCapsule/UpdateCylinder                │  │
│  │  Remove                                                    │  │
│  │  Raycast/QuerySphereOverlap/CapsuleSweep/QuerySlash       │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              │                                   │
│  ┌───────────────────────────┼───────────────────────────────┐  │
│  │                           ▼                                │  │
│  │  ┌─────────────────┐    ┌─────────────────────────────┐   │  │
│  │  │  ShapeRegistry  │    │      WorldPartition         │   │  │
│  │  │                 │    │                             │   │  │
│  │  │  Shape データ   │    │  ┌─────┐ ┌─────┐ ┌─────┐   │   │  │
│  │  │  AABB           │    │  │Zone │ │Zone │ │Zone │   │   │  │
│  │  │  Type           │    │  │ SAP │ │ SAP │ │ SAP │   │   │  │
│  │  │  UserData       │    │  └─────┘ └─────┘ └─────┘   │   │  │
│  │  └─────────────────┘    └─────────────────────────────┘   │  │
│  │            Data Layer             Broad Phase              │  │
│  └────────────────────────────────────────────────────────────┘  │
│                              │                                   │
│  ┌───────────────────────────┼───────────────────────────────┐  │
│  │                           ▼                                │  │
│  │              ┌─────────────────────────┐                   │  │
│  │              │   ShapeIntersection     │                   │  │
│  │              │                         │                   │  │
│  │              │  Point vs Shape         │                   │  │
│  │              │  Ray vs Shape           │                   │  │
│  │              │  Sphere vs Shape        │                   │  │
│  │              │  Capsule vs Shape       │                   │  │
│  │              │  Slash vs Shape         │                   │  │
│  │              └─────────────────────────┘                   │  │
│  │                      Narrow Phase                          │  │
│  └────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### コンポーネント責務

| コンポーネント | 責務 |
|---------------|------|
| **SpatialWorld** | 公開API。登録・更新・削除・クエリを統合 |
| **ShapeRegistry** | Shapeデータの管理。SoAレイアウト |
| **WorldPartition** | ゾーン管理。Broad Phase候補絞り込み |
| **Zone** | 1ゾーン内のSAP配列管理 |
| **ShapeIntersection** | 幾何学的交差判定。static メソッド群 |

---

## Broad Phase詳細

### WorldPartition

3D空間を固定サイズのゾーンに分割する。

```
ワールド空間
┌─────┬─────┬─────┬─────┐
│ Z0  │ Z1  │ Z2  │ Z3  │
├─────┼─────┼─────┼─────┤
│ Z4  │ Z5  │ Z6  │ Z7  │  ← 各ゾーンは独立したSAP配列を持つ
├─────┼─────┼─────┼─────┤
│ Z8  │ Z9  │ Z10 │ Z11 │
└─────┴─────┴─────┴─────┘
       gridSize
       ◀───────▶
```

**ゾーン座標のパック:**
```csharp
// 3軸座標を64ビット整数にパック
// 各軸21ビット（±100万の範囲）
long coord = ((x + 0x100000) << 42) | ((y + 0x100000) << 21) | (z + 0x100000);
```

### Zone（SAP配列）

各ゾーン内では主軸でソートされたSAP（Sweep and Prune）配列を保持する。

```
主軸方向にソート（例: X軸モードの場合）
[MinPrimary=1.0, MaxPrimary=2.5] [MinPrimary=2.0, MaxPrimary=4.0] [MinPrimary=5.0, MaxPrimary=6.0]
      ◀────────A────────▶           ◀────────B────────▶           ◀────────C────────▶

クエリ: MinPrimary=1.5, MaxPrimary=3.5
  → A, B が候補（MinPrimary ≤ 3.5 かつ MaxPrimary ≥ 1.5）
  → C はスキップ（MinPrimary > クエリMaxPrimary）
```

**SAP軸モード:**

| モード | 主軸 | 副軸 | 動作 |
|--------|------|------|------|
| `X` | X | なし | X軸でソート・判定（デフォルト） |
| `Z` | Z | なし | Z軸でソート・判定 |
| `XZ` | X | Z | X軸でソート、Z軸も追加フィルタ |

```
SAPAxisMode.X:
    主軸でソート → 主軸範囲で絞り込み → 候補確定

SAPAxisMode.XZ:
    主軸でソート → 主軸範囲で絞り込み → 副軸範囲でフィルタ → 候補確定
```

**差分更新:**
```csharp
// 位置更新時は差分ソート（Insertion Sort）
// 移動量が小さい場合は O(1) ～ O(k) で完了
zone.Update(shapeIndex, newMinPrimary, newMaxPrimary, newMinSecondary, newMaxSecondary);
```

### クエリフロー

```
Query(AABB)
    │
    ├─1. クエリAABBがカバーするゾーン座標を列挙
    │
    ├─2. 各ゾーンで主軸オーバーラップ候補を収集
    │   └─ 二分探索で開始位置を特定
    │   └─ MaxPrimary < queryMinPrimary の間スキップ
    │   └─ MinPrimary > queryMaxPrimary で終了
    │   └─ XZモードの場合は副軸でもフィルタ
    │
    ├─3. 重複排除
    │   └─ 複数ゾーンにまたがるShapeの重複を除去
    │
    └─4. 3軸AABBオーバーラップで最終フィルタ
```

### 愚直検索へのフォールバック

Shape数が32個以下の場合、ゾーン分割のオーバーヘッドが逆効果になるため愚直検索を使用する。

```csharp
private int QueryBruteForce(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
{
    int count = 0;
    foreach (var shapeIndex in _shapeToZones.Keys)
    {
        if (allAABBs[shapeIndex].Intersects(queryAABB))
            candidates[count++] = shapeIndex;
    }
    return count;
}
```

---

## Narrow Phase詳細

### ShapeIntersection

全て static メソッドでゼロアロケーション。結果は out パラメータで返却。

### 対応する判定組み合わせ

| クエリ / Shape | Sphere | Capsule | Cylinder |
|:---------------|:------:|:-------:|:--------:|
| Point          | ✓      | ✓       | ✓        |
| Ray            | ✓      | ✓       | ✓        |
| Sphere         | ✓      | ✓       | ✓        |
| CapsuleSweep   | ✓      | ✓       | ✓        |
| Slash          | ✓      | ✓       | ✓        |

### Point vs Shape

```csharp
// Point vs Sphere: 距離判定
bool PointSphere(in Vector3 point, in SphereData sphere)
{
    return DistanceSquared(point, sphere.Center) <= sphere.Radius²;
}

// Point vs Capsule: 線分への最近点からの距離
bool PointCapsule(in Vector3 point, in CapsuleData capsule)
{
    var closest = ClosestPointOnSegment(point, capsule.Point1, capsule.Point2);
    return DistanceSquared(point, closest) <= capsule.Radius²;
}

// Point vs Cylinder: Y軸範囲 + XZ平面距離
bool PointCylinder(in Vector3 point, in CylinderData cylinder)
{
    float localY = point.Y - cylinder.BaseCenter.Y;
    if (localY < 0 || localY > cylinder.Height) return false;

    float dx = point.X - cylinder.BaseCenter.X;
    float dz = point.Z - cylinder.BaseCenter.Z;
    return dx² + dz² <= cylinder.Radius²;
}
```

### Ray vs Shape

```csharp
// Ray vs Sphere: 二次方程式の判別式
bool RaySphere(origin, direction, maxDistance, sphere, out t, out point, out normal)
{
    // |origin + t*direction - center|² = radius²
    // t² + 2*b*t + c = 0  (b = dot(oc, dir), c = |oc|² - r²)

    var oc = origin - sphere.Center;
    var b = Dot(oc, direction);
    var c = oc.LengthSquared - sphere.Radius²;
    var discriminant = b² - c;

    if (discriminant < 0) return false;

    t = -b - Sqrt(discriminant);
    if (t < 0 || t > maxDistance) return false;

    point = origin + direction * t;
    normal = (point - center) / radius;
    return true;
}
```

### Sphere vs Sphere

```csharp
bool SphereSphere(sphereA, sphereB, out point, out normal, out distance)
{
    var diff = sphereB.Center - sphereA.Center;
    var distSq = diff.LengthSquared;
    var radiusSum = sphereA.Radius + sphereB.Radius;

    if (distSq > radiusSum²) return false;

    distance = Sqrt(distSq);
    normal = distance > 0 ? diff / distance : Vector3.UnitY;
    point = sphereA.Center + normal * sphereA.Radius;
    return true;
}
```

### Capsule Sweep

移動するカプセルがいつ・どこで形状に衝突するかを計算する。

```csharp
// Time of Impact (TOI) を計算
// TOI = 0.0: 開始位置で衝突
// TOI = 0.5: 移動の半分で衝突
// TOI = 1.0: 終了位置で衝突

bool CapsuleSweep(start, end, radius, target, out toi, out point, out normal)
{
    // 簡易実装: 移動方向へのレイキャストで近似
    var direction = end - start;
    var length = direction.Length;
    if (length < Epsilon) return false;

    direction /= length;

    // 拡張されたターゲット（半径分膨張）に対してレイキャスト
    if (RayExpandedShape(start, direction, length, target, radius, out var t, ...))
    {
        toi = t / length;
        return true;
    }
    return false;
}
```

### Slash（斬撃線）

剣の軌跡が形状と交差するかを判定する。四角形の軌跡を2つの三角形に分割して判定。

```
StartBase ─────────── StartTip
    │ ＼               │
    │   ＼  Triangle1  │
    │     ＼           │
    │       ＼─────────│
    │  Triangle2  ＼   │
    │               ＼ │
EndBase ──────────── EndTip
```

```csharp
bool SlashSphere(startBase, startTip, endBase, endTip, sphere, out point, out normal, out distance)
{
    // 1. 三角形 (StartBase, StartTip, EndTip) vs Sphere
    // 2. 三角形 (StartBase, EndTip, EndBase) vs Sphere
    // 3. いずれかがヒットすれば衝突

    var tri1Hit = TriangleSphere(startBase, startTip, endTip, sphere, ...);
    var tri2Hit = TriangleSphere(startBase, endTip, endBase, sphere, ...);

    return tri1Hit || tri2Hit;
}
```

---

## クエリ詳細

### RayQuery

```csharp
public readonly struct RayQuery
{
    public readonly Vector3 Origin;      // レイの始点
    public readonly Vector3 Direction;   // 正規化された方向
    public readonly float MaxDistance;   // 最大距離

    public Vector3 End => Origin + Direction * MaxDistance;
    public AABB GetAABB();  // Broad Phase用
}
```

**使用例:**
```csharp
// 銃弾のヒットスキャン
var ray = new RayQuery(gunPosition, aimDirection, 100f);
if (world.Raycast(ray, out var hit))
{
    SpawnBulletHole(hit.Point, hit.Normal);
    DealDamage(hit.ShapeIndex);
}

// 貫通弾（全ヒット取得）
Span<HitResult> hits = stackalloc HitResult[16];
int count = world.RaycastAll(ray, hits);
for (int i = 0; i < count; i++)
{
    DealDamage(hits[i].ShapeIndex, damageMultiplier: 1f / (i + 1));
}
```

### SphereOverlapQuery

```csharp
public readonly struct SphereOverlapQuery
{
    public readonly Vector3 Center;
    public readonly float Radius;

    public AABB GetAABB();
}
```

**使用例:**
```csharp
// 爆発範囲内の敵検索
var query = new SphereOverlapQuery(explosionCenter, explosionRadius);
Span<HitResult> hits = stackalloc HitResult[32];
int count = world.QuerySphereOverlap(query, hits);

for (int i = 0; i < count; i++)
{
    float distanceRatio = hits[i].Distance / explosionRadius;
    float damage = baseDamage * (1f - distanceRatio);
    DealDamage(hits[i].ShapeIndex, damage);
}
```

### CapsuleSweepQuery

```csharp
public readonly struct CapsuleSweepQuery
{
    public readonly Vector3 Start;   // 開始位置
    public readonly Vector3 End;     // 終了位置
    public readonly float Radius;    // カプセル半径

    public AABB GetAABB();
}
```

**使用例:**
```csharp
// キャラクター移動判定
var sweep = new CapsuleSweepQuery(currentPos, targetPos, characterRadius);
if (world.CapsuleSweep(sweep, out var hit))
{
    // 衝突点の手前で停止
    var safePos = Vector3.Lerp(currentPos, targetPos, hit.Distance * 0.99f);
    character.Position = safePos;

    // 壁ずり移動
    var remainder = targetPos - safePos;
    var slideDir = remainder - hit.Normal * Vector3.Dot(remainder, hit.Normal);
    character.Position += slideDir;
}
```

### SlashQuery

```csharp
public readonly struct SlashQuery
{
    public readonly Vector3 StartBase;  // 開始時の剣の根元
    public readonly Vector3 StartTip;   // 開始時の剣の先端
    public readonly Vector3 EndBase;    // 終了時の剣の根元
    public readonly Vector3 EndTip;     // 終了時の剣の先端

    public AABB GetAABB();
}
```

**使用例:**
```csharp
// 前フレームと今フレームの剣の位置から斬撃判定
var slash = new SlashQuery(
    prevSwordBase, prevSwordTip,
    currSwordBase, currSwordTip);

Span<HitResult> hits = stackalloc HitResult[8];
int count = world.QuerySlash(slash, hits);

for (int i = 0; i < count; i++)
{
    PlaySlashEffect(hits[i].Point, hits[i].Normal);
    DealDamage(hits[i].ShapeIndex);
}
```

---

## パフォーマンス設計

### メモリレイアウト

**ShapeRegistry: SoA（Structure of Arrays）**

```csharp
// AoS（遅い）
struct ShapeAoS {
    ShapeType type;
    SphereData sphere;
    CapsuleData capsule;
    CylinderData cylinder;
    AABB aabb;
    int userData;
}
ShapeAoS[] shapes;

// SoA（速い - キャッシュ効率が良い）
ShapeType[] types;
SphereData[] spheres;
CapsuleData[] capsules;
CylinderData[] cylinders;
AABB[] aabbs;
int[] userDatas;
```

### グリッドサイズの選択

グリッドサイズはクエリ性能を左右する最重要パラメータ。

```
┌─────────────────────────────────────────────────────────────────┐
│ グリッドサイズが小さすぎる場合                                     │
│                                                                 │
│  ┌──┬──┬──┬──┐                                                  │
│  │●●│● │  │  │  1つの形状が複数ゾーンにまたがる                │
│  ├──┼──┼──┼──┤  → 登録・更新時に複数ゾーンを操作                │
│  │● │●●│  │  │  → オーバーヘッド増加                           │
│  └──┴──┴──┴──┘                                                  │
│                                                                 │
│  問題: 形状サイズ > グリッドサイズ だと効率低下                    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ グリッドサイズが大きすぎる場合                                     │
│                                                                 │
│  ┌────────────────┬────────────────┐                            │
│  │ ●  ●  ●  ●     │                │  1ゾーンに形状が集中       │
│  │ ●  ●  ●  ●     │                │  → Broad Phaseの絞り込み │
│  │ ●  ●  ●  ●     │                │    効果が薄れる           │
│  └────────────────┴────────────────┘                            │
│                                                                 │
│  問題: ほぼ全形状が候補になり O(n) に近づく                      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ 適切なグリッドサイズ                                               │
│                                                                 │
│  ┌─────┬─────┬─────┬─────┐                                      │
│  │     │ ●●  │     │     │  形状が1～2ゾーンに収まる            │
│  ├─────┼─────┼─────┼─────┤  → 効率的な絞り込み                  │
│  │     │     │ ●●  │     │  → クエリは関係ゾーンのみ検索        │
│  └─────┴─────┴─────┴─────┘                                      │
│                                                                 │
│  目安: 形状サイズの 5～10倍                                      │
└─────────────────────────────────────────────────────────────────┘
```

**選定基準**:

| 基準 | 推奨グリッドサイズ |
|-----|-----------------|
| 形状サイズ基準 | 典型的な形状サイズ × 5～10 |
| 密度基準 | 1ゾーンあたり10～100形状が理想 |

**ゲームタイプ別**:

| ゲームタイプ | 形状サイズ例 | 推奨グリッドサイズ |
|-------------|-------------|-----------------|
| 格闘ゲーム | 0.5～2m | 8～16m |
| アクションRPG | 1～3m | 16～32m |
| シューター | 0.5～2m | 16～32m |
| オープンワールド | 1～10m | 32～64m |
| RTS | 1～50m | 64～128m |

**ワールドサイズ別**:

| ワールドサイズ | 推奨グリッドサイズ | 理由 |
|---------------|-----------------|------|
| < 100m        | 8～16m          | ゾーン数を適度に保つ |
| 100m～1km     | 16～64m         | バランス |
| > 1km         | 64～512m        | ゾーン管理のオーバーヘッド削減 |

**コンストラクタの選択**:

```csharp
// 方法1: 固定サイズ指定（推奨）
// 形状サイズが分かっている場合
var world = new SpatialWorld(gridSize: 16f);

// 方法2: ワールドサイズから自動計算
// gridSize = estimatedWorldSize / 64（8m～512mにクランプ）
var world = new SpatialWorld(estimatedWorldSize: 500f, _: true);
// → 500 / 64 ≈ 8m

// 方法3: SAP軸モードを指定
var world = new SpatialWorld(gridSize: 16f, axisMode: SAPAxisMode.XZ);

// 方法4: デフォルト（省略時）
var world = new SpatialWorld();  // gridSize = 8m（MinGridSize）, axisMode = X
```

**SAP軸モードの選択**:

| ゲームタイプ | 推奨モード | 理由 |
|-------------|-----------|------|
| 横スクロール | `X` | X軸方向に形状が広がる |
| 縦スクロール | `Z` | Z軸方向に形状が広がる |
| オープンワールド | `XZ` | XZ平面に均等に分布 |
| MMORPG | `XZ` | 広いフィールドに均等に分布 |

**動的再構築**:

```csharp
// 形状追加後に現在の分布から最適化
world.RebuildWithOptimalGridSize();

// 明示的にサイズ指定で再構築
world.RebuildPartition(newGridSize: 32f);
```

**デバッグ用プロパティ**:

```csharp
Console.WriteLine($"Zone size: {world.CurrentGridSize}");
Console.WriteLine($"Shape count: {world.ShapeCount}");
Console.WriteLine($"Zone count: {world.CellCount}");
Console.WriteLine($"Shapes per zone: {world.ShapeCount / (float)world.CellCount:F1}");
```

### パフォーマンス指標

| 指標 | 目標 |
|-----|-----|
| 1フレームあたりのアロケーション | 0 |
| 1000 Shape + 100 クエリ | < 1ms |
| Shape追加/削除 | O(log n) |
| Shape位置更新（小移動） | O(1)～O(k) |

### 最適化のヒント

1. **スタックアロケーションを使う**
   ```csharp
   Span<HitResult> results = stackalloc HitResult[32];
   ```

2. **クエリ結果の上限を設定**
   ```csharp
   // 必要以上にバッファを大きくしない
   Span<HitResult> results = stackalloc HitResult[8];  // 8個で十分
   ```

3. **静的Shapeはフラグを設定**
   ```csharp
   world.AddSphere(center, radius, isStatic: true);
   // → 将来的に静的専用の最適化が可能
   ```

4. **UserDataでゲームデータにアクセス**
   ```csharp
   var handle = world.AddSphere(center, radius, userData: entityId);
   // クエリ結果から直接EntityIDを取得
   int entityId = world.GetUserData(hit.ShapeIndex);
   ```

---

## 実践パターン集

### シューターの弾道判定

```csharp
public class ProjectileSystem
{
    private readonly SpatialWorld _world;

    public HitResult? ProcessProjectile(Vector3 start, Vector3 end, float radius)
    {
        // カプセルスイープで弾道判定
        var sweep = new CapsuleSweepQuery(start, end, radius);

        if (_world.CapsuleSweep(sweep, out var hit))
        {
            // 壁や敵にヒット
            return hit;
        }

        return null;
    }

    public void ProcessHitscan(Vector3 origin, Vector3 direction, float maxDistance)
    {
        var ray = new RayQuery(origin, direction, maxDistance);

        Span<HitResult> hits = stackalloc HitResult[16];
        int count = _world.RaycastAll(ray, hits);

        float damageMultiplier = 1f;

        for (int i = 0; i < count; i++)
        {
            var entity = GetEntity(hits[i].ShapeIndex);

            if (entity.BlocksBullets)
            {
                // 壁で停止
                SpawnBulletHole(hits[i].Point, hits[i].Normal);
                break;
            }
            else
            {
                // 敵を貫通（減衰あり）
                entity.TakeDamage(baseDamage * damageMultiplier);
                damageMultiplier *= 0.7f;
            }
        }
    }
}
```

### 範囲攻撃・爆発

```csharp
public class ExplosionSystem
{
    private readonly SpatialWorld _world;

    public void Explode(Vector3 center, float radius, float maxDamage)
    {
        var query = new SphereOverlapQuery(center, radius);
        Span<HitResult> hits = stackalloc HitResult[64];
        int count = _world.QuerySphereOverlap(query, hits);

        for (int i = 0; i < count; i++)
        {
            var entity = GetEntity(hits[i].ShapeIndex);

            // 距離に応じたダメージ減衰
            float distanceRatio = hits[i].Distance / radius;
            float damage = maxDamage * (1f - distanceRatio * distanceRatio);

            // ノックバック方向
            var knockbackDir = hits[i].Normal;

            entity.TakeDamage(damage);
            entity.ApplyKnockback(knockbackDir * damage * 0.1f);

            // エフェクト
            SpawnHitEffect(hits[i].Point, knockbackDir);
        }

        // 爆発エフェクト
        SpawnExplosionEffect(center, radius);
    }
}
```

### 近接武器の軌跡判定

```csharp
public class MeleeWeaponSystem
{
    private readonly SpatialWorld _world;

    private Vector3 _prevWeaponBase;
    private Vector3 _prevWeaponTip;

    public void UpdateWeapon(Vector3 weaponBase, Vector3 weaponTip)
    {
        // 前フレームとの軌跡で斬撃判定
        if (_prevWeaponBase != Vector3.Zero)
        {
            var slash = new SlashQuery(
                _prevWeaponBase, _prevWeaponTip,
                weaponBase, weaponTip);

            Span<HitResult> hits = stackalloc HitResult[8];
            int count = _world.QuerySlash(slash, hits);

            for (int i = 0; i < count; i++)
            {
                ProcessHit(hits[i]);
            }
        }

        _prevWeaponBase = weaponBase;
        _prevWeaponTip = weaponTip;
    }

    private void ProcessHit(in HitResult hit)
    {
        var entity = GetEntity(hit.ShapeIndex);

        // 斬撃エフェクト
        SpawnSlashEffect(hit.Point, hit.Normal);

        // ダメージ
        entity.TakeDamage(slashDamage);
    }
}
```

---

## トラブルシューティング

### ヒットが検出されない

**1. グリッドサイズを確認**
```csharp
Console.WriteLine($"Zone size: {world.CurrentGridSize}");
Console.WriteLine($"Shape count: {world.ShapeCount}");
Console.WriteLine($"Zone count: {world.CellCount}");
```

**2. AABBが正しいか確認**
```csharp
// クエリのAABBと形状のAABBが重なっているか
var queryAABB = query.GetAABB();
var shapeAABB = world.GetAABB(handle.Index);
bool overlaps = queryAABB.Intersects(shapeAABB);
```

### 意図しないヒットが発生

**1. 重複検出を確認**
```csharp
// カスタム衝突検出の場合、A-BとB-Aの重複に注意
if (handleA.Index > handleB.Index)
    continue;  // 小さいインデックスからのみ報告
```

**2. 自己衝突を除外**
```csharp
// 同じエンティティの形状同士は衝突しないように
if (hits[i].ShapeIndex == selfShapeIndex)
    continue;
```

### パフォーマンスが悪い

**1. グリッドサイズを調整**
```csharp
// 大きすぎる → 1ゾーンに形状が集中
// 小さすぎる → ゾーン数が爆発
Console.WriteLine($"Shapes per zone: {world.ShapeCount / world.CellCount}");
```

**2. 結果バッファサイズを適切に**
```csharp
// 大きすぎるとスタックオーバーフロー
// 小さすぎると必要な結果が得られない
Span<HitResult> results = stackalloc HitResult[32];  // 適切なサイズ
```

**3. クエリ範囲を限定**
```csharp
// 広すぎるクエリは避ける
var query = new SphereOverlapQuery(center, radius: 5f);  // 必要最小限
```

### メモリ使用量が多い

**1. 形状数を確認**
```csharp
Console.WriteLine($"Shape count: {world.ShapeCount}");
```

**2. 不要な形状を削除**
```csharp
// 画面外の形状は削除を検討
if (!IsInViewFrustum(shape.Position))
{
    world.Remove(handle);
}
```

**3. ゾーンの再構築**
```csharp
// 形状が大きく移動した後は再構築
world.RebuildWithOptimalGridSize();
```

---

## ディレクトリ構造

```
CollisionSystem/
├── README.md                        # クイックスタート
├── DESIGN.md                        # 本ドキュメント
│
├── CollisionSystem.Core/
│   ├── CollisionSystem.Core.csproj
│   ├── SpatialWorld.cs              # メインAPI
│   │
│   ├── Data/
│   │   ├── ShapeType.cs             # 形状種別 enum
│   │   ├── ShapeData.cs             # SphereData, CapsuleData, CylinderData
│   │   ├── ShapeHandle.cs           # ハンドル型
│   │   ├── ShapeRegistry.cs         # 形状データ管理（SoA）
│   │   └── HitResult.cs             # クエリ結果
│   │
│   ├── BroadPhase/
│   │   ├── SAPAxisMode.cs           # SAP 軸モード enum
│   │   ├── SAPEntry.cs              # SAP エントリ
│   │   ├── Zone.cs                  # SAP ゾーン
│   │   └── WorldPartition.cs        # 空間分割管理
│   │
│   ├── NarrowPhase/
│   │   └── ShapeIntersection.cs     # 幾何学的交差判定
│   │
│   └── Queries/
│       └── QueryTypes.cs            # RayQuery, SphereOverlapQuery 等
│
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
