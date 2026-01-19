# CollisionSystem

3次元空間上での衝突検出システム。Entity間の衝突判定を効率的に行い、結果をCommandGeneratorに送信する。

## 概要

CollisionSystemは以下の機能を提供する：

- 複数の衝突形状（球、カプセル、ボックス）
- レイヤーベースのフィルタリング
- 空間分割による効率的な広域判定
- CommandGeneratorとの統合

## アーキテクチャ

```
┌─────────────────────────────────────────────────────────┐
│                    CollisionSystem                       │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌─────────────────┐    ┌─────────────────────────┐    │
│  │ CollisionDetector│───▶│     UniformGrid        │    │
│  │    (衝突検出)    │    │    (空間分割)          │    │
│  └────────┬────────┘    └─────────────────────────┘    │
│           │                                              │
│           ▼                                              │
│  ┌─────────────────┐                                    │
│  │CollisionMessage │───▶ CommandGenerator                  │
│  │    Emitter      │    (ダメージメッセージ送信)        │
│  └─────────────────┘                                    │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

## コンポーネント

### 数学型

- `Vector3` - 3次元ベクトル
- `AABB` - 軸平行境界ボックス

### 衝突形状

- `SphereShape` - 球形状
- `CapsuleShape` - カプセル形状
- `BoxShape` - ボックス形状

### フィルタリング

- `CollisionFilter` - レイヤーベースのフィルタ
- `CollisionLayers` - 定義済みレイヤー定数

### 空間分割

- `ISpatialPartition` - 空間分割インターフェース
- `UniformGrid` - グリッドベースの空間分割

### 検出器

- `CollisionDetector` - 衝突検出の中心クラス
- `CollisionVolume` - 衝突判定ボリューム
- `CollisionResult` - 衝突結果

### CommandGenerator統合

- `ICollisionMessageEmitter` - 衝突結果をメッセージに変換するインターフェース
- `CallbackCollisionMessageEmitter` - コールバックベースの実装
- `CollisionInfo` - 衝突情報（Target, Source, Amount, Contact, Hitbox, Hurtbox）

## 使用例

### 基本的な衝突検出

```csharp
// ボリュームタイプ・レイヤーはゲーム側で定義
const int Hitbox = 0;
const int Hurtbox = 1;
const int PlayerLayer = 1;
const int EnemyLayer = 2;

// 検出器を作成
var detector = new CollisionDetector();

// プレイヤーのヒットボックス
var playerHurtbox = new CollisionVolume(
    owner: playerId,
    shape: new SphereShape(1.0f),
    filter: new CollisionFilter(layer: PlayerLayer, mask: EnemyLayer),
    volumeType: Hurtbox);

// 敵の攻撃判定
var enemyHitbox = new CollisionVolume(
    owner: enemyId,
    shape: new SphereShape(0.5f),
    filter: new CollisionFilter(layer: EnemyLayer, mask: PlayerLayer),
    volumeType: Hitbox,
    lifetime: 5);  // 5フレームで消える

// ボリュームを追加
detector.AddVolume(playerHurtbox, playerPosition);
detector.AddVolume(enemyHitbox, attackPosition);

// 衝突を検出
var results = new List<CollisionResult>();
detector.DetectCollisions(results);

// 結果を処理
foreach (var result in results)
{
    Console.WriteLine($"Collision: {result.Volume1.Owner} vs {result.Volume2.Owner}");
}
```

### CommandGeneratorとの統合

```csharp
// ゲーム固有のコマンドを定義
[Command<GameCommandQueue>(Priority = 50)]
public partial class CollisionCommand
{
    public VoidHandle Target;
    public VoidHandle Source;

    public void Execute()
    {
        // 衝突処理ロジック
    }
}

// 衝突結果をコマンドに変換
foreach (var result in results)
{
    queue.Enqueue<CollisionCommand>(cmd =>
    {
        cmd.Target = result.Volume1.Owner;
        cmd.Source = result.Volume2.Owner;
    });
}
```

### レイヤーの使用

```csharp
// レイヤー・マスクはゲーム側で定義
const int PlayerLayer = 1 << 0;
const int EnemyLayer = 1 << 1;
const int TriggerLayer = 1 << 5;

// プレイヤーとトリガーの衝突のみ検出
var customFilter = new CollisionFilter(
    layer: PlayerLayer,
    mask: TriggerLayer);
```

## レイヤー定義の推奨例

レイヤーはゲーム側で`int`定数として定義します：

| 推奨値 | 用途例 |
|--------|--------|
| 1 << 0 | プレイヤーキャラクター |
| 1 << 1 | 敵キャラクター |
| 1 << 2 | プレイヤーの攻撃 |
| 1 << 3 | 敵の攻撃 |
| 1 << 4 | 環境（壁、床など） |
| 1 << 5 | トリガー領域 |

## テスト

```bash
dotnet test libs/CollisionSystem/CollisionSystem.Tests/
```

現在のテスト数: 74

## 依存関係

- CommandGenerator.Core - メッセージ送信のため

## ライセンス

MIT License
