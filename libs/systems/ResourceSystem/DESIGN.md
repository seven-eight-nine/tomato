# ResourceSystem 設計書

ゲームエンジン非依存のリソースローディング抽象化ライブラリの詳細設計ドキュメント。

namespace: `Tomato.ResourceSystem`

---

## 目次

1. [クイックスタート](#クイックスタート)
2. [用語定義](#用語定義)
3. [設計哲学](#設計哲学)
4. [アーキテクチャ](#アーキテクチャ)
5. [IResource詳細](#iresource詳細)
6. [ResourceCatalog詳細](#resourcecatalog詳細)
7. [Loader詳細](#loader詳細)
8. [ResourceHandle詳細](#resourcehandle詳細)
9. [参照カウント](#参照カウント)
10. [依存ロード](#依存ロード)
11. [コーナーケース](#コーナーケース)
12. [実践パターン集](#実践パターン集)
13. [トラブルシューティング](#トラブルシューティング)

---

## クイックスタート

### 1. IResourceを実装

```csharp
public class TextureResource : IResource<Texture>
{
    private readonly string _path;
    private Texture? _texture;
    private AsyncOperation? _loadOp;

    public TextureResource(string path) => _path = path;

    public void Start()
    {
        _loadOp = LoadTextureAsync(_path);
    }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        if (!_loadOp.isDone)
            return ResourceLoadState.Loading;

        _texture = _loadOp.result;
        return ResourceLoadState.Loaded;
    }

    public Texture? GetResource() => _texture;

    public void Unload()
    {
        ReleaseTexture(_texture);
        _texture = null;
    }
}
```

### 2. カタログに登録してロード

```csharp
// カタログ作成・登録
var catalog = new ResourceCatalog();
catalog.Register("texture/player", new TextureResource("player.png"));
catalog.Register("audio/bgm", new AudioResource("bgm.ogg"));

// Loader作成・リクエスト
var loader = new Loader(catalog);
var texHandle = loader.Request("texture/player");
var bgmHandle = loader.Request("audio/bgm");

// ロード開始・完了待ち
loader.Execute();
while (true) { catalog.Tick(); if (loader.Tick()) break; /* 進捗表示など */ }

// 使用
if (texHandle.TryGet<Texture>(out var tex)) DrawSprite(tex);

// 解放
loader.Dispose();
catalog.Tick();
```

---

## 用語定義

### 中核概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **リソース** | Resource | ゲームで使用するデータ。テクスチャ、サウンド、モデル等。 |
| **カタログ** | Catalog | リソースの登録簿。キーとIResourceの対応を管理。 |
| **ローダー** | Loader | リソースのロードを管理。複数リソースを一括でロード/アンロード。 |
| **ハンドル** | Handle | リソースへの参照。ロード状態の確認とリソースの取得に使用。 |

### ロード状態

| 状態 | 英語 | 説明 |
|------|------|------|
| **未ロード** | Unloaded | 初期状態。Start()でLoadingに遷移。 |
| **ロード中** | Loading | 非同期ロード中。Tick()を毎Tick呼び出し。 |
| **ロード完了** | Loaded | ロード成功。GetResource()でアクセス可能。 |
| **失敗** | Failed | ロード失敗。次のTickでリトライ。 |

### 内部概念

| 用語 | 英語 | 定義 |
|------|------|------|
| **エントリ** | Entry | IResourceをラップし、状態と参照カウントを管理する内部オブジェクト。 |
| **参照カウント** | RefCount | リソースを参照しているLoaderの数。0になったら解放。 |
| **Tick** | Tick | ロード処理を1ステップ進める操作。毎フレーム呼び出す。 |

---

## 設計哲学

### 原則1: ユーザー実装のIResource

リソースのロード/アンロードロジックはユーザーが実装する。
システムはライフサイクル管理のみを担当する。

```csharp
public interface IResource
{
    int Point => 1;                            // ロード進捗用ポイント（デフォルト1）
    void Start();                              // ロード開始
    ResourceLoadState Tick(catalog);           // 毎Tick呼ばれる
    object? GetResource();                     // リソース取得
    void Unload();                             // 解放
}
```

**メリット:**
- ゲームエンジンの実装詳細に依存しない
- 非同期ロードの方式をユーザーが選択できる
- テスト用のモック実装が容易

### 原則2: 参照カウントによる自動解放

リソースの解放タイミングをシステムが管理する。
複数のLoaderが同じリソースを使用しても安全。

```
Loader A: Request("tex") → RefCount=1
Loader B: Request("tex") → RefCount=2
Loader A: Dispose()      → RefCount=1（まだ解放しない）
Loader B: Dispose()      → RefCount=0（解放する）
```

**メリット:**
- 二重解放を防止
- 共有リソースの管理が容易
- シーン遷移時の安全な解放

### 原則3: カタログとLoaderの分離

カタログはリソースの「存在」を管理。
Loaderはリソースの「使用」を管理。

```
ResourceCatalog ─── "何がロードできるか"
      │
      ├─── Loader A ─── "シーンAで何を使うか"
      │
      └─── Loader B ─── "シーンBで何を使うか"
```

**メリット:**
- リソースの登録と使用を分離
- 複数シーンでの共有が容易
- リソースの事前登録が可能

### 原則4: 依存の動的解決

リソースが他のリソースに依存する場合、Tick内で動的にロードする。

```csharp
public ResourceLoadState Tick(ResourceCatalog catalog)
{
    // ファイル読み込みが終わるまで待つ
    if (!IsMaterialFileLoaded())
        return ResourceLoadState.Loading;

    // ファイルを解析して依存テクスチャが判明
    if (_dependencyLoader == null)
    {
        _dependencyLoader = new Loader(catalog);
        _dependencyLoader.Request("texture/diffuse");
        _dependencyLoader.Execute();
        return ResourceLoadState.Loading;
    }

    // 依存のロード完了を待つ（Tick()は呼ばない、AllLoadedで確認）
    if (!_dependencyLoader.AllLoaded)
        return ResourceLoadState.Loading;

    // 全部揃った
    return ResourceLoadState.Loaded;
}
```

**メリット:**
- 依存は実行時に発見できる（事前定義不要）
- 循環依存も参照カウントで安全に処理
- 依存の深さに制限なし
- CatalogがすべてのリソースのTickを一括管理

### 原則5: 同期ポイントとしてのTick

ロード処理はTick()で明示的に進行させる。
暗黙の非同期処理を排除し、制御を明確にする。

```csharp
// ゲームループ
void Update()
{
    // リソースのロード処理を進める（Catalogが一括管理）
    catalog.Tick();

    // ローダーの状態チェック
    if (loader.State == LoaderState.Loading)
    {
        loader.Tick();
    }

    // ゲーム処理...
}
```

**メリット:**
- ロード処理のタイミングを制御可能
- フレームレートへの影響を予測可能
- デバッグしやすい
- 同一リソースが複数のLoaderからTickされることを防止

---

## アーキテクチャ

```
┌──────────────────────────────────────────────────────────────┐
│                     IResource (public)                       │
│  ユーザーが実装するインターフェース                             │
│  ・Start()                                                   │
│  ・Tick(catalog) → ResourceLoadState                         │
│  ・GetResource() → object?                                   │
│  ・Unload()                                                  │
└──────────────────────────────────────────────────────────────┘
                               │
                               │ 登録
                               ▼
┌──────────────────────────────────────────────────────────────┐
│                     ResourceCatalog                          │
│  IResourceを登録し、内部でResourceEntryを生成・管理             │
│  ・Register(key, IResource)                                  │
│  ・Contains(key)                                             │
│  ・Tick() → リクエスト処理 + 全リソースのTick                   │
│  ・internal: GetEntry(key) → ResourceEntry                   │
│  ・internal: RequestLoad/Unload(entry)                       │
├──────────────────────────────────────────────────────────────┤
│  内部: Dictionary<string, ResourceEntry>                     │
│  内部: _pendingRequests / _processingRequests (ダブルバッファ)  │
└──────────────────────────────────────────────────────────────┘
                               │
                               │ 参照
         ┌─────────────────────┼─────────────────────┐
         │                     │                     │
         ▼                     ▼                     ▼
┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
│    Loader A     │   │    Loader B     │   │    Loader C     │
│  ・_catalog      │   │  ・_catalog      │   │  ・_catalog      │
│  ・_requests     │   │  ・_requests     │   │  ・_requests     │
└─────────────────┘   └─────────────────┘   └─────────────────┘
         │                     │
         │ 同じリソースをリクエスト
         ▼                     ▼
┌──────────────────────────────────────────────────────────────┐
│                  ResourceEntry (internal)                    │
│  IResourceをラップし、参照カウント・状態を管理                  │
│  ・Resource: IResource                                       │
│  ・State, RefCount=2（AとBがリクエスト）                       │
│  ・LoadedResource: object?（ロード後のリソース）               │
│  ・Acquire()/Release() 参照カウント操作                        │
└──────────────────────────────────────────────────────────────┘
         │
         │ ハンドル経由でアクセス
         ▼
┌──────────────────────────────────────────────────────────────┐
│                     ResourceHandle (struct)                  │
│  ・IsValid, IsLoaded, State                                  │
│  ・TryGet<T>()                                               │
│  ・内部でResourceEntryへの参照を保持                           │
└──────────────────────────────────────────────────────────────┘
```

---

## IResource詳細

### インターフェース

```csharp
public interface IResource
{
    /// <summary>
    /// ロード進捗計算用のポイント。
    /// 重いリソースは大きな値を返すことで、ローディングバーの進捗を正確に表現できる。
    /// デフォルトは1。
    /// </summary>
    int Point => 1;

    /// <summary>ロード開始。非同期処理を開始する。</summary>
    void Start();

    /// <summary>
    /// ロード処理。毎Tick呼ばれる。
    /// catalogを使って依存リソースを動的にロードできる。
    /// </summary>
    /// <returns>
    /// Loading: まだロード中
    /// Loaded: ロード完了
    /// Failed: ロード失敗（次のTickでリトライ）
    /// </returns>
    ResourceLoadState Tick(ResourceCatalog catalog);

    /// <summary>ロード完了後のリソースを取得。</summary>
    object? GetResource();

    /// <summary>アンロード。リソースを解放する。</summary>
    void Unload();
}

/// <summary>型安全版</summary>
public interface IResource<TResource> : IResource
    where TResource : class
{
    new TResource? GetResource();
}
```

### 実装パターン: カスタムポイント

重いリソースには大きなポイントを設定することで、ローディングバーの進捗を正確に表現できる。

```csharp
public class HeavyTextureResource : IResource<Texture>
{
    private readonly string _path;
    private readonly int _point;
    private Texture? _texture;

    public HeavyTextureResource(string path, int point = 10)
    {
        _path = path;
        _point = point;
    }

    // 大きなテクスチャは重いのでポイントを大きくする
    public int Point => _point;

    public void Start() { /* ... */ }
    public ResourceLoadState Tick(ResourceCatalog catalog) { /* ... */ }
    public Texture? GetResource() => _texture;
    public void Unload() { /* ... */ }
}

// 使用例
catalog.Register("texture/4k_background", new HeavyTextureResource("bg.png", point: 20));
catalog.Register("texture/icon", new SmallTextureResource("icon.png"));  // デフォルト point=1
```

### 実装パターン: 同期ロード

```csharp
public class SyncTextureResource : IResource<Texture>
{
    private readonly string _path;
    private Texture? _texture;

    public SyncTextureResource(string path) => _path = path;

    public void Start()
    {
        // 同期ロードの場合、ここで完了させてもOK
        _texture = LoadTextureSync(_path);
    }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        // Startで完了済み
        return _texture != null
            ? ResourceLoadState.Loaded
            : ResourceLoadState.Failed;
    }

    public Texture? GetResource() => _texture;

    public void Unload()
    {
        ReleaseTexture(_texture);
        _texture = null;
    }
}
```

### 実装パターン: 非同期ロード

```csharp
public class AsyncTextureResource : IResource<Texture>
{
    private readonly string _path;
    private Texture? _texture;
    private Task<Texture>? _loadTask;

    public AsyncTextureResource(string path) => _path = path;

    public void Start()
    {
        _loadTask = LoadTextureAsync(_path);
    }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        if (_loadTask == null)
            return ResourceLoadState.Failed;

        if (!_loadTask.IsCompleted)
            return ResourceLoadState.Loading;

        if (_loadTask.IsFaulted)
            return ResourceLoadState.Failed;

        _texture = _loadTask.Result;
        return ResourceLoadState.Loaded;
    }

    public Texture? GetResource() => _texture;

    public void Unload()
    {
        ReleaseTexture(_texture);
        _texture = null;
        _loadTask = null;
    }
}
```

### 実装パターン: 段階的ロード

```csharp
public class StagedModelResource : IResource<Model>
{
    private enum Stage { Init, LoadingMesh, LoadingTextures, Creating, Done, Error }

    private readonly string _path;
    private Stage _stage;
    private Model? _model;
    private Task<MeshData>? _meshTask;
    private Loader? _textureLoader;

    public void Start()
    {
        _stage = Stage.LoadingMesh;
        _meshTask = LoadMeshAsync(_path);
    }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        switch (_stage)
        {
            case Stage.LoadingMesh:
                if (!_meshTask!.IsCompleted)
                    return ResourceLoadState.Loading;

                if (_meshTask.IsFaulted)
                {
                    _stage = Stage.Error;
                    return ResourceLoadState.Failed;
                }

                // メッシュ読み込み完了、テクスチャ依存を開始
                _stage = Stage.LoadingTextures;
                _textureLoader = new Loader(catalog);
                foreach (var texPath in _meshTask.Result.TexturePaths)
                    _textureLoader.Request(texPath);
                _textureLoader.Execute();
                return ResourceLoadState.Loading;

            case Stage.LoadingTextures:
                // 依存ローダーのTick()は呼ばない（catalog.Tick()が担当）
                if (!_textureLoader!.AllLoaded)
                    return ResourceLoadState.Loading;

                _stage = Stage.Creating;
                return ResourceLoadState.Loading;

            case Stage.Creating:
                _model = CreateModel(_meshTask!.Result, _textureLoader!);
                _stage = Stage.Done;
                return ResourceLoadState.Loaded;

            default:
                return _stage == Stage.Done
                    ? ResourceLoadState.Loaded
                    : ResourceLoadState.Failed;
        }
    }

    public Model? GetResource() => _model;

    public void Unload()
    {
        _textureLoader?.Dispose();
        ReleaseModel(_model);
        _model = null;
        _stage = Stage.Init;
    }
}
```

---

## ResourceCatalog詳細

### API

```csharp
public sealed class ResourceCatalog
{
    /// <summary>登録されているリソースの数</summary>
    public int Count { get; }

    /// <summary>リソースを登録</summary>
    /// <exception cref="ArgumentNullException">key/resourceがnull</exception>
    /// <exception cref="ArgumentException">keyが重複</exception>
    public void Register(string key, IResource resource);

    /// <summary>リソースの登録を解除</summary>
    /// <exception cref="ArgumentNullException">keyがnull</exception>
    /// <exception cref="InvalidOperationException">参照カウント>0</exception>
    public void Unregister(string key);

    /// <summary>キーが登録されているか確認</summary>
    public bool Contains(string key);

    /// <summary>登録されている全キーを取得</summary>
    public IReadOnlyCollection<string> GetAllKeys();

    /// <summary>
    /// 保留中のリクエストを処理し、ロード中のリソースをTickする
    /// 毎フレーム1回呼び出す
    /// </summary>
    public void Tick();
}
```

### 使用例

```csharp
var catalog = new ResourceCatalog();

// 登録
catalog.Register("texture/player", new TextureResource("player.png"));
catalog.Register("texture/enemy", new TextureResource("enemy.png"));
catalog.Register("audio/bgm", new AudioResource("bgm.ogg"));

// 確認
Console.WriteLine(catalog.Count);  // 3
Console.WriteLine(catalog.Contains("texture/player"));  // True
Console.WriteLine(catalog.Contains("texture/boss"));    // False

// 全キー取得
foreach (var key in catalog.GetAllKeys())
{
    Console.WriteLine(key);
}
// texture/player
// texture/enemy
// audio/bgm

// ゲームループでTick
void Update()
{
    catalog.Tick();  // リソースのロード/アンロード処理を進める
}

// 登録解除（参照カウントが0でないと例外、Tick()後に呼ぶ）
catalog.Unregister("audio/bgm");
```

### キー命名規則（推奨）

```
{カテゴリ}/{サブカテゴリ}/{名前}

texture/character/player
texture/character/enemy
texture/ui/button
audio/bgm/title
audio/se/attack
model/character/player
model/environment/tree
```

---

## Loader詳細

### API

```csharp
public sealed class Loader : IDisposable
{
    /// <summary>Loaderの状態</summary>
    public LoaderState State { get; }

    /// <summary>リクエストされたリソースの数</summary>
    public int RequestCount { get; }

    /// <summary>ロード完了したリソースの数</summary>
    public int LoadedCount { get; }

    /// <summary>全リソースがロード済みか</summary>
    public bool AllLoaded { get; }

    /// <summary>リクエストされた全リソースの合計ポイント</summary>
    public int TotalPoints { get; }

    /// <summary>ロード完了したリソースの合計ポイント</summary>
    public int LoadedPoints { get; }

    /// <summary>ロード進捗（0.0～1.0）</summary>
    public float Progress { get; }

    /// <summary>
    /// リソースのロードをリクエスト
    /// Catalogへの送信はExecute()で行われる
    /// </summary>
    /// <exception cref="KeyNotFoundException">キーが存在しない</exception>
    public ResourceHandle Request(string resourceKey);

    /// <summary>
    /// リソースのロードを即座にリクエスト（依存ローダー用）
    /// Catalogへ即座に送信される
    /// </summary>
    /// <exception cref="KeyNotFoundException">キーが存在しない</exception>
    public ResourceHandle RequestImmediate(string resourceKey);

    /// <summary>ロード開始（保留中のリクエストをCatalogに送信）</summary>
    public void Execute();

    /// <summary>
    /// Tick処理。全完了でtrueを返す
    /// リソースのTickはCatalog.Tick()が担当する
    /// </summary>
    public bool Tick();

    /// <summary>全リソース解放してDispose</summary>
    public void Dispose();
}
```

### LoaderState

```csharp
public enum LoaderState : byte
{
    Idle = 0,       // Execute()前、またはDispose()後
    Loading = 1,    // ロード中
    Loaded = 2      // 全リソースロード完了
}
```

### ライフサイクル

```
┌─────────────────────────────────────────────────────────────┐
│ new Loader(catalog)                                         │
│   ↓                                                          │
│ State = Idle                                                 │
│   ↓                                                          │
│ Request("key1")  →  _pendingLoads に追加                     │
│ Request("key2")  →  _pendingLoads に追加                     │
│   ↓                                                          │
│ Execute()  →  RequestLoad を Catalog に送信, State = Loading │
│   ↓                                                          │
│ ┌─── 毎フレーム ───────────────────────────────────────────┐ │
│ │ catalog.Tick()  →  RefCount++ + Start/Tick を一括処理    │ │
│ │ loader.Tick()   →  AllLoaded を確認                       │ │
│ │   ↓                                                       │ │
│ │ 全完了 → State = Loaded, Tick() returns true              │ │
│ └──────────────────────────────────────────────────────────┘ │
│   ↓                                                          │
│ Dispose()  →  RequestUnload を Catalog に送信, State = Idle  │
│ catalog.Tick()  →  RefCount-- (0なら Unload)                 │
└─────────────────────────────────────────────────────────────┘
```

### 使用例: 基本

```csharp
var loader = new Loader(catalog);

// リクエスト
var handle1 = loader.Request("texture/player");
var handle2 = loader.Request("audio/bgm");

// ロード開始
loader.Execute();

// 毎フレーム
while (loader.State == LoaderState.Loading)
{
    catalog.Tick();  // リソースのロード処理を進める
    if (loader.Tick())
    {
        Console.WriteLine("All loaded!");
        break;
    }
    UpdateLoadingProgress(loader.LoadedCount, loader.RequestCount);
}

// 使用
if (handle1.TryGet<Texture>(out var tex)) { /* ... */ }

// 解放
loader.Dispose();
catalog.Tick();  // アンロード処理を実行
```

### 使用例: ロード中に追加

```csharp
var loader = new Loader(catalog);

loader.Request("texture/player");
loader.Execute();

// ロード中に追加リクエスト
loader.Request("texture/enemy");

// 次のloader.Tick()でリクエストがCatalogに送信され、
// その次のcatalog.Tick()でロード開始される
while (true) { catalog.Tick(); if (loader.Tick()) break; }
```

---

## ResourceHandle詳細

### API

```csharp
public readonly struct ResourceHandle
{
    /// <summary>ハンドルが有効か（Requestで取得したもの）</summary>
    public bool IsValid { get; }

    /// <summary>リソースのロード状態</summary>
    public ResourceLoadState State { get; }

    /// <summary>リソースがロード済みか</summary>
    public bool IsLoaded { get; }

    /// <summary>型安全にリソースを取得</summary>
    public bool TryGet<T>(out T? resource) where T : class;

    /// <summary>型チェックなしでリソースを取得</summary>
    public object? GetResourceUnsafe();
}
```

### 使用例

```csharp
var handle = loader.Request("texture/player");

// ロード前
Console.WriteLine(handle.IsValid);   // True
Console.WriteLine(handle.IsLoaded);  // False
Console.WriteLine(handle.State);     // Unloaded

// ロード後
loader.Execute();
while (true) { catalog.Tick(); if (loader.Tick()) break; }

Console.WriteLine(handle.IsLoaded);  // True
Console.WriteLine(handle.State);     // Loaded

// 型安全な取得
if (handle.TryGet<Texture>(out var texture))
{
    DrawSprite(texture);
}

// 型が合わない場合
if (handle.TryGet<AudioClip>(out var audio))  // False
{
    // ここには来ない
}

// 型チェックなし（注意して使用）
var resource = handle.GetResourceUnsafe();
if (resource is Texture tex)
{
    DrawSprite(tex);
}
```

### デフォルトハンドル

```csharp
var handle = default(ResourceHandle);

Console.WriteLine(handle.IsValid);   // False
Console.WriteLine(handle.IsLoaded);  // False
Console.WriteLine(handle.State);     // Unloaded
handle.TryGet<Texture>(out var tex); // False, tex = null
```

---

## 参照カウント

### 基本動作

```
操作                              | RefCount | リソース状態
----------------------------------|----------|---------------
初期状態                          | 0        | Unloaded
LoaderA.Request("tex")            | 0        | -（リクエスト登録のみ）
LoaderA.Execute()                 | 0        | -（Catalogに送信）
catalog.Tick()                    | 1        | Loading（RefCount++後にStart）
catalog.Tick() → Loaded           | 1        | Loaded
LoaderB.Request("tex")            | 1        | Loaded
LoaderB.Execute() + catalog.Tick()| 2        | Loaded (再ロードしない)
LoaderA.Dispose() + catalog.Tick()| 1        | Loaded (まだ保持)
LoaderB.Dispose() + catalog.Tick()| 0        | Unloaded (解放)
```

### 同一Loader内での重複リクエスト

同じLoaderが同じリソースを複数回リクエストしても、参照カウントは1回分のみ。

```csharp
var loader = new Loader(catalog);

loader.Request("texture/player");  // RefCount=1
loader.Request("texture/player");  // RefCount=1（変化なし）

loader.Dispose();  // RefCount=0（1回のDisposeで解放）
```

### 複数Loaderでの共有

```csharp
var catalog = new ResourceCatalog();
catalog.Register("texture/shared", new TextureResource("shared.png"));

var loaderA = new Loader(catalog);
var loaderB = new Loader(catalog);

// 両方が同じリソースをリクエスト
loaderA.Request("texture/shared");
loaderB.Request("texture/shared");

// Aをロード
loaderA.Execute();
while (true) { catalog.Tick(); if (loaderA.Tick()) break; }  // RefCount=1

// Bはリクエストした時点で既にLoaded
loaderB.Execute();
catalog.Tick();  // RefCount=2
Console.WriteLine(loaderB.Tick());  // True（即座に完了）

// Aを解放
loaderA.Dispose();
catalog.Tick();  // RefCount=1

// リソースはまだ使用可能
var handleB = loaderB.Request("texture/shared");
Console.WriteLine(handleB.IsLoaded);  // True

// Bを解放
loaderB.Dispose();
catalog.Tick();  // RefCount=0 → 実際にUnload()が呼ばれる
```

---

## 依存ロード

### 基本パターン

Tick内で依存リソース用のLoaderを作成する。
依存LoaderのTick()は呼ばない（Catalog.Tick()が一括で担当）。

```csharp
public class MaterialResource : IResource<Material>
{
    private readonly string _diffusePath;
    private readonly string _normalPath;
    private Material? _material;
    private Loader? _dependencyLoader;
    private ResourceHandle _diffuseHandle;
    private ResourceHandle _normalHandle;

    public MaterialResource(string diffuse, string normal)
    {
        _diffusePath = diffuse;
        _normalPath = normal;
    }

    public void Start() { }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        // 依存Loaderを初回のみ作成
        if (_dependencyLoader == null)
        {
            _dependencyLoader = new Loader(catalog);
            _diffuseHandle = _dependencyLoader.Request(_diffusePath);
            _normalHandle = _dependencyLoader.Request(_normalPath);
            _dependencyLoader.Execute();
            return ResourceLoadState.Loading;
        }

        // 依存のロード完了を待つ（Tick()は呼ばない、AllLoadedで確認）
        if (!_dependencyLoader.AllLoaded)
            return ResourceLoadState.Loading;

        // 依存が揃ったのでマテリアルを作成
        _diffuseHandle.TryGet<Texture>(out var diffuse);
        _normalHandle.TryGet<Texture>(out var normal);

        _material = CreateMaterial(diffuse, normal);
        return ResourceLoadState.Loaded;
    }

    public Material? GetResource() => _material;

    public void Unload()
    {
        // 依存Loaderを解放
        _dependencyLoader?.Dispose();
        _dependencyLoader = null;

        ReleaseMaterial(_material);
        _material = null;
    }
}
```

### 動的依存発見

ファイル読み込み後に依存が判明するケース。

```csharp
public class PrefabResource : IResource<Prefab>
{
    private readonly string _path;
    private Prefab? _prefab;
    private Task<PrefabData>? _loadTask;
    private Loader? _dependencyLoader;
    private PrefabData? _data;
    private Dictionary<string, ResourceHandle>? _dependencyHandles;

    public void Start()
    {
        _loadTask = LoadPrefabDataAsync(_path);
    }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        // Phase 1: ファイル読み込み
        if (_data == null)
        {
            if (!_loadTask!.IsCompleted)
                return ResourceLoadState.Loading;

            if (_loadTask.IsFaulted)
                return ResourceLoadState.Failed;

            _data = _loadTask.Result;
        }

        // Phase 2: 依存ロード
        if (_dependencyLoader == null)
        {
            _dependencyLoader = new Loader(catalog);
            _dependencyHandles = new Dictionary<string, ResourceHandle>();

            // ファイルから判明した依存をロード
            foreach (var dep in _data.Dependencies)
            {
                _dependencyHandles[dep] = _dependencyLoader.Request(dep);
            }
            _dependencyLoader.Execute();
            return ResourceLoadState.Loading;
        }

        // 依存のロード完了を待つ（Tick()は呼ばない、AllLoadedで確認）
        if (!_dependencyLoader.AllLoaded)
            return ResourceLoadState.Loading;

        // Phase 3: Prefab作成
        _prefab = CreatePrefab(_data, _dependencyHandles);
        return ResourceLoadState.Loaded;
    }

    public Prefab? GetResource() => _prefab;

    public void Unload()
    {
        _dependencyLoader?.Dispose();
        _dependencyLoader = null;
        _dependencyHandles = null;
        ReleasePrefab(_prefab);
        _prefab = null;
    }
}
```

### 循環依存の処理

参照カウントにより循環依存も安全に処理される。

```
A depends on B
B depends on A

LoaderX: Request("A")
  ├─ A.Tick() creates LoaderA
  │   └─ LoaderA.Request("B")  → RefCount(B)=1
  │       └─ B.Tick() creates LoaderB
  │           └─ LoaderB.Request("A")  → RefCount(A)=2（既にリクエスト済み）
  │               └─ A is already Loading, wait
  │
  └─ 結果: AとBは相互参照するが、参照カウントで正しく管理される
```

---

## コーナーケース

### 1. ロード中に追加リクエストを載せたケース

```csharp
loader.Request("A");
loader.Execute();           // State = Loading
loader.Request("B");        // ← ロード中に追加

// 動作:
// ・Request("B")は _pendingLoads に追加
// ・次のloader.Tick()で RequestLoad がCatalogに送信される
// ・その次のcatalog.Tick()でBの参照カウント+1
// ・BがまだUnloadedなら、同じcatalog.Tick()でStart()が呼ばれる
// ・BがLoadingまたはLoadedなら、参照カウント+1のみ
// ・LoaderのStateはLoadingのまま継続
// ・A, B両方がLoadedになるまでloader.Tick()はfalseを返す
```

### 2. ロード中にDisposeしたケース

```csharp
loader.Request("A");
loader.Execute();           // State = Loading
catalog.Tick();             // Aのロード開始
loader.Dispose();           // ← ロード中にDispose
catalog.Tick();             // ← アンロード処理を実行

// 動作:
// ・Dispose()でExecute済みリソースにRequestUnloadを送信
// ・catalog.Tick()でリクエスト済みの全リソースの参照カウント-1
// ・参照カウントが0になったリソースはUnload()を呼び、Unloaded状態に戻す
// ・LoaderのStateはIdleに戻る
// ・ロード中だったリソースはキャンセル扱い
// ・次にRequestされたら、最初からやり直し（Start()から）
```

### 3. 同じキーを複数Loaderにリクエストしたケース

```csharp
loaderA.Request("A");
loaderA.Execute();
catalog.Tick();           // RefCount=1, Aのロード開始
loaderB.Request("A");
loaderB.Execute();
catalog.Tick();           // RefCount=2

// 動作:
// ・loaderB.Execute()でRequestLoadがCatalogに送信
// ・catalog.Tick()で参照カウント+1
// ・Aが既にLoadingまたはLoadedなら、再度Start()は呼ばない
// ・loaderBからもResourceHandleが返る（同じEntryを参照）
// ・両方のハンドルから同じリソースにアクセス可能
// ・loaderA.Dispose() + catalog.Tick()でRefCount=1（リソースは解放されない）
// ・loaderB.Dispose() + catalog.Tick()でRefCount=0（リソースが実際に解放される）
```

### 4. 存在しないキーをリクエストしたケース

```csharp
loader.Request("not_exist");  // カタログに存在しないキー

// 動作:
// ・KeyNotFoundException例外をスロー
// ・ResourceHandleは返却されない
// ・Loaderの状態は変化しない
```

### 5. 同じLoaderで同じキーを複数回リクエストしたケース

```csharp
var handle1 = loader.Request("A");  // RefCount=1
var handle2 = loader.Request("A");  // RefCount=1（変化なし）

// 動作:
// ・RefCount=1（同一Loader内では重複カウントしない）
// ・Loader内部でHashSetで管理し、既にリクエスト済みならスキップ
// ・handle1とhandle2は同じEntryを参照（同一のハンドル）
// ・Dispose()でRefCount-1
```

### 6. Execute()を複数回呼んだケース

```csharp
loader.Request("A");
loader.Execute();           // AのRequestLoadをCatalogに送信
catalog.Tick();             // Aのロード開始
loader.Request("B");        // 追加リクエスト（_pendingLoadsに追加）
loader.Execute();           // ← 2回目（BのRequestLoadをCatalogに送信）
catalog.Tick();             // Bのロード開始

// 動作:
// ・Execute()は_pendingLoadsのリクエストをCatalogに送信
// ・catalog.Tick()でUnloadedのリソースのみStart()を呼ぶ
// ・既にLoading/Loadedのリソースは参照カウント+1のみ
// ・LoaderのStateがIdleならLoadingに変更、既にLoadingなら維持
```

### 7. シーン遷移で同一フレームにアンロード→ロードが発生するケース

```csharp
// シーンAがtexを使用中（RefCount=1, Loaded）
loaderA.Dispose();                    // RequestUnload("tex")
loaderB.Request("tex");
loaderB.Execute();                    // RequestLoad("tex")
catalog.Tick();                       // ← 同一フレームで処理

// 動作:
// ・catalog.Tick()でリクエストをグループ化
// ・"tex"に対して: loadCount=1, unloadCount=1
// ・netChange = 1 - 1 = 0
// ・RefCountは変更しない（元のRefCount=1のまま）
// ・Unloadしない（最適化!）
// ・entry.StateはLoaded → Startも呼ばない
// ・リソースはロード済みのまま維持される
```

この最適化により、シーン遷移時に共有リソースの無駄なアンロード→再ロードを防止できる。

---

## 実践パターン集

### シーンロード

```csharp
public class SceneLoader : IDisposable
{
    private readonly ResourceCatalog _catalog;
    private Loader? _loader;

    public SceneLoader(ResourceCatalog catalog)
    {
        _catalog = catalog;
    }

    public void StartLoad(string[] resourceKeys)
    {
        _loader?.Dispose();
        _loader = new Loader(_catalog);

        foreach (var key in resourceKeys)
        {
            _loader.Request(key);
        }
        _loader.Execute();
    }

    public bool UpdateLoad()
    {
        _catalog.Tick();  // リソースのロード処理を進める
        return _loader?.Tick() ?? true;
    }

    public float Progress => _loader?.Progress ?? 1f;

    public void Dispose()
    {
        _loader?.Dispose();
        _catalog.Tick();  // アンロード処理を実行
    }
}

// 使用
var sceneLoader = new SceneLoader(catalog);
sceneLoader.StartLoad(new[] {
    "texture/background",
    "texture/player",
    "audio/bgm"
});

// 毎フレーム
void Update()
{
    if (sceneLoader.UpdateLoad())
    {
        OnSceneReady();
    }
    else
    {
        loadingBar.Progress = sceneLoader.Progress;
    }
}
```

### プリロード

```csharp
public class PreloadManager
{
    private readonly ResourceCatalog _catalog;
    private Loader? _preloader;

    public PreloadManager(ResourceCatalog catalog)
    {
        _catalog = catalog;
    }

    public void PreloadCommon()
    {
        _preloader = new Loader(_catalog);
        _preloader.Request("texture/ui/common");
        _preloader.Request("audio/se/click");
        _preloader.Request("font/main");
        _preloader.Execute();
    }

    public void Update()
    {
        _catalog.Tick();  // リソースのロード処理を進める
        _preloader?.Tick();
    }

    // プリロードしたリソースは解放しない
    // アプリ終了時に Dispose()
}
```

### オンデマンドロード

```csharp
public class OnDemandLoader
{
    private readonly ResourceCatalog _catalog;
    private readonly Dictionary<string, ResourceHandle> _handles = new();
    private Loader? _loader;

    public OnDemandLoader(ResourceCatalog catalog)
    {
        _catalog = catalog;
    }

    public ResourceHandle GetOrLoad(string key)
    {
        if (_handles.TryGetValue(key, out var handle))
            return handle;

        _loader ??= new Loader(_catalog);

        handle = _loader.Request(key);
        _handles[key] = handle;

        // 即座にExecute
        _loader.Execute();

        return handle;
    }

    public void Update()
    {
        _catalog.Tick();  // リソースのロード処理を進める
        _loader?.Tick();
    }

    public void Dispose()
    {
        _loader?.Dispose();
        _catalog.Tick();  // アンロード処理を実行
        _handles.Clear();
    }
}

// 使用
var handle = onDemandLoader.GetOrLoad("texture/enemy_boss");
if (handle.IsLoaded)
{
    // 即座に使用可能（既にロード済みの場合）
}
else
{
    // ロード完了まで待機が必要
}
```

---

## トラブルシューティング

### リソースがロードされない

**1. カタログに登録されているか確認**
```csharp
if (!catalog.Contains("texture/player"))
{
    Console.WriteLine("Resource not registered!");
}
```

**2. Loader.Execute()を呼んでいるか確認**
```csharp
loader.Request("texture/player");
loader.Execute();  // ← これを忘れていないか
```

**3. catalog.Tick()を呼んでいるか確認**
```csharp
while (loader.State == LoaderState.Loading)
{
    catalog.Tick();  // ← 毎フレーム呼ぶ（リソースのロード処理を進める）
    loader.Tick();   // ← 毎フレーム呼ぶ（状態チェック）
}
```

**4. IResource.Tick()の戻り値を確認**
```csharp
public ResourceLoadState Tick(ResourceCatalog catalog)
{
    // ResourceLoadState.Loaded を返さないと完了しない
    return ResourceLoadState.Loaded;
}
```

### リソースが解放されない

**1. 参照カウントを確認**
- 複数のLoaderが同じリソースを参照している可能性
- 全てのLoaderでDispose()を呼んでいるか確認

**2. Unregister時の例外を確認**
```csharp
// RefCount > 0 だと InvalidOperationException
try
{
    catalog.Unregister("texture/player");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
}
```

### ハンドルからリソースが取得できない

**1. ロード完了を確認**
```csharp
if (!handle.IsLoaded)
{
    Console.WriteLine($"State: {handle.State}");  // Loading? Failed?
}
```

**2. 型が正しいか確認**
```csharp
// 登録時の型と取得時の型が一致しているか
catalog.Register("tex", new TextureResource(...));  // Texture
handle.TryGet<Texture>(out var tex);                // Texture（一致）
handle.TryGet<AudioClip>(out var audio);            // False（不一致）
```

### Dispose後のアクセス

```csharp
loader.Dispose();
loader.Request("tex");  // ObjectDisposedException

// Dispose後は新しいLoaderを作成する
loader = new Loader(catalog);
```

---

## ディレクトリ構造

```
ResourceSystem/
├── DESIGN.md                          # 本ドキュメント
├── README.md                          # クイックスタート
│
├── ResourceSystem.Core/
│   ├── ResourceSystem.Core.csproj
│   │
│   ├── Enums/
│   │   ├── ResourceLoadState.cs       # Unloaded/Loading/Loaded/Failed
│   │   └── LoaderState.cs             # Idle/Loading/Loaded
│   │
│   ├── Resources/
│   │   └── IResource.cs               # ユーザー実装インターフェース
│   │
│   ├── Catalog/
│   │   ├── ResourceCatalog.cs         # リソース登録管理
│   │   └── ResourceEntry.cs           # 参照カウント・状態管理（internal）
│   │
│   └── Loader/
│       ├── ResourceHandle.cs          # リソースハンドル（struct）
│       └── Loader.cs                  # ロード管理
│
└── ResourceSystem.Tests/
    ├── Mocks/
    │   └── MockResource.cs            # テスト用モック
    ├── Catalog/
    │   └── ResourceCatalogTests.cs
    ├── Loader/
    │   ├── ResourceHandleTests.cs
    │   └── LoaderTests.cs
    └── Integration/
        └── IntegrationTests.cs
```
