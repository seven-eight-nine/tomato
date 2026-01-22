# StatusEffectSystem 実装仕様書

## 1. 概要

### 1.1 目的

本仕様書は、Tomatoフレームワーク内で状態異常管理システム（StatusEffectSystem）を実装するための詳細仕様を定義する。

### 1.2 位置づけ

```
libs/
├── foundation/           # 基盤システム
│   ├── HandleSystem/     # ← 識別子パターンを継承
│   └── CommandGenerator/ # ← Wave処理との統合
├── orchestration/
│   └── EntitySystem/     # ← EntityContextとの連携
└── systems/
    └── StatusEffectSystem/  # ← 本システム（新規追加）
```

### 1.3 設計原則

| 原則 | 説明 |
|------|------|
| Tomatoとの整合性 | 既存のHandleSystem、CommandGeneratorパターンに従う |
| 完全抽象化 | 列挙型による固定概念を排除、すべてユーザー定義可能 |
| 決定論的実行 | 同一入力に対して常に同一結果を保証 |
| テスト容易性 | 1システム1,000テスト以上を目標 |
| GC最小化 | オブジェクトプール、struct活用 |

---

## 2. プロジェクト構成

### 2.1 ソリューション構造

```
libs/systems/StatusEffectSystem/
├── StatusEffectSystem.Core/
│   ├── StatusEffectSystem.Core.csproj
│   ├── Identifiers/
│   │   ├── EffectId.cs
│   │   ├── EffectInstanceId.cs
│   │   ├── TagId.cs
│   │   ├── GroupId.cs
│   │   ├── PhaseId.cs
│   │   ├── ComponentTypeId.cs
│   │   ├── InteractionRuleId.cs
│   │   ├── RemovalReasonId.cs
│   │   ├── FailureReasonId.cs
│   │   └── FlagId.cs
│   ├── Time/
│   │   ├── GameTick.cs
│   │   └── TickDuration.cs
│   ├── Collections/
│   │   ├── TagSet.cs
│   │   └── FlagSet.cs
│   ├── Definitions/
│   │   ├── StatusEffectDefinition.cs
│   │   ├── TagDefinition.cs
│   │   ├── PhaseDefinition.cs
│   │   └── StackConfig.cs
│   ├── Instances/
│   │   ├── EffectInstance.cs
│   │   ├── EffectInstanceArena.cs
│   │   └── ComponentState.cs
│   ├── Components/
│   │   ├── IEffectComponent.cs
│   │   ├── ITickableComponent.cs
│   │   ├── IOnApplyComponent.cs
│   │   ├── IOnRemoveComponent.cs
│   │   ├── IReactiveComponent.cs
│   │   └── IModifierComponent.cs
│   ├── Conditions/
│   │   ├── ICondition.cs
│   │   ├── IConditionContext.cs
│   │   └── Combinators/
│   │       ├── AndCondition.cs
│   │       ├── OrCondition.cs
│   │       └── NotCondition.cs
│   ├── Values/
│   │   ├── IValueSource.cs
│   │   ├── ConstantValue.cs
│   │   ├── StackScaledValue.cs
│   │   └── SnapshotAwareValue.cs
│   ├── Behaviors/
│   │   ├── Stack/
│   │   │   ├── IStackBehavior.cs
│   │   │   ├── AdditiveStackBehavior.cs
│   │   │   ├── IndependentStackBehavior.cs
│   │   │   └── HighestOnlyStackBehavior.cs
│   │   ├── Duration/
│   │   │   ├── IDurationBehavior.cs
│   │   │   ├── RefreshDurationBehavior.cs
│   │   │   ├── ExtendDurationBehavior.cs
│   │   │   └── LongestDurationBehavior.cs
│   │   └── Snapshot/
│   │       ├── ISnapshotTrigger.cs
│   │       ├── NoSnapshotTrigger.cs
│   │       └── OnApplySnapshotTrigger.cs
│   ├── Filters/
│   │   ├── IApplyFilter.cs
│   │   ├── ApplyFilterPipeline.cs
│   │   ├── TagRejectFilter.cs
│   │   ├── MaxInstancesFilter.cs
│   │   └── ExclusiveGroupFilter.cs
│   ├── Interactions/
│   │   ├── InteractionRule.cs
│   │   ├── InteractionTrigger.cs
│   │   ├── EffectMatcher.cs
│   │   ├── IInteractionEffect.cs
│   │   └── InteractionEngine.cs
│   ├── Removal/
│   │   ├── IRemovalStrategy.cs
│   │   ├── IRemovalSelector.cs
│   │   ├── TagBasedRemovalStrategy.cs
│   │   └── StealStrategy.cs
│   ├── Modifiers/
│   │   ├── IModifier.cs
│   │   ├── ICalculationStep.cs
│   │   ├── CalculationPipeline.cs
│   │   └── Steps/
│   │       ├── FlatAdditionStep.cs
│   │       ├── PercentAdditiveStep.cs
│   │       └── PercentMultiplicativeStep.cs
│   ├── Registries/
│   │   ├── IEffectRegistry.cs
│   │   ├── EffectRegistry.cs
│   │   ├── ITagRegistry.cs
│   │   ├── TagRegistry.cs
│   │   ├── IPhaseRegistry.cs
│   │   ├── PhaseRegistry.cs
│   │   ├── IFlagRegistry.cs
│   │   └── FlagRegistry.cs
│   ├── Contexts/
│   │   ├── IEffectContext.cs
│   │   ├── EffectContext.cs
│   │   ├── ApplyFilterContext.cs
│   │   ├── InteractionContext.cs
│   │   └── RemovalStrategyContext.cs
│   ├── Manager/
│   │   ├── IEffectManager.cs
│   │   ├── EffectManager.cs
│   │   └── EffectManagerConfig.cs
│   ├── Events/
│   │   ├── EffectAppliedEvent.cs
│   │   ├── EffectRemovedEvent.cs
│   │   ├── StackChangedEvent.cs
│   │   └── EffectTickedEvent.cs
│   └── Integration/
│       ├── StatusEffectSystem.cs
│       ├── StatusEffectSystemConfig.cs
│       └── IContextExtensionProvider.cs
├── StatusEffectSystem.Tests/
│   ├── StatusEffectSystem.Tests.csproj
│   ├── Identifiers/
│   ├── Collections/
│   ├── Behaviors/
│   ├── Filters/
│   ├── Interactions/
│   ├── Modifiers/
│   └── Integration/
└── StatusEffectSystem.Samples/
    └── StatusEffectSystem.Samples.csproj
```

### 2.2 依存関係

```xml
<!-- StatusEffectSystem.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- 外部依存なし - 純粋ライブラリとして設計 -->
  </ItemGroup>
</Project>
```

```xml
<!-- StatusEffectSystem.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\StatusEffectSystem.Core\StatusEffectSystem.Core.csproj" />
  </ItemGroup>
</Project>
```

---

## 3. 識別子型

### 3.1 識別子の分類

| 分類 | 識別子 | 管理方式 | 説明 |
|------|--------|----------|------|
| **実体あり** | `EffectInstanceId` | Arena（index + generation） | 実行時インスタンスへの参照 |
| **実体なし** | その他すべて | 静的カウンター | 定義・分類用のID |

### 3.2 実体なしID（静的カウンター方式）

レジストリ登録時に自動採番される。ユーザーが直接値を指定することはない。

```csharp
/// <summary>
/// 実体なしIDの基本テンプレート
/// </summary>
public readonly struct XxxId : IEquatable<XxxId>, IComparable<XxxId>
{
    public readonly int Value;

    internal XxxId(int value) => Value = value;  // internal: レジストリからのみ生成

    public bool Equals(XxxId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is XxxId other && Equals(other);
    public override int GetHashCode() => Value;
    public int CompareTo(XxxId other) => Value.CompareTo(other.Value);

    public static bool operator ==(XxxId left, XxxId right) => left.Equals(right);
    public static bool operator !=(XxxId left, XxxId right) => !left.Equals(right);

    public override string ToString() => $"XxxId({Value})";

    public static readonly XxxId Invalid = new(-1);
    public bool IsValid => Value >= 0;
}
```

**ID生成例（レジストリ内部）：**

```csharp
public sealed class TagRegistry : ITagRegistry
{
    private int _nextId = 0;  // 静的カウンター

    public TagId Register(string name, TagId? parentId = null)
    {
        var id = new TagId(_nextId++);
        _definitions[id] = new TagDefinition(id, name, parentId);
        return id;
    }
}
```

**使用例：**

```csharp
// ユーザーはIDの値を気にしない
var buffTag = tagRegistry.Register("buff");
var debuffTag = tagRegistry.Register("debuff");
var poisonTag = tagRegistry.Register("poison", parentId: debuffTag);

var poisonEffect = effectRegistry.Register("Poison", builder => builder
    .WithTags(buffTag, poisonTag)
    .WithDuration(new TickDuration(300)));
```

### 3.3 実体なしID一覧

| 識別子 | 用途 | 生成元 |
|--------|------|--------|
| `EffectId` | 状態異常定義の識別 | `IEffectRegistry.Register()` |
| `TagId` | タグの識別 | `ITagRegistry.Register()` |
| `GroupId` | 排他グループの識別 | `IGroupRegistry.Register()` |
| `PhaseId` | 処理フェーズの識別 | `IPhaseRegistry.Register()` |
| `ComponentTypeId` | コンポーネント種別の識別 | `IComponentTypeRegistry.Register()` |
| `InteractionRuleId` | 相互作用ルールの識別 | `IInteractionRegistry.Register()` |
| `FlagId` | フラグの識別 | `IFlagRegistry.Register()` |

### 3.4 理由ID（ライブラリ定義 + ユーザー拡張）

ライブラリが内部で使用する最小限の定数のみ定義。ユーザーは任意の値で拡張可能。

```csharp
/// <summary>除去理由ID</summary>
public readonly struct RemovalReasonId : IEquatable<RemovalReasonId>
{
    public readonly int Value;

    public RemovalReasonId(int value) => Value = value;

    /// <summary>期限切れによる自動除去（ライブラリ内部で使用）</summary>
    public const int Expired = 0;
}

/// <summary>付与失敗理由ID</summary>
public readonly struct FailureReasonId : IEquatable<FailureReasonId>
{
    public readonly int Value;

    public FailureReasonId(int value) => Value = value;

    /// <summary>効果定義が未登録（ライブラリ内部で使用）</summary>
    public const int DefinitionNotFound = 0;
}
```

**使用例：**

```csharp
// ライブラリ内部
if (instance.IsExpired(currentTick))
    Remove(instanceId, new RemovalReasonId(RemovalReasonId.Expired));

// ユーザー定義
public static class MyRemovalReasons
{
    public const int Dispelled = 1;
    public const int Stolen = 2;
    public const int Cleansed = 3;
}

manager.Remove(instanceId, new RemovalReasonId(MyRemovalReasons.Dispelled));
```

### 3.5 EffectInstanceId（Arenaパターン）

実体を持つ唯一のID。インデックスと世代番号で安全な参照を実現。

```csharp
/// <summary>
/// 実行時インスタンスID
/// 上位32bit: 世代番号（削除検出用）
/// 下位32bit: Arenaインデックス
/// </summary>
public readonly struct EffectInstanceId : IEquatable<EffectInstanceId>
{
    public readonly ulong Value;

    internal EffectInstanceId(int index, int generation)  // internal: Arenaからのみ生成
    {
        Value = ((ulong)generation << 32) | (uint)index;
    }

    public int Index => (int)(Value & 0xFFFFFFFF);
    public int Generation => (int)(Value >> 32);

    public bool Equals(EffectInstanceId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EffectInstanceId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(EffectInstanceId left, EffectInstanceId right) => left.Equals(right);
    public static bool operator !=(EffectInstanceId left, EffectInstanceId right) => !left.Equals(right);

    public override string ToString() => $"EffectInstanceId(idx:{Index}, gen:{Generation})";

    public static readonly EffectInstanceId Invalid = new(-1, 0);
    public bool IsValid => Index >= 0;
}
```

---

## 4. 時間モデル

### 4.1 GameTick

```csharp
/// <summary>
/// ゲーム内の論理時刻（フレーム番号）
/// 決定論的実行のため、実時間ではなくティック単位で管理
/// </summary>
public readonly struct GameTick : IEquatable<GameTick>, IComparable<GameTick>
{
    public readonly long Value;

    public GameTick(long value) => Value = value;

    public static GameTick operator +(GameTick tick, TickDuration duration)
        => new(tick.Value + duration.Value);

    public static GameTick operator -(GameTick tick, TickDuration duration)
        => new(tick.Value - duration.Value);

    public static TickDuration operator -(GameTick a, GameTick b)
        => new((int)(a.Value - b.Value));

    public static bool operator <(GameTick a, GameTick b) => a.Value < b.Value;
    public static bool operator >(GameTick a, GameTick b) => a.Value > b.Value;
    public static bool operator <=(GameTick a, GameTick b) => a.Value <= b.Value;
    public static bool operator >=(GameTick a, GameTick b) => a.Value >= b.Value;

    public bool Equals(GameTick other) => Value == other.Value;
    public int CompareTo(GameTick other) => Value.CompareTo(other.Value);

    public override string ToString() => $"Tick({Value})";
}
```

### 4.2 TickDuration

```csharp
/// <summary>
/// ティック単位の持続時間
/// </summary>
public readonly struct TickDuration : IEquatable<TickDuration>, IComparable<TickDuration>
{
    public readonly int Value;

    public TickDuration(int value) => Value = value;

    public static readonly TickDuration Zero = new(0);
    public static readonly TickDuration Infinite = new(int.MaxValue);

    public bool IsInfinite => Value == int.MaxValue;
    public bool IsZero => Value == 0;

    public static TickDuration operator +(TickDuration a, TickDuration b)
    {
        if (a.IsInfinite || b.IsInfinite) return Infinite;
        return new(a.Value + b.Value);
    }

    public static TickDuration operator -(TickDuration a, TickDuration b)
    {
        if (a.IsInfinite) return Infinite;
        return new(Math.Max(0, a.Value - b.Value));
    }

    public static TickDuration operator *(TickDuration duration, int multiplier)
    {
        if (duration.IsInfinite) return Infinite;
        return new(duration.Value * multiplier);
    }

    public static bool operator <(TickDuration a, TickDuration b) => a.Value < b.Value;
    public static bool operator >(TickDuration a, TickDuration b) => a.Value > b.Value;

    public bool Equals(TickDuration other) => Value == other.Value;
    public int CompareTo(TickDuration other) => Value.CompareTo(other.Value);

    public override string ToString() => IsInfinite ? "Infinite" : $"{Value}ticks";
}
```

---

## 5. コレクション型

### 5.1 TagSet

```csharp
/// <summary>
/// タグの集合（最大128タグ、2つのulongで管理）
/// </summary>
public readonly struct TagSet : IEquatable<TagSet>
{
    private readonly ulong _bits0;  // TagId 0-63
    private readonly ulong _bits1;  // TagId 64-127

    private TagSet(ulong bits0, ulong bits1)
    {
        _bits0 = bits0;
        _bits1 = bits1;
    }

    public static readonly TagSet Empty = new(0, 0);

    public bool Contains(TagId tag)
    {
        if (tag.Value < 64)
            return (_bits0 & (1UL << tag.Value)) != 0;
        if (tag.Value < 128)
            return (_bits1 & (1UL << (tag.Value - 64))) != 0;
        return false;
    }

    public TagSet With(TagId tag)
    {
        if (tag.Value < 64)
            return new(_bits0 | (1UL << tag.Value), _bits1);
        if (tag.Value < 128)
            return new(_bits0, _bits1 | (1UL << (tag.Value - 64)));
        return this;
    }

    public TagSet Without(TagId tag)
    {
        if (tag.Value < 64)
            return new(_bits0 & ~(1UL << tag.Value), _bits1);
        if (tag.Value < 128)
            return new(_bits0, _bits1 & ~(1UL << (tag.Value - 64)));
        return this;
    }

    public bool ContainsAny(TagSet other)
        => ((_bits0 & other._bits0) | (_bits1 & other._bits1)) != 0;

    public bool ContainsAll(TagSet other)
        => (_bits0 & other._bits0) == other._bits0
        && (_bits1 & other._bits1) == other._bits1;

    public static TagSet operator |(TagSet a, TagSet b)
        => new(a._bits0 | b._bits0, a._bits1 | b._bits1);

    public static TagSet operator &(TagSet a, TagSet b)
        => new(a._bits0 & b._bits0, a._bits1 & b._bits1);

    public static TagSet operator ~(TagSet a)
        => new(~a._bits0, ~a._bits1);

    public bool Equals(TagSet other)
        => _bits0 == other._bits0 && _bits1 == other._bits1;

    public override int GetHashCode()
        => HashCode.Combine(_bits0, _bits1);

    public int Count => BitOperations.PopCount(_bits0) + BitOperations.PopCount(_bits1);
    public bool IsEmpty => _bits0 == 0 && _bits1 == 0;

    /// <summary>含まれるTagIdを列挙</summary>
    public TagSetEnumerator GetEnumerator() => new(this);

    public ref struct TagSetEnumerator
    {
        private readonly TagSet _set;
        private int _current;

        internal TagSetEnumerator(TagSet set)
        {
            _set = set;
            _current = -1;
        }

        public TagId Current => new(_current);

        public bool MoveNext()
        {
            while (++_current < 128)
            {
                if (_set.Contains(new TagId(_current)))
                    return true;
            }
            return false;
        }
    }
}
```

### 5.2 FlagSet

```csharp
/// <summary>
/// フラグの集合（最大64フラグ）
/// </summary>
public readonly struct FlagSet : IEquatable<FlagSet>
{
    private readonly ulong _bits;

    public FlagSet(ulong bits) => _bits = bits;

    public static readonly FlagSet Empty = new(0);

    public bool Has(FlagId flag) => (_bits & (1UL << flag.Value)) != 0;

    public FlagSet With(FlagId flag) => new(_bits | (1UL << flag.Value));

    public FlagSet Without(FlagId flag) => new(_bits & ~(1UL << flag.Value));

    public FlagSet Toggle(FlagId flag) => new(_bits ^ (1UL << flag.Value));

    public static FlagSet operator |(FlagSet a, FlagSet b) => new(a._bits | b._bits);
    public static FlagSet operator &(FlagSet a, FlagSet b) => new(a._bits & b._bits);
    public static FlagSet operator ~(FlagSet a) => new(~a._bits);

    public bool Equals(FlagSet other) => _bits == other._bits;
    public override int GetHashCode() => _bits.GetHashCode();

    public int Count => BitOperations.PopCount(_bits);
    public bool IsEmpty => _bits == 0;
}
```

---

## 6. 定義クラス

### 6.1 TagDefinition

```csharp
/// <summary>
/// タグ定義（マスタデータ）
/// </summary>
public sealed class TagDefinition
{
    public TagId Id { get; }
    public string Name { get; }
    public TagId? ParentId { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    public TagDefinition(
        TagId id,
        string name,
        TagId? parentId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ParentId = parentId;
        Metadata = metadata ?? ImmutableDictionary<string, string>.Empty;
    }
}
```

### 6.2 PhaseDefinition

```csharp
/// <summary>
/// 処理フェーズの種別
/// </summary>
public enum PhaseType
{
    /// <summary>毎ティック実行</summary>
    PerTick,
    /// <summary>イベント発生時に実行</summary>
    OnEvent,
    /// <summary>修正値収集時に実行</summary>
    OnCollect
}

/// <summary>
/// 処理フェーズ定義
/// </summary>
public sealed class PhaseDefinition
{
    public PhaseId Id { get; }
    public string Name { get; }
    public int Order { get; }
    public PhaseType Type { get; }

    public PhaseDefinition(PhaseId id, string name, int order, PhaseType type)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Order = order;
        Type = type;
    }
}
```

### 6.3 StackConfig

```csharp
/// <summary>
/// スタック設定
/// </summary>
public sealed class StackConfig
{
    /// <summary>最大スタック数（0で無制限）</summary>
    public int MaxStacks { get; }

    /// <summary>スタック合成の振る舞い</summary>
    public IStackBehavior StackBehavior { get; }

    /// <summary>時間更新の振る舞い</summary>
    public IDurationBehavior DurationBehavior { get; }

    /// <summary>スタック源の識別方法</summary>
    public IStackSourceIdentifier SourceIdentifier { get; }

    public StackConfig(
        int maxStacks,
        IStackBehavior stackBehavior,
        IDurationBehavior durationBehavior,
        IStackSourceIdentifier sourceIdentifier)
    {
        MaxStacks = maxStacks >= 0 ? maxStacks : throw new ArgumentOutOfRangeException(nameof(maxStacks));
        StackBehavior = stackBehavior ?? throw new ArgumentNullException(nameof(stackBehavior));
        DurationBehavior = durationBehavior ?? throw new ArgumentNullException(nameof(durationBehavior));
        SourceIdentifier = sourceIdentifier ?? throw new ArgumentNullException(nameof(sourceIdentifier));
    }

    /// <summary>デフォルト設定（最大1スタック、上書き）</summary>
    public static StackConfig Default => new(
        maxStacks: 1,
        stackBehavior: AdditiveStackBehavior.Instance,
        durationBehavior: RefreshDurationBehavior.Instance,
        sourceIdentifier: AnySourceIdentifier.Instance);
}
```

### 6.4 SnapshotConfig

```csharp
/// <summary>
/// スナップショット設定
/// </summary>
public sealed class SnapshotConfig
{
    /// <summary>スナップショットを取得するトリガー</summary>
    public ISnapshotTrigger Trigger { get; }

    /// <summary>スナップショットするキーのリスト</summary>
    public IReadOnlyList<string> SnapshotKeys { get; }

    /// <summary>スナップショット値の提供者</summary>
    public ISnapshotProvider Provider { get; }

    public SnapshotConfig(
        ISnapshotTrigger trigger,
        IReadOnlyList<string> snapshotKeys,
        ISnapshotProvider provider)
    {
        Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        SnapshotKeys = snapshotKeys ?? throw new ArgumentNullException(nameof(snapshotKeys));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>スナップショットなし</summary>
    public static SnapshotConfig None => new(
        NoSnapshotTrigger.Instance,
        Array.Empty<string>(),
        NullSnapshotProvider.Instance);
}
```

### 6.5 StatusEffectDefinition

```csharp
/// <summary>
/// 状態異常定義（マスタデータ）
/// </summary>
public sealed class StatusEffectDefinition
{
    #region Identification

    /// <summary>定義ID</summary>
    public EffectId Id { get; }

    /// <summary>内部名（デバッグ用）</summary>
    public string InternalName { get; }

    /// <summary>排他グループID</summary>
    public GroupId GroupId { get; }

    /// <summary>タグセット</summary>
    public TagSet Tags { get; }

    #endregion

    #region Timing

    /// <summary>基本持続時間</summary>
    public TickDuration BaseDuration { get; }

    /// <summary>永続フラグ</summary>
    public bool IsPermanent { get; }

    #endregion

    #region Stacking

    /// <summary>スタック設定</summary>
    public StackConfig StackConfig { get; }

    #endregion

    #region Effects

    /// <summary>効果コンポーネントリスト</summary>
    public IReadOnlyList<IEffectComponent> Components { get; }

    #endregion

    #region Interactions

    /// <summary>相互作用ルールリスト</summary>
    public IReadOnlyList<InteractionRule> InteractionRules { get; }

    #endregion

    #region Conditions

    /// <summary>付与条件</summary>
    public ICondition ApplyCondition { get; }

    /// <summary>除去条件（trueで自動除去）</summary>
    public ICondition RemoveCondition { get; }

    #endregion

    #region Snapshot

    /// <summary>スナップショット設定</summary>
    public SnapshotConfig SnapshotConfig { get; }

    #endregion

    #region Flags

    /// <summary>初期フラグセット</summary>
    public FlagSet InitialFlags { get; }

    #endregion

    #region Metadata

    /// <summary>カスタムメタデータ</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }

    #endregion

    private StatusEffectDefinition(Builder builder)
    {
        Id = builder.Id;
        InternalName = builder.InternalName;
        GroupId = builder.GroupId;
        Tags = builder.Tags;
        BaseDuration = builder.BaseDuration;
        IsPermanent = builder.IsPermanent;
        StackConfig = builder.StackConfig;
        Components = builder.Components.ToImmutableList();
        InteractionRules = builder.InteractionRules.ToImmutableList();
        ApplyCondition = builder.ApplyCondition;
        RemoveCondition = builder.RemoveCondition;
        SnapshotConfig = builder.SnapshotConfig;
        InitialFlags = builder.InitialFlags;
        Metadata = builder.Metadata.ToImmutableDictionary();
    }

    public static Builder CreateBuilder(EffectId id, string internalName)
        => new(id, internalName);

    /// <summary>
    /// StatusEffectDefinitionのビルダー
    /// </summary>
    public sealed class Builder
    {
        internal EffectId Id { get; }
        internal string InternalName { get; }
        internal GroupId GroupId { get; set; } = new(0);
        internal TagSet Tags { get; set; } = TagSet.Empty;
        internal TickDuration BaseDuration { get; set; } = new(300); // 5秒@60fps
        internal bool IsPermanent { get; set; }
        internal StackConfig StackConfig { get; set; } = StackConfig.Default;
        internal List<IEffectComponent> Components { get; } = new();
        internal List<InteractionRule> InteractionRules { get; } = new();
        internal ICondition ApplyCondition { get; set; } = AlwaysTrueCondition.Instance;
        internal ICondition RemoveCondition { get; set; } = AlwaysFalseCondition.Instance;
        internal SnapshotConfig SnapshotConfig { get; set; } = SnapshotConfig.None;
        internal FlagSet InitialFlags { get; set; } = FlagSet.Empty;
        internal Dictionary<string, object> Metadata { get; } = new();

        internal Builder(EffectId id, string internalName)
        {
            Id = id;
            InternalName = internalName ?? throw new ArgumentNullException(nameof(internalName));
        }

        public Builder WithGroupId(GroupId groupId) { GroupId = groupId; return this; }
        public Builder WithTags(TagSet tags) { Tags = tags; return this; }
        public Builder AddTag(TagId tag) { Tags = Tags.With(tag); return this; }
        public Builder WithDuration(TickDuration duration) { BaseDuration = duration; return this; }
        public Builder AsPermanent() { IsPermanent = true; BaseDuration = TickDuration.Infinite; return this; }
        public Builder WithStackConfig(StackConfig config) { StackConfig = config; return this; }
        public Builder AddComponent(IEffectComponent component) { Components.Add(component); return this; }
        public Builder AddInteractionRule(InteractionRule rule) { InteractionRules.Add(rule); return this; }
        public Builder WithApplyCondition(ICondition condition) { ApplyCondition = condition; return this; }
        public Builder WithRemoveCondition(ICondition condition) { RemoveCondition = condition; return this; }
        public Builder WithSnapshotConfig(SnapshotConfig config) { SnapshotConfig = config; return this; }
        public Builder WithInitialFlags(FlagSet flags) { InitialFlags = flags; return this; }
        public Builder WithMetadata(string key, object value) { Metadata[key] = value; return this; }

        public StatusEffectDefinition Build() => new(this);
    }
}
```

---

## 7. 実行時インスタンス

### 7.1 EffectInstance

```csharp
/// <summary>
/// 状態異常の実行時インスタンス
/// </summary>
public sealed class EffectInstance
{
    #region Identity

    /// <summary>インスタンスID</summary>
    public EffectInstanceId InstanceId { get; }

    /// <summary>定義ID</summary>
    public EffectId DefinitionId { get; }

    /// <summary>所有者エンティティID</summary>
    public ulong OwnerId { get; }

    /// <summary>付与者エンティティID</summary>
    public ulong SourceId { get; }

    #endregion

    #region Timing

    /// <summary>付与されたティック</summary>
    public GameTick AppliedAt { get; }

    /// <summary>期限切れティック</summary>
    public GameTick ExpiresAt { get; internal set; }

    /// <summary>最後にティック処理されたティック</summary>
    public GameTick LastTickedAt { get; internal set; }

    #endregion

    #region State

    /// <summary>現在のスタック数</summary>
    public int CurrentStacks { get; internal set; }

    /// <summary>フラグセット</summary>
    public FlagSet Flags { get; internal set; }

    /// <summary>有効フラグ</summary>
    public bool IsActive { get; internal set; }

    #endregion

    #region Data

    /// <summary>スナップショット値</summary>
    public IReadOnlyDictionary<string, FixedPoint> Snapshot { get; }

    /// <summary>コンポーネント状態（LocalId → 状態）</summary>
    public IReadOnlyDictionary<int, IComponentState> ComponentStates { get; }

    /// <summary>カスタムデータ</summary>
    public IReadOnlyDictionary<string, object> CustomData { get; }

    #endregion

    internal EffectInstance(
        EffectInstanceId instanceId,
        EffectId definitionId,
        ulong ownerId,
        ulong sourceId,
        GameTick appliedAt,
        GameTick expiresAt,
        int initialStacks,
        FlagSet initialFlags,
        IReadOnlyDictionary<string, FixedPoint> snapshot,
        IReadOnlyDictionary<int, IComponentState> componentStates)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        OwnerId = ownerId;
        SourceId = sourceId;
        AppliedAt = appliedAt;
        ExpiresAt = expiresAt;
        LastTickedAt = appliedAt;
        CurrentStacks = initialStacks;
        Flags = initialFlags;
        IsActive = true;
        Snapshot = snapshot;
        ComponentStates = componentStates;
        CustomData = ImmutableDictionary<string, object>.Empty;
    }

    /// <summary>残り時間を取得</summary>
    public TickDuration GetRemainingDuration(GameTick currentTick)
    {
        if (ExpiresAt.Value == long.MaxValue) return TickDuration.Infinite;
        var remaining = ExpiresAt.Value - currentTick.Value;
        return remaining <= 0 ? TickDuration.Zero : new TickDuration((int)remaining);
    }

    /// <summary>期限切れか判定</summary>
    public bool IsExpired(GameTick currentTick) => currentTick >= ExpiresAt;
}
```

### 7.2 EffectInstanceArena

HandleSystemのパターンに従ったArena実装：

```csharp
/// <summary>
/// EffectInstanceのArena（オブジェクトプール + 世代管理）
/// </summary>
public sealed class EffectInstanceArena
{
    private readonly object _lock = new();
    private EffectInstance?[] _instances;
    private int[] _generations;
    private int[] _freeIndices;
    private int _freeCount;
    private int _count;

    public object LockObject => _lock;
    public int Count => _count;
    public int Capacity => _instances.Length;

    public EffectInstanceArena(int initialCapacity = 256)
    {
        _instances = new EffectInstance?[initialCapacity];
        _generations = new int[initialCapacity];
        _freeIndices = new int[initialCapacity];

        // 全インデックスを空きリストに追加
        for (int i = initialCapacity - 1; i >= 0; i--)
        {
            _freeIndices[_freeCount++] = i;
            _generations[i] = 1; // 世代1からスタート
        }
    }

    /// <summary>新しいインスタンスを割り当て</summary>
    public EffectInstanceId Allocate(
        EffectId definitionId,
        ulong ownerId,
        ulong sourceId,
        GameTick appliedAt,
        GameTick expiresAt,
        int initialStacks,
        FlagSet initialFlags,
        IReadOnlyDictionary<string, FixedPoint> snapshot,
        IReadOnlyDictionary<int, IComponentState> componentStates)
    {
        lock (_lock)
        {
            if (_freeCount == 0)
                Grow();

            int index = _freeIndices[--_freeCount];
            int generation = _generations[index];

            var instanceId = new EffectInstanceId(index, generation);

            _instances[index] = new EffectInstance(
                instanceId,
                definitionId,
                ownerId,
                sourceId,
                appliedAt,
                expiresAt,
                initialStacks,
                initialFlags,
                snapshot,
                componentStates);

            _count++;
            return instanceId;
        }
    }

    /// <summary>インスタンスを解放</summary>
    public bool Free(EffectInstanceId id)
    {
        lock (_lock)
        {
            if (!IsValidInternal(id))
                return false;

            int index = id.Index;
            _instances[index] = null;
            _generations[index]++; // 世代をインクリメント
            _freeIndices[_freeCount++] = index;
            _count--;
            return true;
        }
    }

    /// <summary>インスタンスを取得</summary>
    public EffectInstance? Get(EffectInstanceId id)
    {
        lock (_lock)
        {
            return IsValidInternal(id) ? _instances[id.Index] : null;
        }
    }

    /// <summary>有効性チェック</summary>
    public bool IsValid(EffectInstanceId id)
    {
        lock (_lock)
        {
            return IsValidInternal(id);
        }
    }

    private bool IsValidInternal(EffectInstanceId id)
    {
        int index = id.Index;
        return index >= 0
            && index < _instances.Length
            && _generations[index] == id.Generation
            && _instances[index] != null;
    }

    private void Grow()
    {
        int newCapacity = _instances.Length * 2;
        Array.Resize(ref _instances, newCapacity);
        Array.Resize(ref _generations, newCapacity);
        Array.Resize(ref _freeIndices, newCapacity);

        for (int i = newCapacity - 1; i >= _instances.Length / 2; i--)
        {
            _freeIndices[_freeCount++] = i;
            _generations[i] = 1;
        }
    }

    /// <summary>全有効インスタンスを列挙</summary>
    public IEnumerable<EffectInstance> GetAll()
    {
        lock (_lock)
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                var instance = _instances[i];
                if (instance != null)
                    yield return instance;
            }
        }
    }
}
```

---

## 8. コンポーネントシステム

### 8.1 基本インターフェース

```csharp
/// <summary>
/// 効果コンポーネントの基本インターフェース
/// </summary>
public interface IEffectComponent
{
    /// <summary>定義内でのローカルID</summary>
    int LocalId { get; }

    /// <summary>コンポーネントタイプID</summary>
    ComponentTypeId TypeId { get; }

    /// <summary>実行条件</summary>
    ICondition Condition { get; }

    /// <summary>処理フェーズ</summary>
    PhaseId Phase { get; }

    /// <summary>フェーズ内優先度（小さいほど先に実行）</summary>
    int Priority { get; }

    /// <summary>初期状態を生成</summary>
    IComponentState CreateInitialState();
}
```

### 8.2 ライフサイクルインターフェース

```csharp
/// <summary>毎ティック実行されるコンポーネント</summary>
public interface ITickableComponent : IEffectComponent
{
    /// <summary>ティック間隔（1=毎ティック）</summary>
    int TickInterval { get; }

    /// <summary>ティック処理</summary>
    void OnTick(IEffectContext context, IComponentState state);
}

/// <summary>付与時に実行されるコンポーネント</summary>
public interface IOnApplyComponent : IEffectComponent
{
    /// <summary>付与時処理</summary>
    void OnApply(IEffectContext context, IComponentState state);
}

/// <summary>除去時に実行されるコンポーネント</summary>
public interface IOnRemoveComponent : IEffectComponent
{
    /// <summary>除去時処理</summary>
    void OnRemove(IEffectContext context, IComponentState state, RemovalReasonId reason);
}

/// <summary>イベントに反応するコンポーネント</summary>
public interface IReactiveComponent : IEffectComponent
{
    /// <summary>反応するイベントタイプのリスト</summary>
    IReadOnlyList<int> ReactsTo { get; }

    /// <summary>イベント処理</summary>
    void OnEvent(IEffectContext context, IComponentState state, IEffectEvent evt);
}

/// <summary>修正値を提供するコンポーネント</summary>
public interface IModifierComponent : IEffectComponent
{
    /// <summary>修正値を収集</summary>
    void CollectModifiers(IEffectContext context, IComponentState state, IModifierCollector collector);
}
```

### 8.3 コンポーネント状態

```csharp
/// <summary>
/// コンポーネントの可変状態
/// </summary>
public interface IComponentState
{
    /// <summary>コンポーネントのローカルID</summary>
    int ComponentLocalId { get; }

    /// <summary>状態を複製</summary>
    IComponentState Clone();
}

/// <summary>
/// 状態を持たないコンポーネント用
/// </summary>
public sealed class EmptyComponentState : IComponentState
{
    public int ComponentLocalId { get; }

    public EmptyComponentState(int localId) => ComponentLocalId = localId;

    public IComponentState Clone() => new EmptyComponentState(ComponentLocalId);

    public static EmptyComponentState For(int localId) => new(localId);
}

/// <summary>
/// ティックカウンターを持つ状態
/// </summary>
public sealed class TickCounterState : IComponentState
{
    public int ComponentLocalId { get; }
    public int TickCount { get; set; }

    public TickCounterState(int localId) => ComponentLocalId = localId;

    public IComponentState Clone() => new TickCounterState(ComponentLocalId) { TickCount = TickCount };
}
```

---

## 9. 振る舞いインターフェース

### 9.1 スタック振る舞い

```csharp
/// <summary>
/// スタック合成時のコンテキスト
/// </summary>
public readonly struct StackMergeContext
{
    public EffectInstance ExistingInstance { get; init; }
    public int IncomingStacks { get; init; }
    public int MaxStacks { get; init; }
    public ulong IncomingSourceId { get; init; }
    public GameTick CurrentTick { get; init; }
}

/// <summary>
/// スタック合成の結果
/// </summary>
public readonly struct StackMergeResult
{
    public int NewStackCount { get; init; }
    public StackMergeAction Action { get; init; }

    public static StackMergeResult Update(int newStacks)
        => new() { NewStackCount = newStacks, Action = StackMergeAction.UpdateExisting };

    public static StackMergeResult CreateNew(int stacks)
        => new() { NewStackCount = stacks, Action = StackMergeAction.CreateNew };

    public static StackMergeResult Reject()
        => new() { Action = StackMergeAction.Reject };
}

public enum StackMergeAction
{
    UpdateExisting,
    CreateNew,
    Reject
}

/// <summary>
/// スタック合成の振る舞い
/// </summary>
public interface IStackBehavior
{
    StackMergeResult Merge(in StackMergeContext context);
}

/// <summary>加算スタック</summary>
public sealed class AdditiveStackBehavior : IStackBehavior
{
    public static readonly AdditiveStackBehavior Instance = new();

    public StackMergeResult Merge(in StackMergeContext context)
    {
        int newStacks = context.ExistingInstance.CurrentStacks + context.IncomingStacks;
        if (context.MaxStacks > 0)
            newStacks = Math.Min(newStacks, context.MaxStacks);
        return StackMergeResult.Update(newStacks);
    }
}

/// <summary>独立スタック（別インスタンス）</summary>
public sealed class IndependentStackBehavior : IStackBehavior
{
    public static readonly IndependentStackBehavior Instance = new();

    public StackMergeResult Merge(in StackMergeContext context)
        => StackMergeResult.CreateNew(context.IncomingStacks);
}

/// <summary>最大値のみ維持</summary>
public sealed class HighestOnlyStackBehavior : IStackBehavior
{
    public static readonly HighestOnlyStackBehavior Instance = new();

    public StackMergeResult Merge(in StackMergeContext context)
    {
        if (context.IncomingStacks > context.ExistingInstance.CurrentStacks)
            return StackMergeResult.Update(context.IncomingStacks);
        return StackMergeResult.Reject();
    }
}
```

### 9.2 時間更新振る舞い

```csharp
/// <summary>
/// 時間更新のコンテキスト
/// </summary>
public readonly struct DurationUpdateContext
{
    public EffectInstance ExistingInstance { get; init; }
    public TickDuration IncomingDuration { get; init; }
    public GameTick CurrentTick { get; init; }
}

/// <summary>
/// 時間更新の振る舞い
/// </summary>
public interface IDurationBehavior
{
    GameTick CalculateNewExpiry(in DurationUpdateContext context);
}

/// <summary>時間をリフレッシュ（新しい時間で上書き）</summary>
public sealed class RefreshDurationBehavior : IDurationBehavior
{
    public static readonly RefreshDurationBehavior Instance = new();

    public GameTick CalculateNewExpiry(in DurationUpdateContext context)
    {
        if (context.IncomingDuration.IsInfinite)
            return new GameTick(long.MaxValue);
        return context.CurrentTick + context.IncomingDuration;
    }
}

/// <summary>時間を延長</summary>
public sealed class ExtendDurationBehavior : IDurationBehavior
{
    public static readonly ExtendDurationBehavior Instance = new();

    public GameTick CalculateNewExpiry(in DurationUpdateContext context)
    {
        if (context.ExistingInstance.ExpiresAt.Value == long.MaxValue)
            return context.ExistingInstance.ExpiresAt;
        if (context.IncomingDuration.IsInfinite)
            return new GameTick(long.MaxValue);
        return context.ExistingInstance.ExpiresAt + context.IncomingDuration;
    }
}

/// <summary>長い方を採用</summary>
public sealed class LongestDurationBehavior : IDurationBehavior
{
    public static readonly LongestDurationBehavior Instance = new();

    public GameTick CalculateNewExpiry(in DurationUpdateContext context)
    {
        var newExpiry = context.CurrentTick + context.IncomingDuration;
        return newExpiry > context.ExistingInstance.ExpiresAt
            ? newExpiry
            : context.ExistingInstance.ExpiresAt;
    }
}
```

### 9.3 スタック源識別

```csharp
/// <summary>
/// スタック源の識別
/// </summary>
public interface IStackSourceIdentifier
{
    bool IsSameSource(EffectInstance existing, ulong incomingSourceId);
}

/// <summary>どのソースでも同一扱い</summary>
public sealed class AnySourceIdentifier : IStackSourceIdentifier
{
    public static readonly AnySourceIdentifier Instance = new();

    public bool IsSameSource(EffectInstance existing, ulong incomingSourceId) => true;
}

/// <summary>ソースごとに独立</summary>
public sealed class PerSourceIdentifier : IStackSourceIdentifier
{
    public static readonly PerSourceIdentifier Instance = new();

    public bool IsSameSource(EffectInstance existing, ulong incomingSourceId)
        => existing.SourceId == incomingSourceId;
}
```

---

## 10. 条件システム

### 10.1 インターフェース

```csharp
/// <summary>
/// 条件評価コンテキスト
/// </summary>
public interface IConditionContext
{
    /// <summary>効果の付与者</summary>
    ulong SourceId { get; }

    /// <summary>効果の対象者</summary>
    ulong TargetId { get; }

    /// <summary>現在のティック</summary>
    GameTick CurrentTick { get; }

    /// <summary>現在のスタック数</summary>
    int CurrentStacks { get; }

    /// <summary>拡張データを取得</summary>
    T? GetExtension<T>() where T : class;
}

/// <summary>
/// 条件インターフェース
/// </summary>
public interface ICondition
{
    /// <summary>条件を評価</summary>
    bool Evaluate(IConditionContext context);

    /// <summary>条件の説明を取得</summary>
    string GetDescription();
}
```

### 10.2 基本条件

```csharp
/// <summary>常にtrue</summary>
public sealed class AlwaysTrueCondition : ICondition
{
    public static readonly AlwaysTrueCondition Instance = new();

    public bool Evaluate(IConditionContext context) => true;
    public string GetDescription() => "Always true";
}

/// <summary>常にfalse</summary>
public sealed class AlwaysFalseCondition : ICondition
{
    public static readonly AlwaysFalseCondition Instance = new();

    public bool Evaluate(IConditionContext context) => false;
    public string GetDescription() => "Always false";
}

/// <summary>確率条件</summary>
public sealed class ProbabilityCondition : ICondition
{
    private readonly int _probability; // 0-10000 (0.00%-100.00%)
    private readonly IRandom _random;

    public ProbabilityCondition(int probability, IRandom random)
    {
        _probability = Math.Clamp(probability, 0, 10000);
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public bool Evaluate(IConditionContext context)
        => _random.Next(10000) < _probability;

    public string GetDescription()
        => $"Probability: {_probability / 100.0:F2}%";
}

/// <summary>スタック数条件</summary>
public sealed class StackCountCondition : ICondition
{
    private readonly ComparisonOperator _operator;
    private readonly int _threshold;

    public StackCountCondition(ComparisonOperator op, int threshold)
    {
        _operator = op;
        _threshold = threshold;
    }

    public bool Evaluate(IConditionContext context)
    {
        return _operator switch
        {
            ComparisonOperator.Equal => context.CurrentStacks == _threshold,
            ComparisonOperator.NotEqual => context.CurrentStacks != _threshold,
            ComparisonOperator.LessThan => context.CurrentStacks < _threshold,
            ComparisonOperator.LessThanOrEqual => context.CurrentStacks <= _threshold,
            ComparisonOperator.GreaterThan => context.CurrentStacks > _threshold,
            ComparisonOperator.GreaterThanOrEqual => context.CurrentStacks >= _threshold,
            _ => false
        };
    }

    public string GetDescription()
        => $"Stacks {_operator} {_threshold}";
}

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual
}
```

### 10.3 合成条件

```csharp
/// <summary>AND条件</summary>
public sealed class AndCondition : ICondition
{
    private readonly IReadOnlyList<ICondition> _conditions;

    public AndCondition(params ICondition[] conditions)
    {
        _conditions = conditions?.ToList() ?? throw new ArgumentNullException(nameof(conditions));
    }

    public bool Evaluate(IConditionContext context)
    {
        foreach (var condition in _conditions)
        {
            if (!condition.Evaluate(context))
                return false;
        }
        return true;
    }

    public string GetDescription()
        => $"({string.Join(" AND ", _conditions.Select(c => c.GetDescription()))})";
}

/// <summary>OR条件</summary>
public sealed class OrCondition : ICondition
{
    private readonly IReadOnlyList<ICondition> _conditions;

    public OrCondition(params ICondition[] conditions)
    {
        _conditions = conditions?.ToList() ?? throw new ArgumentNullException(nameof(conditions));
    }

    public bool Evaluate(IConditionContext context)
    {
        foreach (var condition in _conditions)
        {
            if (condition.Evaluate(context))
                return true;
        }
        return false;
    }

    public string GetDescription()
        => $"({string.Join(" OR ", _conditions.Select(c => c.GetDescription()))})";
}

/// <summary>NOT条件</summary>
public sealed class NotCondition : ICondition
{
    private readonly ICondition _condition;

    public NotCondition(ICondition condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public bool Evaluate(IConditionContext context)
        => !_condition.Evaluate(context);

    public string GetDescription()
        => $"NOT ({_condition.GetDescription()})";
}
```

---

## 11. 値ソース

### 11.1 インターフェース

```csharp
/// <summary>
/// 固定小数点数（決定論的計算用）
/// </summary>
public readonly struct FixedPoint : IEquatable<FixedPoint>, IComparable<FixedPoint>
{
    private const int SCALE = 10000;

    public readonly long RawValue;

    private FixedPoint(long rawValue) => RawValue = rawValue;

    public static FixedPoint FromInt(int value) => new(value * SCALE);
    public static FixedPoint FromFloat(float value) => new((long)(value * SCALE));
    public static FixedPoint FromRaw(long rawValue) => new(rawValue);

    public int ToInt() => (int)(RawValue / SCALE);
    public float ToFloat() => RawValue / (float)SCALE;

    public static FixedPoint operator +(FixedPoint a, FixedPoint b) => new(a.RawValue + b.RawValue);
    public static FixedPoint operator -(FixedPoint a, FixedPoint b) => new(a.RawValue - b.RawValue);
    public static FixedPoint operator *(FixedPoint a, FixedPoint b) => new(a.RawValue * b.RawValue / SCALE);
    public static FixedPoint operator /(FixedPoint a, FixedPoint b) => new(a.RawValue * SCALE / b.RawValue);

    public static bool operator <(FixedPoint a, FixedPoint b) => a.RawValue < b.RawValue;
    public static bool operator >(FixedPoint a, FixedPoint b) => a.RawValue > b.RawValue;
    public static bool operator <=(FixedPoint a, FixedPoint b) => a.RawValue <= b.RawValue;
    public static bool operator >=(FixedPoint a, FixedPoint b) => a.RawValue >= b.RawValue;

    public bool Equals(FixedPoint other) => RawValue == other.RawValue;
    public int CompareTo(FixedPoint other) => RawValue.CompareTo(other.RawValue);

    public static readonly FixedPoint Zero = new(0);
    public static readonly FixedPoint One = FromInt(1);

    public override string ToString() => ToFloat().ToString("F4");
}

/// <summary>
/// 値ソースインターフェース
/// </summary>
public interface IValueSource
{
    FixedPoint Evaluate(IEffectContext context);
}
```

### 11.2 基本実装

```csharp
/// <summary>定数値</summary>
public sealed class ConstantValue : IValueSource
{
    private readonly FixedPoint _value;

    public ConstantValue(FixedPoint value) => _value = value;
    public ConstantValue(int value) => _value = FixedPoint.FromInt(value);
    public ConstantValue(float value) => _value = FixedPoint.FromFloat(value);

    public FixedPoint Evaluate(IEffectContext context) => _value;
}

/// <summary>スタック数に応じてスケール</summary>
public sealed class StackScaledValue : IValueSource
{
    private readonly FixedPoint _baseValue;
    private readonly FixedPoint _perStackValue;

    public StackScaledValue(FixedPoint baseValue, FixedPoint perStackValue)
    {
        _baseValue = baseValue;
        _perStackValue = perStackValue;
    }

    public FixedPoint Evaluate(IEffectContext context)
    {
        var stacks = FixedPoint.FromInt(context.CurrentStacks);
        return _baseValue + _perStackValue * stacks;
    }
}

/// <summary>スナップショット値を参照</summary>
public sealed class SnapshotAwareValue : IValueSource
{
    private readonly string _snapshotKey;
    private readonly FixedPoint _fallback;

    public SnapshotAwareValue(string snapshotKey, FixedPoint fallback = default)
    {
        _snapshotKey = snapshotKey ?? throw new ArgumentNullException(nameof(snapshotKey));
        _fallback = fallback;
    }

    public FixedPoint Evaluate(IEffectContext context)
    {
        return context.TryGetSnapshot(_snapshotKey, out var value) ? value : _fallback;
    }
}

/// <summary>条件分岐値</summary>
public sealed class ConditionalValue : IValueSource
{
    private readonly ICondition _condition;
    private readonly IValueSource _trueValue;
    private readonly IValueSource _falseValue;

    public ConditionalValue(ICondition condition, IValueSource trueValue, IValueSource falseValue)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _trueValue = trueValue ?? throw new ArgumentNullException(nameof(trueValue));
        _falseValue = falseValue ?? throw new ArgumentNullException(nameof(falseValue));
    }

    public FixedPoint Evaluate(IEffectContext context)
    {
        return _condition.Evaluate(context)
            ? _trueValue.Evaluate(context)
            : _falseValue.Evaluate(context);
    }
}
```

---

## 12. フィルタパイプライン

### 12.1 インターフェース

```csharp
/// <summary>
/// 付与フィルタコンテキスト
/// </summary>
public sealed class ApplyFilterContext
{
    public StatusEffectDefinition Definition { get; init; }
    public ulong TargetId { get; init; }
    public ulong SourceId { get; init; }
    public GameTick CurrentTick { get; init; }
    public TickDuration RequestedDuration { get; set; }
    public int RequestedStacks { get; set; }
    public FlagSet RequestedFlags { get; set; }
    public IReadOnlyList<EffectInstance> ExistingEffects { get; init; }

    /// <summary>拡張データを取得</summary>
    public T? GetExtension<T>() where T : class => Extensions?.GetValueOrDefault(typeof(T)) as T;

    internal Dictionary<Type, object>? Extensions { get; set; }
}

/// <summary>
/// フィルタ結果
/// </summary>
public readonly struct ApplyFilterResult
{
    public ApplyFilterAction Action { get; init; }
    public FailureReasonId FailureReason { get; init; }

    public static ApplyFilterResult Continue()
        => new() { Action = ApplyFilterAction.Continue };

    public static ApplyFilterResult Reject(FailureReasonId reason)
        => new() { Action = ApplyFilterAction.Reject, FailureReason = reason };
}

public enum ApplyFilterAction
{
    Continue,
    Reject
}

/// <summary>
/// 付与フィルタインターフェース
/// </summary>
public interface IApplyFilter
{
    /// <summary>実行順序（小さいほど先に実行）</summary>
    int Order { get; }

    /// <summary>フィルタを実行</summary>
    ApplyFilterResult Filter(ApplyFilterContext context);
}
```

### 12.2 パイプライン

```csharp
/// <summary>
/// 付与フィルタパイプライン
/// </summary>
public sealed class ApplyFilterPipeline
{
    private readonly List<IApplyFilter> _filters = new();
    private bool _sorted = false;

    public void AddFilter(IApplyFilter filter)
    {
        _filters.Add(filter ?? throw new ArgumentNullException(nameof(filter)));
        _sorted = false;
    }

    public void RemoveFilter(IApplyFilter filter)
    {
        _filters.Remove(filter);
    }

    public ApplyFilterResult Execute(ApplyFilterContext context)
    {
        EnsureSorted();

        foreach (var filter in _filters)
        {
            var result = filter.Filter(context);
            if (result.Action == ApplyFilterAction.Reject)
                return result;
        }

        return ApplyFilterResult.Continue();
    }

    private void EnsureSorted()
    {
        if (_sorted) return;
        _filters.Sort((a, b) => a.Order.CompareTo(b.Order));
        _sorted = true;
    }
}
```

### 12.3 基本フィルタ

```csharp
/// <summary>
/// 特定タグを持つ場合に拒否
/// </summary>
public sealed class TagRejectFilter : IApplyFilter
{
    private readonly TagSet _rejectTags;
    private readonly FailureReasonId _failureReason;

    public int Order { get; }

    public TagRejectFilter(TagSet rejectTags, FailureReasonId failureReason, int order = 0)
    {
        _rejectTags = rejectTags;
        _failureReason = failureReason;
        Order = order;
    }

    public ApplyFilterResult Filter(ApplyFilterContext context)
    {
        if (context.Definition.Tags.ContainsAny(_rejectTags))
            return ApplyFilterResult.Reject(_failureReason);
        return ApplyFilterResult.Continue();
    }
}

/// <summary>
/// 最大インスタンス数制限
/// </summary>
public sealed class MaxInstancesFilter : IApplyFilter
{
    private readonly int _maxInstances;

    public int Order { get; }

    public MaxInstancesFilter(int maxInstances, int order = 100)
    {
        _maxInstances = maxInstances;
        Order = order;
    }

    public ApplyFilterResult Filter(ApplyFilterContext context)
    {
        int count = 0;
        foreach (var existing in context.ExistingEffects)
        {
            if (existing.DefinitionId == context.Definition.Id)
                count++;
        }

        if (count >= _maxInstances)
            return ApplyFilterResult.Reject(FailureReasonId.MaxInstancesReached);

        return ApplyFilterResult.Continue();
    }
}

/// <summary>
/// 排他グループフィルタ
/// </summary>
public sealed class ExclusiveGroupFilter : IApplyFilter
{
    public int Order => 50;

    public ApplyFilterResult Filter(ApplyFilterContext context)
    {
        var groupId = context.Definition.GroupId;
        if (groupId.Value == 0) // グループなし
            return ApplyFilterResult.Continue();

        foreach (var existing in context.ExistingEffects)
        {
            // 同一グループの効果が存在する場合、既存の方が優先
            // （除去と新規付与は別途EffectManagerで処理）
        }

        return ApplyFilterResult.Continue();
    }
}
```

---

## 13. 相互作用システム

### 13.1 ルール定義

```csharp
/// <summary>
/// 効果マッチャー
/// </summary>
public sealed class EffectMatcher
{
    /// <summary>特定の効果IDにマッチ（nullで任意）</summary>
    public EffectId? EffectId { get; init; }

    /// <summary>必要なタグ（nullで任意）</summary>
    public TagSet? RequiredTags { get; init; }

    /// <summary>タグマッチモード</summary>
    public TagMatchMode TagMatchMode { get; init; } = TagMatchMode.Any;

    public bool Matches(StatusEffectDefinition definition)
    {
        if (EffectId.HasValue && definition.Id != EffectId.Value)
            return false;

        if (RequiredTags.HasValue)
        {
            return TagMatchMode switch
            {
                TagMatchMode.Any => definition.Tags.ContainsAny(RequiredTags.Value),
                TagMatchMode.All => definition.Tags.ContainsAll(RequiredTags.Value),
                _ => false
            };
        }

        return true;
    }
}

public enum TagMatchMode
{
    Any,
    All
}

/// <summary>
/// 相互作用トリガー
/// </summary>
public sealed class InteractionTrigger
{
    /// <summary>トリガーとなる効果（新規付与される効果）</summary>
    public EffectMatcher TriggeringEffect { get; init; }

    /// <summary>既存の効果</summary>
    public EffectMatcher ExistingEffect { get; init; }
}

/// <summary>
/// 相互作用による消費設定
/// </summary>
public sealed class InteractionConsumption
{
    /// <summary>トリガー効果を消費するか</summary>
    public bool ConsumeTriggeringEffect { get; init; }

    /// <summary>既存効果を消費するか</summary>
    public bool ConsumeExistingEffect { get; init; }

    /// <summary>トリガー効果の消費量</summary>
    public IValueSource? TriggeringConsumeAmount { get; init; }

    /// <summary>既存効果の消費量</summary>
    public IValueSource? ExistingConsumeAmount { get; init; }

    public static InteractionConsumption None => new();
    public static InteractionConsumption ConsumeBoth => new()
    {
        ConsumeTriggeringEffect = true,
        ConsumeExistingEffect = true
    };
}

/// <summary>
/// 相互作用ルール
/// </summary>
public sealed class InteractionRule
{
    public InteractionRuleId Id { get; }
    public string InternalName { get; }
    public InteractionTrigger Trigger { get; }
    public IReadOnlyList<IInteractionEffect> Effects { get; }
    public int Priority { get; }
    public InteractionConsumption Consumption { get; }
    public ICondition AdditionalCondition { get; }

    public InteractionRule(
        InteractionRuleId id,
        string internalName,
        InteractionTrigger trigger,
        IReadOnlyList<IInteractionEffect> effects,
        int priority = 0,
        InteractionConsumption? consumption = null,
        ICondition? additionalCondition = null)
    {
        Id = id;
        InternalName = internalName ?? throw new ArgumentNullException(nameof(internalName));
        Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        Priority = priority;
        Consumption = consumption ?? InteractionConsumption.None;
        AdditionalCondition = additionalCondition ?? AlwaysTrueCondition.Instance;
    }
}
```

### 13.2 相互作用効果

```csharp
/// <summary>
/// 相互作用コンテキスト
/// </summary>
public sealed class InteractionContext
{
    public EffectInstance? TriggeringInstance { get; init; }
    public EffectInstance ExistingInstance { get; init; }
    public StatusEffectDefinition TriggeringDefinition { get; init; }
    public StatusEffectDefinition ExistingDefinition { get; init; }
    public ulong TargetId { get; init; }
    public GameTick CurrentTick { get; init; }

    internal IEffectManager Manager { get; init; }

    /// <summary>新しい効果を付与</summary>
    public void ApplyEffect(EffectId effectId, ulong targetId, ulong sourceId)
    {
        Manager.TryApply(targetId, effectId, sourceId);
    }

    /// <summary>効果を除去</summary>
    public void RemoveEffect(EffectInstanceId instanceId, RemovalReasonId reason)
    {
        Manager.Remove(instanceId, reason);
    }

    /// <summary>スタックを変更</summary>
    public void ModifyStacks(EffectInstanceId instanceId, int delta)
    {
        Manager.AddStacks(instanceId, delta);
    }
}

/// <summary>
/// 相互作用効果インターフェース
/// </summary>
public interface IInteractionEffect
{
    ICondition Condition { get; }
    void Execute(InteractionContext context);
}

/// <summary>
/// 効果付与相互作用
/// </summary>
public sealed class ApplyEffectInteraction : IInteractionEffect
{
    private readonly EffectId _effectId;
    private readonly bool _useExistingTarget;

    public ICondition Condition { get; }

    public ApplyEffectInteraction(
        EffectId effectId,
        bool useExistingTarget = true,
        ICondition? condition = null)
    {
        _effectId = effectId;
        _useExistingTarget = useExistingTarget;
        Condition = condition ?? AlwaysTrueCondition.Instance;
    }

    public void Execute(InteractionContext context)
    {
        var targetId = _useExistingTarget
            ? context.ExistingInstance.OwnerId
            : context.TriggeringInstance?.OwnerId ?? context.TargetId;
        var sourceId = context.TriggeringInstance?.SourceId ?? context.ExistingInstance.SourceId;

        context.ApplyEffect(_effectId, targetId, sourceId);
    }
}

/// <summary>
/// 効果除去相互作用
/// </summary>
public sealed class RemoveEffectInteraction : IInteractionEffect
{
    private readonly bool _removeExisting;
    private readonly RemovalReasonId _reason;

    public ICondition Condition { get; }

    public RemoveEffectInteraction(
        bool removeExisting,
        RemovalReasonId reason,
        ICondition? condition = null)
    {
        _removeExisting = removeExisting;
        _reason = reason;
        Condition = condition ?? AlwaysTrueCondition.Instance;
    }

    public void Execute(InteractionContext context)
    {
        var instanceId = _removeExisting
            ? context.ExistingInstance.InstanceId
            : context.TriggeringInstance?.InstanceId ?? default;

        if (instanceId.IsValid)
            context.RemoveEffect(instanceId, _reason);
    }
}
```

---

## 14. 除去戦略

### 14.1 インターフェース

```csharp
/// <summary>
/// 除去戦略コンテキスト
/// </summary>
public sealed class RemovalStrategyContext
{
    public ulong TargetId { get; init; }
    public ulong SourceId { get; init; }
    public IReadOnlyList<EffectInstance> CandidateEffects { get; init; }
    public GameTick CurrentTick { get; init; }
    public IEffectManager Manager { get; init; }
    public IEffectRegistry Registry { get; init; }
}

/// <summary>
/// 除去結果
/// </summary>
public readonly struct RemovalResult
{
    public IReadOnlyList<EffectInstance> RemovedEffects { get; init; }
    public int RemovedCount => RemovedEffects?.Count ?? 0;

    public static RemovalResult Empty => new() { RemovedEffects = Array.Empty<EffectInstance>() };
}

/// <summary>
/// 除去戦略インターフェース
/// </summary>
public interface IRemovalStrategy
{
    /// <summary>除去対象を選択</summary>
    IEnumerable<EffectInstance> SelectTargets(RemovalStrategyContext context);

    /// <summary>除去後の後処理</summary>
    void PostProcess(IReadOnlyList<EffectInstance> removed, RemovalStrategyContext context);

    /// <summary>除去理由</summary>
    RemovalReasonId Reason { get; }
}

/// <summary>
/// 除去対象の選択順序
/// </summary>
public interface IRemovalSelector
{
    IEnumerable<EffectInstance> Select(IEnumerable<EffectInstance> candidates, int maxCount);
}
```

### 14.2 基本実装

```csharp
/// <summary>
/// タグベースの除去戦略（浄化など）
/// </summary>
public sealed class TagBasedRemovalStrategy : IRemovalStrategy
{
    private readonly TagSet _targetTags;
    private readonly int _maxCount;
    private readonly IRemovalSelector _selector;

    public RemovalReasonId Reason { get; }

    public TagBasedRemovalStrategy(
        TagSet targetTags,
        RemovalReasonId reason,
        int maxCount = int.MaxValue,
        IRemovalSelector? selector = null)
    {
        _targetTags = targetTags;
        Reason = reason;
        _maxCount = maxCount;
        _selector = selector ?? NewestFirstSelector.Instance;
    }

    public IEnumerable<EffectInstance> SelectTargets(RemovalStrategyContext context)
    {
        var candidates = context.CandidateEffects
            .Where(e =>
            {
                var def = context.Registry.Get(e.DefinitionId);
                return def != null && def.Tags.ContainsAny(_targetTags);
            });

        return _selector.Select(candidates, _maxCount);
    }

    public void PostProcess(IReadOnlyList<EffectInstance> removed, RemovalStrategyContext context)
    {
        // 浄化は後処理なし
    }
}

/// <summary>
/// 奪取戦略
/// </summary>
public sealed class StealStrategy : IRemovalStrategy
{
    private readonly TagSet _targetTags;
    private readonly int _maxCount;
    private readonly IRemovalSelector _selector;

    public RemovalReasonId Reason { get; }

    public StealStrategy(
        TagSet targetTags,
        RemovalReasonId reason,
        int maxCount = 1,
        IRemovalSelector? selector = null)
    {
        _targetTags = targetTags;
        Reason = reason;
        _maxCount = maxCount;
        _selector = selector ?? NewestFirstSelector.Instance;
    }

    public IEnumerable<EffectInstance> SelectTargets(RemovalStrategyContext context)
    {
        var candidates = context.CandidateEffects
            .Where(e =>
            {
                var def = context.Registry.Get(e.DefinitionId);
                return def != null && def.Tags.ContainsAny(_targetTags);
            });

        return _selector.Select(candidates, _maxCount);
    }

    public void PostProcess(IReadOnlyList<EffectInstance> removed, RemovalStrategyContext context)
    {
        // 除去した効果を自分に付与
        foreach (var effect in removed)
        {
            context.Manager.TryApply(
                context.SourceId,
                effect.DefinitionId,
                context.SourceId,
                new ApplyOptions
                {
                    InitialStacks = effect.CurrentStacks,
                    Duration = effect.GetRemainingDuration(context.CurrentTick)
                });
        }
    }
}

/// <summary>新しい順に選択</summary>
public sealed class NewestFirstSelector : IRemovalSelector
{
    public static readonly NewestFirstSelector Instance = new();

    public IEnumerable<EffectInstance> Select(IEnumerable<EffectInstance> candidates, int maxCount)
    {
        return candidates
            .OrderByDescending(e => e.AppliedAt.Value)
            .Take(maxCount);
    }
}

/// <summary>古い順に選択</summary>
public sealed class OldestFirstSelector : IRemovalSelector
{
    public static readonly OldestFirstSelector Instance = new();

    public IEnumerable<EffectInstance> Select(IEnumerable<EffectInstance> candidates, int maxCount)
    {
        return candidates
            .OrderBy(e => e.AppliedAt.Value)
            .Take(maxCount);
    }
}

/// <summary>スタック数が多い順に選択</summary>
public sealed class HighestStacksFirstSelector : IRemovalSelector
{
    public static readonly HighestStacksFirstSelector Instance = new();

    public IEnumerable<EffectInstance> Select(IEnumerable<EffectInstance> candidates, int maxCount)
    {
        return candidates
            .OrderByDescending(e => e.CurrentStacks)
            .Take(maxCount);
    }
}
```

---

## 15. Modifier計算システム

### 15.1 インターフェース

```csharp
/// <summary>
/// 修正値
/// </summary>
public readonly struct Modifier
{
    /// <summary>対象のステータスID（ユーザー定義）</summary>
    public int TargetStatId { get; init; }

    /// <summary>処理フェーズ</summary>
    public PhaseId Phase { get; init; }

    /// <summary>フェーズ内優先度</summary>
    public int Priority { get; init; }

    /// <summary>修正タイプ（ユーザー定義）</summary>
    public int ModifierTypeId { get; init; }

    /// <summary>修正値</summary>
    public FixedPoint Value { get; init; }

    /// <summary>ソースのインスタンスID</summary>
    public EffectInstanceId SourceInstanceId { get; init; }
}

/// <summary>
/// 修正値コレクター
/// </summary>
public interface IModifierCollector
{
    void Add(Modifier modifier);
}

/// <summary>
/// 計算ステップ
/// </summary>
public interface ICalculationStep
{
    PhaseId Phase { get; }
    FixedPoint Execute(FixedPoint currentValue, IReadOnlyList<Modifier> modifiers);
}

/// <summary>
/// 計算パイプライン
/// </summary>
public interface ICalculationPipeline
{
    void AddStep(ICalculationStep step);
    FixedPoint Calculate(FixedPoint baseValue, IReadOnlyList<Modifier> modifiers);
}
```

### 15.2 計算ステップ実装

```csharp
/// <summary>
/// 加算ステップ
/// </summary>
public sealed class FlatAdditionStep : ICalculationStep
{
    private readonly int _modifierTypeId;

    public PhaseId Phase { get; }

    public FlatAdditionStep(PhaseId phase, int modifierTypeId)
    {
        Phase = phase;
        _modifierTypeId = modifierTypeId;
    }

    public FixedPoint Execute(FixedPoint currentValue, IReadOnlyList<Modifier> modifiers)
    {
        var sum = FixedPoint.Zero;
        foreach (var mod in modifiers)
        {
            if (mod.ModifierTypeId == _modifierTypeId)
                sum = sum + mod.Value;
        }
        return currentValue + sum;
    }
}

/// <summary>
/// 加算パーセントステップ（+10%, +20% → +30%）
/// </summary>
public sealed class PercentAdditiveStep : ICalculationStep
{
    private readonly int _modifierTypeId;

    public PhaseId Phase { get; }

    public PercentAdditiveStep(PhaseId phase, int modifierTypeId)
    {
        Phase = phase;
        _modifierTypeId = modifierTypeId;
    }

    public FixedPoint Execute(FixedPoint currentValue, IReadOnlyList<Modifier> modifiers)
    {
        var sum = FixedPoint.Zero;
        foreach (var mod in modifiers)
        {
            if (mod.ModifierTypeId == _modifierTypeId)
                sum = sum + mod.Value;
        }
        // sum はパーセンテージ（100 = 100%）
        return currentValue * (FixedPoint.One + sum / FixedPoint.FromInt(100));
    }
}

/// <summary>
/// 乗算パーセントステップ（×1.1 ×1.2 → ×1.32）
/// </summary>
public sealed class PercentMultiplicativeStep : ICalculationStep
{
    private readonly int _modifierTypeId;

    public PhaseId Phase { get; }

    public PercentMultiplicativeStep(PhaseId phase, int modifierTypeId)
    {
        Phase = phase;
        _modifierTypeId = modifierTypeId;
    }

    public FixedPoint Execute(FixedPoint currentValue, IReadOnlyList<Modifier> modifiers)
    {
        var result = currentValue;
        foreach (var mod in modifiers)
        {
            if (mod.ModifierTypeId == _modifierTypeId)
            {
                var multiplier = FixedPoint.One + mod.Value / FixedPoint.FromInt(100);
                result = result * multiplier;
            }
        }
        return result;
    }
}
```

---

## 16. EffectManager

### 16.1 インターフェース

```csharp
/// <summary>
/// 付与オプション
/// </summary>
public sealed class ApplyOptions
{
    public int InitialStacks { get; init; } = 1;
    public TickDuration? Duration { get; init; }
    public FlagSet? InitialFlags { get; init; }
    public IReadOnlyDictionary<string, object>? CustomData { get; init; }
}

/// <summary>
/// 付与結果
/// </summary>
public readonly struct ApplyResult
{
    public bool Success { get; init; }
    public EffectInstanceId InstanceId { get; init; }
    public FailureReasonId FailureReason { get; init; }
    public bool WasMerged { get; init; }

    public static ApplyResult Succeeded(EffectInstanceId instanceId, bool wasMerged = false)
        => new() { Success = true, InstanceId = instanceId, WasMerged = wasMerged };

    public static ApplyResult Failed(FailureReasonId reason)
        => new() { Success = false, FailureReason = reason };
}

/// <summary>
/// 効果マネージャーインターフェース
/// </summary>
public interface IEffectManager
{
    #region Apply

    /// <summary>効果を付与</summary>
    ApplyResult TryApply(ulong targetId, EffectId effectId, ulong sourceId, ApplyOptions? options = null);

    #endregion

    #region Remove

    /// <summary>インスタンスを除去</summary>
    void Remove(EffectInstanceId instanceId, RemovalReasonId reason);

    /// <summary>対象の全効果を除去</summary>
    void RemoveAll(ulong targetId, RemovalReasonId reason);

    /// <summary>タグで効果を除去</summary>
    int RemoveByTag(ulong targetId, TagId tag, RemovalReasonId reason);

    /// <summary>戦略に基づいて除去</summary>
    RemovalResult RemoveWithStrategy(ulong targetId, IRemovalStrategy strategy, ulong sourceId);

    #endregion

    #region Modify

    /// <summary>スタックを追加</summary>
    void AddStacks(EffectInstanceId instanceId, int count);

    /// <summary>スタックを設定</summary>
    void SetStacks(EffectInstanceId instanceId, int count);

    /// <summary>時間を延長</summary>
    void ExtendDuration(EffectInstanceId instanceId, TickDuration extension);

    /// <summary>フラグを設定</summary>
    void SetFlag(EffectInstanceId instanceId, FlagId flag, bool value);

    #endregion

    #region Query

    /// <summary>インスタンスを取得</summary>
    EffectInstance? GetInstance(EffectInstanceId instanceId);

    /// <summary>対象の全効果を取得</summary>
    IEnumerable<EffectInstance> GetEffects(ulong targetId);

    /// <summary>タグで効果を取得</summary>
    IEnumerable<EffectInstance> GetEffectsByTag(ulong targetId, TagId tag);

    /// <summary>フラグで効果を取得</summary>
    IEnumerable<EffectInstance> GetEffectsByFlag(ulong targetId, FlagId flag);

    /// <summary>効果を持っているか</summary>
    bool HasEffect(ulong targetId, EffectId effectId);

    /// <summary>タグを持つ効果があるか</summary>
    bool HasEffectWithTag(ulong targetId, TagId tag);

    #endregion

    #region Tick

    /// <summary>ティック処理</summary>
    void ProcessTick(GameTick currentTick);

    #endregion

    #region Events

    event Action<EffectAppliedEvent>? OnEffectApplied;
    event Action<EffectRemovedEvent>? OnEffectRemoved;
    event Action<StackChangedEvent>? OnStackChanged;
    event Action<EffectTickedEvent>? OnEffectTicked;

    #endregion
}
```

### 16.2 イベント

```csharp
public readonly struct EffectAppliedEvent
{
    public EffectInstanceId InstanceId { get; init; }
    public EffectId DefinitionId { get; init; }
    public ulong TargetId { get; init; }
    public ulong SourceId { get; init; }
    public GameTick AppliedAt { get; init; }
    public int InitialStacks { get; init; }
    public bool WasMerged { get; init; }
}

public readonly struct EffectRemovedEvent
{
    public EffectInstanceId InstanceId { get; init; }
    public EffectId DefinitionId { get; init; }
    public ulong TargetId { get; init; }
    public RemovalReasonId Reason { get; init; }
    public GameTick RemovedAt { get; init; }
    public int FinalStacks { get; init; }
}

public readonly struct StackChangedEvent
{
    public EffectInstanceId InstanceId { get; init; }
    public int OldStacks { get; init; }
    public int NewStacks { get; init; }
    public GameTick ChangedAt { get; init; }
}

public readonly struct EffectTickedEvent
{
    public EffectInstanceId InstanceId { get; init; }
    public GameTick TickedAt { get; init; }
}
```

---

## 17. レジストリ

### 17.1 効果レジストリ

```csharp
/// <summary>
/// 効果定義レジストリ
/// </summary>
public interface IEffectRegistry
{
    void Register(StatusEffectDefinition definition);
    StatusEffectDefinition? Get(EffectId id);
    bool Contains(EffectId id);
    IEnumerable<StatusEffectDefinition> GetAll();
}

public sealed class EffectRegistry : IEffectRegistry
{
    private readonly Dictionary<EffectId, StatusEffectDefinition> _definitions = new();
    private readonly object _lock = new();

    public void Register(StatusEffectDefinition definition)
    {
        lock (_lock)
        {
            if (_definitions.ContainsKey(definition.Id))
                throw new ArgumentException($"Effect {definition.Id} already registered");
            _definitions[definition.Id] = definition;
        }
    }

    public StatusEffectDefinition? Get(EffectId id)
    {
        lock (_lock)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }
    }

    public bool Contains(EffectId id)
    {
        lock (_lock)
        {
            return _definitions.ContainsKey(id);
        }
    }

    public IEnumerable<StatusEffectDefinition> GetAll()
    {
        lock (_lock)
        {
            return _definitions.Values.ToList();
        }
    }
}
```

### 17.2 タグレジストリ

```csharp
/// <summary>
/// タグレジストリ
/// </summary>
public interface ITagRegistry
{
    void Register(TagDefinition definition);
    TagDefinition? Get(TagId id);
    TagId? GetByName(string name);
    TagSet CreateSet(params TagId[] tags);
    bool IsDescendantOf(TagId tag, TagId ancestor);
}

public sealed class TagRegistry : ITagRegistry
{
    private readonly Dictionary<TagId, TagDefinition> _definitions = new();
    private readonly Dictionary<string, TagId> _nameToId = new();
    private readonly object _lock = new();

    public void Register(TagDefinition definition)
    {
        lock (_lock)
        {
            if (_definitions.ContainsKey(definition.Id))
                throw new ArgumentException($"Tag {definition.Id} already registered");

            _definitions[definition.Id] = definition;
            _nameToId[definition.Name] = definition.Id;
        }
    }

    public TagDefinition? Get(TagId id)
    {
        lock (_lock)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }
    }

    public TagId? GetByName(string name)
    {
        lock (_lock)
        {
            return _nameToId.TryGetValue(name, out var id) ? id : null;
        }
    }

    public TagSet CreateSet(params TagId[] tags)
    {
        var set = TagSet.Empty;
        foreach (var tag in tags)
            set = set.With(tag);
        return set;
    }

    public bool IsDescendantOf(TagId tag, TagId ancestor)
    {
        lock (_lock)
        {
            var current = tag;
            while (_definitions.TryGetValue(current, out var def))
            {
                if (!def.ParentId.HasValue)
                    return false;
                if (def.ParentId.Value == ancestor)
                    return true;
                current = def.ParentId.Value;
            }
            return false;
        }
    }
}
```

---

## 18. システム統合

### 18.1 StatusEffectSystem

```csharp
/// <summary>
/// StatusEffectSystem設定
/// </summary>
public sealed class StatusEffectSystemConfig
{
    /// <summary>インスタンスArenaの初期容量</summary>
    public int InitialInstanceCapacity { get; init; } = 1024;

    /// <summary>エンティティあたりの最大効果数</summary>
    public int MaxEffectsPerEntity { get; init; } = 64;

    /// <summary>ランダム生成器</summary>
    public IRandom? Random { get; init; }
}

/// <summary>
/// コンテキスト拡張プロバイダー
/// </summary>
public interface IContextExtensionProvider
{
    T? GetExtension<T>(ulong entityId) where T : class;
}

/// <summary>
/// StatusEffectSystemエントリポイント
/// </summary>
public sealed class StatusEffectSystem : IDisposable
{
    #region Services

    public IEffectManager EffectManager { get; }
    public IEffectRegistry EffectRegistry { get; }
    public ITagRegistry TagRegistry { get; }
    public IPhaseRegistry PhaseRegistry { get; }
    public IFlagRegistry FlagRegistry { get; }
    public ApplyFilterPipeline ApplyFilterPipeline { get; }

    #endregion

    private readonly StatusEffectSystemConfig _config;
    private readonly IContextExtensionProvider? _extensionProvider;
    private bool _disposed;

    private StatusEffectSystem(
        StatusEffectSystemConfig config,
        IContextExtensionProvider? extensionProvider)
    {
        _config = config;
        _extensionProvider = extensionProvider;

        // レジストリ初期化
        EffectRegistry = new EffectRegistry();
        TagRegistry = new TagRegistry();
        PhaseRegistry = new PhaseRegistry();
        FlagRegistry = new FlagRegistry();

        // パイプライン初期化
        ApplyFilterPipeline = new ApplyFilterPipeline();

        // マネージャー初期化
        EffectManager = new EffectManager(
            config,
            EffectRegistry,
            TagRegistry,
            ApplyFilterPipeline,
            extensionProvider);
    }

    /// <summary>
    /// システムを生成
    /// </summary>
    public static StatusEffectSystem Create(
        StatusEffectSystemConfig? config = null,
        IContextExtensionProvider? extensionProvider = null)
    {
        return new StatusEffectSystem(
            config ?? new StatusEffectSystemConfig(),
            extensionProvider);
    }

    /// <summary>
    /// ティック処理
    /// </summary>
    public void ProcessTick(GameTick currentTick)
    {
        EffectManager.ProcessTick(currentTick);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // リソースクリーンアップ
    }
}
```

---

## 19. テスト仕様

### 19.1 テスト構成

```
StatusEffectSystem.Tests/
├── Identifiers/
│   ├── EffectIdTests.cs
│   ├── EffectInstanceIdTests.cs
│   └── TagIdTests.cs
├── Collections/
│   ├── TagSetTests.cs
│   └── FlagSetTests.cs
├── Behaviors/
│   ├── StackBehaviorTests.cs
│   ├── DurationBehaviorTests.cs
│   └── SourceIdentifierTests.cs
├── Conditions/
│   ├── BasicConditionTests.cs
│   └── CompositeConditionTests.cs
├── Values/
│   ├── FixedPointTests.cs
│   └── ValueSourceTests.cs
├── Filters/
│   ├── ApplyFilterPipelineTests.cs
│   └── BasicFilterTests.cs
├── Interactions/
│   ├── EffectMatcherTests.cs
│   ├── InteractionRuleTests.cs
│   └── InteractionEngineTests.cs
├── Removal/
│   ├── RemovalStrategyTests.cs
│   └── RemovalSelectorTests.cs
├── Modifiers/
│   ├── CalculationStepTests.cs
│   └── CalculationPipelineTests.cs
├── Manager/
│   ├── EffectManagerApplyTests.cs
│   ├── EffectManagerRemoveTests.cs
│   ├── EffectManagerTickTests.cs
│   └── EffectManagerQueryTests.cs
├── Arena/
│   └── EffectInstanceArenaTests.cs
└── Integration/
    ├── StatusEffectSystemTests.cs
    ├── ScenarioTests.cs
    └── ConcurrencyTests.cs
```

### 19.2 テストパターン

```csharp
public class EffectManagerApplyTests
{
    #region Setup

    private StatusEffectSystem CreateSystem()
    {
        return StatusEffectSystem.Create();
    }

    private StatusEffectDefinition CreateSimpleEffect(EffectId id)
    {
        return StatusEffectDefinition.CreateBuilder(id, "TestEffect")
            .WithDuration(new TickDuration(100))
            .Build();
    }

    #endregion

    #region TryApply Tests

    [Fact]
    public void TryApply_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        using var system = CreateSystem();
        var effectId = new EffectId(1);
        system.EffectRegistry.Register(CreateSimpleEffect(effectId));

        // Act
        var result = system.EffectManager.TryApply(
            targetId: 100,
            effectId: effectId,
            sourceId: 200);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.InstanceId.IsValid);
    }

    [Fact]
    public void TryApply_WithUnregisteredEffect_ShouldFail()
    {
        // Arrange
        using var system = CreateSystem();
        var effectId = new EffectId(999);

        // Act
        var result = system.EffectManager.TryApply(
            targetId: 100,
            effectId: effectId,
            sourceId: 200);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(FailureReasonId.DefinitionNotFound, result.FailureReason);
    }

    [Fact]
    public void TryApply_WithStacking_ShouldMergeInstances()
    {
        // Arrange
        using var system = CreateSystem();
        var effectId = new EffectId(1);
        var definition = StatusEffectDefinition.CreateBuilder(effectId, "StackingEffect")
            .WithStackConfig(new StackConfig(
                maxStacks: 5,
                stackBehavior: AdditiveStackBehavior.Instance,
                durationBehavior: RefreshDurationBehavior.Instance,
                sourceIdentifier: AnySourceIdentifier.Instance))
            .Build();
        system.EffectRegistry.Register(definition);

        // Act
        var result1 = system.EffectManager.TryApply(100, effectId, 200);
        var result2 = system.EffectManager.TryApply(100, effectId, 200);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result2.WasMerged);

        var instance = system.EffectManager.GetInstance(result1.InstanceId);
        Assert.NotNull(instance);
        Assert.Equal(2, instance.CurrentStacks);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void TryApply_ConcurrentAccess_ShouldNotThrow()
    {
        // Arrange
        using var system = CreateSystem();
        var effectId = new EffectId(1);
        system.EffectRegistry.Register(CreateSimpleEffect(effectId));

        // Act & Assert
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                system.EffectManager.TryApply((ulong)i, effectId, 0);
            }))
            .ToArray();

        Task.WaitAll(tasks);
    }

    #endregion
}
```

---

## 20. 使用例

### 20.1 基本的な使用方法

```csharp
// システム初期化
using var system = StatusEffectSystem.Create();

// タグ登録
system.TagRegistry.Register(new TagDefinition(new TagId(1), "buff"));
system.TagRegistry.Register(new TagDefinition(new TagId(2), "debuff"));
system.TagRegistry.Register(new TagDefinition(new TagId(3), "dot"));
system.TagRegistry.Register(new TagDefinition(new TagId(4), "dispellable"));

// 効果定義
var poisonId = new EffectId(1);
var poisonDef = StatusEffectDefinition.CreateBuilder(poisonId, "Poison")
    .WithTags(system.TagRegistry.CreateSet(new TagId(2), new TagId(3), new TagId(4)))
    .WithDuration(new TickDuration(300)) // 5秒@60fps
    .WithStackConfig(new StackConfig(
        maxStacks: 5,
        stackBehavior: AdditiveStackBehavior.Instance,
        durationBehavior: RefreshDurationBehavior.Instance,
        sourceIdentifier: PerSourceIdentifier.Instance))
    .Build();

system.EffectRegistry.Register(poisonDef);

// 効果付与
var result = system.EffectManager.TryApply(
    targetId: playerEntityId,
    effectId: poisonId,
    sourceId: enemyEntityId);

if (result.Success)
{
    Console.WriteLine($"Poison applied: {result.InstanceId}");
}

// ティック処理
for (long tick = 0; tick < 600; tick++)
{
    system.ProcessTick(new GameTick(tick));
}
```

### 20.2 相互作用の例（元素反応）

```csharp
// 元素タグ登録
var fireTag = new TagId(10);
var waterTag = new TagId(11);
var vaporizeTag = new TagId(12);

system.TagRegistry.Register(new TagDefinition(fireTag, "fire"));
system.TagRegistry.Register(new TagDefinition(waterTag, "water"));
system.TagRegistry.Register(new TagDefinition(vaporizeTag, "vaporize"));

// 火元素付着
var fireId = new EffectId(10);
var fireDef = StatusEffectDefinition.CreateBuilder(fireId, "FireElement")
    .WithTags(system.TagRegistry.CreateSet(fireTag))
    .WithDuration(new TickDuration(600))
    .Build();

// 水元素付着
var waterId = new EffectId(11);
var waterDef = StatusEffectDefinition.CreateBuilder(waterId, "WaterElement")
    .WithTags(system.TagRegistry.CreateSet(waterTag))
    .WithDuration(new TickDuration(600))
    .Build();

// 蒸発効果
var vaporizeId = new EffectId(12);
var vaporizeDef = StatusEffectDefinition.CreateBuilder(vaporizeId, "Vaporize")
    .WithTags(system.TagRegistry.CreateSet(vaporizeTag))
    .WithDuration(new TickDuration(1))
    .Build();

// 相互作用ルール（火+水→蒸発）
var vaporizeRule = new InteractionRule(
    id: new InteractionRuleId(1),
    internalName: "FireWaterVaporize",
    trigger: new InteractionTrigger
    {
        TriggeringEffect = new EffectMatcher { RequiredTags = system.TagRegistry.CreateSet(waterTag) },
        ExistingEffect = new EffectMatcher { RequiredTags = system.TagRegistry.CreateSet(fireTag) }
    },
    effects: new IInteractionEffect[]
    {
        new ApplyEffectInteraction(vaporizeId),
        new RemoveEffectInteraction(removeExisting: true, reason: new RemovalReasonId(100))
    },
    consumption: new InteractionConsumption
    {
        ConsumeTriggeringEffect = true,
        ConsumeExistingEffect = true
    });
```

---

## 21. マイグレーションガイド

既存のStatusEffectSystem_Design_v2-1.mdからの主な変更点：

| 項目 | v2-1設計 | 本仕様 |
|------|----------|--------|
| EntityId | `EntityId` struct | `ulong` 直接使用（Tomato統合） |
| EffectInstanceId | シンプルな`ulong` | Arena index + generation |
| タグ最大数 | 64 | 128（2つのulong） |
| 乱数 | 未定義 | `IRandom` インターフェース |
| 固定小数点 | 未定義 | `FixedPoint` struct |
| ビルダー | なし | `StatusEffectDefinition.Builder` |

---

## 22. 今後の拡張ポイント

1. **CommandGenerator統合**: Wave処理による状態変更の決定論的実行
2. **EntitySystem統合**: EntityContextへの効果情報の公開
3. **シリアライズ**: インスタンス状態の保存・復元
4. **デバッグツール**: 効果の可視化、計算過程のトレース
5. **パフォーマンス最適化**: SIMD対応、Span活用

---

## 変更履歴

| 日付 | バージョン | 変更内容 |
|------|-----------|----------|
| 2026-01-21 | 1.0.0 | 初版作成 |
