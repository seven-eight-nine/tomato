# ResourceSystem

ゲームエンジン非依存のリソースローディング抽象化ライブラリ。

## これは何？

リソース（テクスチャ、サウンド、モデル等）のロード/アンロードを管理するシステム。
参照カウントで自動解放し、複数のLoaderでリソースを安全に共有できる。

```
シーンA: texture/player をロード → RefCount=1
シーンB: texture/player をロード → RefCount=2（既にロード済みなので再ロードしない）
シーンA: アンロード → RefCount=1（シーンBが使用中なので解放されない）
シーンB: アンロード → RefCount=0（実際に解放される）
```

## なぜ使うのか

- **参照カウント管理**: 複数箇所で同じリソースを安全に共有
- **依存ロード対応**: リソースが他のリソースに依存する場合も自動解決
- **ゲームエンジン非依存**: Unity/Godot/自作エンジン問わず使用可能
- **非同期ロード対応**: 毎フレームTickで進捗管理

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

### 2. カタログにリソースを登録

```csharp
var catalog = new ResourceCatalog();
catalog.Register("texture/player", new TextureResource("player.png"));
catalog.Register("texture/enemy", new TextureResource("enemy.png"));
catalog.Register("audio/bgm", new AudioResource("bgm.ogg"));
```

### 3. Loaderでロードリクエスト

```csharp
var loader = new Loader(catalog);

var texHandle = loader.Request("texture/player");
var bgmHandle = loader.Request("audio/bgm");

loader.Execute();
```

### 4. 毎フレームTickで進捗管理

```csharp
void Update()
{
    if (loader.State == LoaderState.Loading)
    {
        if (loader.Tick())
        {
            OnLoadComplete();
        }
    }
}
```

### 5. リソースを使用

```csharp
if (texHandle.TryGet<Texture>(out var texture))
{
    DrawSprite(texture);
}
```

### 6. 解放

```csharp
loader.Dispose();
```

---

## 詳細ドキュメント

**[DESIGN.md](./DESIGN.md)** に以下が記載されている：

- 用語定義
- 設計哲学
- アーキテクチャ図
- 各コンポーネント詳細
- 依存ロードの実装方法
- コーナーケースの動作仕様
- トラブルシューティング

---

## 主要な概念

**リソースのライフサイクル**

| 状態 | 説明 |
|:----:|------|
| **Unloaded** | 未ロード。Start()待ち |
| **Loading** | ロード中。Tick()を毎Tick呼び出し |
| **Loaded** | ロード完了。GetResource()でアクセス可能 |
| **Failed** | ロード失敗。次のTickでリトライ |

**参照カウントの流れ**

| 操作 | RefCount変化 | 説明 |
|------|:------------:|------|
| `Request()` | +1 | Loaderがリソースを要求 |
| `Dispose()` | -1 | Loaderがリソースを解放 |
| RefCount=0 | → | 実際にリソースをUnload |

---

## よく使うパターン

### 複数Loaderでの共有

```csharp
// シーンAのLoader
var loaderA = new Loader(catalog);
loaderA.Request("texture/player");  // RefCount=1
loaderA.Execute();
while (!loaderA.Tick()) { }

// シーンBのLoader（シーンAがまだアクティブな状態で）
var loaderB = new Loader(catalog);
loaderB.Request("texture/player");  // RefCount=2、再ロードしない
loaderB.Execute();

// シーンAを解放
loaderA.Dispose();  // RefCount=1、まだ解放されない

// シーンBを解放
loaderB.Dispose();  // RefCount=0、実際に解放される
```

### 依存リソースのロード

```csharp
public class MaterialResource : IResource<Material>
{
    private Loader? _dependencyLoader;
    private Material? _material;

    public void Start() { }

    public ResourceLoadState Tick(ResourceCatalog catalog)
    {
        if (_dependencyLoader == null)
        {
            _dependencyLoader = new Loader(catalog);
            _dependencyLoader.Request("texture/diffuse");
            _dependencyLoader.Request("texture/normal");
            _dependencyLoader.Execute();
        }

        if (!_dependencyLoader.Tick())
            return ResourceLoadState.Loading;

        _material = CreateMaterial();
        return ResourceLoadState.Loaded;
    }

    public void Unload()
    {
        _dependencyLoader?.Dispose();
        _material = null;
    }
}
```

### ロード進捗の表示

```csharp
void UpdateLoadingUI()
{
    // ポイントベースの進捗（推奨）
    progressBar.Value = loader.Progress;

    // または個数ベースの進捗
    float countProgress = (float)loader.LoadedCount / loader.RequestCount;
}
```

### 重いリソースにポイントを設定

```csharp
public class HeavyTextureResource : IResource<Texture>
{
    // 大きなテクスチャは10ポイント
    public int Point => 10;

    // ... 他の実装
}

public class SmallConfigResource : IResource<Config>
{
    // デフォルト（1ポイント）を使用するので Point プロパティは不要
    // ... 実装
}
```

---

## デバッグ

```csharp
// Loaderの状態確認
Console.WriteLine($"State: {loader.State}");
Console.WriteLine($"Requests: {loader.RequestCount}");
Console.WriteLine($"Loaded: {loader.LoadedCount}");
Console.WriteLine($"AllLoaded: {loader.AllLoaded}");
Console.WriteLine($"TotalPoints: {loader.TotalPoints}");
Console.WriteLine($"LoadedPoints: {loader.LoadedPoints}");
Console.WriteLine($"Progress: {loader.Progress:P0}");

// ハンドルの状態確認
Console.WriteLine($"IsValid: {handle.IsValid}");
Console.WriteLine($"IsLoaded: {handle.IsLoaded}");
Console.WriteLine($"State: {handle.State}");

// カタログの確認
Console.WriteLine($"Registered: {catalog.Count}");
foreach (var key in catalog.GetAllKeys())
{
    Console.WriteLine($"  - {key}");
}
```

---

## ライセンス

MIT License
