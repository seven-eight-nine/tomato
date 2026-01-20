using System;

namespace Tomato.CharacterSpawnSystem;

/// <summary>
/// キャラクター出現状態管理コントローラー
/// </summary>
public class CharacterSpawnController
{
    // ========================================
    // 定数
    // ========================================

    /// <summary>
    /// 1回のUpdate()で許可する最大状態遷移回数
    /// </summary>
    private const int MAX_TRANSITIONS_PER_UPDATE = 10;

    // ========================================
    // フィールド
    // ========================================

    private readonly string characterId;
    private readonly IResourceLoader resourceLoader;
    private readonly IGameObjectFactory gameObjectFactory;

    private CharacterInternalState currentState;
    private CharacterRequestState targetRequestState;

    private object dataResource;
    private object gameObjectResource;
    private IGameObjectProxy gameObjectProxy;

    // ========================================
    // イベント
    // ========================================

    /// <summary>
    /// 状態が変更されたときに発火
    /// </summary>
    public event StateChangedEventHandler StateChanged;

    // ========================================
    // プロパティ
    // ========================================

    /// <summary>
    /// 現在の内部状態（読み取り専用）
    /// </summary>
    public CharacterInternalState CurrentState
    {
        get { return currentState; }
    }

    /// <summary>
    /// 目標リクエスト状態（読み取り専用）
    /// </summary>
    public CharacterRequestState TargetRequestState
    {
        get { return targetRequestState; }
    }

    /// <summary>
    /// キャラクターID（読み取り専用）
    /// </summary>
    public string CharacterId
    {
        get { return characterId; }
    }

    /// <summary>
    /// データリソースがロード済みか
    /// </summary>
    public bool IsDataLoaded
    {
        get { return dataResource != null; }
    }

    /// <summary>
    /// ゲームオブジェクトが存在するか
    /// </summary>
    public bool HasGameObject
    {
        get { return gameObjectProxy != null; }
    }

    /// <summary>
    /// ロード済みデータリソース（読み取り専用）
    /// </summary>
    public object LoadedDataResource
    {
        get { return dataResource; }
    }

    /// <summary>
    /// 生成されたGameObjectプロキシ（読み取り専用）
    /// </summary>
    public IGameObjectProxy LoadedGameObjectProxy
    {
        get { return gameObjectProxy; }
    }

    // ========================================
    // コンストラクタ
    // ========================================

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="characterId">キャラクターID</param>
    /// <param name="resourceLoader">リソースローダー</param>
    /// <param name="gameObjectFactory">ゲームオブジェクトファクトリ</param>
    public CharacterSpawnController(
        string characterId,
        IResourceLoader resourceLoader,
        IGameObjectFactory gameObjectFactory)
    {
        if (string.IsNullOrEmpty(characterId))
            throw new ArgumentException("characterId cannot be null or empty");
        if (resourceLoader == null)
            throw new ArgumentNullException("resourceLoader");
        if (gameObjectFactory == null)
            throw new ArgumentNullException("gameObjectFactory");

        this.characterId = characterId;
        this.resourceLoader = resourceLoader;
        this.gameObjectFactory = gameObjectFactory;

        this.currentState = CharacterInternalState.NotPlaced;
        this.targetRequestState = CharacterRequestState.None;
    }

    // ========================================
    // 公開メソッド
    // ========================================

    /// <summary>
    /// 目標状態をリクエスト
    /// </summary>
    /// <param name="requestState">目標状態</param>
    public void RequestState(CharacterRequestState requestState)
    {
        if (targetRequestState == requestState)
            return;

        targetRequestState = requestState;
        ProcessStateTransitionLoop();
    }

    /// <summary>
    /// 状態遷移を進める（Update等で定期的に呼ぶ）
    /// </summary>
    public void Update()
    {
        ProcessStateTransitionLoop();
    }

    // ========================================
    // 状態遷移ループ
    // ========================================

    /// <summary>
    /// 状態が安定するまで遷移を繰り返す
    /// </summary>
    private void ProcessStateTransitionLoop()
    {
        int transitionCount = 0;
        CharacterInternalState previousState;

        do
        {
            previousState = currentState;
            ProcessStateTransition();
            transitionCount++;

            // 無限ループ防止
            if (transitionCount >= MAX_TRANSITIONS_PER_UPDATE)
            {
                OnMaxTransitionsExceeded();
                break;
            }

        } while (previousState != currentState); // 状態が変化しなくなるまで繰り返す
    }

    /// <summary>
    /// 最大遷移回数を超えた場合の処理（オーバーライド可能）
    /// </summary>
    protected virtual void OnMaxTransitionsExceeded()
    {
        // デバッグ用：実装側でログ出力などを行う
        // 例：Debug.LogError("Maximum state transitions exceeded for " + characterId);
    }

    // ========================================
    // 状態遷移処理
    // ========================================

    /// <summary>
    /// 現在の状態と目標状態に応じて遷移を実行
    /// </summary>
    private void ProcessStateTransition()
    {
        switch (currentState)
        {
            case CharacterInternalState.NotPlaced:
                ProcessFromNotPlaced();
                break;

            case CharacterInternalState.PlacedDataLoading:
                ProcessFromPlacedDataLoading();
                break;

            case CharacterInternalState.PlacedDataLoaded:
                ProcessFromPlacedDataLoaded();
                break;

            case CharacterInternalState.DataLoadFailed:
                ProcessFromDataLoadFailed();
                break;

            case CharacterInternalState.InstantiatingGOLoading:
                ProcessFromInstantiatingGOLoading();
                break;

            case CharacterInternalState.InstantiatedInactive:
                ProcessFromInstantiatedInactive();
                break;

            case CharacterInternalState.InstantiatedActive:
                ProcessFromInstantiatedActive();
                break;

            case CharacterInternalState.GameObjectLoadFailed:
                ProcessFromGameObjectLoadFailed();
                break;
        }
    }

    private void ProcessFromNotPlaced()
    {
        if (targetRequestState >= CharacterRequestState.PlacedOnly)
        {
            // データリソースのロード開始
            ChangeState(CharacterInternalState.PlacedDataLoading);
            resourceLoader.LoadDataResourceAsync(characterId, OnDataResourceLoaded);
        }
    }

    private void ProcessFromPlacedDataLoading()
    {
        if (targetRequestState == CharacterRequestState.None)
        {
            // キャンセル（ロード完了を待たずに破棄）
            ChangeState(CharacterInternalState.NotPlaced);
        }
        // ロード中は待機（コールバックで遷移）
    }

    private void ProcessFromPlacedDataLoaded()
    {
        if (targetRequestState == CharacterRequestState.None)
        {
            // データリソース解放
            UnloadDataResource();
            ChangeState(CharacterInternalState.NotPlaced);
        }
        else if (targetRequestState >= CharacterRequestState.Ready)
        {
            // ゲームオブジェクトリソースのロード開始
            ChangeState(CharacterInternalState.InstantiatingGOLoading);
            resourceLoader.LoadGameObjectResourceAsync(characterId, OnGameObjectResourceLoaded);
        }
    }

    private void ProcessFromDataLoadFailed()
    {
        if (targetRequestState == CharacterRequestState.None)
        {
            ChangeState(CharacterInternalState.NotPlaced);
        }
        else if (targetRequestState >= CharacterRequestState.PlacedOnly)
        {
            // リトライ
            ChangeState(CharacterInternalState.PlacedDataLoading);
            resourceLoader.LoadDataResourceAsync(characterId, OnDataResourceLoaded);
        }
    }

    private void ProcessFromInstantiatingGOLoading()
    {
        if (targetRequestState == CharacterRequestState.None)
        {
            // キャンセル → 完全破棄
            UnloadDataResource();
            ChangeState(CharacterInternalState.NotPlaced);
        }
        else if (targetRequestState == CharacterRequestState.PlacedOnly)
        {
            // キャンセル → データのみ保持
            ChangeState(CharacterInternalState.PlacedDataLoaded);
        }
        // ロード中は待機（コールバックで遷移）
    }

    private void ProcessFromInstantiatedInactive()
    {
        if (targetRequestState == CharacterRequestState.None)
        {
            // 完全破棄
            DestroyGameObject();
            UnloadGameObjectResource();
            UnloadDataResource();
            ChangeState(CharacterInternalState.NotPlaced);
        }
        else if (targetRequestState == CharacterRequestState.PlacedOnly)
        {
            // GameObjectのみ破棄
            DestroyGameObject();
            UnloadGameObjectResource();
            ChangeState(CharacterInternalState.PlacedDataLoaded);
        }
        else if (targetRequestState == CharacterRequestState.Active)
        {
            // アクティブ化
            if (gameObjectProxy != null)
            {
                gameObjectProxy.IsActive = true;
                ChangeState(CharacterInternalState.InstantiatedActive);
            }
        }
    }

    private void ProcessFromInstantiatedActive()
    {
        if (targetRequestState == CharacterRequestState.None)
        {
            // 完全破棄
            DestroyGameObject();
            UnloadGameObjectResource();
            UnloadDataResource();
            ChangeState(CharacterInternalState.NotPlaced);
        }
        else if (targetRequestState == CharacterRequestState.PlacedOnly)
        {
            // GameObjectのみ破棄
            DestroyGameObject();
            UnloadGameObjectResource();
            ChangeState(CharacterInternalState.PlacedDataLoaded);
        }
        else if (targetRequestState == CharacterRequestState.Ready)
        {
            // 非アクティブ化
            if (gameObjectProxy != null)
            {
                gameObjectProxy.IsActive = false;
                ChangeState(CharacterInternalState.InstantiatedInactive);
            }
        }
    }

    private void ProcessFromGameObjectLoadFailed()
    {
        if (targetRequestState == CharacterRequestState.None)
        {
            // 完全破棄
            UnloadDataResource();
            ChangeState(CharacterInternalState.NotPlaced);
        }
        else if (targetRequestState == CharacterRequestState.PlacedOnly)
        {
            // データのみ保持
            ChangeState(CharacterInternalState.PlacedDataLoaded);
        }
        else if (targetRequestState >= CharacterRequestState.Ready)
        {
            // リトライ
            ChangeState(CharacterInternalState.InstantiatingGOLoading);
            resourceLoader.LoadGameObjectResourceAsync(characterId, OnGameObjectResourceLoaded);
        }
    }

    // ========================================
    // コールバック
    // ========================================

    /// <summary>
    /// データリソースロード完了コールバック
    /// </summary>
    private void OnDataResourceLoaded(ResourceLoadResult result, object resource)
    {
        if (currentState != CharacterInternalState.PlacedDataLoading)
            return; // 既にキャンセルされている

        if (result == ResourceLoadResult.Success)
        {
            dataResource = resource;
            ChangeState(CharacterInternalState.PlacedDataLoaded);

            // コールバック後に状態遷移を継続
            // targetRequestStateが変更されている場合は次の遷移をトリガー
            ProcessStateTransitionLoop();
        }
        else
        {
            ChangeState(CharacterInternalState.DataLoadFailed);
        }
    }

    /// <summary>
    /// ゲームオブジェクトリソースロード完了コールバック
    /// </summary>
    private void OnGameObjectResourceLoaded(ResourceLoadResult result, object resource)
    {
        if (currentState != CharacterInternalState.InstantiatingGOLoading)
            return; // 既にキャンセルされている

        if (result == ResourceLoadResult.Success)
        {
            gameObjectResource = resource;
            gameObjectProxy = gameObjectFactory.CreateGameObject(gameObjectResource, dataResource);
            gameObjectProxy.IsActive = false; // 最初は非アクティブ

            ChangeState(CharacterInternalState.InstantiatedInactive);

            // コールバック後に状態遷移を継続
            // targetRequestStateが変更されている場合は次の遷移をトリガー
            ProcessStateTransitionLoop();
        }
        else
        {
            ChangeState(CharacterInternalState.GameObjectLoadFailed);
        }
    }

    // ========================================
    // リソース管理
    // ========================================

    private void UnloadDataResource()
    {
        if (dataResource != null)
        {
            resourceLoader.UnloadDataResource(dataResource);
            dataResource = null;
        }
    }

    private void UnloadGameObjectResource()
    {
        if (gameObjectResource != null)
        {
            resourceLoader.UnloadGameObjectResource(gameObjectResource);
            gameObjectResource = null;
        }
    }

    private void DestroyGameObject()
    {
        if (gameObjectProxy != null)
        {
            gameObjectProxy.Destroy();
            gameObjectProxy = null;
        }
    }

    // ========================================
    // 状態変更
    // ========================================

    private void ChangeState(CharacterInternalState newState)
    {
        if (currentState == newState)
            return;

        CharacterInternalState oldState = currentState;
        currentState = newState;

        OnStateChanged(oldState, newState);
    }

    /// <summary>
    /// 状態変更イベントの発火（オーバーライド可能）
    /// </summary>
    protected virtual void OnStateChanged(CharacterInternalState oldState, CharacterInternalState newState)
    {
        if (StateChanged != null)
        {
            StateChanged(this, new StateChangedEventArgs(oldState, newState));
        }
    }
}
