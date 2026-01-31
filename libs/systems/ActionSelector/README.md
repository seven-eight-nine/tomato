# ActionSelector

ゲーム向けアクション選択ライブラリ。

## これは何？

キャラクターが「どのアクションを実行するか」を毎フレーム決定するシステム。
複数の入力条件・状態条件から、優先度に基づいて最適なアクションを選ぶ。

```
攻撃ボタン押下 + 接地中 → 攻撃（優先度: 高）
ジャンプボタン押下 + 接地中 → ジャンプ（優先度: 中）
何も押してない → 待機（優先度: 最低）
```

## なぜ使うのか

- **疎結合**: アクション同士が依存しない。追加・削除が他に影響しない
- **優先度ベース**: 競合を明確なルールで解決。デバッグしやすい
- **ゼロアロケーション**: 毎フレームのGC負荷なし

---

## クイックスタート

### 1. カテゴリを定義

```csharp
public enum ActionCategory { FullBody, Upper, Lower }
```

### 2. ゲーム固有のセレクタを定義

```csharp
using Tomato.ActionSelector;

// 継承するだけ。ボディは空でOK
public class FighterActionSelector
    : ActionSelector<ActionCategory, InputState, GameState> { }
```

これで `FighterActionSelector.Judgment`, `FighterActionSelector.Builder`, `FighterActionSelector.List` 等の短い型名が使えるようになる。

### 3. ジャッジメントを定義

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

### 4. リストに登録してフレーム処理

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

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** に以下が記載されている：

- 用語定義
- 設計哲学
- セットアップ手順
- プライオリティの仕組み
- 入力システム（コマンド入力、チャージ等）
- カテゴリと排他ルール
- デバッグ方法
- パフォーマンス設計

---

## 主要な概念

**ジャッジメント** = アクションの成立条件を宣言したもの

各ジャッジメントは以下の順序で評価される：

| 順序 | 概念 | 説明 |
|:----:|------|------|
| 1 | **プライオリティ** | 優先順位。高い方から評価。Disabledなら除外 |
| 2 | **入力(Input)** | ボタン押下、コマンド入力など。不成立→スキップ |
| 3 | **条件(Condition)** | 接地中、HP残量など。不成立→スキップ |
| 4 | **カテゴリ** | 同時実行グループ。既に埋まっている→スキップ |
| 5 | **リゾルバ** | 実行アクションを決定（オプション）。None→スキップ |
| → | **リクエスト** | すべて通過したジャッジメントが選択される |

---

## よく使うパターン

### 入力トリガー

```csharp
using static Tomato.ActionSelector.Trig;

Press(Attack)              // ボタン押下
Hold(Guard, 30)            // 30 tick以上ホールド
Charge(Attack, 30, 60)     // 段階チャージ（30tickでLv1、60tickでLv2）
Simultaneous(L1, R1)       // 同時押し
Always                     // 常に成立（デフォルトアクション用）
```

### コマンド入力

```csharp
// 波動拳: ↓↘→+P
Cmd(Down, DownRight, Right.Plus(Punch))

// テンキー表記
using static Tomato.ActionSelector.NumPad;
Cmd(_2, _3, _6.Plus(P))    // 236P
```

### 状態条件

```csharp
using static Tomato.ActionSelector.Cond;

Grounded                   // 接地中
Airborne                   // 空中
HealthAbove(0.5f)          // HP50%以上
HasTarget                  // ターゲットあり
```

### 複数条件（AND結合）

`.Condition()` を複数回呼ぶと、すべてがANDで結合される：

```csharp
.AddJudgment(ActionCategory.FullBody, Normal)
    .Input(Press(Attack))
    .Condition(Grounded)           // 接地中
    .Condition(HealthAbove(0.5f))  // かつ HP50%以上
    .Label("GroundAttack")
    .Out(out groundAttack)
```

> **Note:** `.Input()` を複数回呼んだ場合は最後の設定のみが有効になる。

### 動的優先度

```csharp
// HP20%未満なら最優先、それ以外は通常
.AddJudgment(ActionCategory.FullBody,
    state => state.Context.HealthRatio < 0.2f ? Highest : Normal)
    .Input(Press(Dash))
    .Label("EmergencyDodge")
```

---

## カテゴリ排他ルール

デフォルトでは同一カテゴリのみ排他。カスタムが必要な場合：

```csharp
public class FighterCategoryRules : CategoryRules<ActionCategory>
{
    public static readonly FighterCategoryRules Instance = new();

    public override bool AreExclusive(ActionCategory a, ActionCategory b)
    {
        // FullBodyは他のすべてと排他
        if (a == ActionCategory.FullBody || b == ActionCategory.FullBody)
            return true;
        return a == b;
    }
}

public class FighterActionSelector
    : ActionSelector<ActionCategory, InputState, GameState>
{
    public FighterActionSelector() : base(FighterCategoryRules.Instance) { }
}
```

---

## デバッグ

```csharp
// なぜこのアクションが選ばれた/選ばれなかったかを確認
foreach (var eval in result.Evaluations)
{
    Console.WriteLine($"{eval.Label}: {eval.Outcome}");
}
// 出力例:
// Attack: InputNotFired     ← 入力なし
// Jump: ConditionFailed     ← 条件不成立（空中だった）
// Idle: Selected            ← 選択された
```

---

## ライセンス

MIT License
