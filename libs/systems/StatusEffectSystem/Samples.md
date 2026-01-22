# 典型的な状態異常サンプル集

このドキュメントでは、StatusEffectSystemを使用して実装できる典型的な状態異常パターンを紹介します。

## 目次

1. [継続ダメージ系](#継続ダメージ系)
2. [行動阻害系](#行動阻害系)
3. [能力強化系](#能力強化系)
4. [能力弱体系](#能力弱体系)
5. [防御系](#防御系)
6. [回復系](#回復系)
7. [特殊効果系](#特殊効果系)
8. [複合効果系](#複合効果系)
9. [効果結果活用](#効果結果活用)
10. [効果の依存関係](#効果の依存関係)
11. [イベント活用パターン](#イベント活用パターン)

---

## セットアップ

以下のサンプルで使用する共通のセットアップコードです。

```csharp
var tagRegistry = new TagRegistry();
var effectRegistry = new EffectRegistry();
var manager = new EffectManager(effectRegistry, tagRegistry);

// 基本タグ
var buffTag = tagRegistry.Register("buff");
var debuffTag = tagRegistry.Register("debuff");
var dotTag = tagRegistry.Register("dot");           // Damage over Time
var hotTag = tagRegistry.Register("hot");           // Heal over Time
var ccTag = tagRegistry.Register("cc");             // Crowd Control
var defensiveTag = tagRegistry.Register("defensive");
var offensiveTag = tagRegistry.Register("offensive");

// 属性タグ
var fireTag = tagRegistry.Register("fire");
var iceTag = tagRegistry.Register("ice");
var lightningTag = tagRegistry.Register("lightning");
var poisonTag = tagRegistry.Register("poison");
var physicalTag = tagRegistry.Register("physical");
```

---

## 継続ダメージ系

### 毒

スタック可能な継続ダメージ。スタックごとにダメージが増加。

```csharp
var poison = effectRegistry.Register("Poison", b => b
    .WithDuration(new TickDuration(300))      // 5秒（60fps想定）
    .WithStackConfig(StackConfig.Additive(5)) // 最大5スタック
    .WithDurationBehavior(DurationBehavior.Refresh) // 再適用で期間リセット
    .WithTags(tagRegistry.CreateSet(debuffTag, dotTag, poisonTag)));
```

**活用例**: 毎tick、`CurrentStacks * 基礎ダメージ`のダメージを与える

### 出血

物理的な継続ダメージ。スタックごとに独立した期間を持つ。

```csharp
var bleed = effectRegistry.Register("Bleed", b => b
    .WithDuration(new TickDuration(180))      // 3秒
    .WithStackConfig(StackConfig.Additive(10)) // 最大10スタック
    .WithDurationBehavior(DurationBehavior.KeepExisting) // 期間は延長しない
    .WithTags(tagRegistry.CreateSet(debuffTag, dotTag, physicalTag)));
```

**活用例**: 高スタックでの爆発的なダメージ、移動で悪化など

### 炎上

火属性の継続ダメージ。スタックせず、再適用で期間がリセット。

```csharp
var burning = effectRegistry.Register("Burning", b => b
    .WithDuration(new TickDuration(240))      // 4秒
    .WithStackConfig(StackConfig.None)        // スタックしない
    .WithDurationBehavior(DurationBehavior.Refresh) // 再適用で期間リセット
    .WithTags(tagRegistry.CreateSet(debuffTag, dotTag, fireTag)));
```

**活用例**: 火属性攻撃の追加効果、水で消火可能

### 感電

雷属性の短期間高ダメージ。

```csharp
var electrified = effectRegistry.Register("Electrified", b => b
    .WithDuration(new TickDuration(60))       // 1秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Replace) // 毎回上書き
    .WithTags(tagRegistry.CreateSet(debuffTag, dotTag, lightningTag)));
```

**活用例**: 短い間隔で高ダメージ、水場での範囲拡大

---

## 行動阻害系

### 気絶

完全な行動不能。延長不可、スタック不可。

```csharp
var stun = effectRegistry.Register("Stun", b => b
    .WithDuration(new TickDuration(90))       // 1.5秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting) // 連続適用で延長しない
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag)));
```

**活用例**: 強力だが短時間の行動阻害

### 凍結

氷属性の行動不能。解除時に追加効果がある場合も。

```csharp
var freeze = effectRegistry.Register("Freeze", b => b
    .WithDuration(new TickDuration(120))      // 2秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag, iceTag)));
```

**活用例**: 凍結中は被ダメージ増加、火属性で即解除

### 減速

移動速度低下。スタック可能で効果が累積。

```csharp
var slow = effectRegistry.Register("Slow", b => b
    .WithDuration(new TickDuration(180))      // 3秒
    .WithStackConfig(StackConfig.Additive(3)) // 最大3スタック
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag)));
```

**活用例**: 1スタック=20%減速、3スタックで60%減速

### 拘束

移動不可だが他の行動は可能。

```csharp
var root = effectRegistry.Register("Root", b => b
    .WithDuration(new TickDuration(150))      // 2.5秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag)));
```

**活用例**: 遠距離キャラには効果薄、近接には致命的

### 沈黙

スキル使用不可。

```csharp
var silence = effectRegistry.Register("Silence", b => b
    .WithDuration(new TickDuration(180))      // 3秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag)));
```

**活用例**: 通常攻撃は可能、魔法使い系への対策

### 混乱

操作が逆転または不規則になる。

```csharp
var confusion = effectRegistry.Register("Confusion", b => b
    .WithDuration(new TickDuration(240))      // 4秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag)));
```

**活用例**: 左右反転、ランダムな方向への移動

### 睡眠

行動不能だが、ダメージで解除。

```csharp
var sleep = effectRegistry.Register("Sleep", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag)));
```

**活用例**: 長時間だが被弾で即解除、継続ダメージとの相性が悪い

### 恐怖

強制的に逃走させる。

```csharp
var fear = effectRegistry.Register("Fear", b => b
    .WithDuration(new TickDuration(120))      // 2秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag)));
```

**活用例**: 発生源から離れる方向に強制移動

---

## 能力強化系

### 攻撃力上昇

一時的な攻撃力増加。

```csharp
var attackUp = effectRegistry.Register("AttackUp", b => b
    .WithDuration(new TickDuration(600))      // 10秒
    .WithStackConfig(StackConfig.Additive(3)) // 最大3スタック
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, offensiveTag)));
```

**活用例**: 1スタック=10%増加、複数のバフ源から重ねがけ

### 防御力上昇

一時的な防御力増加。

```csharp
var defenseUp = effectRegistry.Register("DefenseUp", b => b
    .WithDuration(new TickDuration(600))      // 10秒
    .WithStackConfig(StackConfig.Additive(3))
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, defensiveTag)));
```

### 速度上昇

移動・攻撃速度の増加。

```csharp
var haste = effectRegistry.Register("Haste", b => b
    .WithDuration(new TickDuration(480))      // 8秒
    .WithStackConfig(StackConfig.None)        // スタックしない
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag)));
```

### クリティカル率上昇

会心率の一時的増加。

```csharp
var critUp = effectRegistry.Register("CriticalUp", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.Additive(5)) // 最大5スタック
    .WithDurationBehavior(DurationBehavior.Extend) // 再適用で延長
    .WithTags(tagRegistry.CreateSet(buffTag, offensiveTag)));
```

**活用例**: コンボで積み上げていく

### 再生

毎tick微量回復。

```csharp
var regeneration = effectRegistry.Register("Regeneration", b => b
    .WithDuration(new TickDuration(600))      // 10秒
    .WithStackConfig(StackConfig.Additive(3))
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, hotTag)));
```

### 無敵

全てのダメージを無効化。

```csharp
var invincible = effectRegistry.Register("Invincible", b => b
    .WithDuration(new TickDuration(60))       // 1秒（短い）
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting) // 延長不可
    .WithTags(tagRegistry.CreateSet(buffTag, defensiveTag)));
```

**活用例**: 回避スキル、復活直後の保護

### 透明化

敵から視認されなくなる。

```csharp
var invisible = effectRegistry.Register("Invisible", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag)));
```

**活用例**: 攻撃で解除、背後からの攻撃でボーナス

---

## 能力弱体系

### 攻撃力低下

一時的な攻撃力減少。

```csharp
var attackDown = effectRegistry.Register("AttackDown", b => b
    .WithDuration(new TickDuration(480))      // 8秒
    .WithStackConfig(StackConfig.Additive(3))
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag)));
```

### 防御力低下

一時的な防御力減少。

```csharp
var defenseDown = effectRegistry.Register("DefenseDown", b => b
    .WithDuration(new TickDuration(480))      // 8秒
    .WithStackConfig(StackConfig.Additive(3))
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag)));
```

### 脆弱

受けるダメージが増加。

```csharp
var vulnerable = effectRegistry.Register("Vulnerable", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.Additive(5)) // 最大5スタック
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag)));
```

**活用例**: 1スタック=10%被ダメージ増加

### 弱体化

与えるダメージが減少。

```csharp
var weakness = effectRegistry.Register("Weakness", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.Additive(3))
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag)));
```

### 盲目

命中率低下または視界制限。

```csharp
var blind = effectRegistry.Register("Blind", b => b
    .WithDuration(new TickDuration(180))      // 3秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag)));
```

---

## 防御系

### シールド

追加の耐久力。ダメージを代わりに吸収。

```csharp
var shield = effectRegistry.Register("Shield", b => b
    .WithDuration(new TickDuration(600))      // 10秒
    .WithStackConfig(StackConfig.Additive(1)) // スタック=シールド量として使用
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, defensiveTag)));

// シールド量の管理例
// manager.AddStacks(instanceId, shieldAmount);  // シールド付与
// manager.AddStacks(instanceId, -damageAmount); // ダメージ吸収
// → スタックが0になると自動削除
```

### ダメージ軽減

一定割合のダメージをカット。

```csharp
var damageReduction = effectRegistry.Register("DamageReduction", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.Additive(5)) // 最大5スタック（50%軽減）
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, defensiveTag)));
```

### 反射

受けたダメージの一部を攻撃者に返す。

```csharp
var reflect = effectRegistry.Register("Reflect", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, defensiveTag)));
```

### 吸収

特定属性のダメージを回復に変換。

```csharp
var fireAbsorb = effectRegistry.Register("FireAbsorb", b => b
    .WithDuration(new TickDuration(480))      // 8秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, defensiveTag, fireTag)));
```

**活用例**: 火属性攻撃を受けると回復

---

## 回復系

### 継続回復

毎tick一定量回復。

```csharp
var healOverTime = effectRegistry.Register("HealOverTime", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.Additive(3)) // 最大3スタック
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag, hotTag)));
```

### 回復強化

受ける回復効果を増加。

```csharp
var healingBoost = effectRegistry.Register("HealingBoost", b => b
    .WithDuration(new TickDuration(600))      // 10秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(buffTag)));
```

### 回復阻害

受ける回復効果を減少または無効化。

```csharp
var healingReduction = effectRegistry.Register("HealingReduction", b => b
    .WithDuration(new TickDuration(480))      // 8秒
    .WithStackConfig(StackConfig.Additive(5)) // 最大5スタック（100%=回復無効）
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(debuffTag)));
```

---

## 特殊効果系

### マーク

特定の対象を追跡可能にする。永続。

```csharp
var marked = effectRegistry.Register("Marked", b => b
    .AsPermanent()  // 手動解除のみ
    .WithStackConfig(StackConfig.None)
    .WithTags(tagRegistry.CreateSet(debuffTag)));
```

**活用例**: ミニマップに表示、自動追尾攻撃の対象

### 挑発

強制的に攻撃対象にさせる。

```csharp
var taunt = effectRegistry.Register("Taunt", b => b
    .WithDuration(new TickDuration(180))      // 3秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.Refresh)
    .WithTags(tagRegistry.CreateSet(ccTag)));
```

### 連鎖

近くの敵に効果が伝播。

```csharp
var chain = effectRegistry.Register("Chain", b => b
    .WithDuration(new TickDuration(60))       // 1秒
    .WithStackConfig(StackConfig.Additive(3)) // 伝播回数をスタックで管理
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(lightningTag)));
```

**活用例**: 期限切れ時に近くの敵に1スタック減らして伝播

### 時限爆発

一定時間後に爆発ダメージ。

```csharp
var timeBomb = effectRegistry.Register("TimeBomb", b => b
    .WithDuration(new TickDuration(180))      // 3秒後に爆発
    .WithStackConfig(StackConfig.Additive(5)) // スタック=威力
    .WithDurationBehavior(DurationBehavior.KeepExisting) // 期間は延長しない
    .WithTags(tagRegistry.CreateSet(debuffTag)));

// OnEffectRemoved で reason == Expired なら爆発処理を実行
manager.OnEffectRemoved += e =>
{
    if (e.DefinitionId == timeBomb && e.Reason.Value == RemovalReasonId.Expired)
    {
        // 爆発ダメージ = e.FinalStacks * 基礎ダメージ
    }
};
```

### 変身

外見や能力が一時的に変化。

```csharp
var transform = effectRegistry.Register("Transform", b => b
    .WithDuration(new TickDuration(600))      // 10秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(buffTag)));
```

---

## 複合効果系

### 状態異常耐性

特定タグの効果を受け付けなくする。

```csharp
var ccImmunity = effectRegistry.Register("CCImmunity", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(buffTag)));

// 適用時に免疫チェック
// if (manager.HasEffect(targetId, ccImmunity))
// {
//     // CC効果は適用しない
// }
```

### 状態異常反転

デバフをバフに、バフをデバフに変換。

```csharp
var inversion = effectRegistry.Register("Inversion", b => b
    .WithDuration(new TickDuration(300))      // 5秒
    .WithStackConfig(StackConfig.None)
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(buffTag)));
```

**活用例**: 毒が回復に、攻撃力上昇が攻撃力低下になる

### 効果延長

全ての有効なバフの期間を延長。

```csharp
// 特定の効果ではなく、マネージャー操作として実装
public void ExtendAllBuffs(ulong targetId, TickDuration extension)
{
    foreach (var effect in manager.GetEffects(targetId))
    {
        var def = effectRegistry.GetDefinition(effect.DefinitionId);
        if (def?.Tags.Contains(buffTag) == true)
        {
            manager.ExtendDuration(effect.Id, extension);
        }
    }
}
```

### 効果消去

全てのデバフを解除。

```csharp
// ディスペル効果として実装
public void DispelDebuffs(ulong targetId)
{
    manager.RemoveByTag(targetId, debuffTag, new RemovalReasonId(10));
}
```

### 効果窃盗

対象のバフを奪い取る。

```csharp
// 特殊処理として実装
public void StealBuffs(ulong targetId, ulong thiefId)
{
    foreach (var effect in manager.GetEffects(targetId).ToList())
    {
        var def = effectRegistry.GetDefinition(effect.DefinitionId);
        if (def?.Tags.Contains(buffTag) == true)
        {
            // 対象から削除
            manager.Remove(effect.Id, new RemovalReasonId(11));
            // 窃盗者に適用
            manager.TryApply(thiefId, effect.DefinitionId, thiefId);
        }
    }
}
```

---

## 効果結果活用

### 基本的な効果結果定義

```csharp
// ゲーム側で効果結果構造体を定義
public struct GameResult
{
    // 攻撃系
    public int AttackFlatBonus;
    public int AttackPercentAdd;    // 加算%（+20%, +30% → 合計50%）
    public int AttackPercentMult;   // 乗算%（×150% × 120% → 積）

    // 防御系
    public int DefenseFlatBonus;
    public int DefensePercentAdd;

    // 速度系
    public int MoveSpeedPercent;
    public int AttackSpeedPercent;

    // 状態フラグ
    public bool IsStunned;
    public bool IsSilenced;
    public bool IsRooted;
    public bool IsInvincible;

    // 継続効果
    public int DotDamagePerTick;
    public int HotHealPerTick;
}
```

### コントリビュータ付き効果定義（Priorityによる適用順制御）

コントリビュータはPriority昇順で適用されます。同じPriorityならEffectId（定義順）で決まります。

```csharp
// Priority 0: フラット加算
var attackFlatUp = effectRegistry.Register("AttackFlatUp", b => b
    .WithDuration(new TickDuration(600))
    .WithPriority(0)
    .WithTags(tagRegistry.CreateSet(buffTag))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.AttackFlatBonus += 10 * stacks;
    }));

// Priority 100: 加算%
var attackPercentUp = effectRegistry.Register("AttackPercentUp", b => b
    .WithDuration(new TickDuration(600))
    .WithPriority(100)
    .WithStackConfig(StackConfig.Additive(3))
    .WithTags(tagRegistry.CreateSet(buffTag))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.AttackPercentAdd += 10 * stacks;  // 1スタックあたり+10%
    }));

// Priority 200: 乗算%
var attackMultUp = effectRegistry.Register("AttackMultUp", b => b
    .WithDuration(new TickDuration(600))
    .WithPriority(200)
    .WithTags(tagRegistry.CreateSet(buffTag))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.AttackPercentMult += 50;  // ×1.5
    }));

// フラグ系（Priorityは結果に影響しないがデフォルト0で問題なし）
var stun = effectRegistry.Register("Stun", b => b
    .WithDuration(new TickDuration(90))
    .WithTags(tagRegistry.CreateSet(debuffTag, ccTag))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.IsStunned = true;
    }));

// 継続ダメージ
var poison = effectRegistry.Register("Poison", b => b
    .WithDuration(new TickDuration(300))
    .WithStackConfig(StackConfig.Additive(5))
    .WithTags(tagRegistry.CreateSet(debuffTag, dotTag))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.DotDamagePerTick += 5 * stacks;
    }));
```

**重要**: 適用順はPriorityとEffectIdで**決定論的**に決まります。効果がかかった順番には依存しません。

### フレーム処理での効果結果計算

```csharp
public class GameLoop
{
    private Dictionary<ulong, GameResult> _prevResults = new();
    private Dictionary<ulong, GameResult> _currResults = new();

    public void Update(GameTick tick)
    {
        // 1. 状態異常の時間経過
        manager.ProcessTick(tick);

        // 2. 効果結果の計算とトリガー検出
        foreach (var entity in entities)
        {
            // 前フレームの効果結果を保存
            _prevResults.TryGetValue(entity.Id, out var prev);

            // 今フレームの効果結果を計算
            var curr = manager.CalculateResult<GameResult>(entity.Id, default);
            _currResults[entity.Id] = curr;

            // トリガー検出
            DetectTriggers(entity, prev, curr);
        }

        // 3. 各システムは効果結果を参照
        battleSystem.Update(_currResults);
        movementSystem.Update(_currResults);

        // 4. 次フレームの準備
        (_prevResults, _currResults) = (_currResults, _prevResults);
        _currResults.Clear();
    }

    private void DetectTriggers(Entity entity, GameResult prev, GameResult curr)
    {
        // スタン開始
        if (!prev.IsStunned && curr.IsStunned)
            entity.OnStunStart();

        // スタン終了
        if (prev.IsStunned && !curr.IsStunned)
            entity.OnStunEnd();

        // 無敵開始/終了
        if (!prev.IsInvincible && curr.IsInvincible)
            entity.OnInvincibleStart();
        if (prev.IsInvincible && !curr.IsInvincible)
            entity.OnInvincibleEnd();
    }
}
```

### 最終ステータス計算

```csharp
public int CalculateFinalAttack(int baseAttack, GameResult result)
{
    // 基礎値 + フラット加算
    var value = baseAttack + result.AttackFlatBonus;

    // 加算%（合計して一度に適用）
    value = value * (100 + result.AttackPercentAdd) / 100;

    // 乗算%（個別に適用、Resultでは積を保持する設計も可）
    value = value * (100 + result.AttackPercentMult) / 100;

    return Math.Max(0, value);
}
```

---

## 効果の依存関係

スキルから派生する効果など、親子関係を持つ効果のパターン。

### パターン1: タグによる管理

```csharp
// スキル用のタグ
var auraSkillTag = tagRegistry.Register("skill:aura");
var auraDerivedTag = tagRegistry.Register("skill:aura:derived");

// 親効果（スキル本体）
var auraSkill = effectRegistry.Register("AuraSkill", b => b
    .AsPermanent()
    .WithTags(tagRegistry.CreateSet(buffTag, auraSkillTag)));

// 子効果（スキルから派生する効果）
var auraBuff = effectRegistry.Register("AuraBuff", b => b
    .WithDuration(new TickDuration(60))  // 1秒ごとに再適用される想定
    .WithTags(tagRegistry.CreateSet(buffTag, auraDerivedTag))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.AttackPercentAdd += 15;
    }));

// 親効果が消えたら子効果も削除
manager.OnEffectRemoved += e =>
{
    var def = effectRegistry.GetDefinition(e.DefinitionId);
    if (def?.Tags.Contains(auraSkillTag) == true)
    {
        // このスキルから派生した効果を全削除
        // 注: 実際のゲームでは、対象となる味方全員から削除する必要あり
        manager.RemoveByTag(e.TargetId, auraDerivedTag, e.Reason);
    }
};
```

### パターン2: SourceIdによる追跡

```csharp
// 親効果のInstanceIdをSourceIdとして使用
public void ApplyDerivedEffect(EffectInstanceId parentId, ulong targetId, EffectId derivedEffectId)
{
    var parentInstance = manager.GetInstance(parentId);
    if (parentInstance == null) return;

    // 親効果のInstanceId.Valueをsourceとして使用
    manager.TryApply(targetId, derivedEffectId, parentInstance.InstanceId.Value);
}

// 親効果が消えたら、同じSourceIdを持つ効果を削除
manager.OnEffectRemoved += e =>
{
    var parentIdValue = e.InstanceId.Value;

    // 全エンティティから、このSourceIdを持つ効果を探して削除
    foreach (var entity in GetAllEntities())
    {
        foreach (var effect in manager.GetEffects(entity.Id).ToList())
        {
            if (effect.SourceId == parentIdValue)
            {
                manager.Remove(effect.Id, e.Reason);
            }
        }
    }
};
```

### パターン3: 外部管理による依存関係

```csharp
// 効果の依存関係を外部で管理
public class EffectDependencyManager
{
    private readonly Dictionary<EffectInstanceId, List<EffectInstanceId>> _children = new();
    private readonly EffectManager _effectManager;

    public EffectDependencyManager(EffectManager effectManager)
    {
        _effectManager = effectManager;
        _effectManager.OnEffectRemoved += OnParentRemoved;
    }

    public void RegisterChild(EffectInstanceId parentId, EffectInstanceId childId)
    {
        if (!_children.TryGetValue(parentId, out var children))
        {
            children = new List<EffectInstanceId>();
            _children[parentId] = children;
        }
        children.Add(childId);
    }

    private void OnParentRemoved(EffectRemovedEvent e)
    {
        if (!_children.TryGetValue(e.InstanceId, out var children))
            return;

        // 子効果を全て削除
        foreach (var childId in children)
        {
            _effectManager.Remove(childId, e.Reason);
        }
        _children.Remove(e.InstanceId);
    }
}

// 使用例
var parentResult = manager.TryApply(targetId, skillEffectId, sourceId);
if (parentResult.Success)
{
    // 派生効果を適用
    var childResult = manager.TryApply(targetId, derivedEffectId, sourceId);
    if (childResult.Success)
    {
        dependencyManager.RegisterChild(parentResult.InstanceId, childResult.InstanceId);
    }
}
```

### 相互依存（条件付き効果）

```csharp
// 「両方の効果が揃っている時だけ有効」パターン
// Resultで実現

var effectA = effectRegistry.Register("EffectA", b => b
    .WithDuration(new TickDuration(600))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.HasEffectA = true;  // フラグをセット
    }));

var effectB = effectRegistry.Register("EffectB", b => b
    .WithDuration(new TickDuration(600))
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.HasEffectB = true;  // フラグをセット
    }));

// 実際の効果は、効果結果を使う側で判定
public void ApplyEffects(Entity entity, GameResult result)
{
    // AとBの両方がある時だけシナジー効果を発動
    if (result.HasEffectA && result.HasEffectB)
    {
        // シナジーボーナス
        entity.SynergyBonus = 50;
    }
}
```

---

## イベント活用パターン

### ダメージ処理との連携

```csharp
// 継続ダメージの処理
manager.OnEffectApplied += e =>
{
    var def = effectRegistry.GetDefinition(e.DefinitionId);
    if (def?.Tags.Contains(dotTag) == true)
    {
        // DoTティックを開始
        StartDotTick(e.InstanceId, e.TargetId);
    }
};

manager.OnEffectRemoved += e =>
{
    var def = effectRegistry.GetDefinition(e.DefinitionId);
    if (def?.Tags.Contains(dotTag) == true)
    {
        // DoTティックを停止
        StopDotTick(e.InstanceId);
    }
};

manager.OnStackChanged += e =>
{
    var def = effectRegistry.GetDefinition(e.DefinitionId);
    if (def?.Tags.Contains(dotTag) == true)
    {
        // DoTダメージ量を更新
        UpdateDotDamage(e.InstanceId, e.NewStacks);
    }
};
```

### UI連携

```csharp
manager.OnEffectApplied += e =>
{
    ShowEffectIcon(e.TargetId, e.DefinitionId);
};

manager.OnEffectRemoved += e =>
{
    HideEffectIcon(e.TargetId, e.DefinitionId);
};

manager.OnStackChanged += e =>
{
    UpdateStackCount(e.TargetId, e.DefinitionId, e.NewStacks);
};
```

---

## 注意事項

1. **パフォーマンス**: `GetEffects`は全インスタンスを列挙するため、毎フレーム呼び出す場合はキャッシュを検討
2. **イベント順序**: `OnEffectRemoved`は`Remove`呼び出し後に発火するため、インスタンスは既に無効
3. **スタックと削除**: `AddStacks`で0以下になった場合は自動削除される
4. **期間とPermanent**: `AsPermanent()`で作成した効果は`ProcessTick`で削除されない
