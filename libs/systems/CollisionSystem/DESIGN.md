# CollisionSystem 設計書

ゲーム向け空間クエリライブラリの詳細設計ドキュメント。

namespace: `Tomato.CollisionSystem`

---

## 目次

1. [用語定義](#用語定義)
2. [設計哲学](#設計哲学)
3. [アーキテクチャ概要](#アーキテクチャ概要)
4. [Broad Phase 戦略](#broad-phase-戦略)
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
| **Shape** | Shape | 3D空間内の幾何学的形状。Sphere、Capsule、Cylinder、Boxがある。 |
| **クエリ** | Query | 空間に対する問い合わせ。レイキャスト、オーバーラップなど。 |
| **ヒット** | Hit | クエリの結果、形状との交差が検出されたこと。 |
| **AABB** | Axis-Aligned Bounding Box | 軸平行境界ボックス。形状を囲む最小の直方体。 |

### 2段階判定

| 用語 | 英語 | 定義 |
|------|------|------|
| **Broad Phase** | Broad Phase | 空間分割によるおおまかな候補絞り込み。O(n) → O(k)。 |
| **Narrow Phase** | Narrow Phase | 幾何学的な詳細判定。候補に対してのみ実行。 |
| **候補** | Candidate | Broad Phaseで絞り込まれた形状。Narrow Phaseで判定される。 |

### レイヤーマスク

| 用語 | 英語 | 定義 |
|------|------|------|
| **レイヤーマスク** | Layer Mask | 形状に割り当てる32ビットのビットマスク。フィルタリングに使用。 |
| **IncludeMask** | Include Mask | クエリで判定対象とするレイヤー。いずれかのビットが一致すれば候補。 |
| **ExcludeMask** | Exclude Mask | クエリで除外するレイヤー。いずれかのビットが一致すれば除外。 |

### Broad Phase 戦略

| 用語 | 英語 | 定義 |
|------|------|------|
| **BVH** | Bounding Volume Hierarchy | バウンディングボリューム階層。再帰的に空間を二分割。 |
| **DBVT** | Dynamic Bounding Volume Tree | 動的BVH。増分更新に最適化。 |
| **Octree** | Octree | 8分木。空間を再帰的に8分割。 |
| **MBP** | Multi-Box Pruning | 複数リージョンによる枝刈り。固定境界向け。 |
| **GridSAP** | Grid + Sweep and Prune | グリッド分割 + 1軸ソート。 |
| **SpatialHash** | Spatial Hash | 空間ハッシュ。O(1)の追加・削除。 |

---

## 設計哲学

### 原則1: 2段階判定（Broad → Narrow）

Broad Phaseでおおまかな候補を絞り込み、Narrow Phaseで詳細判定を行う。

```
全Shape（数千個）
    │
    ▼ Broad Phase（空間分割）
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
// 良い: スタックアロケーション
Span<HitResult> results = stackalloc HitResult[32];
int count = world.QuerySphereOverlap(query, results);

// 悪い: ヒープアロケーション（このライブラリでは使わない）
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

### 原則4: 戦略パターン

用途に応じて最適な Broad Phase を選択可能。

```csharp
// 動的シーン向け
var world = new SpatialWorld(new BVHBroadPhase(maxShapes: 10000));

// オープンワールド向け
var world = new SpatialWorld(new DBVTBroadPhase(maxShapes: 50000));

// 固定マップ向け
var world = new SpatialWorld(new MBPBroadPhase(bounds, 8, 8, maxShapes: 10000));
```

### 原則5: レイヤーマスクフィルタリング

Narrow Phase前にビットマスクで候補をフィルタリング。

```csharp
// 判定ロジック
bool shouldTest = (shapeMask & includeMask) != 0 && (shapeMask & excludeMask) == 0;
```

**メリット:**
- Narrow Phaseの計算をスキップできる（パフォーマンス向上）
- ゲームロジックと衝突判定を分離
- 複数レイヤーの組み合わせが簡単

```csharp
// レイヤー定義
const uint LayerPlayer = 0x01;
const uint LayerEnemy = 0x02;
const uint LayerEnvironment = 0x04;

// 敵レイヤーのみ攻撃対象
var attackRay = new RayQuery(origin, dir, 10f, includeMask: LayerEnemy);

// 環境のみ衝突（キャラクターは通過）
var moveSweep = new CapsuleSweepQuery(start, end, 0.4f, includeMask: LayerEnvironment);
```

---

## アーキテクチャ概要

```
┌─────────────────────────────────────────────────────────────────┐
│                        SpatialWorld                             │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                    Public API                              │  │
│  │  AddSphere/AddCapsule/AddCylinder/AddBox (+ layerMask)    │  │
│  │  UpdateSphere/UpdateCapsule/UpdateCylinder/UpdateBox      │  │
│  │  Remove, GetLayerMask, SetLayerMask                        │  │
│  │  Raycast/QuerySphereOverlap/CapsuleSweep/QuerySlash       │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              │                                   │
│  ┌───────────────────────────┼───────────────────────────────┐  │
│  │                           ▼                                │  │
│  │  ┌─────────────────┐    ┌─────────────────────────────┐   │  │
│  │  │  ShapeRegistry  │    │       IBroadPhase           │   │  │
│  │  │                 │    │                             │   │  │
│  │  │  Shape データ   │    │  ┌─────────────────────┐   │   │  │
│  │  │  AABB           │    │  │ BVHBroadPhase       │   │   │  │
│  │  │  Type           │    │  │ DBVTBroadPhase      │   │   │  │
│  │  │  UserData       │    │  │ OctreeBroadPhase    │   │   │  │
│  │  │  LayerMask      │    │  │ ...                 │   │   │  │
│  │  └─────────────────┘    │  │ MBPBroadPhase       │   │   │  │
│  │                         │  │ GridSAPBroadPhase   │   │   │  │
│  │                         │  │ SpatialHashBroadPhase│  │   │  │
│  │                         │  └─────────────────────┘   │   │  │
│  │            Data Layer   └─────────────────────────────┘   │  │
│  │                                  Broad Phase              │  │
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
| **SpatialWorld** | 公開API。登録・更新・削除・クエリを統合。レイヤーマスクアクセス |
| **ShapeRegistry** | Shapeデータの管理。SoAレイアウト。LayerMask保持 |
| **IBroadPhase** | Broad Phase インターフェース |
| **ShapeIntersection** | 幾何学的交差判定。static メソッド群 |

---

## Broad Phase 戦略

### IBroadPhase インターフェース

すべての Broad Phase 戦略が実装するインターフェース。

```csharp
public interface IBroadPhase
{
    void Add(int shapeIndex, in AABB aabb);
    bool Remove(int shapeIndex);
    void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB);
    int Query(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs);
    void Clear();
}
```

### 戦略比較

| 戦略 | 境界 | Add | Remove | Update | Query | 適用シーン |
|------|------|-----|--------|--------|-------|----------|
| BVH | 不要 | O(log n) | O(log n) | O(log n) | O(log n) | 動的シーン全般 |
| DBVT | 不要 | O(log n) | O(log n) | O(1)* | O(log n) | 静的優位シーン |
| Octree | 必要 | O(log n) | O(log n) | O(log n) | O(log n) | 疎な空間 |
| MBP | 必要 | O(1) | O(1) | O(1) | O(k) | 固定境界マップ |
| GridSAP | 不要 | O(z) | O(z) | O(z) | O(z·k) | 均一分布 |
| SpatialHash | 不要 | O(c) | O(c) | O(c) | O(c·k) | 中規模均一 |

*: DBVT の Update は移動量が小さい場合 O(1)、大きい場合 O(log n)
z: 形状がまたがるゾーン数、c: セル数、k: 候補数

### BVHBroadPhase

バウンディングボリューム階層。空間を再帰的に二分割する木構造。

```
         [Root AABB]
        /          \
    [Left]        [Right]
    /    \        /     \
  [A]   [B]    [C]     [D]
```

**特徴:**
- バランスの取れた性能（クエリ・更新ともに高速）
- ワールド境界が不要
- SAH（Surface Area Heuristic）による最適な分割

**初期化:**
```csharp
var bvh = new BVHBroadPhase(
    maxShapes: 10000,    // 最大形状数
    useSAH: true         // SAH を使用（推奨）
);
var world = new SpatialWorld(bvh);
```

**推奨用途:**
- ゲーム全般（推奨デフォルト）
- 動的シーン
- 形状の追加・削除が頻繁

### DBVTBroadPhase

動的バウンディングボリューム木。増分更新に最適化された BVH 変種。

**特徴:**
- クエリ性能が最速
- 増分更新で小さな移動は O(1)
- margin パラメータで更新頻度を調整

**初期化:**
```csharp
var dbvt = new DBVTBroadPhase(
    maxShapes: 10000,
    margin: 0.1f         // AABB のマージン（移動許容量）
);
var world = new SpatialWorld(dbvt);
```

**推奨用途:**
- 静的オブジェクトが多いシーン
- 物理シミュレーション（ペア検出）
- オープンワールド

### OctreeBroadPhase

8分木。空間を再帰的に8つの立方体に分割。

```
      ┌───┬───┐
     /│  /│  /│
    ┌─┼─┬─┼─┐ │
    │ └─┼─┴─┼─┤
    │  /│  /│ /
    └─┼─┴─┼─┘/
      └───┴───┘
```

**特徴:**
- 疎な空間で効率的（空のノードをスキップ）
- 深度制限で過度な分割を防止
- ワールド境界が必要

**初期化:**
```csharp
var bounds = new AABB(
    new Vector3(-1000, -100, -1000),
    new Vector3(1000, 100, 1000)
);
var octree = new OctreeBroadPhase(
    bounds,
    maxDepth: 8,         // 最大深度
    maxShapes: 10000
);
var world = new SpatialWorld(octree);
```

**推奨用途:**
- 広大で疎なワールド
- 静的シーン
- 形状サイズが均一

### MBPBroadPhase

Multi-Box Pruning。空間を固定リージョンに分割し、各リージョン内でソートベース枝刈り。

```
┌─────┬─────┬─────┬─────┐
│ R0  │ R1  │ R2  │ R3  │
├─────┼─────┼─────┼─────┤
│ R4  │ R5  │ R6  │ R7  │  8x8 = 64 リージョン
├─────┼─────┼─────┼─────┤
│ ... │     │     │     │
└─────┴─────┴─────┴─────┘
```

**特徴:**
- 安定した性能（最悪ケースが予測可能）
- X-Z 平面でリージョン分割
- ワールド境界が必要

**初期化:**
```csharp
var bounds = new AABB(
    new Vector3(-500, -500, -500),
    new Vector3(500, 500, 500)
);
var mbp = new MBPBroadPhase(
    bounds,
    regionsX: 8,         // X方向のリージョン数
    regionsZ: 8,         // Z方向のリージョン数
    maxShapes: 10000
);
var world = new SpatialWorld(mbp);
```

**推奨用途:**
- 固定境界のあるマップ
- アリーナ型ゲーム
- 大規模バッチ処理

### GridSAPBroadPhase

グリッド分割 + Sweep and Prune。固定サイズのゾーンに分割し、各ゾーン内で SAP を適用。

**特徴:**
- 動的にゾーンを生成（境界不要）
- SAP 軸モードで絞り込み効率を調整
- 大きすぎるオブジェクトは別リストで管理

**初期化:**
```csharp
var gridSap = new GridSAPBroadPhase(
    gridSize: 16f,                    // ゾーンの辺の長さ
    axisMode: SAPAxisMode.XZ          // SAP 軸モード
);
var world = new SpatialWorld(gridSap);
```

**SAP軸モード:**

| モード | 説明 | 用途 |
|--------|------|------|
| `X` | X軸でソート（デフォルト） | 横スクロール |
| `Z` | Z軸でソート | 縦スクロール |
| `XZ` | X軸ソート + Z軸フィルタ | オープンワールド |

**推奨用途:**
- 均一分布のシーン
- 小〜中規模マップ

### SpatialHashBroadPhase

空間ハッシュ。固定サイズのセルにオブジェクトをハッシュ登録。

**特徴:**
- O(1) の追加・削除
- シンプルな実装
- セルサイズの選択が重要

**初期化:**
```csharp
var spatialHash = new SpatialHashBroadPhase(
    cellSize: 8f,        // セルの辺の長さ
    maxShapes: 10000,
    cellCapacity: 4096   // ハッシュテーブルサイズ
);
var world = new SpatialWorld(spatialHash);
```

**推奨用途:**
- 中規模シーン
- 均一な形状サイズ
- シンプルさ重視

---

## Narrow Phase詳細

### ShapeIntersection

全て static メソッドでゼロアロケーション。結果は out パラメータで返却。

### 対応する判定組み合わせ

| クエリ / Shape | Sphere | Capsule | Cylinder | Box |
|:---------------|:------:|:-------:|:--------:|:---:|
| Point          | ○      | ○       | ○        | ○   |
| Ray            | ○      | ○       | ○        | ○   |
| Sphere         | ○      | ○       | ○        | ○   |
| CapsuleSweep   | ○      | ○       | ○        | ○   |
| Slash          | ○      | ○       | ○        | ○   |

### Point vs Shape

```csharp
// Point vs Sphere: 距離判定
bool PointSphere(in Vector3 point, in SphereData sphere)
{
    return DistanceSquared(point, sphere.Center) <= sphere.Radius * sphere.Radius;
}

// Point vs Capsule: 線分への最近点からの距離
bool PointCapsule(in Vector3 point, in CapsuleData capsule)
{
    var closest = ClosestPointOnSegment(point, capsule.Point1, capsule.Point2);
    return DistanceSquared(point, closest) <= capsule.Radius * capsule.Radius;
}

// Point vs Cylinder: Y軸範囲 + XZ平面距離
bool PointCylinder(in Vector3 point, in CylinderData cylinder)
{
    float localY = point.Y - cylinder.BaseCenter.Y;
    if (localY < 0 || localY > cylinder.Height) return false;

    float dx = point.X - cylinder.BaseCenter.X;
    float dz = point.Z - cylinder.BaseCenter.Z;
    return dx * dx + dz * dz <= cylinder.Radius * cylinder.Radius;
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
    var c = oc.LengthSquared - sphere.Radius * sphere.Radius;
    var discriminant = b * b - c;

    if (discriminant < 0) return false;

    t = -b - Sqrt(discriminant);
    if (t < 0 || t > maxDistance) return false;

    point = origin + direction * t;
    normal = (point - center) / radius;
    return true;
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
    public readonly uint IncludeMask;    // 判定対象レイヤー（デフォルト: 全ビットON）
    public readonly uint ExcludeMask;    // 除外レイヤー（デフォルト: 0）

    public Vector3 End => Origin + Direction * MaxDistance;
    public AABB GetAABB();  // Broad Phase用
    public bool PassesMask(uint shapeMask);  // マスク判定
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

// 敵のみを狙う（プレイヤー・環境を無視）
var enemyRay = new RayQuery(gunPosition, aimDirection, 100f, includeMask: LayerEnemy);
if (world.Raycast(enemyRay, out var hit))
{
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
    public readonly uint IncludeMask;    // 判定対象レイヤー
    public readonly uint ExcludeMask;    // 除外レイヤー

    public AABB GetAABB();
    public bool PassesMask(uint shapeMask);
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
    float damage = baseDamage * (1f - distanceRatio * distanceRatio);
    DealDamage(hits[i].ShapeIndex, damage);
}

// ダメージ可能なターゲットのみ（環境を除外）
var damageQuery = new SphereOverlapQuery(explosionCenter, explosionRadius,
    includeMask: LayerPlayer | LayerEnemy);
```

### CapsuleSweepQuery

```csharp
public readonly struct CapsuleSweepQuery
{
    public readonly Vector3 Start;   // 開始位置
    public readonly Vector3 End;     // 終了位置
    public readonly float Radius;    // カプセル半径
    public readonly uint IncludeMask;    // 判定対象レイヤー
    public readonly uint ExcludeMask;    // 除外レイヤー

    public AABB GetAABB();
    public bool PassesMask(uint shapeMask);
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

// 環境のみと衝突判定（他のキャラクターは通過）
var envSweep = new CapsuleSweepQuery(currentPos, targetPos, characterRadius,
    includeMask: LayerEnvironment);
```

### SlashQuery

```csharp
public readonly struct SlashQuery
{
    public readonly Vector3 StartBase;  // 開始時の剣の根元
    public readonly Vector3 StartTip;   // 開始時の剣の先端
    public readonly Vector3 EndBase;    // 終了時の剣の根元
    public readonly Vector3 EndTip;     // 終了時の剣の先端
    public readonly uint IncludeMask;   // 判定対象レイヤー
    public readonly uint ExcludeMask;   // 除外レイヤー

    public AABB GetAABB();
    public bool PassesMask(uint shapeMask);
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

// 敵のみにダメージ（環境はスルー）
var enemySlash = new SlashQuery(
    prevSwordBase, prevSwordTip,
    currSwordBase, currSwordTip,
    includeMask: LayerEnemy);
```

---

## パフォーマンス設計

### 戦略別ベンチマーク

1000 shapes, 500 queries での測定結果（参考値）:

| パターン | BVH | DBVT | Octree | MBP | GridSAP | SpatialHash |
|----------|-----|------|--------|-----|---------|-------------|
| 均一分布（小オブジェクト） | 0 us | 0 us | 2 us | 0 us | 4 us | 4 us |
| 均一分布（大オブジェクト） | 0 us | 0 us | 4 us | 0 us | 24 us | 44 us |
| 混合サイズ | 16 us | 0 us | 8 us | 0 us | 22 us | 94 us |
| 長いレイ | 2 us | 4 us | 4 us | 2 us | 28 us | 16 us |
| 高頻度更新（5000回） | 1 ms | 39 ms | 4 ms | 3 ms | 40 ms | 4 ms |

### 戦略選択ガイド

```
迷ったら BVH を選択（バランス良好）

用途別推奨:
├── ゲーム全般        → BVH
├── オープンワールド   → BVH or DBVT
├── 物理シミュレーション → DBVT
├── 固定マップ + 動的NPC → MBP
└── 広大で疎なワールド  → Octree

更新頻度で選択:
├── 高頻度更新（毎フレーム多数） → BVH, SpatialHash
├── 中頻度更新 → BVH, Octree, MBP
└── 低頻度更新（静的優位） → DBVT

形状サイズで選択:
├── 均一サイズ → どれでも OK
├── 混合サイズ → BVH, DBVT, MBP
└── 大きいオブジェクトが多い → BVH, DBVT
```

### メモリレイアウト

**ShapeRegistry: SoA（Structure of Arrays）**

```csharp
// AoS（遅い）
struct ShapeAoS {
    ShapeType type;
    SphereData sphere;
    CapsuleData capsule;
    CylinderData cylinder;
    BoxData box;
    AABB aabb;
    int userData;
}
ShapeAoS[] shapes;

// SoA（速い - キャッシュ効率が良い）
ShapeType[] types;
SphereData[] spheres;
CapsuleData[] capsules;
CylinderData[] cylinders;
BoxData[] boxes;
AABB[] aabbs;
int[] userDatas;
```

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

3. **適切な Broad Phase を選択**
   ```csharp
   // オープンワールド → 境界不要の戦略
   var world = new SpatialWorld(new BVHBroadPhase(50000));

   // 固定マップ → リージョン分割
   var world = new SpatialWorld(new MBPBroadPhase(bounds, 8, 8, 10000));
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

    // レイヤー定義
    private const uint LayerEnemy = 0x02;
    private const uint LayerEnvironment = 0x10;
    private const uint LayerProjectile = 0x08;

    public HitResult? ProcessProjectile(Vector3 start, Vector3 end, float radius)
    {
        // カプセルスイープで弾道判定（弾丸同士は判定しない）
        var sweep = new CapsuleSweepQuery(start, end, radius,
            excludeMask: LayerProjectile);

        if (_world.CapsuleSweep(sweep, out var hit))
        {
            // 壁や敵にヒット
            return hit;
        }

        return null;
    }

    public void ProcessHitscan(Vector3 origin, Vector3 direction, float maxDistance)
    {
        // 敵と環境のみを判定対象に
        var ray = new RayQuery(origin, direction, maxDistance,
            includeMask: LayerEnemy | LayerEnvironment);

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

    // レイヤー定義
    private const uint LayerPlayer = 0x01;
    private const uint LayerEnemy = 0x02;
    private const uint LayerDamageable = LayerPlayer | LayerEnemy;

    public void Explode(Vector3 center, float radius, float maxDamage)
    {
        // ダメージを受けるレイヤーのみを対象に（環境は除外）
        var query = new SphereOverlapQuery(center, radius,
            includeMask: LayerDamageable);
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

    // レイヤー定義
    private const uint LayerEnemy = 0x02;

    private Vector3 _prevWeaponBase;
    private Vector3 _prevWeaponTip;

    public void UpdateWeapon(Vector3 weaponBase, Vector3 weaponTip)
    {
        // 前フレームとの軌跡で斬撃判定
        if (_prevWeaponBase != Vector3.Zero)
        {
            // 敵のみを攻撃対象に
            var slash = new SlashQuery(
                _prevWeaponBase, _prevWeaponTip,
                weaponBase, weaponTip,
                includeMask: LayerEnemy);

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

**1. Broad Phase の選択を確認**
```csharp
// 境界が必要な戦略（Octree, MBP）で境界外の形状は検出されない
var bounds = new AABB(...);
var world = new SpatialWorld(new OctreeBroadPhase(bounds, ...));

// → 境界不要の戦略を使用
var world = new SpatialWorld(new BVHBroadPhase(...));
```

**2. AABBが正しいか確認**
```csharp
// クエリのAABBと形状のAABBが重なっているか
var queryAABB = query.GetAABB();
var shapeAABB = world.GetAABB(handle.Index);
bool overlaps = queryAABB.Intersects(shapeAABB);
```

### パフォーマンスが悪い

**1. 適切な戦略を選択**
```csharp
// 大きいオブジェクトが多い → BVH or DBVT
// 均一サイズ → どれでも OK
// 高頻度更新 → BVH, SpatialHash

Console.WriteLine($"Shape count: {world.ShapeCount}");
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
│   │   ├── ShapeData.cs             # SphereData, CapsuleData, CylinderData, BoxData
│   │   ├── ShapeHandle.cs           # ハンドル型
│   │   ├── ShapeRegistry.cs         # 形状データ管理（SoA）
│   │   └── HitResult.cs             # クエリ結果
│   │
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
│   │
│   ├── NarrowPhase/
│   │   └── ShapeIntersection.cs     # 幾何学的交差判定
│   │
│   └── Queries/
│       └── QueryTypes.cs            # RayQuery, SphereOverlapQuery 等
│
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
