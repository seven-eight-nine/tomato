# TimelineSystem 設計書

ゲーム向け超高速タイムラインシステムの詳細設計ドキュメント。

namespace: `Tomato.TimelineSystem`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [アーキテクチャ](#アーキテクチャ)
5. [クリップ詳細](#クリップ詳細)
6. [トラック詳細](#トラック詳細)
7. [クエリアルゴリズム](#クエリアルゴリズム)
8. [ループ処理](#ループ処理)
9. [ブレンド計算](#ブレンド計算)
10. [構築API](#構築api)
11. [シリアライズ](#シリアライズ)
12. [パフォーマンス](#パフォーマンス)
13. [実践パターン集](#実践パターン集)
14. [トラブルシューティング](#トラブルシューティング)

---

## クイックスタート

### 1. トラックとクリップを定義

```csharp
using Tomato.TimelineSystem;

// トラック（Track継承するだけ）
public class AnimationTrack : Track
{
}

// クリップ（どのトラックに所属するかを型で明示）
public class AnimationClip : Clip<AnimationTrack>
{
    private static int _nextId = 1;

    public override ClipType Type => ClipType.Range;
    public string AnimationName { get; }

    public AnimationClip(string name, int start, int end)
        : base(new ClipId(_nextId++), start, end)
    {
        AnimationName = name;
    }
}
```

### 2. シーケンスを構築

```csharp
var sequence = new SequenceBuilder()
    .WithLoop(startFrame: 0, endFrame: 120)
    .AddTrack<AnimationTrack>(t => t
        .AddClip(new AnimationClip("idle", 0, 30))
        .AddClip(new AnimationClip("walk", 20, 60))
    )
    .Build();
```

### 3. クエリを実行

```csharp
var ctx = new QueryContext();
sequence.Query(currentFrame: 25, deltaFrames: 5, ctx);

foreach (var evt in ctx.Events)
{
    Console.WriteLine($"{evt.EventType}: {((AnimationClip)evt.Clip).AnimationName}");
}
```

---

## 用語定義

### 中核概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **シーケンス** | Sequence | タイムラインの再生単位。トラック群とループ設定を持つ。 |
| **トラック** | Track | クリップを格納するコンテナ。同一トラック内のクリップが重複するとブレンド対象になる。 |
| **クリップ** | Clip | タイムライン上の要素。Instant（点）またはRange（区間）。 |
| **フレーム** | Frame | タイムライン上の位置を表す整数値。 |

### クエリ関連

| 用語 | 英語 | 定義 |
|------|------|------|
| **クエリ** | Query | フレーム区間を指定してイベントを取得する操作。 |
| **イベント** | Event | クエリ結果として返されるクリップの状態変化。 |
| **経過率** | Progress | Rangeクリップ内での進行度（0.0〜1.0）。 |
| **ブレンド重み** | BlendWeight | 重複クリップに割り当てられる合成比率。 |

### イベントタイプ

| 用語 | 英語 | 対象 | 定義 |
|------|------|------|------|
| **Fired** | Fired | Instant | 点イベントがクエリ範囲内で発火。 |
| **Enter** | Enter | Range | 区間開始フレームがクエリ範囲内。 |
| **Exit** | Exit | Range | 区間終了フレームがクエリ範囲内。 |
| **Active** | Active | Range | クエリ終了時点で区間内（継続中）。 |

---

## 設計哲学

### 原則1: 1次元当たり判定としての設計

タイムラインを「1次元空間」、クリップを「オブジェクト」と見なし、フレーム区間を「光線」としてクエリする。空間インデックス（バイナリサーチ）により高速な検索を実現。

```
Timeline (1D空間)
    0        10        20        30        40        50
    |---------|---------|---------|---------|---------|
    [===ClipA===]
              [=========ClipB=========]
                        [===ClipC===]

Query(15, 10) → 範囲 [15, 25] と交差するクリップを検出
    → ClipA: Exit (frame=20)
    → ClipB: Active (progress=0.5)
    → ClipC: Enter (frame=20)
```

### 原則2: 型安全なクリップ・トラック関係

クリップはどのトラック型に所属するかを型パラメータで明示する。これにより、Builder経由では不正なクリップの追加がコンパイルエラーになる。

```csharp
// クリップは所属するトラック型を宣言
public class AnimationClip : Clip<AnimationTrack> { ... }
public class SoundClip : Clip<SoundTrack> { ... }

// Builder経由は型安全
.AddTrack<AnimationTrack>(t => t
    .AddClip(new AnimationClip(...))  // OK
    // .AddClip(new SoundClip(...))   // コンパイルエラー！
)
```

### 原則3: ゼロアロケーション

QueryContextを再利用することで、毎フレームのクエリでヒープアロケーションを発生させない。

```csharp
// 一度だけ確保
var ctx = new QueryContext();

// 毎フレーム再利用
void Update()
{
    sequence.Query(frame, delta, ctx);  // アロケーションなし
}
```

### 原則4: データとロジックの分離

シリアライズ用のDTO層を分離。クリップのデータはDTOで保存し、ロジック（Clip実装）は実行時に復元する。

```csharp
// データ層（シリアライズ可能）
public record AnimationClipDto : ClipDto
{
    public string AnimationName { get; init; }
}

// ロジック層（実行時）
public class AnimationClip : Clip<AnimationTrack>
{
    public string AnimationName { get; }
}
```

---

## アーキテクチャ

### クラス構成

```
Sequence
├── LoopSettings (ループ設定)
├── IBlendCalculator (ブレンド計算戦略)
├── List<Track> (トラック群)
│   └── Track (基底クラス)
│       └── List<Clip> (クリップ群)
│           ├── Clip<TTrack> (Instant: StartFrame == EndFrame)
│           └── Clip<TTrack> (Range: StartFrame < EndFrame)
└── Query(currentFrame, deltaFrames, ctx) → QueryContext
```

### データフロー

```
Query(currentFrame, deltaFrames, ctx)
    │
    ├─1. ctx.Reset()
    │
    ├─2. ループ処理
    │   ├─ 範囲がループ終端を超える場合
    │   ├─ ループ回数を計算
    │   └─ 結果フレームをラップ
    │
    ├─3. 各トラックをクエリ
    │   ├─ バイナリサーチで開始位置を特定
    │   ├─ クリップをスキャン
    │   └─ イベントをctxに追加
    │
    ├─4. 重複クリップを収集
    │   └─ 同一トラック内で2つ以上アクティブなクリップ
    │
    ├─5. ブレンド重み計算
    │   └─ IBlendCalculator.CalculateWeights()
    │
    └─6. 結果をctxに格納
        ├─ ctx.Events
        ├─ ctx.Overlaps
        ├─ ctx.ResultFrame
        ├─ ctx.DidLoop
        └─ ctx.LoopCount
```

---

## クリップ詳細

### Clip基底クラス

```csharp
public abstract class Clip
{
    public ClipId Id { get; }
    public abstract ClipType Type { get; }
    public int StartFrame { get; }
    public int EndFrame { get; }

    protected Clip(ClipId id, int startFrame, int endFrame);
}

// 特定のトラック型に所属するクリップ
public abstract class Clip<TTrack> : Clip where TTrack : Track
{
    protected Clip(ClipId id, int startFrame, int endFrame)
        : base(id, startFrame, endFrame) { }
}
```

### Instantクリップ

点イベント。StartFrameとEndFrameは同じ値。

```csharp
public class SoundClip : Clip<SoundTrack>
{
    public override ClipType Type => ClipType.Instant;
    public string SoundName { get; }

    public SoundClip(ClipId id, string name, int frame)
        : base(id, frame, frame)  // StartFrame == EndFrame
    {
        SoundName = name;
    }
}
```

**用途例:**
- 効果音の再生
- パーティクル発生
- ヒット判定の発生点
- コールバックの呼び出し

### Rangeクリップ

区間イベント。StartFrameからEndFrameまでの期間を持つ。

```csharp
public class AnimationClip : Clip<AnimationTrack>
{
    public override ClipType Type => ClipType.Range;
    public string AnimationName { get; }

    public AnimationClip(ClipId id, string name, int start, int end)
        : base(id, start, end)  // StartFrame < EndFrame
    {
        AnimationName = name;
    }
}
```

**用途例:**
- アニメーション再生
- ステート維持
- 当たり判定区間
- 無敵時間

### ClipHit（クエリ結果）

```csharp
public readonly struct ClipHit
{
    public readonly Clip Clip;
    public readonly ClipEventType EventType;  // Fired, Enter, Exit, Active
    public readonly int EventFrame;
    public readonly float Progress;           // 0.0-1.0
}
```

| フィールド | 説明 |
|-----------|------|
| Clip | 該当クリップへの参照 |
| EventType | イベント種類 |
| EventFrame | イベント発生フレーム |
| Progress | Rangeクリップの経過率（Instant/Enterは0、Exitは1） |

---

## トラック詳細

### Track基底クラス

```csharp
public abstract class Track
{
    public TrackId Id { get; }

    public void AddClip(Clip clip);
    public void RemoveClip(ClipId clipId);
    public Clip? GetClip(ClipId clipId);

    // 内部メソッド（Sequenceから呼ばれる）
    internal int QueryRange(int startFrame, int endFrame, Span<ClipHit> results, bool includeActive);
    internal int GetActiveClips(int frame, Span<OverlapInfo> results);
    internal void AssignId(TrackId id);
}
```

Track基底クラスは以下を全て実装済み：
- クリップリストの管理
- StartFrameでのソート
- バイナリサーチによる高速検索
- 最大クリップ長の追跡

### ユーザー定義トラック

```csharp
// 継承するだけ
public class AnimationTrack : Track
{
}

// 追加プロパティが必要な場合
public class PriorityTrack : Track
{
    public int Priority { get; set; }
}
```

### 使用例

```csharp
var sequence = new SequenceBuilder()
    .AddTrack<PriorityTrack>(t =>
    {
        t.Track.Priority = 10;  // トラック本体のプロパティは Track 経由
        t.AddClip(new PriorityClip(...));
    })
    .Build();
```

---

## クエリアルゴリズム

### 基本フロー

```
入力: currentFrame, deltaFrames (deltaFrames >= 0)
出力: イベント群, 重複情報, 結果フレーム

1. フレーム範囲計算
   rangeStart = currentFrame
   rangeEnd = currentFrame + deltaFrames

2. ループ処理（ループ設定がある場合）
   → 後述

3. 各トラックをクエリ
   for each track:
     searchStart = FindFirstPotentialClip(rangeStart)
     for i = searchStart to clipCount:
       clip = clips[i]

       if clip.StartFrame > rangeEnd:
         break  // これ以降のクリップは範囲外

       if clip.Type == Instant:
         if rangeStart <= clip.StartFrame <= rangeEnd:
           emit Fired event

       else:  // Range
         if rangeStart <= clip.StartFrame <= rangeEnd:
           emit Enter event

         if rangeStart <= clip.EndFrame <= rangeEnd:
           emit Exit event

         if clip.StartFrame < rangeStart && clip.EndFrame > rangeEnd:
           emit Active event (with progress)

4. 重複クリップ収集（結果フレーム時点）
   for each track:
     activeClips = clips where clip.Start <= resultFrame <= clip.End
     if activeClips.Count > 1:
       add to overlaps

5. ブレンド重み計算
   blendCalculator.CalculateWeights(overlaps)
```

### バイナリサーチの最適化

```csharp
private int FindFirstPotentialClip(int frame)
{
    // 最大クリップ長を考慮した検索開始位置
    int searchFrame = frame - _maxClipDuration;

    // バイナリサーチ
    int lo = 0, hi = _clips.Count;
    while (lo < hi)
    {
        int mid = lo + (hi - lo) / 2;
        if (_clips[mid].StartFrame < searchFrame)
            lo = mid + 1;
        else
            hi = mid;
    }
    return lo;
}
```

**なぜ `frame - _maxClipDuration` から検索するか？**

```
frame = 100, maxDuration = 50

クリップA: [40, 60]   → 100時点ではアクティブでない
クリップB: [60, 110]  → 100時点でアクティブ！（StartFrame=60 < 100）
クリップC: [100, 150] → 100時点でアクティブ

searchFrame = 100 - 50 = 50
→ StartFrame >= 50 のクリップから検索開始
→ クリップBとCを発見
```

---

## ループ処理

### LoopSettings

```csharp
public readonly struct LoopSettings
{
    public readonly bool Enabled;
    public readonly int StartFrame;
    public readonly int EndFrame;

    public int Duration => EndFrame - StartFrame;

    public static LoopSettings None => new(false, 0, 0);
    public static LoopSettings Create(int start, int end);
}
```

### ループ時のクエリ処理

```
例: LoopSettings(0, 100), Query(currentFrame=90, deltaFrames=25)

1. 範囲計算
   rangeStart = 90
   rangeEnd = 90 + 25 = 115

2. ループ検出
   rangeEnd (115) >= loopEndFrame (100)
   → ループ発生

3. 分割クエリ
   Phase 1: Query(90, 100)   // ループ前
   Phase 2: Query(0, 15)     // ループ後

4. 結果フレーム
   overshoot = 115 - 100 = 15
   resultFrame = 0 + 15 = 15

5. ループ情報
   didLoop = true
   loopCount = 1
```

### 複数ループのケース

```
例: LoopSettings(0, 100), Query(currentFrame=90, deltaFrames=225)

rangeEnd = 90 + 225 = 315

overshoot = 315 - 100 = 215
loopCount = 1 + 215 / 100 = 3
remainder = 215 % 100 = 15

resultFrame = 0 + 15 = 15
```

---

## ブレンド計算

### 重複検出

同一トラック内で、同じフレームに2つ以上のRangeクリップがアクティブな場合に重複として検出される。

```
Track 0:
  [===ClipA===]
        [===ClipB===]
              ^
              frame=25: ClipA と ClipB が重複
```

### IBlendCalculator

```csharp
public interface IBlendCalculator
{
    void CalculateWeights(Span<OverlapInfo> overlaps);
}
```

### ProgressBasedBlend（デフォルト）

経過率に基づいてブレンド重みを計算する。

```csharp
public sealed class ProgressBasedBlend : IBlendCalculator
{
    public static readonly ProgressBasedBlend Instance = new();

    public void CalculateWeights(Span<OverlapInfo> overlaps)
    {
        float totalProgress = 0f;
        for (int i = 0; i < overlaps.Length; i++)
            totalProgress += overlaps[i].Progress;

        for (int i = 0; i < overlaps.Length; i++)
        {
            float weight = overlaps[i].Progress / totalProgress;
            overlaps[i] = new OverlapInfo(
                overlaps[i].Clip,
                overlaps[i].Progress,
                weight
            );
        }
    }
}
```

**例:**
```
ClipA: progress=0.8 → weight = 0.8 / 1.3 = 0.615
ClipB: progress=0.5 → weight = 0.5 / 1.3 = 0.385
合計: 1.0
```

### カスタムブレンド

```csharp
// 等分配ブレンド
public class EqualBlend : IBlendCalculator
{
    public void CalculateWeights(Span<OverlapInfo> overlaps)
    {
        float weight = 1.0f / overlaps.Length;
        for (int i = 0; i < overlaps.Length; i++)
        {
            overlaps[i] = new OverlapInfo(
                overlaps[i].Clip, overlaps[i].Progress, weight);
        }
    }
}

// 最新クリップ優先
public class LatestWinsBlend : IBlendCalculator
{
    public void CalculateWeights(Span<OverlapInfo> overlaps)
    {
        for (int i = 0; i < overlaps.Length - 1; i++)
            overlaps[i] = new OverlapInfo(overlaps[i].Clip, overlaps[i].Progress, 0f);

        overlaps[^1] = new OverlapInfo(overlaps[^1].Clip, overlaps[^1].Progress, 1f);
    }
}
```

---

## 構築API

### SequenceBuilder（Fluent API）

```csharp
var sequence = new SequenceBuilder()
    .WithLoop(0, 120)
    .WithBlendCalculator(customBlend)
    .AddTrack<AnimationTrack>(t => t
        .AddClip(new AnimationClip("idle", 0, 30))
        .AddClip(new AnimationClip("walk", 20, 60))
    )
    .AddTrack<SoundTrack>(t => t
        .AddClip(new SoundClip("footstep", 25))
    )
    .AddTrack(prebuiltTrack)  // 事前に作成したトラックも追加可能
    .Build();
```

### TrackConfigurator（型安全性）

`AddTrack<T>` のラムダには `TrackConfigurator<T>` が渡される。

```csharp
public sealed class TrackConfigurator<T> where T : Track
{
    public TrackConfigurator<T> AddClip(Clip<T> clip);  // Clip<T>のみ受付
    public T Track { get; }  // トラック本体へのアクセス
}
```

これにより、`Track.AddClip(Clip)` は非ジェネリックでも、Builder経由では型安全にクリップを追加できる。

```csharp
.AddTrack<AnimationTrack>(t => t
    .AddClip(new AnimationClip(...))   // OK: Clip<AnimationTrack>
    // .AddClip(new SoundClip(...))    // エラー: Clip<SoundTrack>は不可
)
```

### 直接構築

```csharp
var sequence = new Sequence();
sequence.SetLoopSettings(LoopSettings.Create(0, 120));
sequence.SetBlendCalculator(customBlend);

var animTrack = sequence.CreateTrack<AnimationTrack>();
animTrack.AddClip(new AnimationClip("idle", 0, 30));

var track2 = new AnimationTrack();
sequence.AddTrack(track2);
```

**注意**: 直接構築の場合、`AddClip(Clip)` は非ジェネリックなので型チェックはない。型安全性が必要な場合は SequenceBuilder を使用すること。

---

## シリアライズ

### 設計方針

- **データとロジックを分離**: Clipはロジック、ClipDtoはデータ
- **ユーザー責任**: 具体的なクリップのシリアライズはユーザーが実装
- **フレームワーク提供**: 構造のシリアライズ基盤（DTO）

### DTO群

```csharp
public record ClipDto
{
    public int Id { get; init; }
    public int TrackId { get; init; }
    public ClipType Type { get; init; }
    public int StartFrame { get; init; }
    public int EndFrame { get; init; }
}

public record TrackDto
{
    public int Id { get; init; }
    public List<ClipDto> Clips { get; init; }
}

public record SequenceDto
{
    public LoopSettingsDto? Loop { get; init; }
    public List<TrackDto> Tracks { get; init; }
}

public record LoopSettingsDto
{
    public bool Enabled { get; init; }
    public int StartFrame { get; init; }
    public int EndFrame { get; init; }
}
```

### ユーザー定義DTO

```csharp
public record AnimationClipDto : ClipDto
{
    public string AnimationName { get; init; }
    public float BlendInFrames { get; init; }
}

public record SoundClipDto : ClipDto
{
    public string SoundName { get; init; }
    public float Volume { get; init; }
}
```

### 復元インターフェース

```csharp
public interface IClipFactory
{
    Clip Create(ClipDto dto);
}

public interface ISequenceSerializer
{
    SequenceDto Serialize(Sequence sequence);
    Sequence Deserialize(SequenceDto dto, IClipFactory clipFactory);
}
```

### 実装例

```csharp
public class MyClipFactory : IClipFactory
{
    public Clip Create(ClipDto dto)
    {
        return dto switch
        {
            AnimationClipDto anim => new AnimationClip(
                new ClipId(anim.Id), anim.AnimationName, anim.StartFrame, anim.EndFrame),

            SoundClipDto sound => new SoundClip(
                new ClipId(sound.Id), sound.SoundName, sound.StartFrame),

            _ => throw new NotSupportedException()
        };
    }
}
```

---

## パフォーマンス

### ベンチマーク結果

| テスト | 結果 |
|--------|------|
| 10000クリップ検索 | 3.22 us/query |
| 1000クリップ/100トラック | 31.38 us/query |
| 密集オーバーラップ(50クリップ) | 4.84 us/query |
| ループ境界クエリ | 1.04 us/query |
| ブレンド計算(100オーバーラップ) | 2450 ns |
| メモリアロケーション | 0 bytes |

### 高速化設計

| 手法 | 説明 |
|------|------|
| **struct中心** | ClipHit, OverlapInfo は値型 |
| **バッファ再利用** | QueryContextの内部バッファを使い回し |
| **バイナリサーチ** | StartFrameでソート、O(log n) 探索 |
| **最大長追跡** | _maxClipDuration で検索範囲を限定 |
| **sealed実装** | ProgressBasedBlend は sealed で JIT 最適化 |
| **Span<T>** | クエリ結果はSpanで返却 |

### 最適化のヒント

```csharp
// 1. QueryContextを再利用
var ctx = new QueryContext();
for (int i = 0; i < 1000; i++)
{
    sequence.Query(i, 1, ctx);  // 同じctxを再利用
}

// 2. 適切な初期容量
var ctx = new QueryContext(
    eventCapacity: 256,    // 予想イベント数
    overlapCapacity: 64    // 予想オーバーラップ数
);

// 3. トラック数を適切に
// 少数の大きなトラックより、多数の小さなトラックが効率的
```

---

## 実践パターン集

### アクションゲームのアニメーション

```csharp
public class AnimationTrack : Track { }

public class AnimationClip : Clip<AnimationTrack>
{
    public override ClipType Type => ClipType.Range;
    public string AnimationName { get; }
    public bool Loop { get; }
    public float PlaybackSpeed { get; }

    public AnimationClip(ClipId id, string name, int start, int end)
        : base(id, start, end)
    {
        AnimationName = name;
    }
}

void ProcessAnimation(QueryContext ctx)
{
    foreach (var overlap in ctx.Overlaps)
    {
        var clip = (AnimationClip)overlap.Clip;
        animator.SetLayerWeight(clip.AnimationName, overlap.BlendWeight);
    }
}
```

### 格闘ゲームの当たり判定

```csharp
public class CombatTrack : Track { }

public class HitboxClip : Clip<CombatTrack>
{
    public override ClipType Type => ClipType.Range;
    public int Damage { get; }
    public HitboxShape Shape { get; }
    public Vector2 Offset { get; }

    public HitboxClip(ClipId id, int damage, int start, int end)
        : base(id, start, end)
    {
        Damage = damage;
    }
}

public class InvincibilityClip : Clip<CombatTrack>
{
    public override ClipType Type => ClipType.Range;

    public InvincibilityClip(ClipId id, int start, int end)
        : base(id, start, end) { }
}

void ProcessCombat(QueryContext ctx)
{
    foreach (var evt in ctx.Events)
    {
        if (evt.EventType == ClipEventType.Enter && evt.Clip is HitboxClip hitbox)
        {
            ActivateHitbox(hitbox);
        }
        else if (evt.EventType == ClipEventType.Exit && evt.Clip is HitboxClip)
        {
            DeactivateHitbox();
        }
    }
}
```

### RPGのタイムライン演出

```csharp
public class CameraTrack : Track { }
public class DialogueTrack : Track { }
public class EffectTrack : Track { }

public class CameraClip : Clip<CameraTrack> { /* カメラ制御 */ }
public class DialogueClip : Clip<DialogueTrack> { /* 台詞表示 */ }
public class EffectClip : Clip<EffectTrack> { /* エフェクト発生 */ }

var cutscene = new SequenceBuilder()
    .AddTrack<CameraTrack>(t => t
        .AddClip(new CameraClip("zoom_in", 0, 60))
        .AddClip(new CameraClip("pan_right", 60, 120))
    )
    .AddTrack<DialogueTrack>(t => t
        .AddClip(new DialogueClip("Hello!", 30, 90))
        .AddClip(new DialogueClip("Goodbye!", 100, 150))
    )
    .AddTrack<EffectTrack>(t => t
        .AddClip(new EffectClip("sparkle", 45))      // Instant
        .AddClip(new EffectClip("explosion", 120))  // Instant
    )
    .Build();
```

---

## トラブルシューティング

### イベントが取得できない

**1. フレーム範囲を確認**
```csharp
// deltaFrames=0 だと、ちょうどそのフレームのイベントのみ
sequence.Query(25, 0, ctx);  // frame=25のイベントのみ

// deltaFrames>0 で範囲を指定
sequence.Query(20, 10, ctx);  // frame=20〜30のイベント
```

**2. クリップの範囲を確認**
```csharp
// クリップが追加されているか
var clip = track.GetClip(clipId);

// 範囲が正しいか
Console.WriteLine($"{clip.StartFrame} - {clip.EndFrame}");
```

### 重複が検出されない

**1. 同一トラック内か確認**
```csharp
// 異なるトラックのクリップは重複として検出されない
var track1 = sequence.CreateTrack<AnimationTrack>();
var track2 = sequence.CreateTrack<AnimationTrack>();
track1.AddClip(clipA);
track2.AddClip(clipB);  // 別トラック → 重複しない

// 同一トラックに追加
var track = sequence.CreateTrack<AnimationTrack>();
track.AddClip(clipA);
track.AddClip(clipB);  // 同一トラック → 重複する
```

**2. クリップが実際に重なっているか確認**
```csharp
// 重なっていない
[0, 10] と [20, 30]  // 重複なし

// 重なっている
[0, 20] と [10, 30]  // 10〜20で重複
```

### パフォーマンスが悪い

**1. QueryContextを再利用しているか**
```csharp
// 悪い例
void Update()
{
    var ctx = new QueryContext();  // 毎フレームnew
    sequence.Query(frame, delta, ctx);
}

// 良い例
QueryContext _ctx = new QueryContext();
void Update()
{
    sequence.Query(frame, delta, _ctx);  // 再利用
}
```

**2. トラック数が多すぎないか**
- 各トラックに対してクエリが実行される
- 不要なトラックは削除

**3. クリップが多すぎないか**
- バイナリサーチで O(log n) だが、ヒットするクリップ数が多いと遅くなる
- クリップの配置を見直す

---

## ディレクトリ構造

```
TimelineSystem/
├── README.md                           # クイックスタート
├── DESIGN.md                           # 本ドキュメント
│
├── TimelineSystem.Core/
│   ├── TimelineSystem.Core.csproj
│   │
│   ├── Data/
│   │   ├── ClipId.cs                   # クリップID
│   │   ├── TrackId.cs                  # トラックID
│   │   ├── ClipType.cs                 # Instant/Range
│   │   ├── ClipEventType.cs            # Fired/Enter/Exit/Active
│   │   └── LoopSettings.cs             # ループ設定
│   │
│   ├── Clips/
│   │   ├── Clip.cs                     # クリップ基底クラス
│   │   ├── ClipHit.cs                  # クエリ結果
│   │   └── OverlapInfo.cs              # 重複情報
│   │
│   ├── Tracks/
│   │   └── Track.cs                    # トラック基底クラス
│   │
│   ├── Blending/
│   │   ├── IBlendCalculator.cs         # ブレンド計算インターフェース
│   │   └── ProgressBasedBlend.cs       # デフォルト実装
│   │
│   ├── Queries/
│   │   ├── QueryContext.cs             # クエリコンテキスト
│   │   └── QueryResult.cs              # クエリ結果（ref struct）
│   │
│   ├── Building/
│   │   ├── SequenceBuilder.cs          # Fluent API
│   │   └── TrackConfigurator.cs        # トラック構築ヘルパー
│   │
│   ├── Serialization/
│   │   ├── ClipDto.cs                  # クリップDTO
│   │   ├── TrackDto.cs                 # トラックDTO
│   │   ├── SequenceDto.cs              # シーケンスDTO
│   │   ├── LoopSettingsDto.cs          # ループ設定DTO
│   │   ├── IClipFactory.cs             # クリップ復元
│   │   └── ISequenceSerializer.cs      # シリアライズ
│   │
│   └── Sequence.cs                     # シーケンス本体
│
└── TimelineSystem.Tests/
    ├── TimelineSystem.Tests.csproj
    ├── TestClips.cs                    # テスト用トラック・クリップ
    ├── InstantClipTests.cs             # Instantテスト
    ├── RangeClipTests.cs               # Rangeテスト
    ├── LoopTests.cs                    # ループテスト
    ├── BlendTests.cs                   # ブレンドテスト
    ├── BuilderTests.cs                 # ビルダーテスト
    └── PerformanceTests.cs             # パフォーマンステスト
```
