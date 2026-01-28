# EntityHandleSystem - エンティティハンドルシステム

C# Source Generatorを使用した、安全で効率的なエンティティ管理システムです。世代番号による無効化検出機能と、ECSスタイルのコンポーネントシステムを備えています。

## ドキュメント

- **[テストコード](./EntityHandleSystem.Tests/)** - 実践的なサンプル

## 概要

EntityHandleSystemは、ゲームエンジンでよく使われる「エンティティ-ハンドル」パターンを自動生成します。削除されたエンティティへのアクセスを防ぎ、安全な参照管理を実現します。

### なぜEntityHandleSystemを使うのか？

- **安全性** - 削除済みエンティティへのアクセスを自動検出
- **効率的** - Structure of Arrays (SoA) によるキャッシュ効率
- **型安全** - コンパイル時の型チェック
- **スレッドセーフ** - マルチスレッド環境でも安全
- **ECSスタイル** - コンポーネントベースの設計に対応

## 特徴

- **世代番号による無効化検出** - 削除されたハンドルを自動で検出
- **自動Arena生成** - エンティティプールを自動管理
- **型安全なハンドル** - 間違った型のアクセスを防止
- **オブジェクトプーリング** - メモリ効率の向上
- **スレッドセーフ** - ロックによる安全な並行アクセス
- **コンポーネントシステム** - SoA パターンでコンポーネントを管理
- **AnyHandle** - 型消去されたハンドルで横串操作が可能
- **エンティティグループ** - 派生属性でエンティティをグループ化し、グループ固有のAnyHandleで横串操作
- **グループコンテナ** - Arena順・Index順でソートされたコンテナで、キャッシュ効率を最大化しコンポーネントベースのクエリをサポート

## クイックスタート

### 1. エンティティの定義

```csharp
using Tomato.EntityHandleSystem;

[Entity(InitialCapacity = 100)]
public partial class Enemy
{
    public int Health;
    public Vector3 Position;
    public bool IsAlive;

    // エンティティメソッド（ハンドル経由で呼び出し可能）
    [EntityMethod]
    public void TakeDamage(int damage)
    {
        Health -= damage;
        if (Health <= 0)
        {
            IsAlive = false;
        }
    }

    [EntityMethod]
    public void MoveTo(Vector3 newPos)
    {
        Position = newPos;
    }

    [EntityMethod]
    public int GetHealth()
    {
        return Health;
    }
}
```

### 2. エンティティの使用

```csharp
// Arenaを作成（自動生成される）
var arena = new EnemyArena(
    onSpawn: (ref Enemy enemy) => {
        enemy.Health = 100;
        enemy.IsAlive = true;
    },
    onDespawn: (ref Enemy enemy) => {
        enemy.IsAlive = false;
    }
);

// エンティティを作成
EnemyHandle handle1 = arena.Create();
EnemyHandle handle2 = arena.Create();

// ハンドル経由で安全にメソッド呼び出し
if (handle1.TryTakeDamage(30))
{
    Console.WriteLine("ダメージを与えました");
}

// 値を取得
if (handle1.TryGetHealth(out int health))
{
    Console.WriteLine($"残りHP: {health}");
}

// エンティティを削除
handle1.Dispose();

// 削除後のアクセスは安全に失敗する
if (!handle1.TryTakeDamage(10))
{
    Console.WriteLine("無効なハンドルです");  // ここが実行される
}
```

## 詳細ガイド

### ハンドルの仕組み

```
┌─────────────┐
│   Handle    │  インデックス: 5
│             │  世代番号: 3
└─────────────┘
       │
       ↓
┌─────────────┐
│   Arena     │
│ スロット 5  │  世代番号: 3  ✅ 一致 → 有効
└─────────────┘

# エンティティ削除後
┌─────────────┐
│   Handle    │  インデックス: 5
│             │  世代番号: 3
└─────────────┘
       │
       ↓
┌─────────────┐
│   Arena     │
│ スロット 5  │  世代番号: 4  ❌ 不一致 → 無効
└─────────────┘
```

### EntityMethodの使用

```csharp
[Entity]
public partial class Player
{
    public string Name;
    public int Level;

    // 通常のメソッド
    [EntityMethod]
    public void LevelUp()
    {
        Level++;
    }

    // 戻り値ありのメソッド
    [EntityMethod]
    public string GetInfo()
    {
        return $"{Name} Lv.{Level}";
    }

    // パフォーマンス重視の場合
    [EntityMethod(Unsafe = true)]
    public void FastUpdate(float deltaTime)
    {
        // 高速だが検証なし
    }
}

// 使用例
PlayerHandle handle = arena.Create();

// 安全版（世代番号チェックあり）
if (handle.TryLevelUp())
{
    Console.WriteLine("レベルアップ！");
}

// 高速版（検証なし、ハンドルの有効性は呼び出し側が保証）
handle.FastUpdate_Unsafe(0.016f);
```

### コールバックの活用

```csharp
var arena = new EnemyArena(
    onSpawn: (ref Enemy enemy) =>
    {
        // エンティティ作成時の初期化
        enemy.Health = 100;
        enemy.Position = Vector3.Zero;
        enemy.IsAlive = true;
        Console.WriteLine("Enemy spawned!");
    },
    onDespawn: (ref Enemy enemy) =>
    {
        // エンティティ削除時のクリーンアップ
        enemy.IsAlive = false;
        Console.WriteLine("Enemy despawned!");
    }
);
```

## エンティティグループ（派生属性）

`[Entity]` 属性を継承した派生属性を定義することで、エンティティをグループ化できます。同グループ内のエンティティは、グループ固有の `AnyHandle` を通じて横串操作が可能です。

### 派生属性の定義

```csharp
using Tomato.EntityHandleSystem;

// [Entity] を継承した派生属性を定義
public class PlayerEntityAttribute : EntityAttribute { }
public class ProjectileEntityAttribute : EntityAttribute { }
```

### グループに属するエンティティの定義

```csharp
// 派生属性を使用してエンティティを定義
[PlayerEntity]
public partial class IngamePlayer
{
    public int Health;
    public float Speed;
}

[PlayerEntity]
public partial class OutgamePlayer
{
    public string DisplayName;
}

[PlayerEntity]
public partial class ReplicaPlayer
{
    public int Health;
    public bool IsLocal;
}

[ProjectileEntity]
public partial class Arrow
{
    public float Damage;
    public float Speed;
}

[ProjectileEntity]
public partial class Bullet
{
    public float Damage;
    public int PenetrationCount;
}
```

### 生成される型

各エンティティには従来通り独自の Handle と Arena が生成されます：
- `IngamePlayerHandle`, `IngamePlayerArena`
- `OutgamePlayerHandle`, `OutgamePlayerArena`
- `ReplicaPlayerHandle`, `ReplicaPlayerArena`
- `ArrowHandle`, `ArrowArena`
- `BulletHandle`, `BulletArena`

さらに、グループごとに以下の型が生成されます：
- `IPlayerEntityArena` - グループのマーカーインターフェース
- `PlayerEntityAnyHandle` - グループ固有の型消去ハンドル
- `IProjectileEntityArena`
- `ProjectileEntityAnyHandle`

### グループコンテナの使用

グループ固有のソート済みコンテナ `{GroupName}Container` を使用すると、Arena 順・Index 順でハンドルがソートされ、キャッシュ効率が最大化されます。

```csharp
var ingameArena = new IngamePlayerArena();
var outgameArena = new OutgamePlayerArena();
var replicaArena = new ReplicaPlayerArena();

var ingameHandle1 = ingameArena.Create();
var ingameHandle2 = ingameArena.Create();
var outgameHandle = outgameArena.Create();
var replicaHandle = replicaArena.Create();

// グループ専用コンテナ（自動生成される）
var players = new PlayerEntityContainer();

// ハンドルを追加（内部で Arena 順・Index 順にソートされる）
players.Add(ingameHandle1.ToPlayerEntityAnyHandle());
players.Add(outgameHandle.ToPlayerEntityAnyHandle());
players.Add(ingameHandle2.ToPlayerEntityAnyHandle());  // 同じ Arena のハンドルは連続配置
players.Add(replicaHandle.ToPlayerEntityAnyHandle());

// 全ての PlayerEntity をイテレート（キャッシュ効率が良い）
foreach (var player in players)
{
    player.TryExecute<HealthComponent>((ref HealthComponent h) => h.Hp -= 10);
}

// コンポーネントベースのクエリ
// PositionComponent を持つエンティティのみをイテレート
foreach (var player in players.Query<PositionComponent>())
{
    player.TryExecute<PositionComponent>((ref PositionComponent p) => p.Y -= 9.8f);
}

// 複数コンポーネント条件
// HealthComponent と PositionComponent の両方を持つエンティティのみ
foreach (var player in players.Query<HealthComponent, PositionComponent>())
{
    // 両コンポーネントを持つエンティティの処理
}

// フレーム分散更新（skip=1 で2フレームに分散）
var iterator = players.GetIterator(skip: 1, offset: frameCount % 2);
while (iterator.MoveNext())
{
    iterator.Current.TryExecute<HealthComponent>((ref HealthComponent h) => h.Hp += 1);
}

// ハンドルの削除
players.Remove(outgameHandle.ToPlayerEntityAnyHandle());

// グローバル AnyHandle も引き続き利用可能（全エンティティ横断）
AnyHandle any = ingameHandle1.ToAnyHandle();
```

### グループコンテナのパフォーマンス特性

| 操作 | 計算量 | Boxing | GC |
|------|--------|--------|-----|
| Add | O(log n) | なし | なし |
| Remove | O(log n) | なし | なし |
| foreach MoveNext | O(1) | なし | なし |
| Query<T> 構築 | O(k) | なし | bool[] 1回 |
| Query MoveNext | O(1) | なし | なし |

※ k = Arena 数（通常は小さい）

**キャッシュ効率**: 同一 Arena のハンドルが連続配置されるため、L1/L2 キャッシュヒット率が向上します。

```
従来の配置: [A-0][B-0][A-1][B-1]...  ← ランダム

Container:
  Segment[A]: [A-0][A-1][A-2]...  ← Arena A が連続
  Segment[B]: [B-0][B-1][B-2]...  ← Arena B が連続
```

### グループと直接属性の混在

`[Entity]` を直接使用したエンティティと、派生属性を使用したエンティティを同じプロジェクトで混在させることができます。

```csharp
// 直接 [Entity] を使用 - グループなし
[Entity]
public partial class SimpleEntity
{
    public int Value;
}

// 派生属性を使用 - PlayerEntity グループに所属
[PlayerEntity]
public partial class IngamePlayer
{
    public int Health;
}

// SimpleEntity は ToAnyHandle() のみ
// IngamePlayer は ToAnyHandle() と ToPlayerEntityAnyHandle() の両方を持つ
```

### 派生属性でもパラメータは使用可能

```csharp
[PlayerEntity(InitialCapacity = 512, ArenaName = "PlayerPool")]
public partial class IngamePlayer
{
    public int Health;
}

// PlayerPool クラスが生成され、IPlayerEntityArena を実装する
```

## コンポーネントシステム（ECSスタイル）

より高度なユースケース向けに、ECSスタイルのコンポーネントシステムを提供しています。Structure of Arrays (SoA) パターンでコンポーネントを管理し、キャッシュ効率とクロスエンティティ操作を実現します。

### コンポーネントの定義

```csharp
// コンポーネントは struct で定義
public struct PositionComponent
{
    public float X;
    public float Y;
    public float Z;

    [EntityMethod]
    public void SetPosition(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [EntityMethod]
    public float GetDistanceSquared()
    {
        return X * X + Y * Y + Z * Z;
    }
}

public struct VelocityComponent
{
    public float VX;
    public float VY;
    public float VZ;

    [EntityMethod]
    public void SetVelocity(float vx, float vy, float vz)
    {
        VX = vx;
        VY = vy;
        VZ = vz;
    }

    // 他のコンポーネントへの参照（同一Entity内で自動取得）
    [EntityMethod]
    public void ApplyToPosition(ref PositionComponent pos, float deltaTime)
    {
        pos.X += VX * deltaTime;
        pos.Y += VY * deltaTime;
        pos.Z += VZ * deltaTime;
    }
}
```

### エンティティへのコンポーネント追加

```csharp
// [EntityComponent] で複数のコンポーネントを追加
[Entity]
[EntityComponent(typeof(PositionComponent))]
[EntityComponent(typeof(VelocityComponent))]
public partial class MovableEntity
{
    public int EntityId;

    [EntityMethod]
    public void SetId(int id)
    {
        EntityId = id;
    }
}

// 派生属性と組み合わせることも可能
[PlayerEntity]
[EntityComponent(typeof(PositionComponent))]
[EntityComponent(typeof(HealthComponent))]
public partial class IngamePlayer
{
    public string PlayerName;
}
```

### コンポーネントメソッドの呼び出し

```csharp
var arena = new MovableEntityArena();
var handle = arena.Create();

// コンポーネントメソッドは {ComponentName}_Try{MethodName} パターンで生成
handle.PositionComponent_TrySetPosition(1.0f, 2.0f, 3.0f);
handle.VelocityComponent_TrySetVelocity(10.0f, 0.0f, 0.0f);

// 戻り値がある場合は out パラメータ
if (handle.PositionComponent_TryGetDistanceSquared(out float distance))
{
    Console.WriteLine($"距離の2乗: {distance}");
}

// コンポーネント間参照（同一Entity内で自動取得）
handle.VelocityComponent_TryApplyToPosition(1.0f);  // deltaTime = 1.0
```

### AnyHandle による横串操作

異なるエンティティ型に対して、共通のコンポーネントを操作できます。

```csharp
var movableArena = new MovableEntityArena();
var staticArena = new StaticEntityArena();

var movableHandle = movableArena.Create();
var staticHandle = staticArena.Create();

// グローバル AnyHandle に変換
AnyHandle[] handles = new[]
{
    movableHandle.ToAnyHandle(),
    staticHandle.ToAnyHandle()
};

// 異なるエンティティ型でも PositionComponent があれば処理可能
float totalDistance = 0;
foreach (var vh in handles)
{
    vh.TryExecute<PositionComponent>((ref PositionComponent pos) =>
    {
        totalDistance += pos.X + pos.Y + pos.Z;
    });
}
```

### グループ AnyHandle による横串操作

グループ固有の AnyHandle を使えば、特定グループ内のエンティティのみを対象にできます。
グループ専用コンテナ `{GroupName}Container` を使うと、キャッシュ効率とコンポーネントベースのクエリが利用できます。

```csharp
var ingameArena = new IngamePlayerArena();
var outgameArena = new OutgamePlayerArena();

var ingameHandle = ingameArena.Create();
var outgameHandle = outgameArena.Create();

// グループ専用コンテナを使う方法（推奨）
var players = new PlayerEntityContainer();
players.Add(ingameHandle.ToPlayerEntityAnyHandle());
players.Add(outgameHandle.ToPlayerEntityAnyHandle());

// 全プレイヤーをイテレート
foreach (var player in players)
{
    player.TryExecute<HealthComponent>((ref HealthComponent health) =>
    {
        health.Hp = 100;  // 全プレイヤーのHPを回復
    });
}

// PositionComponent を持つプレイヤーのみ
foreach (var player in players.Query<PositionComponent>())
{
    player.TryExecute<PositionComponent>((ref PositionComponent pos) =>
    {
        pos.Y -= 9.8f;
    });
}

// 配列を直接使うこともできる
PlayerEntityAnyHandle[] playerArray = new[]
{
    ingameHandle.ToPlayerEntityAnyHandle(),
    outgameHandle.ToPlayerEntityAnyHandle()
};

// グローバル AnyHandle に変換することも可能
AnyHandle any = playerArray[0].ToAnyHandle();
```

### TypedHandle.TryExecute

型付きハンドルでもラムダ経由でコンポーネントにアクセス可能です。

```csharp
var handle = arena.Create();
handle.PositionComponent_TrySetPosition(5.0f, 5.0f, 5.0f);

// ラムダでコンポーネントを直接操作
handle.TryExecute<PositionComponent>((ref PositionComponent pos) =>
{
    pos.X *= 2;  // X座標を2倍に
});
```

### パフォーマンス特性

| 操作 | Boxing | 計算量 |
|------|--------|--------|
| コンポーネントアクセス | なし | O(1) |
| AnyHandle.TryExecute | なし | O(1) |
| GroupAnyHandle.TryExecute | なし | O(1) |
| TypedHandle.TryExecute | なし | O(1) |

- **Boxing なし**: Arena はクラス、コンポーネントは ref で返される
- **O(1) アクセス**: 配列インデックスによる直接アクセス
- **SoA パターン**: コンポーネントごとに配列を持ち、キャッシュ効率を向上

## CommandQueue連携

`[HasCommandQueue]`属性を使用して、エンティティにCommandQueueを追加できます。各Entityが独自のキューを持ち、Step処理による決定論的なメッセージ処理を実現します。

### CommandQueueの定義

```csharp
using Tomato.CommandGenerator;

// CommandQueue定義（Source GeneratorがSystemを自動生成）
[CommandQueue]
public partial class GameCommandQueue
{
    [CommandMethod]
    public partial void ExecuteCommand(AnyHandle handle);
}

// Commandの定義
[Command<GameCommandQueue>(Priority = 50)]
public partial class DamageCommand
{
    public int Amount;

    public void ExecuteCommand(AnyHandle handle)
    {
        // ダメージ処理
    }
}
```

### エンティティへのキュー追加

```csharp
using Tomato.EntityHandleSystem;
using Tomato.CommandGenerator;

[Entity]
[HasCommandQueue(typeof(GameCommandQueue))]
[HasCommandQueue(typeof(EventQueue))]  // 複数のキューを追加可能
public partial class Player
{
    public int Health;
    public float Speed;
}
```

### 生成されるコード

Arena側:
- `GameCommandQueue[] _gamecommandqueueQueues` - Entity単位のキュー配列
- `Create()`時に各Entityのキューを自動初期化

Handle側:
- `GameCommandQueue GameCommandQueue` - プロパティベースのキューアクセス

### コマンドの送信と処理

```csharp
var arena = new PlayerArena();
PlayerHandle handle = arena.Create();

// プロパティでキューにアクセス（各Entityが独自のキューを持つ）
handle.GameCommandQueue.Enqueue<DamageCommand>(cmd => {
    cmd.Amount = 50;
});

// 別のEntityは別のキューを持つ
var handle2 = arena.Create();
handle2.GameCommandQueue.Enqueue<DamageCommand>(cmd => {
    cmd.Amount = 30;
});

// SystemPipelineでStep処理
// GameCommandQueueSystemはSource Generatorが自動生成
var messageSystem = new GameCommandQueueSystem(handlerRegistry);
pipeline.Execute(systemGroup, deltaTime);
```

### Entity単位のキュー管理

`[HasCommandQueue]`を使うと、各Entityインスタンスが独自のCommandQueueを持ちます。これにより：
- Entity間でキューが独立（他のEntityのコマンドに影響されない）
- 複数のCommandQueueを1つのEntityに追加可能
- プロパティアクセスで簡潔なコード

```csharp
// 複数のCommandQueueを持つEntity
[Entity]
[HasCommandQueue(typeof(GameCommandQueue))]
[HasCommandQueue(typeof(AICommandQueue))]
public partial class NPC
{
    public int Health;
}

// 使用例
var handle = arena.Create();
handle.GameCommandQueue.Enqueue<DamageCommand>(...);
handle.AICommandQueue.Enqueue<ThinkCommand>(...);
```

## ベストプラクティス

### 推奨

```csharp
// ✅ TryMethodで安全に呼び出し
if (handle.TryTakeDamage(20))
{
    Console.WriteLine("成功");
}

// ✅ 適切な初期容量を設定
[Entity(InitialCapacity = 256)]  // 予想される数に合わせる

// ✅ グループを活用して関連エンティティを整理
public class EnemyEntityAttribute : EntityAttribute { }

[EnemyEntity]
public partial class Zombie { }

[EnemyEntity]
public partial class Skeleton { }

// ✅ グループコンテナで同種のエンティティを管理（キャッシュ効率が良い）
var enemies = new EnemyEntityContainer();
enemies.Add(zombie.ToEnemyEntityAnyHandle());
enemies.Add(skeleton.ToEnemyEntityAnyHandle());

foreach (var enemy in enemies)
{
    enemy.TryExecute<HealthComponent>((ref HealthComponent h) => h.Hp -= poisonDamage);
}

// ✅ Query でコンポーネントベースのフィルタリング
foreach (var enemy in enemies.Query<PositionComponent>())
{
    enemy.TryExecute<PositionComponent>((ref PositionComponent p) => p.Y -= gravity);
}
```

### 避けるべき

```csharp
// ❌ _Unsafeメソッドの乱用
handle.Update_Unsafe(data);  // 本当に必要な場合のみ

// ❌ ハンドルの保存後、削除を忘れる
var handle = arena.Create();
// ... 使用後 Dispose() を忘れるとメモリリーク
```

## 応用例

### ゲームループでの使用

```csharp
// エンティティリストを管理
List<EnemyHandle> activeEnemies = new List<EnemyHandle>();

void Update(float deltaTime)
{
    // すべてのエンティティを更新（逆順で削除しても安全）
    for (int i = activeEnemies.Count - 1; i >= 0; i--)
    {
        var handle = activeEnemies[i];

        // エンティティが有効かチェック
        if (!handle.IsValid)
        {
            activeEnemies.RemoveAt(i);
            continue;
        }

        // 更新処理
        handle.TryUpdate(deltaTime);
    }
}

void SpawnEnemy()
{
    var handle = arena.Create();
    activeEnemies.Add(handle);
}

void KillEnemy(EnemyHandle handle)
{
    handle.Dispose();  // 自動的にリストから削除される（次のUpdateで）
}
```

### グループを活用したシステム設計

```csharp
// 敵エンティティグループ
public class EnemyEntityAttribute : EntityAttribute { }

[EnemyEntity]
[EntityComponent(typeof(PositionComponent))]
[EntityComponent(typeof(HealthComponent))]
public partial class Zombie { }

[EnemyEntity]
[EntityComponent(typeof(HealthComponent))]  // PositionComponent なし
public partial class Ghost { }

[EnemyEntity]
[EntityComponent(typeof(PositionComponent))]
[EntityComponent(typeof(HealthComponent))]
public partial class Skeleton { }

// 敵システム - 全ての敵タイプを一括処理
class EnemySystem
{
    // グループ専用コンテナ（Arena 順・Index 順でソート済み）
    private EnemyEntityContainer _enemies = new();

    public void AddEnemy(EnemyEntityAnyHandle enemy)
    {
        _enemies.Add(enemy);
    }

    public void RemoveEnemy(EnemyEntityAnyHandle enemy)
    {
        _enemies.Remove(enemy);
    }

    public void UpdateAll(float deltaTime)
    {
        // 全ての敵をイテレート（キャッシュ効率が良い）
        foreach (var enemy in _enemies)
        {
            enemy.TryExecute<HealthComponent>((ref HealthComponent h) =>
            {
                if (h.Hp <= 0)
                {
                    // 死亡処理...
                }
            });
        }
    }

    public void ApplyGravity(float deltaTime)
    {
        // PositionComponent を持つ敵のみに重力を適用
        // Ghost は PositionComponent を持たないのでスキップされる
        foreach (var enemy in _enemies.Query<PositionComponent>())
        {
            enemy.TryExecute<PositionComponent>((ref PositionComponent p) =>
            {
                p.Y -= 9.8f * deltaTime;
            });
        }
    }

    public void ApplyPoison(int damage)
    {
        // HealthComponent と PositionComponent の両方を持つ敵のみ
        foreach (var enemy in _enemies.Query<HealthComponent, PositionComponent>())
        {
            enemy.TryExecute<HealthComponent>((ref HealthComponent h) =>
            {
                h.Hp -= damage;
            });
        }
    }
}
```

## API リファレンス

### Entity属性

| プロパティ | 説明 | デフォルト |
|-----------|------|-----------|
| `InitialCapacity` | プールの初期容量 | 256 |
| `ArenaName` | 生成されるArenaクラスの名前 | "{TypeName}Arena" |

`[Entity]` を継承した派生属性（例: `PlayerEntityAttribute`）を定義してエンティティに適用すると、そのエンティティはグループに所属し、グループ固有の型が生成されます。

### EntityMethod属性

| プロパティ | 説明 | デフォルト |
|-----------|------|-----------|
| `Unsafe` | 高速だが検証なしのメソッドも生成 | false |

### EntityComponent属性

| プロパティ | 説明 |
|-----------|------|
| `ComponentType` | コンポーネントの型（typeof で指定） |

### HasCommandQueue属性

エンティティにCommandQueueを追加します。各Entityインスタンスが独自のキューを持ちます。

| プロパティ | 説明 |
|-----------|------|
| `QueueType` | CommandQueueの型（typeof で指定） |

### 生成されるメソッド（基本）

| メソッド | 説明 |
|---------|------|
| `Arena.Create()` | エンティティを作成してハンドルを返す |
| `Handle.IsValid` | ハンドルが有効かチェック |
| `Handle.Try{Method}(...)` | エンティティメソッドを安全に呼び出し |
| `Handle.{Method}_Unsafe(...)` | 検証なしでエンティティメソッドを呼び出し |
| `Handle.Dispose()` | エンティティを削除 |
| `Handle.ToAnyHandle()` | グローバルな型消去ハンドルに変換 |

### 生成されるメソッド（グループ）

派生属性（例: `[PlayerEntity]`）を使用した場合に生成されます。

| メソッド/型 | 説明 |
|------------|------|
| `I{GroupName}Arena` | グループのマーカーインターフェース |
| `{GroupName}AnyHandle` | グループ固有の型消去ハンドル |
| `{GroupName}Container` | グループ専用のソート済みコンテナ |
| `Handle.To{GroupName}AnyHandle()` | グループ固有のAnyHandleに変換 |

`{GroupName}AnyHandle` は以下を持ちます：
- `IsValid` - ハンドルの有効性チェック
- `Index` / `Generation` - ハンドルの内部情報
- `ToAnyHandle()` - グローバル AnyHandle への変換
- `TryAs<TArena>(out TArena arena)` - Arena の型チェック
- `TryExecute<TComponent>(RefAction<TComponent>)` - コンポーネント操作

### 生成されるメソッド（グループコンテナ）

`{GroupName}Container` は以下を持ちます：

| メソッド/プロパティ | 説明 |
|-------------------|------|
| `Count` | コンテナ内の有効なハンドル数 |
| `Add(handle)` | ハンドルを追加（Arena 順・Index 順でソート） |
| `Remove(handle)` | ハンドルを削除（遅延削除） |
| `Clear()` | コンテナをクリア |
| `Compact()` | 無効なスロットを除去してコンパクト化 |
| `GetEnumerator()` | foreach 対応の列挙子を取得 |
| `GetIterator(skip, offset)` | フレーム分散更新用のイテレータを取得 |
| `Query<T>()` | コンポーネント T を持つエンティティのみをフィルタリング |
| `Query<T1, T2>()` | T1 と T2 両方を持つエンティティのみをフィルタリング |
| `Query<T1, T2, T3>()` | 3コンポーネント条件でフィルタリング |

### 生成されるメソッド（コンポーネント）

| メソッド | 説明 |
|---------|------|
| `Handle.{Component}_Try{Method}(...)` | コンポーネントメソッドを安全に呼び出し |
| `Handle.TryExecute<T>(RefAction<T>)` | ラムダでコンポーネントを操作 |
| `AnyHandle.TryExecute<T>(RefAction<T>)` | 型消去ハンドルでコンポーネントを操作 |
| `{GroupName}AnyHandle.TryExecute<T>(RefAction<T>)` | グループハンドルでコンポーネントを操作 |

### 生成されるプロパティ（CommandQueue）

| プロパティ | 説明 |
|-----------|------|
| `Handle.{QueueName}` | キューへのプロパティアクセス（例: `handle.GameCommandQueue`） |

### 生成されるもの（CommandQueue）

| 項目 | 説明 |
|-----|------|
| `Arena._{queuename}Queues` | Entity単位のキュー配列 |
| `Arena.GetCommandQueue_{QueueName}(index)` | インデックスでキューを取得（内部用） |
| `Handle.{QueueName}` | 対応するキューをプロパティで取得 |

## さらに詳しく

- **[テストコード](./EntityHandleSystem.Tests/)** - 実践的なサンプル

## ライセンス

MIT License
