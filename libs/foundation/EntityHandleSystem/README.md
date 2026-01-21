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

## クイックスタート

### 1. エンティティの定義

```csharp
using EntityHandleSystem;

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
    onSpawn: enemy => {
        enemy.Health = 100;
        enemy.IsAlive = true;
    },
    onDespawn: enemy => {
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
handle.TryFastUpdate_Unsafe(0.016f);
```

### コールバックの活用

```csharp
var arena = new EnemyArena(
    onSpawn: enemy =>
    {
        // エンティティ作成時の初期化
        enemy.Health = 100;
        enemy.Position = Vector3.Zero;
        enemy.IsAlive = true;
        Console.WriteLine("Enemy spawned!");
    },
    onDespawn: enemy =>
    {
        // エンティティ削除時のクリーンアップ
        enemy.IsAlive = false;
        Console.WriteLine("Enemy despawned!");
    }
);
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

// AnyHandle に変換
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
| TypedHandle.TryExecute | なし | O(1) |

- **Boxing なし**: Arena はクラス、コンポーネントは ref で返される
- **O(1) アクセス**: 配列インデックスによる直接アクセス
- **SoA パターン**: コンポーネントごとに配列を持ち、キャッシュ効率を向上

## CommandQueue連携

`[HasCommandQueue]`属性を使用して、エンティティにCommandQueueを追加できます。各Entityが独自のキューを持ち、Wave処理による決定論的なメッセージ処理を実現します。

### CommandQueueの定義

```csharp
using CommandGenerator;

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
using EntityHandleSystem;
using CommandGenerator;

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

// SystemPipelineでWave処理
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
// ✅ TryGetで安全にアクセス
if (handle.TryGet(out var enemy))
{
    enemy.Health -= 10;
}

// ✅ TryMethodで安全に呼び出し
if (handle.TryTakeDamage(20))
{
    Console.WriteLine("成功");
}

// ✅ 適切な初期容量を設定
[Entity(InitialCapacity = 256)]  // 予想される数に合わせる
```

### 避けるべき

```csharp
// ❌ TryGetを使わずに直接アクセス
var enemy = arena.GetUnsafe(handle);  // 非推奨

// ❌ _Unsafeメソッドの乱用
handle.TryUpdate_Unsafe(data);  // 本当に必要な場合のみ

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
        if (!handle.IsValid())
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

## API リファレンス

### Entity属性

| プロパティ | 説明 | デフォルト |
|-----------|------|-----------|
| `InitialCapacity` | プールの初期容量 | 256 |
| `ArenaName` | 生成されるArenaクラスの名前 | "{TypeName}Arena" |

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
| `Handle.IsValid()` | ハンドルが有効かチェック |
| `Handle.TryGet(out T)` | エンティティを安全に取得 |
| `Handle.Try{Method}(...)` | エンティティメソッドを安全に呼び出し |
| `Handle.Dispose()` | エンティティを削除 |
| `Handle.ToAnyHandle()` | 型消去されたハンドルに変換 |

### 生成されるメソッド（コンポーネント）

| メソッド | 説明 |
|---------|------|
| `Handle.{Component}_Try{Method}(...)` | コンポーネントメソッドを安全に呼び出し |
| `Handle.TryExecute<T>(RefAction<T>)` | ラムダでコンポーネントを操作 |
| `AnyHandle.TryExecute<T>(RefAction<T>)` | 型消去ハンドルでコンポーネントを操作 |

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
