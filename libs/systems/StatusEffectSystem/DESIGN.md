# StatusEffectSystem 設計ドキュメント

## 概要

StatusEffectSystemは、状態異常効果の管理に必要な機能を提供する軽量ライブラリです。

### 設計方針

1. **値型中心**: 識別子やコレクションは`readonly struct`で実装し、GCプレッシャーを最小化
2. **決定論的**: 浮動小数点を避け、整数ベースの時間・値計算で再現性を確保
3. **イベント駆動**: 効果のライフサイクルをイベントで通知し、外部システムとの連携を容易に
4. **拡張可能**: 条件判定や値計算はインターフェースで抽象化

## アーキテクチャ

```
┌─────────────────────────────────────────────────────┐
│                  EffectManager                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │ TryApply    │  │ Remove      │  │ ProcessTick │  │
│  │ GetEffects  │  │ RemoveByTag │  │ Events      │  │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────┘
           │                   │
           ▼                   ▼
┌─────────────────┐   ┌─────────────────┐
│ EffectRegistry  │   │ EffectInstance  │
│  - Definitions  │   │   Arena         │
│  - Builders     │   │  - Instances    │
└─────────────────┘   └─────────────────┘
           │
           ▼
┌─────────────────┐
│ TagRegistry     │
│  - TagId        │
│  - Hierarchy    │
└─────────────────┘
```

## 識別子システム

### ID型の分類

| 型 | 実装パターン | 理由 |
|---|---|---|
| `EffectId` | static counter | レジストリで管理、実体を持たない |
| `TagId` | static counter | レジストリで管理、実体を持たない |
| `GroupId` | static counter | レジストリで管理、実体を持たない |
| `FlagId` | static counter | レジストリで管理、実体を持たない |
| `EffectInstanceId` | index + generation | Arenaパターン、実体を持つ |

### EffectInstanceId の Arena パターン

`EffectInstanceId`は64ビット値で、下位32ビットがインデックス、上位32ビットがジェネレーションを表します。

```csharp
public readonly struct EffectInstanceId
{
    public readonly ulong Value;

    internal EffectInstanceId(int index, int generation)
    {
        Value = ((ulong)(uint)generation << 32) | (uint)index;
    }

    public int Index => (int)(Value & 0xFFFFFFFF);
    public int Generation => (int)(Value >> 32);
}
```

この設計により：
- インデックスでO(1)アクセスが可能
- ジェネレーションで無効な参照を検出可能
- スロットの再利用が可能

### Reason ID

削除理由（`RemovalReasonId`）と失敗理由（`FailureReasonId`）は、ライブラリ定義の定数とユーザー定義の値を持ちます。

```csharp
public readonly struct RemovalReasonId
{
    public readonly int Value;
    public const int Expired = 0;  // ライブラリ定義：期限切れ
}

public readonly struct FailureReasonId
{
    public readonly int Value;
    public const int DefinitionNotFound = 0;  // ライブラリ定義：定義が見つからない
}
```

## 時間モデル

### GameTick

ゲーム内の論理時間を表す整数値です。

```csharp
public readonly struct GameTick
{
    public readonly long Value;
}
```

### TickDuration

期間を表す整数値です。特殊値として`Zero`と`Infinite`があります。

```csharp
public readonly struct TickDuration
{
    public readonly int Value;

    public static readonly TickDuration Zero = new(0);
    public static readonly TickDuration Infinite = new(int.MaxValue);

    public bool IsInfinite => Value == int.MaxValue;
}
```

加減算は自動的にクランプされます：
- 負の値は0にクランプ
- Infiniteとの加算はInfiniteを返す

## コレクション

### TagSet

最大128個のタグを2つの`ulong`で管理するビットセットです。

```csharp
public readonly struct TagSet
{
    private readonly ulong _bits0;  // TagId 0-63
    private readonly ulong _bits1;  // TagId 64-127
}
```

主な操作：
- `Contains(TagId)` - タグの存在確認
- `With(TagId)` / `Without(TagId)` - タグの追加/削除（新しいインスタンスを返す）
- `ContainsAny(TagSet)` / `ContainsAll(TagSet)` - 集合演算
- `|`, `&`, `~` - ビット演算子

### FlagSet

最大64個のフラグを1つの`ulong`で管理するビットセットです。

```csharp
public readonly struct FlagSet
{
    private readonly ulong _bits;
}
```

## 効果定義

### StatusEffectDefinition

効果の静的な定義を保持します。

```csharp
public sealed class StatusEffectDefinition
{
    public EffectId Id { get; }
    public string Name { get; }
    public TickDuration BaseDuration { get; }
    public StackConfig StackConfig { get; }
    public DurationBehavior DurationBehavior { get; }
    public TagSet Tags { get; }
    public bool IsPermanent { get; }
}
```

### Builder パターン

定義の構築には流暢なAPIを提供します。

```csharp
effectRegistry.Register("EffectName", b => b
    .WithDuration(new TickDuration(100))
    .WithStackConfig(StackConfig.Additive(5))
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagSet)
    .WithApplyCondition(condition));
```

## スタック動作

### StackConfig

```csharp
public readonly struct StackConfig
{
    public StackBehaviorType Type { get; }
    public int MaxStacks { get; }

    public static StackConfig None => new(StackBehaviorType.None, 1);
    public static StackConfig Additive(int max) => new(StackBehaviorType.Additive, max);
}
```

| Type | 動作 |
|---|---|
| `None` | スタックしない（常に1） |
| `Additive` | 再適用時にスタック数を+1（最大値まで） |

## 期間動作

### DurationBehavior

```csharp
public enum DurationBehavior
{
    Refresh,      // 残り時間を最大に戻す
    Extend,       // 残り時間に加算
    KeepExisting, // 変更しない
    Replace       // 新しい期間で置き換え
}
```

## 効果インスタンス

### EffectInstance

実行中の効果の状態を保持します。

```csharp
public sealed class EffectInstance
{
    public EffectInstanceId Id { get; }
    public EffectId DefinitionId { get; }
    public ulong TargetId { get; }
    public ulong SourceId { get; }
    public GameTick AppliedAt { get; }
    public GameTick ExpiresAt { get; }
    public int CurrentStacks { get; }
    public bool IsExpired(GameTick currentTick);
}
```

### EffectInstanceArena

効果インスタンスの管理にArenaパターンを使用します。

```csharp
internal sealed class EffectInstanceArena
{
    public EffectInstanceId Add(EffectInstance instance);
    public EffectInstance? Get(EffectInstanceId id);
    public bool Remove(EffectInstanceId id);
    public IEnumerable<EffectInstance> GetByTarget(ulong targetId);
}
```

スロットの再利用：
1. 削除時にジェネレーションをインクリメント
2. 削除されたスロットをフリーリストに追加
3. 新規追加時にフリーリストから優先的に割り当て

## レジストリ

### TagRegistry

```csharp
public sealed class TagRegistry
{
    public TagId Register(string name, TagId? parentId = null);
    public string? GetName(TagId id);
    public TagSet CreateSet(params TagId[] tags);
    public bool IsDescendantOf(TagId tag, TagId ancestor);
}
```

タグは階層構造を持つことができ、`IsDescendantOf`で親子関係を判定できます。

### EffectRegistry

```csharp
public sealed class EffectRegistry
{
    public EffectId Register(string name, Action<StatusEffectDefinitionBuilder> configure);
    public StatusEffectDefinition? GetDefinition(EffectId id);
}
```

## イベントシステム

### イベント型

```csharp
public readonly struct EffectAppliedEvent
{
    public EffectInstanceId InstanceId { get; }
    public EffectId DefinitionId { get; }
    public ulong TargetId { get; }
    public ulong SourceId { get; }
    public GameTick AppliedAt { get; }
    public bool WasMerged { get; }
}

public readonly struct EffectRemovedEvent
{
    public EffectInstanceId InstanceId { get; }
    public EffectId DefinitionId { get; }
    public ulong TargetId { get; }
    public RemovalReasonId Reason { get; }
    public int FinalStacks { get; }
}

public readonly struct StackChangedEvent
{
    public EffectInstanceId InstanceId { get; }
    public EffectId DefinitionId { get; }
    public ulong TargetId { get; }
    public int PreviousStacks { get; }
    public int NewStacks { get; }
}
```

### 購読

```csharp
manager.OnEffectApplied += handler;
manager.OnEffectRemoved += handler;
manager.OnStackChanged += handler;
```

## EffectManager

### 主要API

```csharp
public sealed class EffectManager
{
    // 適用
    public ApplyResult TryApply(ulong targetId, EffectId effectId, ulong sourceId);

    // 解除
    public void Remove(EffectInstanceId instanceId, RemovalReasonId reason);
    public void RemoveAll(ulong targetId, RemovalReasonId reason);
    public int RemoveByTag(ulong targetId, TagId tag, RemovalReasonId reason);

    // クエリ
    public EffectInstance? GetInstance(EffectInstanceId id);
    public IEnumerable<EffectInstance> GetEffects(ulong targetId);
    public bool HasEffect(ulong targetId, EffectId effectId);
    public bool HasEffectWithTag(ulong targetId, TagId tag);

    // 変更
    public void AddStacks(EffectInstanceId instanceId, int delta);
    public void ExtendDuration(EffectInstanceId instanceId, TickDuration extension);

    // 時間経過
    public void ProcessTick(GameTick currentTick);

    // イベント
    public event Action<EffectAppliedEvent>? OnEffectApplied;
    public event Action<EffectRemovedEvent>? OnEffectRemoved;
    public event Action<StackChangedEvent>? OnStackChanged;
}
```

### ApplyResult

```csharp
public readonly struct ApplyResult
{
    public bool Success { get; }
    public EffectInstanceId InstanceId { get; }
    public bool WasMerged { get; }
    public FailureReasonId FailureReason { get; }
}
```

## 効果結果システム

### 概要

効果結果システムは、対象エンティティに適用されている全効果の累積結果を1フレームに1回だけ計算し、他のシステムから参照できるようにする仕組みです。

### 設計思想

- **フレーム単位の計算**: 毎フレーム1回だけ効果結果を計算し、他システムはそれを参照するだけ
- **ゲーム定義の効果結果型**: 効果結果の構造体はゲーム側で定義（ライブラリは仕組みのみ提供）
- **差分検出によるトリガー**: 前フレームと現フレームの効果結果を比較してトリガー検出が可能
- **決定論的**: 同じバフが揃っていれば、適用順に関係なく常に同じ結果

### 適用順序（Priority）

コントリビュータは以下の順序で適用されます：

1. **Priority昇順**（小さい値が先に適用）
2. **同じPriorityならEffectId.Value順**（定義順）

```csharp
public sealed class StatusEffectDefinition
{
    /// <summary>
    /// 効果結果適用時の優先度（小さいほど先に適用）
    /// </summary>
    public int Priority { get; }
}
```

この設計により：
- **実行時の順番に依存しない**: 効果がかかった順番は結果に影響しない
- **再現性**: 同じ効果セットなら常に同じ結果
- **計算順序の明確化**: フラット加算→加算%→乗算%のような順序を明示的に定義可能

推奨されるPriority設計例：

| Priority | 用途 |
|---|---|
| 0 | フラット加算（+10, +20など） |
| 100 | 加算%（+20%, +30%を合計） |
| 200 | 乗算%（×1.5, ×1.2を個別適用） |
| 1000 | 最終補正（上限クランプなど） |

### ResultContributor

効果が効果結果にどう寄与するかを定義するデリゲートです。

```csharp
public delegate void ResultContributor<TResult>(ref TResult result, int stacks)
    where TResult : struct;
```

### 使用例

```csharp
// ゲーム側で効果結果構造体を定義
public struct GameResult
{
    public int AttackFlatBonus;
    public int AttackPercentAdd;
    public int AttackPercentMult;
    public bool IsStunned;
}

// 効果定義時にコントリビュータと優先度を登録
var flatBonus = effectRegistry.Register("FlatBonus", b => b
    .WithDuration(new TickDuration(600))
    .WithPriority(0)  // 最初に適用
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.AttackFlatBonus += 10 * stacks;
    }));

var percentAdd = effectRegistry.Register("PercentAdd", b => b
    .WithDuration(new TickDuration(600))
    .WithPriority(100)  // 2番目
    .WithStackConfig(StackConfig.Additive(3))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.AttackPercentAdd += 10 * stacks;
    }));

var stun = effectRegistry.Register("Stun", b => b
    .WithDuration(new TickDuration(60))
    .WithPriority(0)  // フラグ系はPriority関係なし
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.IsStunned = true;
    }));
```

### フレーム処理

```csharp
// ゲームループ
public void Update()
{
    // 1. 状態異常の時間経過処理
    manager.ProcessTick(currentTick);

    // 2. 各エンティティの効果結果を計算（フレームで1回）
    foreach (var entity in entities)
    {
        var result = manager.CalculateResult<GameResult>(entity.Id, default);
        entity.EffectResult = result;
    }

    // 3. 他のシステムは効果結果を参照するだけ
    battleSystem.Update(entities);  // entity.EffectResult を使用
    aiSystem.Update(entities);       // entity.EffectResult を使用
}
```

### トリガー検出

前後の効果結果を比較することで、状態変化のトリガーを検出できます。

```csharp
// 前フレームの効果結果を保持
var prevResult = entity.EffectResult;

// 今フレームの効果結果を計算
var currResult = manager.CalculateResult<GameResult>(entity.Id, default);
entity.EffectResult = currResult;

// 差分からトリガーを検出
if (!prevResult.IsStunned && currResult.IsStunned)
{
    // スタン開始トリガー
    OnStunStart(entity);
}
else if (prevResult.IsStunned && !currResult.IsStunned)
{
    // スタン終了トリガー
    OnStunEnd(entity);
}
```

## 拡張ポイント

### ICondition

効果の適用条件をカスタマイズできます。

```csharp
public interface ICondition
{
    bool Evaluate(in ConditionContext context);
}

public readonly struct ConditionContext
{
    public ulong TargetId { get; }
    public ulong SourceId { get; }
    public EffectId EffectId { get; }
}
```

### IValueSource

動的な値の計算をカスタマイズできます。

```csharp
public interface IValueSource
{
    FixedPoint GetValue(in ValueContext context);
}
```

## 遅延実行モデル（Request Queuing）

### 概要

EffectManagerは決定論的動作を保証するため、遅延実行モデルを採用しています。すべての変更操作はリクエストとしてキューに追加され、`ProcessTick`で一括処理されます。

### リクエスト型

```csharp
// 効果適用リクエスト
internal readonly struct ApplyRequest
{
    public readonly EffectInstanceId InstanceId;  // 事前にアロケート
    public readonly EffectId EffectId;
    public readonly ulong TargetId;
    public readonly StatusEffectDefinition Definition;
}

// 効果削除リクエスト
internal readonly struct RemoveRequest
{
    public readonly EffectInstanceId InstanceId;
    public readonly RemovalReasonId Reason;
}

// スタック変更リクエスト
internal readonly struct StackChangeRequest
{
    public readonly EffectInstanceId InstanceId;
    public readonly int Delta;
    public readonly bool IsAbsolute;  // true: SetStacks, false: AddStacks
}

// 期間延長リクエスト
internal readonly struct ExtendDurationRequest
{
    public readonly EffectInstanceId InstanceId;
    public readonly TickDuration Extension;
}

// フラグ設定リクエスト
internal readonly struct SetFlagRequest
{
    public readonly EffectInstanceId InstanceId;
    public readonly FlagId Flag;
    public readonly bool Value;
}
```

### ProcessTickの処理順序

`ProcessTick`は以下の順序でリクエストを処理します：

1. **Apply リクエスト**: ソート済みリストへの挿入
2. **StackChange リクエスト**: スタック数の変更（0になったら削除）
3. **ExtendDuration リクエスト**: 期間延長
4. **SetFlag リクエスト**: フラグ設定
5. **Remove リクエスト**: 効果削除
6. **期限切れチェック**: 期限切れ効果の自動削除

### ソート済みリストの維持

効果インスタンスは`_sortedInstancesByOwner`に以下の順序でソートされて保持されます：

1. **Priority昇順**（小さい値が先）
2. **EffectId.Value昇順**（同じPriorityの場合、定義順）

新規効果の挿入時は二分探索で適切な位置を特定します：

```csharp
private int FindInsertIndex(List<EffectInstanceId> sortedList, int priority, EffectId effectId)
{
    // 二分探索で O(log n) 挿入位置を特定
    int low = 0, high = sortedList.Count;
    while (low < high)
    {
        int mid = (low + high) / 2;
        var midDef = GetDefinition(sortedList[mid]);
        int cmp = midDef.Priority.CompareTo(priority);
        if (cmp == 0)
            cmp = midDef.Id.Value.CompareTo(effectId.Value);
        if (cmp < 0)
            low = mid + 1;
        else
            high = mid;
    }
    return low;
}
```

### 決定論性の保証

この設計により以下が保証されます：

1. **適用順序の独立性**: 効果が適用された順序は結果に影響しない
2. **再現性**: 同じ入力に対して常に同じ出力
3. **予測可能性**: 効果結果の計算結果が一意に決まる

### API動作の変更点

| API | 動作 |
|---|---|
| `TryApply` | リクエストをキューに追加、InstanceIdを即座に返却（事前アロケート） |
| `Remove` | 削除リクエストをキューに追加 |
| `AddStacks` / `SetStacks` | スタック変更リクエストをキューに追加 |
| `ExtendDuration` | 期間延長リクエストをキューに追加 |
| `SetFlag` | フラグ設定リクエストをキューに追加 |
| `HasEffect` / `GetEffects` | **処理済み**の効果のみを返す（ペンディング中は含まない） |
| `ProcessTick` | 全リクエストを処理し、期限切れをチェック |

### 使用上の注意

```csharp
// ✗ ProcessTick前は効果が見えない
manager.TryApply(targetId, effectId, sourceId);
Assert.False(manager.HasEffect(targetId, effectId));  // false!

// ✓ ProcessTick後は効果が見える
manager.ProcessTick(currentTick);
Assert.True(manager.HasEffect(targetId, effectId));   // true
```

通常のゲームループでは毎フレーム`ProcessTick`を呼び出すため、この遅延は実質的に1フレーム以内です。

## パフォーマンス考慮事項

1. **ビットセットによるタグ管理**: TagSetは最大128タグをO(1)で操作可能
2. **Arenaパターン**: インスタンスの追加・削除がO(1)、メモリの再利用でアロケーション削減
3. **値型識別子**: IDのボクシングを回避
4. **ターゲット別インデックス**: `GetByTarget`のためにDictionary<ulong, List<int>>を内部で保持
5. **ソート済みリストの維持**: 効果結果計算時のソート不要（挿入時に二分探索でO(log n)）
6. **バッチ処理**: 複数のリクエストをProcessTickで一括処理
