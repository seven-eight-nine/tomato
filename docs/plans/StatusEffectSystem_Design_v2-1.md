# 超大規模ソーシャルゲーム向け状態異常管理システム設計書 v2

## 1. 概要

### 1.1 目的

本設計書は、長期運営を前提とした超大規模ソーシャルゲーム（500種以上の状態異常、数年単位の運営）に耐えうる状態異常管理システムを定義する。

### 1.2 設計方針

| 方針 | 説明 |
|------|------|
| 純粋ライブラリ | 通信レイヤーなし、クライアントローカルで完結 |
| 言語 | C#（Unity/.NET両対応） |
| データ駆動 | ロジックとデータの分離、マスタデータによる挙動定義 |
| テスト容易性 | 決定論的実行、完全再現可能なシミュレーション |
| 拡張性 | 新しい状態異常タイプを既存コード変更なしで追加可能 |
| **完全抽象化** | 列挙型による固定概念を排除、すべての振る舞いをユーザー定義可能 |

### 1.3 ライブラリとしての境界

**ライブラリが提供するもの：**
- 状態異常のライフサイクル管理（付与・更新・除去）
- 時間経過による変化のフレームワーク
- 値の合成・条件評価のフレームワーク
- 相互作用の評価エンジン
- 付与フィルタのパイプライン
- テスト・検証インフラ

**ユーザーが定義するもの：**
- 具体的な分類・タグ（バフ/デバフ、毒、スタン等）
- 具体的な効果コンポーネント（ダメージ、回復等）
- スタックの合成方法
- 時間更新の方法
- 付与失敗・除去の理由
- インスタンスに付与するフラグの意味
- 相互作用のトリガーと効果

### 1.4 抽象化の原則

本ライブラリでは以下を**固定の列挙型として定義しない**：

| 従来の固定概念 | 本設計での抽象化 |
|---------------|-----------------|
| EffectPolarity (Buff/Debuff) | タグで表現 |
| StackBehavior enum | `IStackBehavior` インターフェース |
| DurationBehavior enum | `IDurationBehavior` インターフェース |
| SnapshotTiming enum | `ISnapshotTrigger` インターフェース |
| DispelConfig | `IRemovalStrategy` インターフェース |
| EffectRemoveReason enum | `RemovalReasonId` (ユーザー定義ID) |
| ApplyFailureReason enum | `FailureReasonId` (ユーザー定義ID) |
| EffectInstanceFlags enum | `FlagId` + `FlagSet` (ユーザー定義) |

---

## 2. 識別子型

### 2.1 ライブラリ定義の識別子

```csharp
/// <summary>状態異常定義ID（マスタデータ参照）</summary>
public readonly struct EffectId : IEquatable<EffectId>
{
    public readonly int Value;
}

/// <summary>実行時インスタンスID（ユニーク）</summary>
public readonly struct EffectInstanceId : IEquatable<EffectInstanceId>
{
    public readonly ulong Value;
}

/// <summary>エンティティID</summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly ulong Value;
}

/// <summary>排他グループID</summary>
public readonly struct GroupId : IEquatable<GroupId>
{
    public readonly int Value;
}

/// <summary>タグID（ユーザー定義分類）</summary>
public readonly struct TagId : IEquatable<TagId>
{
    public readonly int Value;
}

/// <summary>コンポーネントタイプID（ユーザー定義）</summary>
public readonly struct ComponentTypeId : IEquatable<ComponentTypeId>
{
    public readonly int Value;
}

/// <summary>処理フェーズID（適用順序制御）</summary>
public readonly struct PhaseId : IEquatable<PhaseId>
{
    public readonly int Value;
}

/// <summary>相互作用ルールID</summary>
public readonly struct InteractionRuleId : IEquatable<InteractionRuleId>
{
    public readonly int Value;
}
```

### 2.2 ユーザー定義理由・フラグ用ID

```csharp
/// <summary>除去理由ID（ユーザー定義）</summary>
public readonly struct RemovalReasonId : IEquatable<RemovalReasonId>
{
    public readonly int Value;
    
    // ライブラリ予約値（ユーザーは100以降を使用推奨）
    public static readonly RemovalReasonId Expired = new(1);
    public static readonly RemovalReasonId Manual = new(2);
    public static readonly RemovalReasonId Overwritten = new(3);
}

/// <summary>付与失敗理由ID（ユーザー定義）</summary>
public readonly struct FailureReasonId : IEquatable<FailureReasonId>
{
    public readonly int Value;
    
    public static readonly FailureReasonId None = new(0);
    public static readonly FailureReasonId FilterRejected = new(1);
    public static readonly FailureReasonId TargetInvalid = new(2);
}

/// <summary>フラグID（ユーザー定義）</summary>
public readonly struct FlagId : IEquatable<FlagId>
{
    public readonly int Value;
}
```

### 2.3 フラグセット

```csharp
/// <summary>フラグの集合（最大64フラグ）</summary>
public readonly struct FlagSet : IEquatable<FlagSet>
{
    private readonly ulong _bits;
    
    public bool Has(FlagId flag) => (_bits & (1UL << flag.Value)) != 0;
    public FlagSet With(FlagId flag) => new(_bits | (1UL << flag.Value));
    public FlagSet Without(FlagId flag) => new(_bits & ~(1UL << flag.Value));
    
    public static FlagSet operator |(FlagSet a, FlagSet b);
    public static FlagSet operator &(FlagSet a, FlagSet b);
}
```

---

## 3. タグシステム

ユーザーが自由に定義できる分類システム。バフ/デバフ、属性、CCタイプ等すべてタグで表現。

```csharp
public sealed class TagDefinition
{
    public TagId Id { get; }
    public string Name { get; }
    public TagId? ParentId { get; }  // 階層構造
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public readonly struct TagSet : IEquatable<TagSet>
{
    public bool Contains(TagId tag);
    public bool ContainsAny(TagSet other);
    public bool ContainsAll(TagSet other);
    
    public static TagSet operator |(TagSet a, TagSet b);
    public static TagSet operator &(TagSet a, TagSet b);
}

public interface ITagRegistry
{
    void Register(TagDefinition definition);
    TagDefinition Get(TagId id);
    TagSet CreateSet(params TagId[] tags);
}
```

### タグ階層例（ユーザー定義）

```
polarity              # 従来のバフ/デバフはタグで表現
├── positive
├── negative
└── neutral

crowd_control
├── hard_cc
│   ├── stun
│   └── freeze
└── soft_cc
    ├── slow
    └── silence

removable             # 従来のDispellableフラグはタグで表現
├── dispellable
├── stealable
└── transferable
```

---

## 4. 時間モデル

```csharp
public readonly struct GameTick : IEquatable<GameTick>, IComparable<GameTick>
{
    public readonly long Value;
    
    public static GameTick operator +(GameTick a, TickDuration b);
    public static TickDuration operator -(GameTick a, GameTick b);
}

public readonly struct TickDuration : IEquatable<TickDuration>
{
    public readonly int Value;
    
    public static readonly TickDuration Zero = new(0);
    public static readonly TickDuration Infinite = new(int.MaxValue);
}
```

---

## 5. 状態異常定義

```csharp
public sealed class StatusEffectDefinition
{
    // === 識別 ===
    public EffectId Id { get; }
    public string InternalName { get; }
    public GroupId GroupId { get; }
    public TagSet Tags { get; }               // 分類はすべてタグで
    
    // === 時間 ===
    public TickDuration BaseDuration { get; }
    public bool IsPermanent { get; }
    
    // === スタック ===
    public StackConfig StackConfig { get; }
    
    // === 効果 ===
    public IReadOnlyList<IEffectComponent> Components { get; }
    
    // === 相互作用 ===
    public IReadOnlyList<InteractionRule> InteractionRules { get; }
    
    // === 条件 ===
    public ICondition ApplyCondition { get; }
    public ICondition RemoveCondition { get; }
    
    // === スナップショット ===
    public SnapshotConfig SnapshotConfig { get; }
    
    // === 初期フラグ ===
    public FlagSet InitialFlags { get; }
    
    // === メタデータ ===
    public IReadOnlyDictionary<string, object> Metadata { get; }
}
```

---

## 6. スタック設定（完全抽象化）

### 6.1 インターフェース

```csharp
public sealed class StackConfig
{
    public int MaxStacks { get; }
    public IStackBehavior StackBehavior { get; }
    public IDurationBehavior DurationBehavior { get; }
    public IStackSourceIdentifier SourceIdentifier { get; }
}

/// <summary>スタック合成の振る舞い</summary>
public interface IStackBehavior
{
    StackMergeResult Merge(StackMergeContext context);
}

public sealed class StackMergeResult
{
    public int NewStackCount { get; }
    public StackMergeAction Action { get; }  // UpdateExisting / CreateNew / Reject
}

/// <summary>時間更新の振る舞い</summary>
public interface IDurationBehavior
{
    GameTick CalculateNewExpiry(DurationUpdateContext context);
}

/// <summary>スタック源の識別</summary>
public interface IStackSourceIdentifier
{
    bool IsSameSource(EffectInstance existing, EntityId incomingSource, IEffectContext context);
}
```

### 6.2 ライブラリ提供の基本実装

```csharp
// スタック合成
public sealed class AdditiveStackBehavior : IStackBehavior { }
public sealed class IndependentStackBehavior : IStackBehavior { }
public sealed class HighestOnlyStackBehavior : IStackBehavior { }

// 時間更新
public sealed class RefreshDurationBehavior : IDurationBehavior { }
public sealed class ExtendDurationBehavior : IDurationBehavior { }
public sealed class LongestDurationBehavior : IDurationBehavior { }
public sealed class IndependentDurationBehavior : IDurationBehavior { }

// ソース識別
public sealed class AnySourceIdentifier : IStackSourceIdentifier { }
public sealed class PerSourceIdentifier : IStackSourceIdentifier { }
```

---

## 7. 効果コンポーネント

### 7.1 インターフェース

```csharp
public interface IEffectComponent
{
    int LocalId { get; }
    ComponentTypeId TypeId { get; }
    ICondition Condition { get; }
    PhaseId Phase { get; }
    int Priority { get; }
}

public interface ITickableComponent : IEffectComponent
{
    int TickInterval { get; }
    void OnTick(IEffectContext context, IComponentState state);
}

public interface IOnApplyComponent : IEffectComponent
{
    void OnApply(IEffectContext context, IComponentState state);
}

public interface IOnRemoveComponent : IEffectComponent
{
    void OnRemove(IEffectContext context, IComponentState state);
}

public interface IReactiveComponent : IEffectComponent
{
    IReadOnlyList<EventTypeId> ReactsTo { get; }
    void OnEvent(IEffectContext context, IComponentState state, IEffectEvent evt);
}

public interface IModifierComponent : IEffectComponent
{
    void CollectModifiers(IEffectContext context, IComponentState state, IModifierCollector collector);
}
```

### 7.2 コンポーネント状態

```csharp
public interface IComponentState
{
    int ComponentLocalId { get; }
    IComponentState Clone();
}

public sealed class EmptyComponentState : IComponentState
{
    public static readonly EmptyComponentState Instance = new();
}
```

---

## 8. 条件システム

### 8.1 インターフェース

```csharp
public interface ICondition
{
    bool Evaluate(IConditionContext context);
    string GetDescription();
}

public interface IConditionContext
{
    EntityId Source { get; }
    EntityId Target { get; }
    GameTick CurrentTick { get; }
    int CurrentStacks { get; }
    T GetExtension<T>() where T : class;
}
```

### 8.2 ライブラリ提供の合成条件

```csharp
public sealed class AndCondition : ICondition { }
public sealed class OrCondition : ICondition { }
public sealed class NotCondition : ICondition { }
public sealed class AtLeastCondition : ICondition { }
public sealed class AlwaysTrueCondition : ICondition { }
public sealed class AlwaysFalseCondition : ICondition { }
public sealed class ProbabilityCondition : ICondition { }
public sealed class StackCountCondition : ICondition { }
public sealed class TimeElapsedCondition : ICondition { }
```

---

## 9. 値取得（IValueSource）

```csharp
public interface IValueSource
{
    FixedPoint Evaluate(IEffectContext context);
}

// ライブラリ提供
public sealed class ConstantValue : IValueSource { }
public sealed class StackScaledValue : IValueSource { }
public sealed class ConditionalValue : IValueSource { }
public sealed class CompositeValue : IValueSource { }
public sealed class SnapshotAwareValue : IValueSource { }
```

---

## 10. 処理フェーズシステム

```csharp
public sealed class PhaseDefinition
{
    public PhaseId Id { get; }
    public string Name { get; }
    public int Order { get; }          // 実行順序
    public PhaseType Type { get; }     // PerTick / OnEvent / OnCollect
}

public interface IPhaseRegistry
{
    void Register(PhaseDefinition definition);
    IReadOnlyList<PhaseDefinition> GetOrderedPhases();
}

public interface IPhaseInspector
{
    PhaseReport GenerateReport(IEffectRegistry registry);
}
```

---

## 11. 相互作用システム

### 11.1 ルール定義

```csharp
public sealed class InteractionRule
{
    public InteractionRuleId Id { get; }
    public string InternalName { get; }
    public InteractionTrigger Trigger { get; }
    public IReadOnlyList<IInteractionEffect> Effects { get; }
    public int Priority { get; }
    public InteractionConsumption Consumption { get; }
    public ICondition AdditionalCondition { get; }
}

public sealed class InteractionTrigger
{
    public EffectMatcher TriggeringEffect { get; }
    public EffectMatcher ExistingEffect { get; }
}

public sealed class EffectMatcher
{
    public EffectId? EffectId { get; }
    public TagSet? RequiredTags { get; }
    public TagMatchMode TagMatchMode { get; }
}

public sealed class InteractionConsumption
{
    public bool ConsumeTriggeringEffect { get; }
    public bool ConsumeExistingEffect { get; }
    public IValueSource TriggeringConsumeAmount { get; }
    public IValueSource ExistingConsumeAmount { get; }
}
```

### 11.2 効果インターフェース

```csharp
public interface IInteractionEffect
{
    ICondition Condition { get; }
    void Execute(IInteractionContext context);
}

// ライブラリ提供
public sealed class ApplyEffectInteraction : IInteractionEffect { }
public sealed class RemoveEffectInteraction : IInteractionEffect { }
public sealed class ModifyStacksInteraction : IInteractionEffect { }
public sealed class CompositeInteraction : IInteractionEffect { }
```

---

## 12. 付与フィルタシステム

### 12.1 パイプライン

```csharp
public interface IApplyFilter
{
    int Order { get; }
    ApplyFilterResult Filter(ApplyFilterContext context);
}

public readonly struct ApplyFilterResult
{
    public ApplyFilterAction Action { get; }
    public FailureReasonId FailureReason { get; }
    public TickDuration? ModifiedDuration { get; }
    public int? ModifiedStacks { get; }
    public FlagSet? ModifiedFlags { get; }
    
    public static ApplyFilterResult Continue();
    public static ApplyFilterResult Reject(FailureReasonId reason);
}

public interface IApplyFilterPipeline
{
    void AddFilter(IApplyFilter filter);
    ApplyFilterResult Execute(ApplyFilterContext context);
}
```

### 12.2 ライブラリ提供の基本フィルタ

```csharp
public sealed class TagRejectFilter : IApplyFilter { }
public sealed class MaxInstancesFilter : IApplyFilter { }
public sealed class ExclusiveGroupFilter : IApplyFilter { }
public sealed class ConditionFilter : IApplyFilter { }
public sealed class ProbabilityFilter : IApplyFilter { }
public sealed class DurationModifyFilter : IApplyFilter { }
```

---

## 13. スナップショットシステム（完全抽象化）

```csharp
public sealed class SnapshotConfig
{
    public ISnapshotTrigger Trigger { get; }
    public IReadOnlyList<string> SnapshotKeys { get; }
    public ISnapshotProvider Provider { get; }
}

/// <summary>スナップショットのトリガー（ユーザー定義可能）</summary>
public interface ISnapshotTrigger
{
    bool ShouldSnapshot(SnapshotTriggerContext context);
}

// ライブラリ提供
public sealed class NoSnapshotTrigger : ISnapshotTrigger { }
public sealed class OnApplySnapshotTrigger : ISnapshotTrigger { }
public sealed class OnFirstTickSnapshotTrigger : ISnapshotTrigger { }
public sealed class ConditionalSnapshotTrigger : ISnapshotTrigger { }
```

---

## 14. 実行時インスタンス

```csharp
public sealed class EffectInstance
{
    public EffectInstanceId InstanceId { get; }
    public EffectId DefinitionId { get; }
    public EntityId OwnerId { get; }
    public EntityId SourceId { get; }
    public GameTick AppliedAt { get; }
    public GameTick ExpiresAt { get; internal set; }
    public int CurrentStacks { get; internal set; }
    public IReadOnlyDictionary<string, FixedPoint> Snapshot { get; }
    public IReadOnlyDictionary<int, IComponentState> ComponentStates { get; }
    public FlagSet Flags { get; internal set; }  // ユーザー定義フラグ
    public IReadOnlyDictionary<string, object> CustomData { get; }
}

public interface IEffectContext : IConditionContext
{
    EffectInstance Instance { get; }
    StatusEffectDefinition Definition { get; }
    bool TryGetSnapshot(string key, out FixedPoint value);
    void RequestRemove(RemovalReasonId reason);
    void RequestStackChange(int delta);
    void RequestSetFlag(FlagId flag, bool value);
}
```

---

## 15. Effect Manager

```csharp
public interface IEffectManager
{
    // 付与
    ApplyResult TryApply(EntityId target, EffectId effectId, EntityId source, ApplyOptions options = default);
    
    // 除去
    void Remove(EffectInstanceId instanceId, RemovalReasonId reason);
    void RemoveAll(EntityId target, RemovalReasonId reason);
    int RemoveByTag(EntityId target, TagId tag, RemovalReasonId reason);
    
    // 選択的除去（浄化、奪取等を抽象化）
    RemovalResult RemoveWithStrategy(EntityId target, IRemovalStrategy strategy);
    
    // スタック・時間・フラグ操作
    void AddStacks(EffectInstanceId instanceId, int count);
    void ExtendDuration(EffectInstanceId instanceId, TickDuration extension);
    void SetFlag(EffectInstanceId instanceId, FlagId flag, bool value);
    
    // クエリ
    EffectInstance GetInstance(EffectInstanceId instanceId);
    IEnumerable<EffectInstance> GetEffects(EntityId target);
    IEnumerable<EffectInstance> GetEffectsByTag(EntityId target, TagId tag);
    IEnumerable<EffectInstance> GetEffectsByFlag(EntityId target, FlagId flag);
    bool HasEffect(EntityId target, EffectId effectId);
    
    // Tick処理
    void ProcessTick(GameTick currentTick);
    
    // イベント
    event Action<EffectAppliedEvent> OnEffectApplied;
    event Action<EffectRemovedEvent> OnEffectRemoved;
}
```

---

## 16. 除去戦略（浄化、奪取等の抽象化）

```csharp
/// <summary>除去戦略インターフェース</summary>
public interface IRemovalStrategy
{
    IEnumerable<EffectInstance> SelectTargets(RemovalStrategyContext context);
    void PostProcess(IReadOnlyList<EffectInstance> removed, RemovalStrategyContext context);
    RemovalReasonId Reason { get; }
}

/// <summary>除去対象の選択順序</summary>
public interface IRemovalSelector
{
    IEnumerable<EffectInstance> Select(IEnumerable<EffectInstance> candidates, int maxCount);
}

// ライブラリ提供
public sealed class TagBasedRemovalStrategy : IRemovalStrategy { }
public sealed class StealStrategy : IRemovalStrategy { }  // 除去後に自分に付与

public sealed class NewestFirstSelector : IRemovalSelector { }
public sealed class OldestFirstSelector : IRemovalSelector { }
public sealed class HighestStacksFirstSelector : IRemovalSelector { }
public sealed class RandomSelector : IRemovalSelector { }
```

---

## 17. Modifier計算システム

```csharp
public interface IModifier
{
    int TargetId { get; }
    PhaseId Phase { get; }
    int Priority { get; }
    int ModifierTypeId { get; }  // ユーザー定義
    FixedPoint Value { get; }
}

public interface ICalculationStep
{
    PhaseId Phase { get; }
    FixedPoint Execute(FixedPoint currentValue, IReadOnlyList<IModifier> modifiers);
}

public interface ICalculationPipeline
{
    void AddStep(ICalculationStep step);
    FixedPoint Calculate(FixedPoint baseValue, IReadOnlyList<IModifier> modifiers);
}

// ライブラリ提供
public sealed class FlatAdditionStep : ICalculationStep { }
public sealed class PercentAdditiveStep : ICalculationStep { }
public sealed class PercentMultiplicativeStep : ICalculationStep { }
public sealed class OverrideStep : ICalculationStep { }
public sealed class ClampStep : ICalculationStep { }
```

---

## 18. システム構築

```csharp
public sealed class StatusEffectSystem : IDisposable
{
    public static StatusEffectSystem Create(
        StatusEffectSystemConfig config,
        IContextExtensionProvider extensionProvider
    );
    
    // コアサービス
    public IEffectManager EffectManager { get; }
    public IEffectRegistry EffectRegistry { get; }
    public ITagRegistry TagRegistry { get; }
    public IPhaseRegistry PhaseRegistry { get; }
    public IInteractionRegistry InteractionRegistry { get; }
    public IFlagRegistry FlagRegistry { get; }
    
    // パイプライン
    public IApplyFilterPipeline ApplyFilterPipeline { get; }
    
    // デバッグ
    public IPhaseInspector PhaseInspector { get; }
    public ICalculationInspector CalculationInspector { get; }
}
```

---

## 19. テストインフラ

```csharp
public sealed class BattleSimulator
{
    // セットアップ
    public EntityId CreateEntity(EntityConfig config);
    public void RegisterEffect(StatusEffectDefinition definition);
    
    // 実行
    public ApplyResult ApplyEffect(EntityId target, EffectId effectId, EntityId source);
    public void AdvanceTicks(int ticks);
    
    // 検証
    public void AssertHasEffect(EntityId target, EffectId effectId);
    public void AssertHasTag(EntityId target, TagId tag);
    public void AssertHasFlag(EffectInstanceId instance, FlagId flag);
    public void AssertStacks(EntityId target, EffectId effectId, int expected);
}

public sealed class EffectFuzzer
{
    public FuzzResult Fuzz(IEffectRegistry registry, FuzzConfig config);
}
```

---

## 20. ユーザー定義まとめ

| 項目 | 定義方法 | 例 |
|------|----------|-----|
| 分類（バフ/デバフ等） | タグ | `tags.Register("positive")` |
| CC種別 | タグ | `tags.Register("stun", parent: "hard_cc")` |
| 属性 | タグ | `tags.Register("fire", parent: "elemental")` |
| 浄化可能フラグ | タグまたはフラグ | `tags.Register("dispellable")` or `flags.Register("dispellable")` |
| スタック合成方法 | `IStackBehavior` 実装 | `new AdditiveStackBehavior()` |
| 時間更新方法 | `IDurationBehavior` 実装 | `new RefreshDurationBehavior()` |
| スナップショットタイミング | `ISnapshotTrigger` 実装 | `new OnApplySnapshotTrigger()` |
| 付与失敗理由 | `FailureReasonId` | `new FailureReasonId(100) // Immune` |
| 除去理由 | `RemovalReasonId` | `new RemovalReasonId(100) // Dispelled` |
| 除去戦略（浄化等） | `IRemovalStrategy` 実装 | `new TagBasedRemovalStrategy { ... }` |
| 効果コンポーネント | `IEffectComponent` 実装 | `class PeriodicDamage : ITickableComponent` |
| 条件 | `ICondition` 実装 | `class HpThreshold : ICondition` |
| 計算ステップ | `ICalculationStep` 実装 | カスタム計算式 |
| 相互作用効果 | `IInteractionEffect` 実装 | ダメージ倍率、範囲拡散等 |

---

## 21. 機能対応表

本設計で参考タイトルの機能がどう実現されるか：

| 機能 | 実現方法 |
|------|----------|
| バフ/デバフ分類 | タグ `positive` / `negative` |
| 浄化（Dispel） | `IRemovalStrategy` + タグ `dispellable` |
| 奪取（Steal） | `StealStrategy` + タグ `stealable` |
| スタン免疫 | `TagRejectFilter` + タグ `stun` |
| テナシティ | `DurationModifyFilter` + タグ `crowd_control` |
| 累積耐性 | カスタム `IApplyFilter` |
| 元素反応 | `InteractionRule` + 元素タグ |
| スナップショット | `ISnapshotTrigger` + `SnapshotAwareValue` |
| DoT | `ITickableComponent` 実装 |
| シールド | `IReactiveComponent` + カスタム状態 |

---

## 22. ファイル構成

```
StatusEffectSystem/
├── Core/
│   ├── Identifiers/           # EffectId, TagId, FlagId, RemovalReasonId等
│   ├── Time/                  # GameTick, TickDuration
│   ├── Tags/                  # TagDefinition, TagSet, ITagRegistry
│   ├── Flags/                 # FlagDefinition, FlagSet, IFlagRegistry
│   ├── Phases/                # PhaseDefinition, IPhaseRegistry
│   ├── Effects/               # StatusEffectDefinition
│   ├── Stacks/                # IStackBehavior, IDurationBehavior
│   ├── Components/            # IEffectComponent系インターフェース
│   ├── Conditions/            # ICondition, 合成条件
│   ├── Values/                # IValueSource
│   ├── Snapshots/             # ISnapshotTrigger, SnapshotConfig
│   └── Instances/             # EffectInstance, IEffectContext
├── Systems/
│   ├── IEffectManager.cs
│   ├── EffectManager.cs
│   ├── Interactions/          # InteractionRule, IInteractionEffect
│   ├── Filters/               # IApplyFilter, パイプライン
│   ├── Removal/               # IRemovalStrategy, IRemovalSelector
│   └── Modifiers/             # IModifier, ICalculationStep
├── Data/
│   ├── IEffectRegistry.cs
│   └── Validation/
├── Integration/
│   └── StatusEffectSystem.cs  # エントリポイント
├── Debug/
│   ├── IPhaseInspector.cs
│   └── ICalculationInspector.cs
└── Testing/
    ├── BattleSimulator.cs
    └── EffectFuzzer.cs
```

---

## 23. まとめ

本設計の特徴：

1. **完全な抽象化**: 列挙型による固定概念を排除、すべてインターフェースまたはユーザー定義ID
2. **機能の維持**: 参考タイトルのすべての機能を実現可能
3. **明示的な順序制御**: フェーズシステムによる処理順序の可視化
4. **パイプラインアーキテクチャ**: フィルタ、計算、相互作用すべてがパイプライン
5. **テスト容易性**: 決定論的実行、シミュレータ、ファジング
