# TimelineSystem

ゲーム向け超高速タイムラインシステム。

## これは何？

「1次元の当たり判定ライブラリ」として設計されたタイムラインシステム。
フレーム区間をクエリし、その間に発生するイベント（Enter/Exit/Active）と重複クリップのブレンド重みを取得する。

```
Sequence (再生単位)
├── Track (トラック群) ← Track継承
│   └── Clip (クリップ群) ← Clip<TTrack>継承
│       ├── InstantClip (点イベント)
│       └── RangeClip (区間イベント)
└── Query(currentFrame, deltaFrames) → イベント + ブレンド情報
```

## なぜ使うのか

- **超高速**: バイナリサーチで O(log n) クエリ、10000クリップでも3us
- **ゼロアロケーション**: QueryContext再利用でGC負荷なし
- **型安全**: Clip<TTrack>でクリップとトラックの関係を静的に検証
- **シンプル**: Track/Clipを継承するだけで独自のトラック・クリップを定義
- **シリアライズ対応**: DTO層でデータとロジックを分離

---

## クイックスタート

### 1. トラックとクリップを定義

```csharp
using Tomato.TimelineSystem;

// トラック（Track継承するだけ）
public class AnimationTrack : Track
{
    // 必要なら追加プロパティを定義
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

// Instantクリップ
public class SoundClip : Clip<SoundTrack>
{
    private static int _nextId = 1000;

    public override ClipType Type => ClipType.Instant;
    public string SoundName { get; }

    public SoundClip(string name, int frame)
        : base(new ClipId(_nextId++), frame, frame)  // Instant: start == end
    {
        SoundName = name;
    }
}

public class SoundTrack : Track { }
```

### 2. シーケンスを構築

```csharp
// Fluent API
var sequence = new SequenceBuilder()
    .WithLoop(startFrame: 0, endFrame: 120)
    .AddTrack<AnimationTrack>(t => t
        .AddClip(new AnimationClip("idle", 0, 30))
        .AddClip(new AnimationClip("walk", 20, 60))
    )
    .AddTrack<SoundTrack>(t => t
        .AddClip(new SoundClip("footstep", 25))
        .AddClip(new SoundClip("footstep", 45))
    )
    .Build();

// または直接構築
var sequence = new Sequence();
sequence.SetLoopSettings(LoopSettings.Create(0, 120));

var animTrack = sequence.CreateTrack<AnimationTrack>();
animTrack.AddClip(new AnimationClip("idle", 0, 30));
```

### 3. 毎フレームクエリ

```csharp
var ctx = new QueryContext();

void Update(int currentFrame, int deltaFrames)
{
    sequence.Query(currentFrame, deltaFrames, ctx);

    // イベント処理
    foreach (var evt in ctx.Events)
    {
        switch (evt.EventType)
        {
            case ClipEventType.Fired:
                var sound = (SoundClip)evt.Clip;
                PlaySound(sound.SoundName);
                break;

            case ClipEventType.Enter:
                var anim = (AnimationClip)evt.Clip;
                StartAnimation(anim.AnimationName);
                break;

            case ClipEventType.Exit:
                StopAnimation();
                break;

            case ClipEventType.Active:
                // evt.Progress で経過率 (0.0-1.0) を取得
                break;
        }
    }

    // 重複クリップのブレンド処理
    foreach (var overlap in ctx.Overlaps)
    {
        var anim = (AnimationClip)overlap.Clip;
        BlendAnimation(anim.AnimationName, overlap.BlendWeight);
    }

    // ループ情報
    if (ctx.DidLoop)
    {
        OnLoopCompleted(ctx.LoopCount);
    }
}
```

---

## 型安全性

Fluent APIで構築する場合、`TrackConfigurator<T>` が型安全性を担保する。

```csharp
var sequence = new SequenceBuilder()
    .AddTrack<AnimationTrack>(t => t
        .AddClip(new AnimationClip("idle", 0, 30))   // OK
        // .AddClip(new SoundClip("footstep", 25))   // コンパイルエラー！
    )
    .AddTrack<SoundTrack>(t => t
        .AddClip(new SoundClip("footstep", 25))      // OK
    )
    .Build();
```

`Track.AddClip(Clip)` は非ジェネリックだが、Builder経由で追加する際は `Clip<T>` が要求される。

---

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** に以下が記載されている：

- 用語定義
- 設計哲学
- クエリアルゴリズム
- ループ処理
- ブレンド計算
- シリアライズ
- パフォーマンス設計

---

## 主要な概念

### イベントタイプ

| イベント | クリップ種類 | 説明 |
|:--------:|:------------:|------|
| **Fired** | Instant | 点イベントがフレーム範囲内で発火 |
| **Enter** | Range | 区間開始フレームがフレーム範囲内 |
| **Exit** | Range | 区間終了フレームがフレーム範囲内 |
| **Active** | Range | 区間内で継続中（経過率あり） |

### クエリの流れ

```
Query(currentFrame, deltaFrames)
    ↓
1. フレーム範囲を計算 [currentFrame, currentFrame + deltaFrames]
    ↓
2. ループ処理（設定時）
    ↓
3. 各トラックをクエリ → イベント収集
    ↓
4. 重複クリップを検出 → ブレンド重み計算
    ↓
5. QueryContext に結果を格納
```

---

## よく使うパターン

### Instantクリップ（点イベント）

```csharp
public class EffectClip : Clip<EffectTrack>
{
    public override ClipType Type => ClipType.Instant;
    public string EffectName { get; }
    public Vector3 Position { get; }

    public EffectClip(ClipId id, string name, Vector3 pos, int frame)
        : base(id, frame, frame)
    {
        EffectName = name;
        Position = pos;
    }
}
```

### Rangeクリップ（区間イベント）

```csharp
public class HitboxClip : Clip<CombatTrack>
{
    public override ClipType Type => ClipType.Range;
    public int Damage { get; }
    public Rect HitArea { get; }

    public HitboxClip(ClipId id, int damage, Rect area, int start, int end)
        : base(id, start, end)
    {
        Damage = damage;
        HitArea = area;
    }
}
```

### カスタムトラック

```csharp
public class PriorityTrack : Track
{
    public int Priority { get; set; }  // ユーザー独自プロパティ
}

public class PriorityClip : Clip<PriorityTrack>
{
    public override ClipType Type => ClipType.Range;
    // ...
}

// 使用（トラックのプロパティは Track 経由）
var sequence = new SequenceBuilder()
    .AddTrack<PriorityTrack>(t =>
    {
        t.Track.Priority = 10;
        t.AddClip(new PriorityClip(...));
    })
    .Build();
```

### ループ設定

```csharp
sequence.SetLoopSettings(LoopSettings.Create(0, 120));
sequence.SetLoopSettings(LoopSettings.None);  // ループなし
```

### カスタムブレンド計算

```csharp
public class EqualBlend : IBlendCalculator
{
    public static readonly EqualBlend Instance = new();

    public void CalculateWeights(Span<OverlapInfo> overlaps)
    {
        float weight = 1.0f / overlaps.Length;
        for (int i = 0; i < overlaps.Length; i++)
        {
            overlaps[i] = new OverlapInfo(
                overlaps[i].Clip,
                overlaps[i].Progress,
                weight
            );
        }
    }
}

sequence.SetBlendCalculator(EqualBlend.Instance);
```

---

## パフォーマンス

| 指標 | 測定値 |
|------|--------|
| 10000クリップクエリ | 3.2 us |
| 1000クリップ/100トラック | 31 us |
| 50クリップ重複 | 4.8 us |
| ループ境界クエリ | 1.0 us |
| メモリアロケーション | 0 bytes |

### 高速化のポイント

```csharp
// QueryContextを再利用
var ctx = new QueryContext();
for (int frame = 0; frame < 1000; frame++)
{
    sequence.Query(frame, 1, ctx);
    ProcessResult(ctx);
}
```

---

## ライセンス

MIT License
