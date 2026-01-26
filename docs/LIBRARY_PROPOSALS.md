# ライブラリ追加案

本ドキュメントはTomatoフレームワークに追加を検討するライブラリ案をまとめたものである。

## 選定基準

- **汎用性**: どのゲームでも必要になる機能
- **複雑性**: 新規に作ると大規模になりがち
- **再利用価値**: ライブラリとしてまとまっていると嬉しい

既存のCombatSystem、StatusEffectSystem、ActionSelectorと同様の位置づけ。

---

## 1. InventorySystem ✅ 実装済み

> **注**: このシステムは `libs/systems/InventorySystem/` に実装済みです。

### 実装された機能

- 汎用インベントリ（IInventory<TItem>インターフェース）
- アイテムの追加・削除・クエリ
- バリデーションシステム（容量制限、カスタムルール）
- トランザクションベースの操作
- スナップショットと復元
- クラフティング機能（ICraftingRecipe、CraftingManager、CraftingPlanner）
- シリアライゼーション対応

### コア構造

```csharp
// 実装済みのインターフェース
public interface IInventory<TItem> : ISerializable, ISnapshotable<IInventory<TItem>>
    where TItem : class, IInventoryItem
{
    InventoryId Id { get; }
    int Count { get; }
    bool HasSpace { get; }

    IValidationResult CanAdd(TItem item, AddContext? context = null);
    AddResult TryAdd(TItem item, AddContext? context = null);
    RemoveResult TryRemove(ItemInstanceId instanceId, int count = 1);
}

// クラフティング
public interface ICraftingRecipe { ... }
public class CraftingManager { ... }
public class CraftingPlanner { ... }
```

---

## 2. AbilitySystem

### 概要

スキル・アビリティの定義と実行を管理するシステム。クールダウン、コスト、条件判定など、スキルシステムに必要な機能を提供する。

### 主要機能

- アビリティの定義（コスト、クールダウン、条件）
- スキルツリー/アンロック管理
- クールダウン管理
- リソースコスト（MP、スタミナ等）の消費
- 発動条件の評価
- レベルアップ/強化

### コア構造案

```csharp
public interface IAbility
{
    AbilityId Id { get; }
    AbilityDefinition Definition { get; }
    int Level { get; }

    bool CanActivate(in AbilityContext context);
    ActivationResult TryActivate(in AbilityContext context);
}

public sealed class AbilityDefinition
{
    public AbilityId Id { get; }
    public string Name { get; }
    public int MaxLevel { get; }
    public IReadOnlyList<AbilityCost> Costs { get; }
    public float BaseCooldown { get; }
    public IReadOnlyList<IAbilityCondition> Conditions { get; }
    public IReadOnlyList<AbilityEffect> Effects { get; }
}

public readonly struct AbilityCost
{
    public ResourceType Resource { get; }
    public float BaseAmount { get; }
    public float PerLevelAmount { get; }
}

public interface IAbilityCondition
{
    bool IsSatisfied(in AbilityContext context);
}

public sealed class AbilityManager
{
    // アビリティの所持管理
    public void Grant(AbilityId id);
    public void Revoke(AbilityId id);
    public bool HasAbility(AbilityId id);

    // レベル管理
    public void SetLevel(AbilityId id, int level);
    public int GetLevel(AbilityId id);

    // クールダウン
    public float GetRemainingCooldown(AbilityId id);
    public void ResetCooldown(AbilityId id);

    // 発動
    public ActivationResult TryActivate(AbilityId id, in AbilityContext context);

    // フレーム更新
    public void Update(float deltaTime);
}
```

### 設計ポイント

- **ActionSelectorとの連携**: AbilityのCanActivateをActionSelectorの条件として使用可能
- **StatusEffectSystemとの連携**: AbilityEffectがStatusEffectを付与
- **CombatSystemとの連携**: ダメージ系Abilityは攻撃として処理

### スキルツリー

```csharp
public sealed class SkillTree
{
    public IReadOnlyList<SkillNode> Nodes { get; }

    public bool CanUnlock(SkillNodeId nodeId, ISkillPointProvider points);
    public UnlockResult TryUnlock(SkillNodeId nodeId, ISkillPointProvider points);
    public IEnumerable<SkillNodeId> GetAvailableNodes(ISkillPointProvider points);
}

public sealed class SkillNode
{
    public SkillNodeId Id { get; }
    public AbilityId? GrantedAbility { get; }
    public IReadOnlyList<SkillNodeId> Prerequisites { get; }
    public int Cost { get; }
}
```

---

## 3. BehaviorTreeSystem ✅ FlowTreeとして実装済み

> **注**: このシステムは `libs/foundation/FlowTree/` に **FlowTree** として実装済みです。

### 実装された機能

- BehaviorTree風のノードベースフロー制御
- Compositeノード: Sequence, Selector, Race, Join, RandomSelector, ShuffledSelector, WeightedRandomSelector, RoundRobinSelector
- Decoratorノード: Inverter, Succeeder, Failer, Repeat, Retry, RepeatUntilSuccess, RepeatUntilFail, Timeout, Delay, Guard, Event
- Leafノード: Success, Failure, Wait, WaitUntil, Yield, Action, Condition, SubTree, Return
- DSLビルダーによる直感的なツリー構築
- コールスタックによるサブツリー呼び出し
- ゼロアロケーション設計

### コア構造

```csharp
// FlowTree API
var tree = new FlowTree("MyAI")
    .Build(
        Flow.Selector(
            Flow.Sequence(
                Flow.Condition(ctx => IsEnemyVisible(ctx)),
                Flow.Selector(
                    Flow.Sequence(
                        Flow.Condition(ctx => IsInRange(ctx)),
                        Flow.Action(ctx => Attack(ctx))
                    ),
                    Flow.Action(ctx => MoveToEnemy(ctx))
                )
            ),
            Flow.Action(ctx => Patrol(ctx))
        )
    );

// 実行
var status = tree.Tick(deltaTime);
```

---

## 3b. BehaviorTreeSystem（追加提案）

### 概要

FlowTreeの上位互換として、より高度なAI機能が必要な場合の追加提案。

### 主要機能

- ノード定義（Composite, Decorator, Leaf）
- ブラックボード（共有データストア）
- 実行状態の保存・再開
- 並列ノード対応
- デバッグ用のツリー状態出力

### コア構造案

```csharp
public enum NodeStatus
{
    Running,
    Success,
    Failure
}

public interface IBehaviorNode
{
    NodeStatus Execute(BehaviorContext context);
    void Reset();
}

// Composite Nodes
public abstract class CompositeNode : IBehaviorNode
{
    protected IReadOnlyList<IBehaviorNode> Children { get; }
    public abstract NodeStatus Execute(BehaviorContext context);
    public virtual void Reset() => Children.ForEach(c => c.Reset());
}

public sealed class SequenceNode : CompositeNode
{
    // 全て成功で成功、1つでも失敗で失敗
}

public sealed class SelectorNode : CompositeNode
{
    // 1つでも成功で成功、全て失敗で失敗
}

public sealed class ParallelNode : CompositeNode
{
    public ParallelPolicy SuccessPolicy { get; }
    public ParallelPolicy FailurePolicy { get; }
}

// Decorator Nodes
public abstract class DecoratorNode : IBehaviorNode
{
    protected IBehaviorNode Child { get; }
}

public sealed class InverterNode : DecoratorNode { }
public sealed class RepeatNode : DecoratorNode { public int Count { get; } }
public sealed class RetryNode : DecoratorNode { public int MaxAttempts { get; } }
public sealed class CooldownNode : DecoratorNode { public float Duration { get; } }

// Leaf Nodes
public abstract class ActionNode : IBehaviorNode { }
public abstract class ConditionNode : IBehaviorNode { }
```

### ブラックボード

```csharp
public sealed class Blackboard
{
    public void Set<T>(BlackboardKey<T> key, T value);
    public T Get<T>(BlackboardKey<T> key);
    public bool TryGet<T>(BlackboardKey<T> key, out T value);
    public bool Contains<T>(BlackboardKey<T> key);
    public void Remove<T>(BlackboardKey<T> key);
    public void Clear();
}

public readonly struct BlackboardKey<T>
{
    public string Name { get; }
}
```

### ビルダーDSL

```csharp
var tree = BehaviorTree.Builder()
    .Selector()
        .Sequence()
            .Condition<IsEnemyVisible>()
            .Selector()
                .Sequence()
                    .Condition<IsInAttackRange>()
                    .Action<AttackEnemy>()
                .End()
                .Action<MoveToEnemy>()
            .End()
        .End()
        .Action<Patrol>()
    .End()
    .Build();
```

### 設計ポイント

- **ActionSelectorとの連携**: AIの入力ソースとしてBehaviorTreeの結果を使用
- **決定論性**: 同一入力に対して同一結果を保証
- **Running状態の管理**: 複数フレームにまたがるノードの状態保持

---

## 4. DialogueSystem

### 概要

会話・対話を管理するシステム。分岐、条件、変数管理など、ストーリー駆動のゲームに必要な機能を提供する。

### 主要機能

- 会話ノードと分岐
- 条件付き選択肢
- 変数・フラグ管理
- 話者情報（名前、立ち絵等）
- イベントトリガー（会話中のアクション）
- ローカライズ対応の土台

### コア構造案

```csharp
public interface IDialogueRunner
{
    DialogueState State { get; }

    void Start(DialogueId dialogueId);
    void Continue();
    void SelectChoice(int choiceIndex);
    void Stop();

    event Action<DialogueLine> OnLine;
    event Action<IReadOnlyList<DialogueChoice>> OnChoices;
    event Action<DialogueEvent> OnEvent;
    event Action OnEnd;
}

public readonly struct DialogueLine
{
    public string SpeakerId { get; }
    public string Text { get; }
    public IReadOnlyDictionary<string, string>? Metadata { get; }
}

public readonly struct DialogueChoice
{
    public int Index { get; }
    public string Text { get; }
    public bool IsAvailable { get; }
    public string? UnavailableReason { get; }
}

public readonly struct DialogueEvent
{
    public string EventType { get; }
    public IReadOnlyDictionary<string, object> Parameters { get; }
}
```

### 会話データ構造

```csharp
public sealed class DialogueGraph
{
    public DialogueId Id { get; }
    public IReadOnlyDictionary<NodeId, DialogueNode> Nodes { get; }
    public NodeId EntryNode { get; }
}

public abstract class DialogueNode
{
    public NodeId Id { get; }
    public IReadOnlyList<IDialogueCondition>? Conditions { get; }
}

public sealed class LineNode : DialogueNode
{
    public string SpeakerId { get; }
    public LocalizedString Text { get; }
    public NodeId? NextNode { get; }
    public IReadOnlyList<DialogueEvent>? Events { get; }
}

public sealed class BranchNode : DialogueNode
{
    public IReadOnlyList<ConditionalBranch> Branches { get; }
}

public sealed class ChoiceNode : DialogueNode
{
    public IReadOnlyList<Choice> Choices { get; }
}

public sealed class Choice
{
    public LocalizedString Text { get; }
    public NodeId TargetNode { get; }
    public IReadOnlyList<IDialogueCondition>? Conditions { get; }
}
```

### 変数システム

```csharp
public interface IDialogueVariableStore
{
    void SetInt(string key, int value);
    void SetBool(string key, bool value);
    void SetString(string key, string value);

    int GetInt(string key, int defaultValue = 0);
    bool GetBool(string key, bool defaultValue = false);
    string GetString(string key, string defaultValue = "");

    void Increment(string key, int amount = 1);
    bool HasKey(string key);
}
```

### 設計ポイント

- **データ駆動**: 会話データは外部ファイル（JSON/YAML等）から読み込み可能
- **条件の再利用**: IDialogueConditionはActionSelectorのIConditionと互換性を持たせる
- **イベント駆動**: 会話中のアクション（アイテム獲得、クエスト開始等）はイベントとして発行

---

## 5. QuestSystem

### 概要

クエスト・目標の進行を管理するシステム。状態管理、目標トラッキング、報酬処理など、RPGに必要な機能を提供する。

### 主要機能

- クエスト状態管理（未開始/進行中/完了/失敗）
- 目標の進捗トラッキング
- 前提条件・依存クエスト
- 報酬定義と付与
- サブクエスト、クエストチェーン
- 時限クエスト（期限付き）

### コア構造案

```csharp
public enum QuestState
{
    Unavailable,  // 前提条件未達
    Available,    // 受注可能
    Active,       // 進行中
    Completed,    // 完了
    Failed        // 失敗
}

public interface IQuestManager
{
    // クエスト操作
    AcceptResult TryAccept(QuestId id);
    void Abandon(QuestId id);
    void Complete(QuestId id);
    void Fail(QuestId id);

    // 状態取得
    QuestState GetState(QuestId id);
    IEnumerable<QuestId> GetActiveQuests();
    IEnumerable<QuestId> GetAvailableQuests();

    // 目標進捗
    void ReportProgress(ObjectiveType type, string targetId, int amount = 1);
    QuestProgress GetProgress(QuestId id);

    // イベント
    event Action<QuestId, QuestState> OnQuestStateChanged;
    event Action<QuestId, ObjectiveId> OnObjectiveCompleted;
}

public sealed class QuestDefinition
{
    public QuestId Id { get; }
    public LocalizedString Name { get; }
    public LocalizedString Description { get; }

    public IReadOnlyList<IQuestCondition> Prerequisites { get; }
    public IReadOnlyList<ObjectiveDefinition> Objectives { get; }
    public IReadOnlyList<QuestReward> Rewards { get; }

    public QuestId? ParentQuest { get; }  // サブクエストの場合
    public IReadOnlyList<QuestId> NextQuests { get; }  // チェーン

    public float? TimeLimit { get; }  // 時限クエスト
}

public sealed class ObjectiveDefinition
{
    public ObjectiveId Id { get; }
    public ObjectiveType Type { get; }
    public string TargetId { get; }
    public int RequiredCount { get; }
    public LocalizedString Description { get; }
    public bool IsOptional { get; }
}

public enum ObjectiveType
{
    Kill,       // 敵を倒す
    Collect,    // アイテムを集める
    Talk,       // NPCと会話
    Reach,      // 場所に到達
    Interact,   // オブジェクトを操作
    Escort,     // 護衛
    Custom      // カスタム条件
}
```

### 進捗管理

```csharp
public sealed class QuestProgress
{
    public QuestId QuestId { get; }
    public QuestState State { get; }
    public float? RemainingTime { get; }
    public IReadOnlyDictionary<ObjectiveId, ObjectiveProgress> Objectives { get; }
}

public readonly struct ObjectiveProgress
{
    public ObjectiveId Id { get; }
    public int CurrentCount { get; }
    public int RequiredCount { get; }
    public bool IsCompleted => CurrentCount >= RequiredCount;
    public float Progress => (float)CurrentCount / RequiredCount;
}
```

### 設計ポイント

- **イベント駆動**: ReportProgressで汎用的に進捗を報告、内部でマッチング
- **条件の共通化**: IQuestConditionは他システムの条件と互換性を持たせる
- **報酬の抽象化**: QuestRewardはInventorySystem、ProgressionSystemと連携

---

## 6. StatSystem

### 概要

キャラクターのステータス・属性を管理するシステム。基礎値、修飾子、派生ステータスの計算を行う。

### 主要機能

- 基礎ステータス管理（HP, MP, STR, DEX等）
- 修飾子の適用（加算、乗算、上書き）
- 適用順序の管理
- 派生ステータスの依存計算
- 上限/下限クランプ
- スナップショット/比較

### コア構造案

```csharp
public interface IStatContainer
{
    float GetBaseValue(StatId stat);
    float GetFinalValue(StatId stat);

    void SetBaseValue(StatId stat, float value);

    ModifierHandle AddModifier(StatId stat, StatModifier modifier);
    void RemoveModifier(ModifierHandle handle);
    void RemoveAllModifiers(StatId stat);
    void RemoveModifiersFromSource(object source);

    event Action<StatId, float, float> OnStatChanged;
}

public readonly struct StatModifier
{
    public ModifierType Type { get; }
    public float Value { get; }
    public int Priority { get; }  // 適用順序
    public object? Source { get; }  // 装備、バフ等の発生源
}

public enum ModifierType
{
    Flat,           // 加算: base + value
    PercentAdd,     // 加算%: base * (1 + sum(values))
    PercentMult,    // 乗算%: base * (1 + value1) * (1 + value2)
    Override        // 上書き: value（最後のOverrideが適用）
}
```

### 計算順序

```
1. Base Value
2. + Flat modifiers (sum)
3. * PercentAdd modifiers (1 + sum)
4. * PercentMult modifiers (product of (1 + value))
5. Override (if any)
6. Clamp to min/max
```

### 派生ステータス

```csharp
public sealed class DerivedStat
{
    public StatId Id { get; }
    public IReadOnlyList<StatId> Dependencies { get; }
    public Func<IStatContainer, float> Calculator { get; }
}

// 例: 物理攻撃力 = STR * 2 + WeaponDamage
var physicalAttack = new DerivedStat
{
    Id = StatIds.PhysicalAttack,
    Dependencies = new[] { StatIds.Strength, StatIds.WeaponDamage },
    Calculator = stats =>
        stats.GetFinalValue(StatIds.Strength) * 2f +
        stats.GetFinalValue(StatIds.WeaponDamage)
};
```

### StatDefinition

```csharp
public sealed class StatDefinition
{
    public StatId Id { get; }
    public string Name { get; }
    public float DefaultValue { get; }
    public float MinValue { get; }
    public float MaxValue { get; }
    public bool IsInteger { get; }  // 整数として扱うか
}
```

### 設計ポイント

- **StatusEffectSystemとの連携**: StatusEffectがStatModifierを追加/削除
- **CombatSystemとの連携**: ダメージ計算時にStatを参照
- **ソーストラッキング**: 装備解除時にその装備由来の修飾子を一括削除

---

## 7. LootTableSystem

### 概要

ドロップアイテムの抽選を管理するシステム。重み付きランダム、保証枠、ピティシステムなど、ガチャ/ドロップに必要な機能を提供する。

### 主要機能

- 重み付きドロップテーブル
- レアリティ定義
- 保証枠（必ずN個ドロップ等）
- ピティシステム（天井）
- ネストしたテーブル参照
- 条件付きドロップ

### コア構造案

```csharp
public interface ILootTable
{
    LootTableId Id { get; }
    IReadOnlyList<LootResult> Roll(LootContext context, IRandom random);
}

public readonly struct LootResult
{
    public ItemId ItemId { get; }
    public int Count { get; }
    public Rarity Rarity { get; }
}

public readonly struct LootContext
{
    public int PlayerLevel { get; }
    public float LuckModifier { get; }
    public IReadOnlyDictionary<string, object>? CustomData { get; }
}
```

### テーブル定義

```csharp
public sealed class LootTableDefinition
{
    public LootTableId Id { get; }
    public IReadOnlyList<LootEntry> Entries { get; }
    public int GuaranteedDrops { get; }  // 最低ドロップ数
    public int MaxDrops { get; }         // 最大ドロップ数
    public IReadOnlyList<ILootCondition>? Conditions { get; }
}

public sealed class LootEntry
{
    public LootEntryType Type { get; }

    // Type == Item の場合
    public ItemId? ItemId { get; }
    public int MinCount { get; }
    public int MaxCount { get; }

    // Type == TableReference の場合
    public LootTableId? ReferencedTable { get; }

    // 共通
    public float Weight { get; }
    public Rarity? Rarity { get; }
    public IReadOnlyList<ILootCondition>? Conditions { get; }
}

public enum LootEntryType
{
    Item,
    TableReference,  // 別テーブルを参照
    Nothing          // ハズレ枠
}
```

### ピティシステム

```csharp
public sealed class PityTracker
{
    public void RecordRoll(LootTableId tableId, Rarity resultRarity);
    public int GetPityCount(LootTableId tableId, Rarity targetRarity);
    public float GetPityBonus(LootTableId tableId, Rarity targetRarity);
    public void ResetPity(LootTableId tableId, Rarity rarity);
}

public sealed class PityConfig
{
    public Rarity TargetRarity { get; }
    public int SoftPityStart { get; }   // 確率上昇開始
    public int HardPity { get; }        // 天井（確定）
    public float SoftPityBonus { get; } // 1回あたりの確率上昇
}
```

### 設計ポイント

- **決定論性**: IRandomを注入し、シード指定で再現可能
- **テーブルのネスト**: 共通テーブルを参照して再利用
- **条件付きドロップ**: プレイヤーレベル、クエスト状態等で出現を制御

---

## 8. FormationSystem

### 概要

ユニットの隊列配置を管理するシステム。パターン定義、位置計算、動的再編成など、RTS/タクティクス/パーティ制RPGに必要な機能を提供する。

### 主要機能

- 隊列パターン定義（横列、縦列、V字等）
- 相対位置計算
- 欠員時の再編成
- リーダー追従
- 間隔調整
- 障害物回避（オプション）

### コア構造案

```csharp
public interface IFormation
{
    FormationId Id { get; }
    int MaxSlots { get; }

    Vector3 GetSlotPosition(int slotIndex, FormationContext context);
    Quaternion GetSlotRotation(int slotIndex, FormationContext context);
}

public readonly struct FormationContext
{
    public Vector3 LeaderPosition { get; }
    public Quaternion LeaderRotation { get; }
    public float Spacing { get; }
    public int ActiveMemberCount { get; }
}

public sealed class FormationManager
{
    public void SetFormation(FormationId formationId);
    public FormationId CurrentFormation { get; }

    public void AssignSlot(AnyHandle unit, int slotIndex);
    public void RemoveUnit(AnyHandle unit);
    public void ClearSlot(int slotIndex);

    public Vector3 GetTargetPosition(AnyHandle unit);
    public Quaternion GetTargetRotation(AnyHandle unit);

    // 欠員時の再編成
    public void Reorganize(ReorganizeStrategy strategy);
}

public enum ReorganizeStrategy
{
    FillGaps,       // 空きスロットを詰める
    MaintainSlots,  // スロット位置を維持
    Rebalance       // 均等に再配置
}
```

### 組み込みパターン

```csharp
public static class Formations
{
    public static IFormation Line { get; }      // 横一列
    public static IFormation Column { get; }    // 縦一列
    public static IFormation Wedge { get; }     // V字（攻撃的）
    public static IFormation InverseWedge { get; }  // 逆V字（防御的）
    public static IFormation Square { get; }    // 方陣
    public static IFormation Circle { get; }    // 円陣
    public static IFormation Scatter { get; }   // 散開
}
```

### カスタムパターン定義

```csharp
public sealed class CustomFormation : IFormation
{
    public FormationId Id { get; }
    public int MaxSlots => _slots.Count;

    private readonly IReadOnlyList<FormationSlot> _slots;

    public Vector3 GetSlotPosition(int slotIndex, FormationContext context)
    {
        var slot = _slots[slotIndex];
        var localPos = slot.LocalPosition * context.Spacing;
        return context.LeaderPosition + context.LeaderRotation * localPos;
    }
}

public readonly struct FormationSlot
{
    public int Index { get; }
    public Vector3 LocalPosition { get; }  // リーダーからの相対位置
    public float LocalRotation { get; }    // リーダーからの相対角度
    public FormationRole Role { get; }     // 前衛/後衛等
}
```

### 設計ポイント

- **ReconciliationSystemとの連携**: 位置調停時に隊列位置を考慮
- **SpatialSystemとの連携**: 近くのユニットを隊列に組み込む
- **スムーズな遷移**: 隊列変更時の補間をサポート

---

## 9. FactionSystem

### 概要

勢力間の関係を管理するシステム。敵対/中立/友好の関係定義、評判値の増減、関係変化の伝播など、派閥システムに必要な機能を提供する。

### 主要機能

- 勢力定義
- 勢力間の関係（敵対/中立/友好）
- 評判値と閾値
- 関係変化の伝播（同盟/敵対関係の連鎖）
- プレイヤーの評判管理
- 関係に基づくAI行動判定

### コア構造案

```csharp
public enum Relationship
{
    Hostile,    // 敵対（攻撃対象）
    Unfriendly, // 非友好（攻撃はしないが協力もしない）
    Neutral,    // 中立
    Friendly,   // 友好
    Allied      // 同盟（完全な味方）
}

public interface IFactionSystem
{
    // 勢力間の関係
    Relationship GetRelationship(FactionId a, FactionId b);
    void SetRelationship(FactionId a, FactionId b, Relationship relationship);

    // プレイヤー評判
    int GetReputation(FactionId faction);
    void ModifyReputation(FactionId faction, int amount);
    Relationship GetPlayerRelationship(FactionId faction);

    // 判定ヘルパー
    bool IsHostile(FactionId a, FactionId b);
    bool IsAllied(FactionId a, FactionId b);
    bool CanAttack(AnyHandle attacker, AnyHandle target);

    // イベント
    event Action<FactionId, FactionId, Relationship> OnRelationshipChanged;
    event Action<FactionId, int, int> OnReputationChanged;
}

public sealed class FactionDefinition
{
    public FactionId Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<FactionId, Relationship> DefaultRelationships { get; }
    public IReadOnlyList<ReputationThreshold> ReputationThresholds { get; }
}

public readonly struct ReputationThreshold
{
    public int MinReputation { get; }
    public Relationship Relationship { get; }
}
```

### 評判閾値の例

```csharp
// 例: 山賊ギルドの評判閾値
var banditGuild = new FactionDefinition
{
    Id = FactionIds.BanditGuild,
    ReputationThresholds = new[]
    {
        new ReputationThreshold { MinReputation = -100, Relationship = Relationship.Hostile },
        new ReputationThreshold { MinReputation = -50,  Relationship = Relationship.Unfriendly },
        new ReputationThreshold { MinReputation = 0,    Relationship = Relationship.Neutral },
        new ReputationThreshold { MinReputation = 50,   Relationship = Relationship.Friendly },
        new ReputationThreshold { MinReputation = 100,  Relationship = Relationship.Allied },
    }
};
```

### 関係伝播

```csharp
public sealed class RelationshipPropagation
{
    // A と B が同盟になったとき、A の敵は B の敵になる
    public PropagationRule AllianceSharesEnemies { get; }

    // A を攻撃すると、A の同盟勢力との評判も下がる
    public PropagationRule AttackAffectsAllies { get; }
}
```

### 設計ポイント

- **CombatSystemとの連携**: CanAttackで攻撃可否を判定
- **BehaviorTreeSystemとの連携**: AIの敵味方判定に使用
- **QuestSystemとの連携**: 評判変化をクエスト報酬/ペナルティとして

---

## 10. CraftingSystem

### 概要

アイテムの合成・製作を管理するシステム。レシピ定義、素材チェック、成功率、品質バリエーションなど、クラフト機能に必要な機能を提供する。

### 主要機能

- レシピ定義と検索
- 素材チェック・消費
- 成功率と失敗時の挙動
- 品質/バリエーション
- レシピアンロック
- 製作時間（オプション）

### コア構造案

```csharp
public interface ICraftingSystem
{
    // レシピ検索
    IEnumerable<RecipeId> GetAvailableRecipes();
    IEnumerable<RecipeId> GetRecipesForItem(ItemId outputItem);
    IEnumerable<RecipeId> SearchRecipes(Predicate<RecipeDefinition> predicate);

    // 製作可否
    Craftability CanCraft(RecipeId recipeId, IInventory inventory);

    // 製作実行
    CraftResult Craft(RecipeId recipeId, IInventory inventory, CraftContext context);

    // レシピアンロック
    void UnlockRecipe(RecipeId recipeId);
    bool IsRecipeUnlocked(RecipeId recipeId);
}

public readonly struct CraftResult
{
    public bool Success { get; }
    public IReadOnlyList<ItemInstance> ProducedItems { get; }
    public IReadOnlyList<ItemInstance> ConsumedItems { get; }
    public CraftQuality Quality { get; }
    public string? FailureReason { get; }
}

public enum CraftQuality
{
    Poor,
    Normal,
    Good,
    Excellent,
    Masterwork
}
```

### レシピ定義

```csharp
public sealed class RecipeDefinition
{
    public RecipeId Id { get; }
    public LocalizedString Name { get; }

    public IReadOnlyList<RecipeIngredient> Ingredients { get; }
    public IReadOnlyList<RecipeOutput> Outputs { get; }

    public float BaseSuccessRate { get; }
    public IReadOnlyList<IRecipeCondition>? Conditions { get; }
    public float CraftTime { get; }  // 0 = 即時

    public CraftingCategory Category { get; }
    public int RequiredSkillLevel { get; }
}

public readonly struct RecipeIngredient
{
    public ItemId ItemId { get; }
    public int Count { get; }
    public bool IsConsumed { get; }  // false = 触媒（消費されない）
}

public readonly struct RecipeOutput
{
    public ItemId ItemId { get; }
    public int MinCount { get; }
    public int MaxCount { get; }
    public float Weight { get; }  // 複数出力候補がある場合の重み
}
```

### 品質システム

```csharp
public sealed class QualityCalculator
{
    public CraftQuality Calculate(CraftContext context)
    {
        // 基本確率 + スキルボーナス + 素材品質ボーナス
        var roll = context.Random.NextFloat();
        var skillBonus = context.CrafterSkillLevel * 0.01f;
        var materialBonus = context.AverageMaterialQuality * 0.1f;

        var score = roll + skillBonus + materialBonus;

        return score switch
        {
            < 0.2f => CraftQuality.Poor,
            < 0.5f => CraftQuality.Normal,
            < 0.8f => CraftQuality.Good,
            < 0.95f => CraftQuality.Excellent,
            _ => CraftQuality.Masterwork
        };
    }
}
```

### 設計ポイント

- **InventorySystemとの連携**: 素材消費と成果物追加
- **StatSystemとの連携**: 製作スキルレベルの参照
- **QuestSystemとの連携**: 特定アイテム製作が目標になる

---

## 11. ProgressionSystem

### 概要

キャラクターの成長・レベリングを管理するシステム。経験値、成長曲線、スキルポイントなど、RPGの成長要素に必要な機能を提供する。

### 主要機能

- 経験値の獲得と管理
- レベルアップ判定・処理
- 成長曲線（必要経験値テーブル）
- レベルアップ報酬（ステータス上昇、スキルポイント等）
- 複数の成長軸対応（キャラLv、スキルLv、称号等）
- 経験値ソースの追跡

### コア構造案

```csharp
public interface IProgressionSystem
{
    // 経験値
    void AddExperience(ProgressionId progressionId, AnyHandle entity, int amount, string? source = null);
    int GetExperience(ProgressionId progressionId, AnyHandle entity);
    int GetExperienceToNextLevel(ProgressionId progressionId, AnyHandle entity);
    float GetLevelProgress(ProgressionId progressionId, AnyHandle entity);

    // レベル
    int GetLevel(ProgressionId progressionId, AnyHandle entity);
    int GetMaxLevel(ProgressionId progressionId);
    bool IsMaxLevel(ProgressionId progressionId, AnyHandle entity);

    // イベント
    event Action<AnyHandle, ProgressionId, int, int> OnLevelUp;
    event Action<AnyHandle, ProgressionId, int, string?> OnExperienceGained;
}

public sealed class ProgressionDefinition
{
    public ProgressionId Id { get; }
    public string Name { get; }
    public int MaxLevel { get; }
    public IExperienceCurve ExperienceCurve { get; }
    public IReadOnlyList<LevelUpReward> Rewards { get; }
}
```

### 経験値曲線

```csharp
public interface IExperienceCurve
{
    int GetRequiredExperience(int level);
    int GetTotalExperienceForLevel(int level);
    int GetLevelForExperience(int totalExperience);
}

// 組み込み曲線
public sealed class LinearCurve : IExperienceCurve
{
    public int BaseExperience { get; }
    public int PerLevelIncrease { get; }

    public int GetRequiredExperience(int level)
        => BaseExperience + (level - 1) * PerLevelIncrease;
}

public sealed class ExponentialCurve : IExperienceCurve
{
    public int BaseExperience { get; }
    public float GrowthRate { get; }

    public int GetRequiredExperience(int level)
        => (int)(BaseExperience * MathF.Pow(GrowthRate, level - 1));
}

public sealed class TableCurve : IExperienceCurve
{
    public IReadOnlyList<int> ExperienceTable { get; }

    public int GetRequiredExperience(int level)
        => ExperienceTable[Math.Min(level - 1, ExperienceTable.Count - 1)];
}
```

### レベルアップ報酬

```csharp
public abstract class LevelUpReward
{
    public int Level { get; }  // 0 = 毎レベル
    public abstract void Apply(AnyHandle entity, int newLevel);
}

public sealed class StatPointReward : LevelUpReward
{
    public int Points { get; }
}

public sealed class SkillPointReward : LevelUpReward
{
    public int Points { get; }
}

public sealed class StatIncreaseReward : LevelUpReward
{
    public StatId Stat { get; }
    public float Amount { get; }
}

public sealed class AbilityUnlockReward : LevelUpReward
{
    public AbilityId Ability { get; }
}
```

### 複数成長軸の例

```csharp
// プレイヤーの成長軸
public static class ProgressionIds
{
    public static ProgressionId CharacterLevel { get; }  // キャラクターレベル
    public static ProgressionId WeaponMastery_Sword { get; }  // 剣熟練度
    public static ProgressionId WeaponMastery_Bow { get; }    // 弓熟練度
    public static ProgressionId CraftingSkill { get; }        // 製作スキル
    public static ProgressionId Reputation_Guild { get; }     // ギルドランク
}
```

### 設計ポイント

- **StatSystemとの連携**: レベルアップ時のステータス上昇
- **AbilitySystemとの連携**: レベルアップ時のスキルアンロック
- **CombatSystemとの連携**: 敵撃破時の経験値獲得

---

## 実装状況

### ✅ 実装済み

| システム | 場所 | 備考 |
|---------|------|------|
| InventorySystem | libs/systems/InventorySystem/ | クラフティング機能も含む |
| BehaviorTreeSystem | libs/foundation/FlowTree/ | FlowTreeとして実装 |

---

## 優先度検討

### 高（基盤システム）

| システム | 理由 |
|---------|------|
| StatSystem | 他システムの基盤、StatusEffectSystemと密接 |
| ProgressionSystem | RPGの根幹、StatSystemと連携 |

### 中（ゲームプレイ拡張）

| システム | 理由 |
|---------|------|
| AbilitySystem | ActionSelector拡張、戦闘の幅 |
| LootTableSystem | InventorySystemと連携、報酬設計 |
| QuestSystem | ゲーム進行の管理 |

### 低（特定ジャンル向け）

| システム | 理由 |
|---------|------|
| DialogueSystem | ストーリー重視のゲーム向け |
| FormationSystem | RTS/タクティクス向け |
| FactionSystem | 派閥要素があるゲーム向け |

---

## 次のステップ

1. 優先度の高いシステムから詳細設計を行う
2. 既存システムとの連携ポイントを明確化
3. テストケースの洗い出し
4. 実装開始
