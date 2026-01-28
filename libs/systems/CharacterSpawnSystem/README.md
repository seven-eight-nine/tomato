# CharacterSpawnSystem - キャラクター状態管理

段階的なリソース管理とキャラクターの状態遷移を提供するシステムです。

## ドキュメント

- **[テストコード](./CharacterSpawnSystem.Tests/)** - 実践的なサンプル

## 概要

CharacterSpawnSystemは、キャラクターのデータロード、GameObjectの生成、アクティブ化を段階的に管理するシステムです。

## 特徴

- **段階的リソース管理** - データとGameObjectを分離してロード
- **状態遷移** - PlacedOnly → Ready → Active
- **非同期ロード対応** - リソースの非同期読み込みをサポート
- **イベント通知** - 状態変化を監視可能

## 🚀 クイックスタート

### 状態の種類

| 状態 | 説明 |
|------|------|
| `None` | 配置なし |
| `PlacedOnly` | データのみ存在（GOなし） |
| `Ready` | GO生成済み・非アクティブ |
| `Active` | GO生成済み・アクティブ |

### 基本的な使い方

```csharp
using CharacterSpawnSystem.Core;

// コントローラーを作成
var controller = new CharacterSpawnController(
    "character_001",
    resourceLoader,
    gameObjectFactory
);

// イベントを購読
controller.StateChanged += (sender, e) =>
{
    Console.WriteLine($"状態変化: {e.OldState} → {e.NewState}");
};

// 段階的に状態を変更
controller.RequestState(CharacterRequestState.PlacedOnly);  // データロード
await WaitForStateAsync(CharacterInternalState.PlacedDataLoaded);

controller.RequestState(CharacterRequestState.Ready);       // GO生成
await WaitForStateAsync(CharacterInternalState.InstantiatedInactive);

controller.RequestState(CharacterRequestState.Active);      // アクティブ化
```

## 📖 詳細ガイド

### リソースローダーの実装

```csharp
public class MyResourceLoader : IResourceLoader
{
    public async Task<ResourceLoadResult> LoadDataResourceAsync(string id)
    {
        // キャラクターデータをロード
        var data = await LoadCharacterDataAsync(id);
        return ResourceLoadResult.Success;
    }

    public async Task<ResourceLoadResult> LoadGameObjectResourceAsync(string id)
    {
        // プレハブをロード
        var prefab = await LoadPrefabAsync(id);
        return ResourceLoadResult.Success;
    }
}
```

### GameObjectファクトリーの実装

```csharp
public class MyGameObjectFactory : IGameObjectFactory
{
    public GameObject CreateGameObject(string id)
    {
        // GameObjectを生成
        var go = Instantiate(prefab);
        go.SetActive(false);  // 初期は非アクティブ
        return go;
    }

    public void DestroyGameObject(GameObject go)
    {
        Destroy(go);
    }

    public void SetActive(GameObject go, bool active)
    {
        go.SetActive(active);
    }
}
```

## 📋 状態遷移図

```
None
  ↓ RequestState(PlacedOnly)
PlacedDataLoading
  ↓ (ロード完了)
PlacedDataLoaded
  ↓ RequestState(Ready)
InstantiatingGOLoading
  ↓ (ロード完了)
InstantiatedInactive
  ↓ RequestState(Active)
InstantiatedActive
  ↓ RequestState(None)
None
```

## 💡 ベストプラクティス

### ✅ 推奨

```csharp
// ✅ 段階的にロード
controller.RequestState(CharacterRequestState.PlacedOnly);
await WaitForLoad();
controller.RequestState(CharacterRequestState.Ready);
await WaitForLoad();
controller.RequestState(CharacterRequestState.Active);

// ✅ エラーハンドリング
controller.StateChanged += (s, e) =>
{
    if (e.NewState == CharacterInternalState.DataLoadFailed)
    {
        HandleLoadError();
    }
};
```

## 📄 ライセンス

MIT License
