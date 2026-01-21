# HandleSystem - 汎用ハンドルシステム

C# Source Generatorを使用した、汎用的なハンドルパターン実装です。エンティティに限らず、あらゆるオブジェクトに世代番号による無効化検出機能を提供します。

## 概要

HandleSystemは、削除されたオブジェクトへのアクセスを防ぐ「ハンドル-アリーナ」パターンの汎用実装です。

### ユースケース

- **攻撃オブジェクト** - 一時的な攻撃判定の管理
- **エフェクト** - パーティクルやビジュアルエフェクトの管理
- **バッファ/プール** - 任意のオブジェクトプール管理
- **エンティティ以外のゲームオブジェクト** - UI要素、サウンドインスタンスなど

### EntityHandleSystemとの関係

```
HandleSystem.Core (汎用)
├── IHandle, IArena, ArenaBase
├── [Handleable], [HandleableMethod]
└── RefAction<T>

EntityHandleSystem (Entity専用、HandleSystem依存)
├── IEntityHandle : IHandle
├── IEntityArena : IArena
├── EntityArenaBase : ArenaBase
├── [Entity], [EntityMethod]
└── コンポーネント、Query、AnyHandleなど
```

## クイックスタート

### 1. Handleable型の定義

```csharp
using Tomato.HandleSystem;

[Handleable(InitialCapacity = 64)]
public partial struct Attack
{
    public int Damage;
    public float Duration;
    public bool IsActive;

    [HandleableMethod]
    public void SetDamage(int damage)
    {
        Damage = damage;
    }

    [HandleableMethod]
    public int GetDamage()
    {
        return Damage;
    }

    [HandleableMethod(Unsafe = true)]
    public void Tick(float deltaTime)
    {
        Duration -= deltaTime;
        if (Duration <= 0)
        {
            IsActive = false;
        }
    }
}
```

### 2. 使用例

```csharp
// Arenaを作成（自動生成される）
var arena = new AttackArena(
    onSpawn: (ref Attack attack) => {
        attack.Duration = 1.0f;
        attack.IsActive = true;
    },
    onDespawn: (ref Attack attack) => {
        attack.IsActive = false;
    }
);

// オブジェクトを作成
AttackHandle handle = arena.Create();

// ハンドル経由で安全にアクセス
if (handle.TrySetDamage(50))
{
    Console.WriteLine("ダメージ設定完了");
}

// 値を取得
if (handle.TryGetDamage(out int damage))
{
    Console.WriteLine($"ダメージ: {damage}");
}

// オブジェクトを削除
handle.Dispose();

// 削除後のアクセスは安全に失敗
if (!handle.TrySetDamage(100))
{
    Console.WriteLine("無効なハンドル"); // ここが実行される
}
```

## 生成されるコード

`[Handleable]`属性を付けると、以下が自動生成されます：

### Handle構造体

```csharp
public struct AttackHandle : IEquatable<AttackHandle>, IHandle
{
    public bool IsValid { get; }
    public static AttackHandle Invalid { get; }
    public void Dispose();

    // [HandleableMethod]ごとに生成
    public bool TrySetDamage(int damage);
    public bool TryGetDamage(out int result);
    public void Tick_Unsafe(float deltaTime); // Unsafe=trueの場合
}
```

### Arenaクラス

```csharp
public class AttackArena : ArenaBase<Attack, AttackHandle>, IArena
{
    public AttackArena();
    public AttackArena(int initialCapacity);
    public AttackArena(int initialCapacity, RefAction<Attack> onSpawn, RefAction<Attack> onDespawn);

    public AttackHandle Create();
    public bool IsValid(AttackHandle handle);
    public int Count { get; }
    public int Capacity { get; }
}
```

## API リファレンス

### Handleable属性

| プロパティ | 説明 | デフォルト |
|-----------|------|-----------|
| `InitialCapacity` | プールの初期容量 | 256 |
| `ArenaName` | 生成されるArenaクラスの名前 | "{TypeName}Arena" |

### HandleableMethod属性

| プロパティ | 説明 | デフォルト |
|-----------|------|-----------|
| `Unsafe` | 検証なしの高速メソッドも生成 | false |

### 基底クラス・インターフェース

| 型 | 説明 |
|----|------|
| `IHandle` | ハンドルの基本インターフェース（`IsValid`プロパティ） |
| `IArena` | アリーナの基本インターフェース（`IsValid(index, generation)`） |
| `ArenaBase<T, THandle>` | アリーナの基底クラス（プール管理の実装） |
| `RefAction<T>` | `ref T`を受け取るデリゲート（コールバック用） |

## パフォーマンス

- **O(1)アクセス**: 配列インデックスによる直接アクセス
- **Boxingなし**: Arena・Handleともに値型アクセス
- **スレッドセーフ**: 内部でロックによる排他制御

## テスト

```bash
dotnet test libs/foundation/HandleSystem/HandleSystem.Tests/
```

## ライセンス

MIT License
