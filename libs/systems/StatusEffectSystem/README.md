# StatusEffectSystem

状態異常（バフ・デバフ）を管理するための汎用システム。

## これは何？

`StatusEffectSystem`は、ゲーム内のエンティティに対して状態異常効果を付与・管理・解除するためのライブラリです。毒、スタン、攻撃力上昇などの一時的な効果を統一的に扱えます。

## なぜ使うのか

状態異常システムを自前で実装すると、以下のような問題が発生しやすくなります：

- **スタック処理の複雑化**: 同じ効果を重ねがけしたときの挙動が一貫しない
- **期限管理の煩雑さ**: 効果の自動期限切れ、延長、リフレッシュを個別に実装する必要がある
- **条件判定の散在**: 「この効果はこのタグを持つ対象には適用しない」といった条件があちこちに散らばる
- **相互作用の管理困難**: 「Aの効果がかかっている間はBを無効化」のような関係性の管理が難しい

StatusEffectSystemはこれらを統一的に解決します。

## クイックスタート

### 1. レジストリの作成

```csharp
using Tomato.StatusEffectSystem;

// タグとエフェクトのレジストリを作成
var tagRegistry = new TagRegistry();
var effectRegistry = new EffectRegistry();

// マネージャーの作成
var manager = new EffectManager(effectRegistry, tagRegistry);
```

### 2. タグの定義

```csharp
// 基本タグ
var debuffTag = tagRegistry.Register("debuff");
var dotTag = tagRegistry.Register("dot");          // Damage over Time
var crowdControlTag = tagRegistry.Register("cc");  // 行動阻害

// 属性タグ
var fireTag = tagRegistry.Register("fire");
var iceTag = tagRegistry.Register("ice");
```

### 3. 効果の定義

```csharp
// 毒（継続ダメージ、スタック可能）
var poisonId = effectRegistry.Register("Poison", b => b
    .WithDuration(new TickDuration(300))
    .WithStackConfig(StackConfig.Additive(5))
    .WithTags(tagRegistry.CreateSet(debuffTag, dotTag)));

// スタン（行動不能、延長不可）
var stunId = effectRegistry.Register("Stun", b => b
    .WithDuration(new TickDuration(60))
    .WithDurationBehavior(DurationBehavior.KeepExisting)
    .WithTags(tagRegistry.CreateSet(debuffTag, crowdControlTag)));

// 攻撃力上昇（永続、手動解除のみ）
var powerUpId = effectRegistry.Register("PowerUp", b => b
    .AsPermanent());
```

### 4. 効果の適用

```csharp
ulong targetEntityId = 1;
ulong sourceEntityId = 2;

// 効果を適用
var result = manager.TryApply(targetEntityId, poisonId, sourceEntityId);

if (result.Success)
{
    Console.WriteLine($"効果を適用しました: {result.InstanceId}");

    if (result.WasMerged)
    {
        Console.WriteLine("既存の効果にスタックされました");
    }
}
else
{
    Console.WriteLine($"適用失敗: {result.FailureReason}");
}
```

### 5. 効果の確認と解除

```csharp
// 特定の効果を持っているか確認
if (manager.HasEffect(targetEntityId, poisonId))
{
    Console.WriteLine("毒状態です");
}

// タグで確認
if (manager.HasEffectWithTag(targetEntityId, crowdControlTag))
{
    Console.WriteLine("行動阻害を受けています");
}

// 効果を解除
manager.Remove(result.InstanceId, new RemovalReasonId(1));

// タグで一括解除
manager.RemoveByTag(targetEntityId, debuffTag, new RemovalReasonId(2));
```

### 6. 時間経過処理

```csharp
// ゲームループ内で呼び出す（毎フレーム必須）
var currentTick = new GameTick(frameCount);
manager.ProcessTick(currentTick);
// → 保留中のリクエストを処理し、期限切れの効果を自動削除
```

**重要**: `ProcessTick`は毎フレーム呼び出す必要があります。効果の適用・削除・変更はすべてリクエストとしてキューイングされ、`ProcessTick`が呼ばれたタイミングで一括処理されます。

## 主要な概念

### 識別子

| 型 | 用途 |
|---|---|
| `EffectId` | 効果定義の識別（「毒」「スタン」など） |
| `EffectInstanceId` | 実行中の効果インスタンスの識別 |
| `TagId` | 効果の分類タグ |
| `EntityId` | 対象エンティティの識別（ulongのエイリアス） |

### スタック動作

同じ効果を重ねがけしたときの挙動を定義します。

```csharp
// 加算型: スタック数が増加（最大5）
.WithStackConfig(StackConfig.Additive(5))

// 非スタック型: 常に1スタック
.WithStackConfig(StackConfig.None)
```

### 期間動作

同じ効果を再適用したときの期間の扱いを定義します。

```csharp
// 延長: 残り時間に加算
.WithDurationBehavior(DurationBehavior.Extend)

// リフレッシュ: 最大まで戻す
.WithDurationBehavior(DurationBehavior.Refresh)

// 維持: 変更しない
.WithDurationBehavior(DurationBehavior.KeepExisting)

// 上書き: 常に新しい期間で置き換え
.WithDurationBehavior(DurationBehavior.Replace)
```

### タグシステム

効果をカテゴリ分けし、一括操作や条件判定に使用します。

```csharp
// タグセットの作成
var tags = tagRegistry.CreateSet(debuffTag, dotTag);

// 効果にタグを付与
.WithTags(tags)

// タグによる検索
manager.HasEffectWithTag(entityId, debuffTag);
manager.RemoveByTag(entityId, debuffTag, reason);
```

## 効果結果

全ての効果を適用した結果を一度に計算し、他のシステムから参照できます。

```csharp
// ゲーム側で効果結果構造体を定義
public struct GameResult
{
    public int AttackFlat;
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
        s.AttackFlat += 10 * stacks;
    }));

var percentAdd = effectRegistry.Register("PercentAdd", b => b
    .WithDuration(new TickDuration(600))
    .WithPriority(100)  // 2番目
    .WithContributor<GameResult>((ref GameResult s, int stacks) =>
    {
        s.AttackPercentAdd += 20 * stacks;
    }));

// フレーム処理時に効果結果を計算
var result = manager.CalculateResult<GameResult>(entityId, default);
```

### 適用順序（Priority）

コントリビュータは**Priority昇順**で適用されます（小さい値が先）。同じPriorityの場合は**EffectId順**（定義順）で適用されます。

これにより：
- 効果の適用順は**実行時の順番に依存しない**
- 同じバフが揃っていれば**常に同じ結果**（決定論的）

```csharp
// 推奨: 計算順序を明確にするPriority設計
// Priority 0:   フラット加算
// Priority 100: 加算%
// Priority 200: 乗算%
```

### トリガー検出

前後の効果結果を比較して状態変化を検出できます。

```csharp
var prevResult = entity.EffectResult;
var currResult = manager.CalculateResult<GameResult>(entity.Id, default);

// スタン開始トリガー
if (!prevResult.IsStunned && currResult.IsStunned)
{
    OnStunStart(entity);
}
```

## イベント

効果のライフサイクルをイベントで監視できます。

```csharp
manager.OnEffectApplied += e =>
{
    Console.WriteLine($"効果適用: {e.DefinitionId} → {e.TargetId}");
};

manager.OnEffectRemoved += e =>
{
    Console.WriteLine($"効果解除: {e.DefinitionId}, 理由: {e.Reason}");
};

manager.OnStackChanged += e =>
{
    Console.WriteLine($"スタック変化: {e.PreviousStacks} → {e.NewStacks}");
};
```

## 遅延実行モデル

StatusEffectSystemは**決定論的動作**を保証するため、遅延実行モデルを採用しています。

### 動作の流れ

1. **リクエストのキューイング**: `TryApply`, `Remove`, `AddStacks`などの操作はリクエストとしてキューに追加されます
2. **ProcessTickでの一括処理**: `ProcessTick`が呼ばれると、キュー内の全リクエストを決定論的な順序で処理します
3. **ソート済みリストの維持**: 効果はPriority順（→EffectId順）でソートされた状態で保持されます

### 例: 適用のタイミング

```csharp
manager.TryApply(targetId, poisonId, sourceId);

// この時点ではHasEffectはfalse（まだ処理されていない）
Assert.False(manager.HasEffect(targetId, poisonId));

manager.ProcessTick(new GameTick(0));

// ProcessTick後はtrue
Assert.True(manager.HasEffect(targetId, poisonId));
```

### なぜ遅延実行か

- **決定論的動作**: 同じ入力に対して常に同じ結果を保証
- **適用順序の一貫性**: 効果が適用された順序ではなく、定義されたPriority順で処理
- **バッチ処理の効率性**: 複数の操作をまとめて処理することで、ソートのオーバーヘッドを最小化

### 注意事項

- `ProcessTick`を呼ぶまで、クエリAPI（`HasEffect`, `GetEffects`など）には新しい効果が反映されません
- 通常のゲームループでは毎フレーム`ProcessTick`を呼び出すため、この遅延は実質的に1フレーム以内です

## 詳細ドキュメント

- [DESIGN.md](./DESIGN.md) - 設計詳細と内部構造
- [Samples.md](./Samples.md) - 典型的な状態異常の実装サンプル
