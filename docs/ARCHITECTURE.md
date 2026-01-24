# アーキテクチャ概要

本ドキュメントはTomatoフレームワークの全体アーキテクチャを説明する。

---

## 設計哲学

### 核心原則

#### 互換性より正しさ

後方互換性は考慮しない。インターフェースは常に最善の形を追求し、シンプルに保つ。互換性のためにAPIを複雑化させることは許容しない。

#### シンプルさの追求

- 不要な抽象化を避ける
- 一つのことを一つの場所で行う
- 将来の仮定に基づく設計をしない

#### テスト駆動開発（TDD）

全てのコードはテストファーストで開発される。1,603のテストが設計の正しさを証明する。

### ドメイン駆動設計（DDD）

フレームワークは境界づけられたコンテキストで構成される：

| コンテキスト | システム | 責務 |
|-------------|---------|------|
| 統合コンテキスト | **EntitySystem** | 6フェーズゲームループの統括、全システム連携 |
| パイプラインコンテキスト | SystemPipeline | ECSスタイルのシステム実行パイプライン |
| 衝突コンテキスト | CollisionSystem | 空間的な衝突判定とメッセージ発行 |
| 戦闘コンテキスト | CombatSystem | 攻撃とダメージ処理、多段ヒット制御 |
| メッセージコンテキスト | CommandGenerator | メッセージの配送と処理 |
| 行動コンテキスト | ActionSelector, ActionExecutionSystem | 行動の決定と実行 |
| 依存ソートコンテキスト | DependencySortSystem | 汎用トポロジカルソートと循環検出 |
| 調停コンテキスト | ReconciliationSystem | 位置調停と押し出し処理 |
| エンティティコンテキスト | EntityHandleSystem | Entityの生成・管理・コンポーネント |
| スポーンコンテキスト | CharacterSpawnSystem | キャラクターのリソース管理とスポーン |

### 決定論性

- 同じ入力に対して常に同じ結果を保証
- メッセージは優先度順に処理
- 状態変更はMessagePhaseでのみ発生

### 各システムの設計原則

#### EntityHandleSystem: 安全なエンティティ参照

- 世代番号による無効化検出で、削除済みエンティティへのアクセスを防ぐ
- Structure of Arrays (SoA) パターンによるキャッシュ効率の最大化
- コンポーネントベースの設計でECSスタイルの柔軟性を提供
- Source Generatorによるボイラープレート削減

#### SystemPipeline: ECSスタイルのシステムパイプライン

- 3種類の処理パターン: Serial（直列）、Parallel（並列）、MessageQueue（Wave処理）
- IEntityQueryによるエンティティフィルタリング
- QueryCacheによる同一フレーム内クエリ結果キャッシュ
- `[HasCommandQueue]`属性によるEntity単位のキュー管理
- Source GeneratorによるMessageQueueSystem自動生成

#### ActionSelector: 自己判定型アクション選択

- 各ジャッジメントが自身の成立条件を持つ（自己判定）
- 優先度は絶対値で定義（疎結合）
- 相対参照を禁止し、追加・削除が他に影響しない設計

#### CommandGenerator: Wave型処理

- 状態変更はメッセージ処理でのみ発生
- Wave単位で処理し、決定論性を保証
- 優先度ベースの処理順序

#### CollisionSystem: 責務の分離

- 衝突判定のみを担当
- メッセージ発行は別レイヤー（EmitCollisionMessages）で実行
- 形状判定とフィルタリングの分離

#### CombatSystem: ダメージ処理の統一

- HitGroupによる攻撃の同一視（複数ヒット判定を1回の攻撃として扱う）
- HitHistoryは被攻撃側が所有（攻撃が消えても履歴は残る）
- ターゲット判定はアプリ側に委譲（AttackInfo.CanTarget）
- 多段ヒット制御（HittableCount、IntervalTime）

#### EntitySystem: 統合レイヤー

- 6フェーズゲームループの統括（Collision→Message→Decision→Execution→Reconciliation→Cleanup）
- EntityContext によるEntity単位のコンテキスト管理
- IEntityMessageRegistry実装でCommandGeneratorと連携
- SpawnBridgeによるCharacterSpawnSystemとの疎結合

#### CharacterSpawnSystem: リソースライフサイクル

- 状態マシンによるリソースロード/アンロードの管理
- データリソースとGameObjectリソースの分離
- 非同期ロード対応
- EntitySystemとの連携（SpawnBridge経由）

---

## システム依存関係

```
                          ┌─────────────────────────┐
                          │       EntitySystem      │
                          │  (GameLoopOrchestrator) │
                          └───────────┬─────────────┘
                                      │
       ┌──────────────┬───────────────┼───────────────┬──────────────┐
       │              │               │               │              │
       ▼              ▼               ▼               ▼              ▼
┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐
│ Collision  │ │ Command    │ │ Action     │ │ Action     │ │ Character  │
│ System     │ │ Generator  │ │ Selector   │ │ Execution  │ │ SpawnSys   │
└─────┬──────┘ └─────┬──────┘ └─────┬──────┘ └─────┬──────┘ └─────┬──────┘
      │              │              │              │              │
      ▼              │              │              │              │
┌────────────┐       │              │              │              │
│ Combat     │       │              │              │              │
│ System     │       │              │              │              │
└─────┬──────┘       │              │              │              │
      │              │              │              │              │
      └──────────────┴──────────────┴──────┬───────┴──────────────┘
                                           │
                                           ▼
                               ┌─────────────────────┐
                               │   SystemPipeline    │
                               │  (Pipeline/Group)   │
                               └──────────┬──────────┘
                                          │
                                          ▼
                                ┌─────────────────┐
                                │EntityHandleSystem│
                                └─────────────────┘
```

EntitySystemは全システムの統合レイヤーとして機能し、6フェーズゲームループを統括する。SystemPipelineはECSスタイルの実行基盤を提供する。

---

## ゲームループ

### フェーズ構成

```
┌─────────────────────────────────────────────────────────┐
│ Update                                                  │
├─────────────────────────────────────────────────────────┤
│ CollisionPhase                                          │
│   - 全Entityが衝突ボリュームを発行                      │
│   - 衝突判定を実行                                      │
│   - 衝突結果からメッセージを発行                        │
├─────────────────────────────────────────────────────────┤
│ MessagePhase                                            │
│   - 波ごとにメッセージを処理                            │
│   - 優先度順（高→低）で処理                             │
│   - 新メッセージは次波へ                                │
│   - 全波完了まで繰り返し                                │
│   - 【状態変更はここでのみ】                            │
├─────────────────────────────────────────────────────────┤
│ DecisionPhase                                           │
│   - 行動継続中でなければ、次の行動を決定                │
│   - 入力ソース: Controller / AI / Network               │
│   - 【読み取り専用】                                    │
├─────────────────────────────────────────────────────────┤
│ ExecutionPhase                                          │
│   - 決定された行動を実行                                │
│   - 位置・向きの変更                                    │
│   - アニメーション状態の変更                            │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ LateUpdate                                              │
├─────────────────────────────────────────────────────────┤
│ ReconciliationPhase                                     │
│   - 依存順を動的計算                                    │
│   - 依存順に従って位置調停（押し出し等）                │
├─────────────────────────────────────────────────────────┤
│ CleanupPhase                                            │
│   - 消滅フラグが立ったEntityを削除                      │
└─────────────────────────────────────────────────────────┘
```

### データフロー

```
[Input]
   │
   ▼
CollisionPhase ──────────────────────┐
   │                                 │
   │ CollisionResult                 │
   ▼                                 │
EmitCollisionMessages                │
   │                                 │
   │ Message(Damage)                 │
   ▼                                 │
MessagePhase ◀───────────────────────┘
   │
   │ 状態変更
   ▼
DecisionPhase
   │
   │ SelectionResult
   ▼
ExecutionPhase
   │
   │ Position/Rotation変更
   ▼
ReconciliationPhase
   │
   │ 位置調停
   ▼
CleanupPhase
   │
   │ Entity削除
   ▼
[Render]
```

---

## コアコンポーネント

### Entity

ゲーム世界の中心的な概念。EntityHandleSystemで定義し、Source Generatorが自動生成する。

```csharp
[Entity(InitialCapacity = 100)]
public partial class Enemy
{
    public int Health;
    public Vector3 Position;

    [EntityMethod]
    public void TakeDamage(int damage) => Health -= damage;
}

// 自動生成: EnemyArena, EnemyHandle
```

### Message

Entityに対する操作要求。生成後は不変。

```csharp
public readonly struct Message
{
    public readonly VoidHandle Target;
    public readonly MessageType Type;
    public readonly int Priority;
    public readonly VoidHandle Source;
    public readonly IMessagePayload? Payload;
}
```

### CollisionVolume

衝突判定の単位。

```csharp
public readonly struct CollisionVolume
{
    public readonly int OwnerId;
    public readonly ICollisionShape Shape;
    public readonly CollisionFilter Filter;
    public readonly VolumeType VolumeType; // Hitbox, Hurtbox, Pushbox, Trigger
}
```

---

## Wave処理

### 概念

```
時間軸 ────────────────────────────────────────────▶

Wave 0          Wave 1          Wave 2
┌───────┐       ┌───────┐       ┌───────┐
│Entity1│       │Entity1│       │       │
│ Msg A │──┐    │ Msg C │       │       │
│Entity2│  │    │Entity2│       │       │
│ Msg B │  │    │ Msg D │       │       │
└───────┘  │    └───────┘       └───────┘
           │         ▲
           │         │
           └─────────┘
           Msg Aの処理結果
           としてMsg Cが発生
```

### 処理フロー

1. 全EntityのキューからメッセージをDequeue
2. 優先度順にハンドラを実行
3. ハンドラ内で発生した新メッセージは次Waveへ
4. 収束（全キュー空）まで繰り返し
5. 最大深度超過時はDepthExceededを返す

---

## 衝突システム

### ボリュームタイプ

| タイプ | 用途 | 例 |
|-------|------|-----|
| Hitbox | 攻撃判定 | 武器、パンチ |
| Hurtbox | 被ダメージ判定 | 体、頭 |
| Pushbox | 押し出し判定 | 体全体 |
| Trigger | イベントトリガー | アイテム取得範囲 |

### 衝突フィルター

```csharp
[Flags]
public enum CollisionFilter
{
    None = 0,
    PlayerHitbox = 1 << 0,
    PlayerHurtbox = 1 << 1,
    EnemyHitbox = 1 << 2,
    EnemyHurtbox = 1 << 3,
    Trigger = 1 << 4,
    // ...
}
```

### 衝突→メッセージ変換

```
Hitbox vs Hurtbox → DamageMessage
Pushbox vs Pushbox → ReconciliationPhaseで処理
Trigger vs Any → TriggerMessage
```

---

## 行動システム

### ActionSelector

入力とゲーム状態から行動を選択。

```
入力
  │
  ▼
┌────────────────┐
│ジャッジメント群│ (Attack, Jump, Guard, ...)
└────────┬───────┘
         │
         ▼ GetPriority()
┌────────────────┐
│ 候補収集       │ Disabled以外を収集
└────────┬───────┘
         │
         ▼ Sort
┌────────────────┐
│ 優先度ソート   │ 高優先度順
└────────┬───────┘
         │
         ▼ Evaluate
┌────────────────┐
│ 評価           │ Trigger, Condition, カテゴリ排他
└────────┬───────┘
         │
         ▼
SelectionResult (カテゴリ毎の勝者)
```

### ActionStateMachine

選択されたアクションの実行を管理。

```
┌─────────────────────────────────────────┐
│         ActionStateMachine              │
├─────────────────────────────────────────┤
│ Category: Upper                         │
│   └─ CurrentAction: Attack1 (Frame 15)  │
│        ├─ CancelWindow: [10, 25]        │
│        ├─ HitboxWindow: [5, 12]         │
│        └─ TransitionTargets: [Attack2]  │
│                                         │
│ Category: Lower                         │
│   └─ CurrentAction: Walk (Frame 0)      │
└─────────────────────────────────────────┘
```

---

## 位置調停

### 依存順計算

DependencySortSystemを使用してトポロジカルソートを行う。

```
馬B → 騎乗者A → 旗C

処理順: B → A → C

1. DependencyGraph<AnyHandle>にDAGを構築
2. TopologicalSorter<AnyHandle>.Sort()で順序計算
3. 循環検出時はCyclePathで循環経路を取得可能
```

### 押し出しルール

| 組み合わせ | ルール |
|-----------|--------|
| Player vs Wall | Playerが押し出される |
| Player vs 巨大敵 | Playerが押し出される |
| 小型敵 vs 小型敵 | 相互に押し出し |

---

## 拡張ポイント

EntitySystemでは、Providerインターフェースを通じてゲーム固有のロジックを注入する。

### IInputProvider

入力状態の取得。DecisionPhaseで使用。

```csharp
public interface IInputProvider
{
    InputState GetInputState(VoidHandle handle);
}
```

### IActionFactory

アクション生成。ExecutionPhaseで使用。

```csharp
public interface IActionFactory<TCategory> where TCategory : struct, Enum
{
    IExecutableAction<TCategory>? Create(string actionId, TCategory category);
}
```

### IEntityPositionProvider

Entity位置の取得。CollisionPhaseで使用。

```csharp
public interface IEntityPositionProvider
{
    Vector3 GetPosition(VoidHandle handle);
}
```

### IDependencyResolver / IPositionReconciler

位置調停。ReconciliationPhaseで使用。

```csharp
public interface IDependencyResolver
{
    DependencyResolutionResult ResolveDependencies(
        IEnumerable<VoidHandle> entities,
        List<VoidHandle> sortedResult);
}

public interface IPositionReconciler
{
    void Reconcile(VoidHandle handle);
}
```

### IMessageHandlerRegistry

メッセージハンドラの登録。

```csharp
public interface IMessageHandlerRegistry
{
    void Handle(in Message message, in MessageHandlerContext context);
}
```

### IDamageCalculator

ダメージ計算ロジックのカスタマイズ。

```csharp
public interface IDamageCalculator
{
    int CalculateDamage(CollisionVolume hitbox, CollisionVolume hurtbox, in CollisionContact contact);
}
```

---

## パフォーマンス考慮

### 並列化可能なフェーズ

| フェーズ | 並列化 | 備考 |
|----------|--------|------|
| CollisionPhase | ○ | Entity単位で並列化可能 |
| MessagePhase | △ | Wave内は並列可、Wave間は逐次 |
| DecisionPhase | ○ | 読み取り専用のため安全 |
| ExecutionPhase | ○ | 位置競合はLateUpdateで調停 |
| ReconciliationPhase | △ | 依存順に逐次処理 |

### アロケーション削減

- メッセージキューはプリアロケート
- CollisionVolumeリストは再利用
- SelectionBufferはフレーム間で再利用
