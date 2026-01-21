# SpatialIndexSystem

空間ハッシュグリッドによる空間インデックスシステム。効率的な範囲検索・最近傍検索を提供する。

## 構造

```
SpatialIndexSystem/
├── SpatialIndexSystem.Core/
│   ├── ISpatialIndex.cs      # インターフェース
│   ├── SpatialEntry.cs       # エントリ構造体
│   ├── SpatialHashGrid.cs    # 空間ハッシュグリッド実装
│   └── SpatialQuery.cs       # クエリヘルパー
└── SpatialIndexSystem.Tests/
```

## 使用例

### 基本的な使用法

```csharp
// グリッド作成（セルサイズ10）
var grid = new SpatialHashGrid(cellSize: 10f);

// Entityの追加・更新
grid.Update(entityHandle, position);
grid.Update(entityHandle, position, radius: 2f);  // 半径付き

// Entityの削除
grid.Remove(entityHandle);
```

### 球範囲検索

```csharp
var results = new List<AnyHandle>();
grid.QuerySphere(center: playerPosition, radius: 50f, results);

foreach (var handle in results)
{
    // 範囲内のEntity処理
}
```

### AABB範囲検索

```csharp
var bounds = new AABB(
    new Vector3(-10, -10, -10),
    new Vector3(10, 10, 10)
);

var results = new List<AnyHandle>();
grid.QueryAABB(bounds, results);
```

### 最近傍検索

```csharp
if (grid.QueryNearest(point, maxDistance: 100f, out var nearest, out var distance))
{
    // nearestが最も近いEntity
    // distanceが距離
}
```

### 位置更新パターン

```csharp
// ゲームループでの使用例
public void UpdateSpatialIndex()
{
    foreach (var entity in activeEntities)
    {
        if (entity.PositionChanged)
        {
            grid.Update(entity.Handle, entity.Position, entity.Radius);
        }
    }

    // 削除されたEntityの除去
    foreach (var removed in removedEntities)
    {
        grid.Remove(removed.Handle);
    }
}
```

## SpatialQueryヘルパー

静的ヘルパーメソッド:

```csharp
// リストを返す簡易API
var results = SpatialQuery.QuerySphere(grid, center, radius);
var results = SpatialQuery.QueryAABB(grid, bounds);

// 最近傍N件（位置取得関数付き）
var nearest = SpatialQuery.QueryNearestN(
    grid,
    point,
    count: 5,
    positionGetter: handle => GetPosition(handle),
    maxDistance: 100f);
```

## セルサイズの選択

セルサイズは検索半径に応じて選択:

| 検索半径 | 推奨セルサイズ |
|---------|--------------|
| 〜10    | 10           |
| 〜50    | 25           |
| 〜100   | 50           |

- 小さすぎる: セル数が増え、メモリ使用量が増加
- 大きすぎる: 各セルのEntity数が増え、検索効率が低下

## パフォーマンス

| 操作 | 計算量 |
|-----|-------|
| Update | O(1) |
| Remove | O(1) |
| QuerySphere | O(セル数 × セル内Entity数) |
| QueryAABB | O(セル数 × セル内Entity数) |
| QueryNearest | O(QuerySphere + ソート) |

## 依存関係

- EntityHandleSystem.Attributes（AnyHandle）
- CollisionSystem.Core（Vector3, AABB）

## テスト

```bash
dotnet test libs/SpatialIndexSystem/SpatialIndexSystem.Tests/
```

## ライセンス

MIT License
