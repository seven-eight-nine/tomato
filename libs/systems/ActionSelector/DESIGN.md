# ActionSelector 設計書

ゲーム向けアクション選択ライブラリの詳細設計ドキュメント。

namespace: `Tomato.ActionSelector`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [セットアップ](#セットアップ)
5. [ジャッジメント詳細](#ジャッジメント詳細)
6. [プライオリティ詳細](#プライオリティ詳細)
7. [入力トリガー詳細](#入力トリガー詳細)
8. [条件（Condition）詳細](#条件condition詳細)
9. [リゾルバ詳細](#リゾルバ詳細)
10. [カテゴリシステム](#カテゴリシステム)
11. [選択アルゴリズム](#選択アルゴリズム)
12. [JudgmentList詳細](#judgmentlist詳細)
13. [AI制御](#ai制御)
14. [デバッグ](#デバッグ)
15. [パフォーマンス](#パフォーマンス)
16. [実践パターン集](#実践パターン集)
17. [トラブルシューティング](#トラブルシューティング)

---

## クイックスタート

### 1. ゲーム固有のセレクタを定義

```csharp
using Tomato.ActionSelector;

// ActionSelectorを継承するだけ。ボディは空でOK。
public class FighterActionSelector
    : ActionSelector<ActionCategory, InputState, GameState> { }
```

これにより `FighterActionSelector.Judgment`, `FighterActionSelector.Builder`, `FighterActionSelector.List` 等の短い型名が使えるようになる。

### 2. ジャッジメントを定義

```csharp
using static Tomato.ActionSelector.Buttons;
using static Tomato.ActionSelector.Priorities;
using static Tomato.ActionSelector.Trig;
using static Tomato.ActionSelector.Cond;

FighterActionSelector.Judgment attack, jump, idle;

FighterActionSelector.Builder.Begin()
    .AddJudgment(ActionCategory.FullBody, Normal)
        .Input(Press(Attack))
        .Label("Attack")
        .Out(out attack)
    .AddJudgment(ActionCategory.FullBody, High)
        .Input(Press(Jump))
        .Condition(Grounded)
        .Label("Jump")
        .Out(out jump)
    .AddJudgment(ActionCategory.FullBody, Lowest)
        .Input(Always)
        .Label("Idle")
        .Out(out idle)
    .Done();
```

### 3. リストに登録してフレーム処理

```csharp
var selector = new FighterActionSelector();
var list = new FighterActionSelector.List()
    .Add(attack)
    .Add(jump)
    .Add(idle);

// 毎フレーム
void Update()
{
    var result = selector.ProcessFrame(list, in gameState);

    if (result.TryGetRequested(ActionCategory.FullBody, out var requested))
    {
        ExecuteAction(requested.Label);
    }
}
```

---

## 用語定義

### 中核概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **アクション** | Action | キャラクターが実行する動作。「攻撃」「ジャンプ」「ガード」など。選択の結果として実行される。 |
| **ジャッジメント** | Judgment | アクションの**成立条件を表明する宣言**。入力条件・状態条件・優先度を持つ。「攻撃ボタンを押したら攻撃」のような宣言。 |
| **選択** | Selection | 複数のジャッジメントから**リクエストを決定するプロセス**。毎フレーム実行される。 |

### 選択の流れ（処理順）

各ジャッジメントは以下の順序で評価される。どこかで失敗するとスキップされ、次のジャッジメントの評価へ移る。

| 順序 | 用語 | 英語 | 説明 |
|:----:|------|------|------|
| 1 | **プライオリティ** | Priority | 優先順位。Disabledなら除外、それ以外は優先度順にソート。 |
| 2 | **入力** | Input | 入力条件。ボタン押下、コマンド入力など。不成立→スキップ。 |
| 3 | **コンディション** | Condition | 状態条件。接地中、HP残量など。不成立→スキップ。 |
| 4 | **カテゴリ** | Category | 同時実行グループ。既に埋まっている→スキップ。 |
| 5 | **リゾルバ** | Resolver | 実行アクションの決定（オプション）。Noneを返す→スキップ。 |
| → | **リクエスト** | Requested | すべて通過→選択される。カテゴリごとに最大1つ。 |

### 補足用語

| 用語 | 英語 | 定義 |
|------|------|------|
| **フレーム状態** | FrameState | 1フレームの入力とゲーム状態をまとめた構造体。 |
| **評価** | Evaluation | ジャッジメントを処理順に沿ってチェックすること。 |
| **排他性** | Exclusivity | カテゴリ間の同時実行可否を定義するルール。 |

---

## 設計哲学

### 原則1: 自己判定（Self-Judgment）

各ジャッジメントは、自分が成立するかどうかを自分で判定する。外部のマネージャーが「今は攻撃できる状態か？」を判断するのではなく、ジャッジメント自身が入力条件と状態条件を評価する。

```csharp
public interface IActionJudgment<TCategory, TInput, TContext>
{
    IInputTrigger<TInput>? Input { get; }      // 入力条件（自分で持つ）
    ICondition<TContext>? Condition { get; }   // 状態条件（自分で持つ）
}
```

**メリット:**
- ジャッジメントの追加・削除が容易（他に影響しない）
- 条件ロジックがジャッジメントに局所化される
- テストが書きやすい

### 原則2: 疎結合（Loose Coupling）

ジャッジメント間に依存関係を持たせない。

**悪い例（相対参照）:**
```csharp
// ❌ 他のジャッジメントを参照している
attackJudgment.Priority = jumpJudgment.Priority + 1;
```

**良い例（絶対値）:**
```csharp
// ✓ 絶対値で定義
attackJudgment.Priority = new ActionPriority(1, 0, 0);
jumpJudgment.Priority = new ActionPriority(0, 5, 0);
```

**メリット:**
- ジャッジメントの追加・削除が他に影響しない
- 優先度の一覧表で全体を把握できる
- 循環依存が発生しない

### 原則3: 優先度による競合解決（Priority-Based Resolution）

同じ入力で複数のジャッジメントが成立する場合、優先度でリクエストされたアクションを決める。

```
Priority   | Label           | Category   | 説明
-----------|-----------------|------------|------------------
(0,0,0)    | EmergencyDodge  | FullBody   | 緊急回避（最優先）
(0,1,0)    | JustGuard       | FullBody   | ジャストガード
(0,5,0)    | SpecialAttack   | FullBody   | 必殺技
(1,0,0)    | NormalAttack    | FullBody   | 通常攻撃
(2,0,0)    | Walk            | FullBody   | 歩行
(3,0,0)    | Idle            | FullBody   | 待機（最低優先）
```

**優先度の一覧表を作れることが重要。** デバッグ時に「なぜこのアクションが選ばれたか」がすぐわかる。

### 原則4: カテゴリによる同時実行制御（Category-Based Concurrency）

カテゴリは「同時に1つしか実行できないアクションのグループ」を定義する。

```csharp
public enum ActionCategory
{
    FullBody,    // 全身を使うアクション（他と排他）
    UpperBody,   // 上半身のみ（下半身と同時実行可能）
    LowerBody,   // 下半身のみ（上半身と同時実行可能）
}
```

**例: 上半身で射撃しながら下半身で歩く**
- UpperBody: Shoot（選択）
- LowerBody: Walk（選択）
- FullBody: なし

### 原則5: 入力トリガーと条件の分離（Input vs Condition）

入力トリガー（Input）と条件（Condition）は明確に役割が異なる。

| | 入力トリガー (IInputTrigger) | 条件 (ICondition) |
|---|---|---|
| **状態** | 内部状態を**持つ** | 内部状態を**持たない** |
| **役割** | 入力の検出・蓄積 | ゲーム状態の判定 |
| **ライフサイクル** | Start/Stop/Update を受け取る | なし（純粋関数） |
| **例** | チャージ時間、コマンド進行、連打回数 | 接地中、HP残量、ターゲット有無 |

**入力トリガー**: 時間経過で状態が変わる。「いつ押されたか」「どれだけ溜めたか」を追跡する。

```csharp
public interface IInputTrigger<TInput>
{
    bool IsTriggered(in TInput input);
    void OnJudgmentStart();   // 状態初期化
    void OnJudgmentStop();    // 状態クリア
    void OnJudgmentUpdate(in TInput input, int deltaTicks);  // 毎tick更新
}

// 例: チャージトリガー（内部でチャージtick数を蓄積）
public class ChargeTrigger : IInputTrigger<InputState>
{
    private int _chargeTicks;   // ← 内部状態
    private int _chargeLevel;   // ← 内部状態

    public void OnJudgmentUpdate(in InputState input, int deltaTicks)
    {
        if (input.IsHeld(_button))
            _chargeTicks += deltaTicks;  // 蓄積
    }

    public bool IsTriggered(in InputState input)
    {
        // ボタンを離した瞬間 && チャージ完了
        return input.IsReleased(_button) && _chargeLevel > 0;
    }
}
```

**条件**: 現在のゲーム状態だけを見て判定する。同じ状態なら常に同じ結果。

```csharp
public interface ICondition<TContext>
{
    bool Evaluate(in TContext context);  // これだけ
}

// 例: 接地判定（状態を見るだけ、蓄積なし）
public class GroundedCondition : ICondition<GameState>
{
    public bool Evaluate(in GameState context)
        => context.Character.IsGrounded;  // 参照のみ
}
```

**なぜ分けるのか？**

1. **テスタビリティ**: 条件は純粋関数なのでテストが容易
2. **再利用性**: 条件は複数のジャッジメントで共有しやすい（シングルトン化可能）
3. **予測可能性**: 条件は同じ状態で必ず同じ結果を返す
4. **責務の明確化**: 「入力の追跡」と「状態の判定」を混ぜない

**判断基準:**
- 「時間経過で結果が変わる」→ 入力トリガー
- 「現在の状態だけで決まる」→ 条件

---

## セットアップ

### ステップ1: カテゴリを定義

ゲームで必要な同時実行グループをenumで定義する。

```csharp
// シンプルな例（全身アクションのみ）
public enum SimpleCategory
{
    Action
}

// 格闘ゲーム向け
public enum FighterCategory
{
    FullBody     // 全身（すべて排他）
}

// アクションRPG向け（上半身/下半身分離）
public enum ActionCategory
{
    FullBody,    // 全身を使うアクション
    UpperBody,   // 上半身のみ
    LowerBody    // 下半身のみ
}

// シューター向け
public enum ShooterCategory
{
    Movement,    // 移動系
    Weapon,      // 武器操作
    Ability,     // アビリティ
    Interaction  // インタラクション
}
```

### ステップ2: ゲーム固有のセレクタを定義

ActionSelectorを継承する。これにより内部型に短い名前でアクセスできる。

```csharp
using Tomato.ActionSelector;

// 最小限の定義（デフォルト設定）
public class FighterActionSelector
    : ActionSelector<ActionCategory, InputState, GameState> { }
```

これで以下の型が使えるようになる：

| 型名 | 元の型 | 説明 |
|------|--------|------|
| `FighterActionSelector.Judgment` | `SimpleJudgment<...>` | ジャッジメント |
| `FighterActionSelector.Builder` | `JudgmentBuilder<...>` | Fluent APIビルダー |
| `FighterActionSelector.List` | `JudgmentList<...>` | ジャッジメントリスト |
| `FighterActionSelector.Result` | `SelectionResult<...>` | 選択結果 |
| `FighterActionSelector.Frame` | `FrameState<...>` | フレーム状態 |

### ステップ3: カスタム排他ルール（オプション）

デフォルトでは同一カテゴリのみ排他（NoExclusivity）。カテゴリ間の排他が必要な場合はカスタムルールを定義する。

```csharp
public class ActionCategoryRules : CategoryRules<ActionCategory>
{
    public static readonly ActionCategoryRules Instance = new();

    public override bool AreExclusive(ActionCategory a, ActionCategory b)
    {
        // 同一カテゴリは常に排他
        if (a == b) return true;

        // FullBodyは他のすべてと排他
        if (a == ActionCategory.FullBody || b == ActionCategory.FullBody)
            return true;

        // UpperBodyとLowerBodyは独立（同時実行可能）
        return false;
    }
}

// セレクタでルールを指定
public class FighterActionSelector
    : ActionSelector<ActionCategory, InputState, GameState>
{
    public FighterActionSelector() : base(ActionCategoryRules.Instance) { }
}
```

**組み込みルール:**

| ルール | 説明 |
|--------|------|
| `NoExclusivity` | 同一カテゴリのみ排他（デフォルト） |
| `FullExclusivity` | 全カテゴリが排他（1フレームで1アクションのみ） |

```csharp
// 全カテゴリ排他にする場合
public class SimpleActionSelector
    : ActionSelector<ActionCategory, InputState, GameState>
{
    public SimpleActionSelector() : base(FullExclusivity) { }
}
```

---

## ジャッジメント詳細

### インターフェース

```csharp
public interface IActionJudgment<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    /// <summary>ジャッジメントのラベル（識別用）。</summary>
    string Label { get; }

    /// <summary>所属カテゴリ。</summary>
    TCategory Category { get; }

    /// <summary>入力条件。nullまたはIsTriggered=falseで不成立。</summary>
    IInputTrigger<TInput>? Input { get; }

    /// <summary>状態条件。nullは常に成立。</summary>
    ICondition<TContext>? Condition { get; }

    /// <summary>リゾルバ。成立後にアクションを動的に決定（オプション）。</summary>
    IActionResolver<TInput, TContext>? Resolver { get; }

    /// <summary>分類タグ（デバッグ・フィルタリング用）。</summary>
    ReadOnlySpan<string> Tags { get; }

    /// <summary>現在の状態における優先度を返す。</summary>
    ActionPriority GetPriority(in FrameState<TInput, TContext> state);
}
```

### Fluent APIでの定義

```csharp
FighterActionSelector.Judgment attack, jump, special, idle;

FighterActionSelector.Builder.Begin()
    // 通常攻撃
    .AddJudgment(ActionCategory.FullBody, Normal)
        .Input(Press(Attack))
        .Label("Attack")
        .Out(out attack)

    // ジャンプ（接地時のみ）
    .AddJudgment(ActionCategory.FullBody, High)
        .Input(Press(Jump))
        .Condition(Grounded)
        .Label("Jump")
        .Out(out jump)

    // 必殺技（コマンド入力）
    .AddJudgment(ActionCategory.FullBody, Highest)
        .Input(Cmd(Down, DownRight, Right.Plus(Punch)))
        .Condition(Grounded)
        .Label("Hadouken")
        .Tags("Special", "Projectile")
        .Out(out special)

    // 待機（常に成立、最低優先度）
    .AddJudgment(ActionCategory.FullBody, Lowest)
        .Input(Always)
        .Label("Idle")
        .Out(out idle)

    .Done();
```

### Builder APIリファレンス

| メソッド | 説明 | 必須 |
|----------|------|:----:|
| `AddJudgment(category, priority)` | アクション追加を開始 | ✓ |
| `AddJudgment(category, dynamicPriority)` | 動的優先度でアクション追加 | ✓ |
| `.Input(trigger)` | 入力条件を設定 | |
| `.Condition(condition)` | 状態条件を設定 | |
| `.Resolver(resolver)` | リゾルバを設定 | |
| `.Label(name)` | アクション名を設定 | |
| `.Label(name, description)` | アクション名と説明を設定 | |
| `.Tags(...)` | タグを設定 | |
| `.Out(out judgment)` | ジャッジメントを変数に取り出し | |
| `.Done()` | ビルド完了 | ✓ |

**注意点:**
- `.Input()` を省略するとInput=null（入力不成立扱い）
- `.Condition()` を省略するとCondition=null（常に成立）
- `.Label()` を省略すると自動生成名（"Action_1" など）
- メソッドの呼び出し順序は自由

### Input/Conditionの複数回呼び出し

**`.Input()`** を複数回呼ぶと、**最後の設定のみが有効**になる：

```csharp
.AddJudgment(ActionCategory.FullBody, Normal)
    .Input(Press(Attack))   // 上書きされる
    .Input(Press(Jump))     // これが有効
    .Label("OnlyJump")
    .Out(out j)
```

**`.Condition()`** を複数回呼ぶと、**すべてがANDで結合**される：

```csharp
.AddJudgment(ActionCategory.FullBody, Normal)
    .Input(Press(Attack))
    .Condition(Grounded)           // 接地中
    .Condition(HealthAbove(0.5f))  // かつ HP50%以上
    .Condition(HasTarget)          // かつ ターゲットあり
    .Label("ConditionalAttack")
    .Out(out conditionalAttack)

// 上記は以下と同等
.Condition(Grounded.And(HealthAbove(0.5f)).And(HasTarget))
```

### 動的優先度

状態に応じて優先度を変えられる。

```csharp
// HP20%未満なら最優先、それ以外は通常
.AddJudgment(ActionCategory.FullBody,
    state => state.Context.HealthRatio < 0.2f ? Highest : Normal)
    .Input(Press(Dash))
    .Label("EmergencyDodge")
    .Out(out emergencyDodge)

// スタミナ切れなら無効化
.AddJudgment(ActionCategory.FullBody,
    state => state.Context.Character.Stamina > 0 ? Normal : Disabled)
    .Input(Press(Attack))
    .Label("Attack")
    .Out(out attack)

// コンボ中は優先度を上げる
.AddJudgment(ActionCategory.FullBody,
    state => state.Context.Combat.IsInCombo ? High : Normal)
    .Input(Press(Attack))
    .Label("ComboAttack")
    .Out(out comboAttack)
```

### InputとConditionの省略パターン

| Input | Condition | 用途 |
|-------|-----------|------|
| 設定 | 設定 | 通常のプレイヤー操作 |
| 設定 | 省略 | 状態を問わない入力（ポーズボタンなど） |
| 省略 | 設定 | AI専用（ForceInputで発動） |
| 省略 | 省略 | 無効（常に不成立） |

---

## プライオリティ詳細

### 構造

優先度は3層構造（Layer, Group, Detail）で構成される。小さい値ほど高優先。

```csharp
public readonly struct ActionPriority : IComparable<ActionPriority>
{
    public readonly int Layer;   // 大分類（0が最高優先）
    public readonly int Group;   // 中分類
    public readonly int Detail;  // 小分類（微調整）
}
```

比較は Layer → Group → Detail の順に行われる。

### セマンティック定数

```csharp
ActionPriority.Highest   // (0, 0, 0) - 緊急回避、無敵技
ActionPriority.High      // (0, 1, 0) - ジャストガード、カウンター
ActionPriority.Normal    // (1, 0, 0) - 通常攻撃、通常ジャンプ
ActionPriority.Low       // (2, 0, 0) - 歩行、しゃがみ
ActionPriority.Lowest    // (3, 0, 0) - 待機（デフォルトアクション）
ActionPriority.Disabled  // (∞, ∞, ∞) - 評価から除外
```

### 短縮名

```csharp
using static Tomato.ActionSelector.Priorities;

// 以下は同等
.AddJudgment(ActionCategory.FullBody, ActionPriority.Normal)
.AddJudgment(ActionCategory.FullBody, Normal)
```

### 相対操作

```csharp
var attack1 = Normal;              // (1, 0, 0)
var attack2 = Normal.Lower();      // (1, 0, 1)
var attack3 = Normal.Lower(2);     // (1, 0, 2)
var special = Normal.Higher();     // (1, 0, 0) - Detailは0が下限

// Group変更
var highAttack = Normal.WithGroup(1);  // (1, 1, 0)
```

### 優先度設計のガイドライン

```
Layer 0: 割り込み不可・緊急アクション
├── Group 0: 緊急回避、バースト
├── Group 1: ジャストガード、パリィ
├── Group 2: 必殺技、超必殺技
└── Group 3: 特殊技

Layer 1: 通常アクション
├── Group 0: 通常攻撃
├── Group 1: ジャンプ、ダッシュ
└── Group 2: ガード

Layer 2: 移動系
├── Group 0: 走り
└── Group 1: 歩き

Layer 3: デフォルト
└── Group 0: 待機
```

---

## 入力トリガー詳細

### インターフェース

```csharp
public interface IInputTrigger<TInput>
{
    /// <summary>現在の入力でトリガーされているか。</summary>
    bool IsTriggered(in TInput input);

    /// <summary>ジャッジメントがアクティブになった時（内部状態の初期化）。</summary>
    void OnJudgmentStart();

    /// <summary>ジャッジメントが非アクティブになった時（内部状態のクリア）。</summary>
    void OnJudgmentStop();

    /// <summary>毎tick呼ばれる（状態の更新）。</summary>
    void OnJudgmentUpdate(in TInput input, int deltaTicks);
}
```

### 組み込みトリガー一覧

```csharp
using static Tomato.ActionSelector.Trig;

// === 基本入力 ===
Press(Attack)              // ボタンを押した瞬間
Release(Attack)            // ボタンを離した瞬間
Hold(Attack)               // ボタンを押している間
Hold(Attack, 30)           // 30 tick以上押し続けた

// === 高度な入力 ===
Charge(Attack, 60, 120, 180) // 段階的チャージ（60/120/180 tickで各レベル）
Mash(Attack, 5, 60)          // 60 tick以内に5回押下
Simultaneous(L1, R1)       // L1+R1同時押し

// === コマンド入力 ===
Cmd(Down, DownRight, Right.Plus(Punch))        // ↓↘→+P（波動拳）
Cmd(Right, Down, DownRight.Plus(Punch))        // →↓↘+P（昇龍拳）
Cmd(Down, Down.Plus(Kick))                     // ↓↓+K

// === 特殊 ===
Always                     // 常に成立（デフォルトアクション用）
Never                      // 常に不成立（無効化用）
```

### ライフサイクル

エンジンがトリガーのライフサイクルを管理する。

```
┌─────────────────────────────────────────────────────────────┐
│ ジャッジメントがリストに追加された                              │
│   ↓                                                          │
│ OnJudgmentStart() ← 内部状態を初期化                          │
│   ↓                                                          │
│ ┌─── 毎フレーム ───────────────────────────────────────────┐ │
│ │ OnJudgmentUpdate(input, deltaTicks) ← 状態を更新         │ │
│ │ IsTriggered(input) ← 成立判定                            │ │
│ └──────────────────────────────────────────────────────────┘ │
│   ↓                                                          │
│ ジャッジメントがリストから除外された                            │
│   ↓                                                          │
│ OnJudgmentStop() ← 内部状態をクリア                           │
└─────────────────────────────────────────────────────────────┘
```

### チャージトリガーの実装例

```csharp
public class ChargeTrigger : IInputTrigger<InputState>
{
    private readonly ButtonType _button;
    private readonly int[] _thresholds;  // チャージ段階の閾値（tick）

    private int _chargeTicks;
    private int _chargeLevel;
    private bool _released;

    public int ChargeLevel => _chargeLevel;

    public ChargeTrigger(ButtonType button, params int[] thresholds)
    {
        _button = button;
        _thresholds = thresholds;
    }

    public bool IsTriggered(in InputState input)
    {
        // ボタンを離した瞬間 && チャージ完了
        return _released && _chargeLevel > 0;
    }

    public void OnJudgmentStart()
    {
        _chargeTicks = 0;
        _chargeLevel = 0;
        _released = false;
    }

    public void OnJudgmentStop()
    {
        _chargeTicks = 0;
        _chargeLevel = 0;
        _released = false;
    }

    public void OnJudgmentUpdate(in InputState input, int deltaTicks)
    {
        if (input.IsHeld(_button))
        {
            // チャージ中
            _chargeTicks += deltaTicks;
            _released = false;

            // レベルアップ判定
            while (_chargeLevel < _thresholds.Length &&
                   _chargeTicks >= _thresholds[_chargeLevel])
            {
                _chargeLevel++;
            }
        }
        else if (input.IsReleased(_button))
        {
            // 離した瞬間
            _released = true;
        }
        else
        {
            // 離した次のフレームでリセット
            if (_released)
            {
                _chargeTicks = 0;
                _chargeLevel = 0;
                _released = false;
            }
        }
    }
}
```

### コマンド入力の構文

```csharp
using static Tomato.ActionSelector.Trig;
using static Tomato.ActionSelector.Dirs;  // または NumPad

// 方向のみ
Cmd(Down, DownRight, Right)

// 方向 + ボタン（最後にPlus）
Cmd(Down, DownRight, Right.Plus(Punch))

// 方向 + 複数ボタン
Cmd(Down, DownRight, Right.Plus(Punch, Kick))

// テンキー表記
using static Tomato.ActionSelector.NumPad;
Cmd(_2, _3, _6.Plus(P))      // 236P（波動拳）
Cmd(_6, _2, _3.Plus(P))      // 623P（昇龍拳）
Cmd(_2, _1, _4.Plus(K))      // 214K（竜巻）
Cmd(_6, _3, _2, _1, _4.Plus(P))  // 63214P（半回転）
```

---

## 条件（Condition）詳細

### インターフェース

```csharp
public interface ICondition<TContext>
{
    /// <summary>条件を評価する。</summary>
    bool Evaluate(in TContext context);
}
```

条件は**純粋関数**。同じ入力に対して常に同じ結果を返す。内部状態を持たない。

### 組み込み条件一覧

```csharp
using static Tomato.ActionSelector.Cond;

// === 基本条件 ===
Always                     // 常に成立
Never                      // 常に不成立
Grounded                   // 接地中
Airborne                   // 空中

// === リソース条件 ===
HealthAbove(0.5f)          // HP 50%以上
HealthBelow(0.3f)          // HP 30%以下
ResourceAbove("mp", 30)    // MP 30以上
ResourceBelow("stamina", 10)
CooldownReady("skill1")    // クールダウン完了

// === 戦闘条件 ===
HasTarget                  // ターゲットあり
TargetInRange(5.0f)        // ターゲットが5m以内
TargetOutOfRange(10.0f)    // ターゲットが10m以上離れている
InCombo                    // コンボ中
CanCancel                  // キャンセル可能状態

// === カスタム条件 ===
From(state => state.Character.IsRunning)
```

### 条件の合成

```csharp
// 拡張メソッド
Grounded.And(HealthAbove(0.5f))    // 接地中 かつ HP50%以上
Airborne.Or(InCombo)               // 空中 または コンボ中
Grounded.Not()                     // 接地していない

// Cクラス（演算子使用）
using static Tomato.ActionSelector.C;

C.Grounded & C.HealthAbove(0.5f)   // AND
C.Airborne | C.InCombo             // OR
!C.Grounded                        // NOT

// 複雑な条件
var canSpecial = C.Grounded & C.ResourceAbove("mp", 50) & !C.InCombo;
```

### カスタム条件の実装

```csharp
// シングルトンパターン（状態を持たないので共有可能）
public sealed class GroundedCondition : ICondition<GameState>
{
    public static readonly GroundedCondition Instance = new();
    private GroundedCondition() { }

    public bool Evaluate(in GameState context)
        => context.Character.IsGrounded;
}

// パラメータ付き
public sealed class HealthAboveCondition : ICondition<GameState>
{
    private readonly float _threshold;

    public HealthAboveCondition(float threshold)
    {
        _threshold = threshold;
    }

    public bool Evaluate(in GameState context)
        => context.Character.HealthRatio >= _threshold;
}

// ファクトリクラス
public static class MyCond
{
    public static ICondition<GameState> Grounded
        => GroundedCondition.Instance;

    public static ICondition<GameState> HealthAbove(float ratio)
        => new HealthAboveCondition(ratio);
}
```

---

## リゾルバ詳細

### 概要

リゾルバは、ジャッジメント成立後に**実行するアクションを動的に決定**する。1つのジャッジメントから複数種類のアクションを出し分けたり、パラメータを付与したりできる。

### インターフェース

```csharp
public interface IActionResolver<TInput, TContext>
{
    /// <summary>アクションを解決する。</summary>
    ResolvedAction Resolve(in FrameState<TInput, TContext> state);
}

public readonly struct ResolvedAction
{
    public readonly string? Label;
    public readonly object? Parameter;

    public bool IsNone => Label == null;

    public static readonly ResolvedAction None = default;

    public ResolvedAction(string label, object? parameter = null);

    public T? GetParameter<T>();
}
```

### 基本的な使い方

```csharp
// ラムダ式で直接指定
.AddJudgment(ActionCategory.FullBody, Normal)
    .Input(Press(Attack))
    .Resolver(state => state.Context.Character.IsGrounded
        ? new ResolvedAction("GroundAttack", damage: 10)
        : new ResolvedAction("AirAttack", damage: 5))
    .Label("Attack")
    .Out(out attack)

// 文字列だけ返す場合（暗黙変換）
.Resolver(state => state.Context.Character.IsGrounded
    ? "GroundAttack"
    : "AirAttack")
```

### ResolvedAction.None

リゾルバが条件に合うアクションを見つけられない場合は `ResolvedAction.None` を返す。エンジンは `None` を受け取るとこのジャッジメントをスキップし、次のジャッジメントの評価を継続する。

```csharp
// HP50%以上でないと攻撃できない
.AddJudgment(ActionCategory.FullBody, Highest)
    .Input(Press(Attack))
    .Resolver(state => state.Context.HealthRatio >= 0.5f
        ? new ResolvedAction("FullPowerAttack", damage: 100)
        : ResolvedAction.None)  // 該当なし → 次のジャッジメントへ
    .Label("ConditionalAttack")
    .Out(out conditionalAttack)

.AddJudgment(ActionCategory.FullBody, Normal)
    .Input(Press(Attack))
    .Label("WeakAttack")  // フォールバック
    .Out(out weakAttack)
```

### パラメータの取得

```csharp
var result = selector.ProcessFrame(list, in state);
if (result.TryGetRequested(ActionCategory.FullBody, out var judgment))
{
    if (judgment.Resolver != null)
    {
        var resolved = judgment.Resolver.Resolve(in frameState);
        var damage = resolved.GetParameter<int>();  // パラメータを取得
        ExecuteAction(resolved.Label, damage);
    }
    else
    {
        ExecuteAction(judgment.Label);
    }
}
```

### カスタムリゾルバの実装

```csharp
// コンボリゾルバ
public class ComboResolver : IActionResolver<InputState, GameState>
{
    private readonly string[] _sequence = { "Punch1", "Punch2", "Punch3", "Finisher" };
    private int _index = 0;

    public ResolvedAction Resolve(in FrameState<InputState, GameState> state)
    {
        var actionId = _sequence[_index];
        var multiplier = 1.0f + (_index * 0.2f);

        _index = (_index + 1) % _sequence.Length;

        return new ResolvedAction(actionId, multiplier);
    }

    public void Reset() => _index = 0;
}
```

---

## カテゴリシステム

### 排他性の種類

| ルール | 説明 |
|--------|------|
| **NoExclusivity** | 同一カテゴリのみ排他（デフォルト） |
| **FullExclusivity** | 全カテゴリが排他 |
| **カスタム** | 独自のルールを定義 |

### NoExclusivity（デフォルト）

同じカテゴリのジャッジメントは1つしか選択されない。異なるカテゴリは同時に選択可能。

```
カテゴリ: UpperBody, LowerBody

結果:
  UpperBody: Shoot（選択）
  LowerBody: Walk（選択）
  → 上半身で射撃しながら歩ける
```

### FullExclusivity

すべてのカテゴリが排他。1フレームで1つのアクションのみ。

```csharp
public class SimpleActionSelector
    : ActionSelector<ActionCategory, InputState, GameState>
{
    public SimpleActionSelector() : base(FullExclusivity) { }
}
```

### カスタムルールの例

```csharp
public class ActionCategoryRules : CategoryRules<ActionCategory>
{
    public static readonly ActionCategoryRules Instance = new();

    public override bool AreExclusive(ActionCategory a, ActionCategory b)
    {
        // 同一カテゴリは常に排他
        if (a == b) return true;

        // FullBodyは他のすべてと排他
        if (a == ActionCategory.FullBody || b == ActionCategory.FullBody)
            return true;

        // UpperBodyとLowerBodyは独立
        return false;
    }
}
```

**排他マトリクス:**

|          | FullBody | UpperBody | LowerBody |
|----------|:--------:|:---------:|:---------:|
| FullBody | ✗        | ✗         | ✗         |
| UpperBody| ✗        | ✗         | ○         |
| LowerBody| ✗        | ○         | ✗         |

---

## 選択アルゴリズム

### 処理フロー

```
ProcessFrame(list, state)
│
├─1. ライフサイクル管理
│   ├─ 新規ジャッジメント → Input.OnJudgmentStart()
│   ├─ 継続ジャッジメント → Input.OnJudgmentUpdate()
│   └─ 除外ジャッジメント → Input.OnJudgmentStop()
│
├─2. 候補収集
│   ├─ 各ジャッジメントの GetPriority() を呼ぶ
│   └─ Disabled 以外を候補リストに追加
│
├─3. 優先度ソート
│   └─ 候補を優先度の昇順（高優先が先）にソート
│
├─4. リクエスト決定（優先度順に評価）
│   │
│   │  ┌─────────────────────────────────────┐
│   └─►│ FOR each candidate (優先度順)       │
│      │   │                                 │
│      │   ├─ Input == null?                 │
│      │   │   └─ YES → スキップ             │
│      │   │                                 │
│      │   ├─ Input.IsTriggered() == false?  │
│      │   │   └─ YES → スキップ             │
│      │   │                                 │
│      │   ├─ Condition?.Evaluate() == false?│
│      │   │   └─ YES → スキップ             │
│      │   │                                 │
│      │   ├─ カテゴリが既に埋まっている?      │
│      │   │   └─ YES → スキップ             │
│      │   │                                 │
│      │   ├─ 排他カテゴリが埋まっている?      │
│      │   │   └─ YES → スキップ             │
│      │   │                                 │
│      │   ├─ Resolver?.Resolve().IsNone?    │
│      │   │   └─ YES → スキップ             │
│      │   │                                 │
│      │   └─ ★ リクエストとして登録          │
│      │       カテゴリを埋める               │
│      │       排他カテゴリも埋める           │
│      └─────────────────────────────────────┘
│
└─5. 結果返却
    └─ SelectionResult を返す
```

### 擬似コード

```csharp
public SelectionResult ProcessFrame(JudgmentList list, in FrameState state)
{
    // 1. ライフサイクル管理
    ManageLifecycle(list, state);

    // 2. 候補収集
    var candidates = new List<(Judgment, Priority)>();
    foreach (var entry in list)
    {
        var priority = entry.GetEffectivePriority(state);
        if (!priority.IsDisabled)
            candidates.Add((entry.Judgment, priority));
    }

    // 3. 優先度ソート
    candidates.Sort((a, b) => a.Priority.CompareTo(b.Priority));

    // 4. リクエスト決定
    var filledCategories = new HashSet<Category>();
    var requested = new Dictionary<Category, Judgment>();

    foreach (var (judgment, priority) in candidates)
    {
        // 入力チェック
        if (judgment.Input == null) continue;
        if (!judgment.Input.IsTriggered(state.Input)) continue;

        // 条件チェック
        if (judgment.Condition != null && !judgment.Condition.Evaluate(state.Context))
            continue;

        // カテゴリチェック
        if (filledCategories.Contains(judgment.Category)) continue;
        if (HasExclusiveConflict(judgment.Category, filledCategories)) continue;

        // リゾルバチェック
        if (judgment.Resolver != null && judgment.Resolver.Resolve(state).IsNone)
            continue;

        // リクエストとして登録
        requested[judgment.Category] = judgment;
        filledCategories.Add(judgment.Category);
        MarkExclusiveCategories(judgment.Category, filledCategories);
    }

    // 5. 結果返却
    return new SelectionResult(requested);
}
```

---

## JudgmentList詳細

### 基本操作

```csharp
var list = new FighterActionSelector.List();

// 追加
list.Add(attack);
list.Add(jump);
list.Add(idle);

// チェーン可能
var list = new FighterActionSelector.List()
    .Add(attack)
    .Add(jump)
    .Add(idle);

// 複数追加
list.AddRange(new[] { attack, jump, idle });
list.AddRange(otherList);

// クリア
list.Clear();

// 件数
int count = list.Count;
```

### 優先度の上書き

リスト追加時に優先度を上書きできる。元のジャッジメントは変更されない。

```csharp
// 追加時に上書き
list.Add(attack, Highest);  // 一時的に最優先化

// 後から変更
list.SetPriority(attack, Highest);

// 元に戻す
list.ClearPriority(attack);
```

**活用例:**

```csharp
// スタン状態：攻撃を無効化
if (character.IsStunned)
{
    list.SetPriority(attack, Disabled);
}

// バフ状態：特定アクションを最優先化
if (character.HasBuff("PowerUp"))
{
    list.SetPriority(specialAttack, Highest);
}
```

### 入力リセット

```csharp
// 特定のジャッジメントの入力をリセット
list.ResetInput(attack);

// タグで一括リセット
list.ResetInputsByTag("Combo");

// 全リセット
list.ResetAllInputs();

// 特定のジャッジメント以外をリセット
list.ResetInputsExcept(selectedJudgment);
```

---

## AI制御

### ForceInput

AIがボタン入力なしでアクションを発動させる。

```csharp
// AI決定ロジック
if (ShouldAttack())
{
    attack.ForceInput();
}

// 選択実行
var result = selector.ProcessFrame(list, state);

// ForceInputをクリア
attack.ClearForceInput();
```

### ForcedInputOnlyモード

AI専用の最適化モード。ForceInputされたジャッジメントのみを評価する。

```csharp
// 通常モード（すべての入力を評価）
var result = selector.ProcessFrame(list, state);

// AI専用モード（ForceInputのみ評価、ライフサイクル管理もスキップ）
var result = selector.ProcessFrame(list, state, ProcessFrameOptions.ForcedInputOnly);
```

### AI専用ジャッジメント

Inputを省略し、Conditionのみで定義する。ForceInputで発動。

```csharp
FighterActionSelector.Judgment retreat, attack, approach, idle;

FighterActionSelector.Builder.Begin()
    // HP20%以下で撤退
    .AddJudgment(ActionCategory.FullBody, Highest)
        .Condition(HealthBelow(0.2f))
        .Label("Retreat")
        .Out(out retreat)

    // ターゲットが近ければ攻撃
    .AddJudgment(ActionCategory.FullBody, High)
        .Condition(TargetInRange(3.0f))
        .Label("Attack")
        .Out(out attack)

    // ターゲットが遠ければ接近
    .AddJudgment(ActionCategory.FullBody, Normal)
        .Condition(HasTarget.And(TargetOutOfRange(3.0f)))
        .Label("Approach")
        .Out(out approach)

    // デフォルト
    .AddJudgment(ActionCategory.FullBody, Lowest)
        .Condition(Cond.Always)
        .Label("Idle")
        .Out(out idle)

    .Done();

// AI制御
void AIUpdate()
{
    // 条件を満たすジャッジメントを探してForceInput
    if (retreat.Condition.Evaluate(state.Context))
        retreat.ForceInput();
    else if (attack.Condition.Evaluate(state.Context))
        attack.ForceInput();
    else if (approach.Condition.Evaluate(state.Context))
        approach.ForceInput();
    else
        idle.ForceInput();

    // ForcedInputOnlyで高速評価
    var result = selector.ProcessFrame(list, state, ProcessFrameOptions.ForcedInputOnly);

    // クリア
    retreat.ClearForceInput();
    attack.ClearForceInput();
    approach.ClearForceInput();
    idle.ClearForceInput();
}
```

---

## デバッグ

### 評価履歴

```csharp
selector.RecordEvaluations = true;  // デフォルトtrue
var result = selector.ProcessFrame(list, in state);

foreach (var eval in result.Evaluations)
{
    Console.WriteLine($"{eval.Label}: {eval.Outcome} (Priority: {eval.Priority})");
}

// 出力例:
// Hadouken: InputNotFired (Priority: (0,0,0))
// Attack: Selected (Priority: (1,0,0))
// Jump: ConditionFailed (Priority: (0,5,0))
// Idle: CategoryOccupied (Priority: (3,0,0))
```

### EvaluationOutcome一覧

| 値 | 意味 | 原因 |
|----|------|------|
| `Selected` | 選択された | すべての条件を通過 |
| `Disabled` | 無効化 | GetPriorityがDisabledを返した |
| `InputNotFired` | 入力不成立 | Input==null または IsTriggered==false |
| `ConditionFailed` | 条件不成立 | Condition.Evaluate==false |
| `CategoryOccupied` | カテゴリ占有 | 同一カテゴリで先に選択あり |
| `ExclusivityConflict` | 排他競合 | 排他カテゴリで先に選択あり |
| `ResolverRejected` | リゾルバ拒否 | Resolver.Resolve().IsNone |

### SelectionDebugger

```csharp
var debugger = new SelectionDebugger<ActionCategory>();

// 優先度テーブル表示
Console.WriteLine(debugger.FormatPriorityTable(result));
// 出力:
// Priority   | Category   | Label               | Outcome
// -----------|------------|---------------------|----------
// (0,0,0)    | FullBody   | Hadouken            | NO INPUT
// (0,5,0)    | FullBody   | Jump                | COND FAIL
// (1,0,0)    | FullBody   | Attack              | * SELECTED
// (3,0,0)    | FullBody   | Idle                | CAT FULL

// 特定アクションの拒否理由
Console.WriteLine(debugger.ExplainRejection(result, "Jump"));
// 出力: 'Jump' は Condition.Evaluate が false でした。

// 全拒否理由
Console.WriteLine(debugger.ExplainAllRejections(result));

// 統計
var stats = debugger.GetStats(result);
Console.WriteLine(stats);
// 出力: Total:4 Selected:1 Disabled:0 InputFail:1 CondFail:1 CatConflict:1
```

### 本番ビルドでの無効化

```csharp
#if !DEBUG
selector.RecordEvaluations = false;  // パフォーマンス向上
#endif
```

---

## パフォーマンス

### 目標値

| 指標 | 目標値 |
|-----|-------|
| 1フレームあたりのアロケーション | 0 |
| 100ジャッジメント評価時間 | < 0.1ms |
| GC Gen0 発生 | フレーム外のみ |

### 設計

- **内部バッファの再利用**: 候補リスト、結果配列は事前確保
- **struct + in渡し**: FrameState, ActionPriority はstruct
- **Span対応**: JudgmentListはSpanでアクセス可能
- **条件の共有**: IConditionはシングルトン化可能

### 最適化設定

```csharp
// 本番モード
selector.RecordEvaluations = false;  // 評価履歴を記録しない
selector.ValidateInput = false;       // 入力検証をスキップ

// AI専用モード
var result = selector.ProcessFrame(list, state, ProcessFrameOptions.ForcedInputOnly);
```

---

## 実践パターン集

### 格闘ゲームキャラクター

```csharp
using static Tomato.ActionSelector.Trig;
using static Tomato.ActionSelector.Cond;
using static Tomato.ActionSelector.NumPad;
using static Tomato.ActionSelector.B;
using static Tomato.ActionSelector.Priorities;

public static JudgmentList<FighterCategory> CreateRyuJudgments()
{
    FighterActionSelector.Judgment
        shoryuken, hadouken, hurricane,
        standLP, standHP, crouchLK, jumpHP,
        jump, walkF, walkB, crouch, idle;

    FighterActionSelector.Builder.Begin()
        // === 必殺技（最優先） ===
        .AddJudgment(FighterCategory.FullBody, Highest)
            .Input(Cmd(_6, _2, _3.Plus(P)))
            .Condition(Grounded)
            .Label("Shoryuken")
            .Tags("Special", "Invincible")
            .Out(out shoryuken)

        .AddJudgment(FighterCategory.FullBody, Highest)
            .Input(Cmd(_2, _3, _6.Plus(P)))
            .Condition(Grounded)
            .Label("Hadouken")
            .Tags("Special", "Projectile")
            .Out(out hadouken)

        .AddJudgment(FighterCategory.FullBody, Highest)
            .Input(Cmd(_2, _1, _4.Plus(K)))
            .Condition(Grounded)
            .Label("TatsumakiSenpukyaku")
            .Tags("Special")
            .Out(out hurricane)

        // === 通常技（高優先） ===
        .AddJudgment(FighterCategory.FullBody, High)
            .Input(Press(LP))
            .Condition(Grounded)
            .Label("StandLP")
            .Out(out standLP)

        .AddJudgment(FighterCategory.FullBody, High)
            .Input(Press(HP))
            .Condition(Grounded)
            .Label("StandHP")
            .Out(out standHP)

        .AddJudgment(FighterCategory.FullBody, High)
            .Input(Press(LK))
            .Condition(Grounded.And(From(s => s.Character.IsCrouching)))
            .Label("CrouchLK")
            .Out(out crouchLK)

        .AddJudgment(FighterCategory.FullBody, High)
            .Input(Press(HP))
            .Condition(Airborne)
            .Label("JumpHP")
            .Out(out jumpHP)

        // === 移動系（通常優先） ===
        .AddJudgment(FighterCategory.FullBody, Normal)
            .Input(Press(_8))
            .Condition(Grounded)
            .Label("Jump")
            .Out(out jump)

        .AddJudgment(FighterCategory.FullBody, Low)
            .Input(Hold(_6))
            .Condition(Grounded)
            .Label("WalkForward")
            .Out(out walkF)

        .AddJudgment(FighterCategory.FullBody, Low)
            .Input(Hold(_4))
            .Condition(Grounded)
            .Label("WalkBackward")
            .Out(out walkB)

        .AddJudgment(FighterCategory.FullBody, Low)
            .Input(Hold(_2))
            .Condition(Grounded)
            .Label("Crouch")
            .Out(out crouch)

        // === デフォルト ===
        .AddJudgment(FighterCategory.FullBody, Lowest)
            .Input(Always)
            .Label("Idle")
            .Out(out idle)

        .Done();

    return new FighterActionSelector.List()
        .Add(shoryuken).Add(hadouken).Add(hurricane)
        .Add(standLP).Add(standHP).Add(crouchLK).Add(jumpHP)
        .Add(jump).Add(walkF).Add(walkB).Add(crouch)
        .Add(idle);
}
```

### 3Dアクションゲーム（上半身/下半身分離）

```csharp
public static JudgmentList<ActionCategory> CreateActionHeroJudgments()
{
    ActionActionSelector.Judgment
        dodge, heavyAttack, lightAttack, aim, shoot,
        sprint, run, walk, jump, idle;

    FighterActionSelector.Builder.Begin()
        // === 全身アクション ===
        .AddJudgment(ActionCategory.FullBody, Highest)
            .Input(Press(Dash))
            .Label("Dodge")
            .Out(out dodge)

        .AddJudgment(ActionCategory.FullBody, High)
            .Input(Charge(Attack, 1.0f))
            .Label("HeavyAttack")
            .Out(out heavyAttack)

        .AddJudgment(ActionCategory.FullBody, Normal)
            .Input(Press(Attack))
            .Label("LightAttack")
            .Out(out lightAttack)

        // === 上半身アクション ===
        .AddJudgment(ActionCategory.UpperBody, High)
            .Input(Hold(Aim))
            .Label("Aim")
            .Out(out aim)

        .AddJudgment(ActionCategory.UpperBody, Normal)
            .Input(Press(Shoot))
            .Condition(From(s => s.Character.IsAiming))
            .Label("Shoot")
            .Out(out shoot)

        // === 下半身アクション ===
        .AddJudgment(ActionCategory.LowerBody, High)
            .Input(Hold(Sprint))
            .Condition(Grounded)
            .Label("Sprint")
            .Out(out sprint)

        .AddJudgment(ActionCategory.LowerBody, Normal)
            .Input(From(s => s.Input.MoveVector.magnitude > 0.5f))
            .Condition(Grounded)
            .Label("Run")
            .Out(out run)

        .AddJudgment(ActionCategory.LowerBody, Low)
            .Input(From(s => s.Input.MoveVector.magnitude > 0.1f))
            .Condition(Grounded)
            .Label("Walk")
            .Out(out walk)

        .AddJudgment(ActionCategory.LowerBody, Normal)
            .Input(Press(Jump))
            .Condition(Grounded)
            .Label("Jump")
            .Out(out jump)

        .AddJudgment(ActionCategory.LowerBody, Lowest)
            .Input(Always)
            .Condition(Grounded)
            .Label("Idle")
            .Out(out idle)

        .Done();

    return new ActionActionSelector.List()
        .Add(dodge).Add(heavyAttack).Add(lightAttack)
        .Add(aim).Add(shoot)
        .Add(sprint).Add(run).Add(walk).Add(jump).Add(idle);
}
```

---

## トラブルシューティング

### アクションが選択されない

**1. 評価履歴を確認**
```csharp
selector.RecordEvaluations = true;
var result = selector.ProcessFrame(list, state);

var eval = result.Evaluations.FirstOrDefault(e => e.Label == "MyAction");
Console.WriteLine($"Outcome: {eval.Outcome}");
```

**2. Outcomeごとの対処**

| Outcome | 対処 |
|---------|------|
| `Disabled` | GetPriorityでDisabledを返していないか確認 |
| `InputNotFired` | Inputを設定しているか、IsTriggeredがtrueになるか確認 |
| `ConditionFailed` | Conditionの評価ロジックを確認 |
| `CategoryOccupied` | 優先度を上げるか、カテゴリを変更 |
| `ExclusivityConflict` | カテゴリルールを確認 |
| `ResolverRejected` | ResolverがNone以外を返すか確認 |

### 意図しないアクションが選択される

**1. 優先度テーブルを確認**
```csharp
var debugger = new SelectionDebugger<ActionCategory>();
Console.WriteLine(debugger.FormatPriorityTable(result));
```

**2. 優先度を調整**
- 意図したアクションの優先度を上げる
- 意図しないアクションの優先度を下げる

### 入力が蓄積されない

**1. ライフサイクルが呼ばれているか確認**
- ジャッジメントがリストに含まれているか
- ForcedInputOnlyモードではライフサイクルがスキップされる

**2. OnJudgmentUpdateで蓄積しているか確認**
- deltaTicksが正しく渡されているか
- 蓄積ロジックにバグがないか

### パフォーマンスが悪い

**1. 評価履歴を無効化**
```csharp
selector.RecordEvaluations = false;
```

**2. 入力検証を無効化**
```csharp
selector.ValidateInput = false;
```

**3. AI用には ForcedInputOnly を使用**
```csharp
selector.ProcessFrame(list, state, ProcessFrameOptions.ForcedInputOnly);
```

---

## ディレクトリ構造

```
ActionSelector/
├── DESIGN.md                       # 本ドキュメント
├── README.md                       # クイックスタート
├── ActionSelector.csproj
│
├── Core/
│   ├── ActionSelector.cs           # メインエンジン
│   ├── ActionSelector.Types.cs     # 内部型定義（継承用）
│   ├── IActionJudgment.cs          # ジャッジメントインターフェース
│   ├── SimpleJudgment.cs           # 標準実装
│   ├── ActionPriority.cs           # 優先度
│   ├── SelectionResult.cs          # 選択結果
│   ├── JudgmentList.cs             # ジャッジメントリスト
│   └── FrameState.cs               # フレーム状態
│
├── Category/
│   └── CategoryRules.cs            # カテゴリルール
│
├── Trigger/
│   ├── IInputTrigger.cs            # トリガーインターフェース
│   ├── ButtonTriggers.cs           # Press/Release/Hold
│   ├── ChargeTrigger.cs            # チャージ
│   ├── CommandTrigger.cs           # コマンド入力
│   └── SpecialTriggers.cs          # Always/Never
│
├── Condition/
│   ├── ICondition.cs               # 条件インターフェース
│   ├── BasicConditions.cs          # Grounded/Airborne等
│   ├── ResourceConditions.cs       # HP/MP/クールダウン
│   └── CombatConditions.cs         # ターゲット/コンボ
│
├── Resolver/
│   ├── IActionResolver.cs          # リゾルバインターフェース
│   ├── ResolvedAction.cs           # 解決結果
│   └── DelegateResolver.cs         # ラムダ用
│
├── State/
│   ├── GameState.cs                # ゲーム状態
│   └── InputState.cs               # 入力状態
│
├── Dsl/
│   ├── ActionBuilder.cs            # Fluent API
│   ├── Buttons.cs                  # ボタン短縮名
│   ├── Dirs.cs                     # 方向短縮名
│   ├── NumPad.cs                   # テンキー表記
│   ├── Priorities.cs               # 優先度短縮名
│   ├── Trig.cs                     # トリガーファクトリ
│   └── Cond.cs                     # 条件ファクトリ
│
└── Debug/
    └── SelectionDebugger.cs        # デバッグ支援
```
