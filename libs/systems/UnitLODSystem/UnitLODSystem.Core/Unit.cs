using System;
using System.Collections.Generic;

namespace Tomato.UnitLODSystem
{

/// <summary>
/// ユニットのLODライフサイクルを管理するメインクラス。
/// 複数のIUnitDetailを目標レベルに応じて生成・ロード・破棄する。
/// 継承して使用する場合はCRTPパターンで自身の型を指定する。
/// </summary>
/// <typeparam name="TSelf">自身の型（CRTP）</typeparam>
public class Unit<TSelf> where TSelf : Unit<TSelf>
{
    private readonly List<DetailRegistration> _registrations = new List<DetailRegistration>();
    private readonly List<ActiveDetail> _activeDetails = new List<ActiveDetail>();
    private int _targetState;
    private UnitInternalPhase _internalPhase = UnitInternalPhase.Idle;
    private int _currentGroupRequiredAt;

    public int TargetState { get { return _targetState; } }

    public bool IsStable
    {
        get
        {
            if (_internalPhase != UnitInternalPhase.Idle)
                return false;

            foreach (var active in _activeDetails)
            {
                if (active.Instance.Phase != UnitPhase.Ready)
                    return false;
            }

            return true;
        }
    }

    public void Register<T>(int requiredAt) where T : class, IUnitDetail<TSelf>, new()
    {
        var registration = new DetailRegistration(
            typeof(T),
            requiredAt,
            () => new T()
        );
        _registrations.Add(registration);
    }

    public void RequestState(int targetState)
    {
        if (targetState < 0)
            throw new ArgumentOutOfRangeException("targetState", "Target state must be non-negative.");

        _targetState = targetState;
    }

    public void Tick()
    {
        if (_internalPhase == UnitInternalPhase.Instantiating ||
            _internalPhase == UnitInternalPhase.Loading ||
            _internalPhase == UnitInternalPhase.Creating)
        {
            TickForward();
        }
        else if (_internalPhase == UnitInternalPhase.Unloading)
        {
            TickBackward();
        }
        else if (_targetState > GetCurrentEffectiveState())
        {
            TickForward();
        }
        else if (_targetState < GetCurrentEffectiveState())
        {
            TickBackward();
        }
        else
        {
            TickActiveDetails();
        }
    }

    public T Get<T>() where T : class, IUnitDetail<TSelf>
    {
        foreach (var active in _activeDetails)
        {
            if (active.Instance is T typed && active.Instance.Phase == UnitPhase.Ready)
            {
                return typed;
            }
        }
        return null;
    }

    public event UnitPhaseChangedEventHandler UnitPhaseChanged;

    /// <summary>
    /// 自身をTSelf型として返す（コールバック呼び出し用）。
    /// </summary>
    protected TSelf Self => (TSelf)this;

    private void TickForward()
    {
        switch (_internalPhase)
        {
            case UnitInternalPhase.Idle:
                StartInstantiatingNextGroup();
                break;

            case UnitInternalPhase.Instantiating:
                TickInstantiating();
                break;

            case UnitInternalPhase.Loading:
                TickLoading();
                break;

            case UnitInternalPhase.Creating:
                TickCreating();
                break;
        }
    }

    /// <summary>
    /// 次にInstantiateすべき1グループのみをInstantiate開始
    /// </summary>
    private void StartInstantiatingNextGroup()
    {
        var nextRequiredAt = GetNextRequiredAtToInstantiate();
        if (!nextRequiredAt.HasValue)
        {
            return;
        }

        InstantiateGroup(nextRequiredAt.Value);
        _currentGroupRequiredAt = nextRequiredAt.Value;
        _internalPhase = UnitInternalPhase.Instantiating;
    }

    /// <summary>
    /// 指定されたrequiredAtのグループをInstantiate
    /// </summary>
    private void InstantiateGroup(int requiredAt)
    {
        var registrations = GetRegistrationsForRequiredAt(requiredAt);
        foreach (var reg in registrations)
        {
            var instance = reg.Factory();
            var active = new ActiveDetail(instance, reg);
            _activeDetails.Add(active);
        }
    }

    /// <summary>
    /// まだInstantiateされていない最小のrequiredAtを取得
    /// </summary>
    private int? GetNextRequiredAtToInstantiate()
    {
        int? lowest = null;
        foreach (var reg in _registrations)
        {
            if (reg.RequiredAt > _targetState)
                continue;

            var alreadyActive = false;
            foreach (var active in _activeDetails)
            {
                if (active.Registration.DetailType == reg.DetailType)
                {
                    alreadyActive = true;
                    break;
                }
            }

            if (!alreadyActive)
            {
                if (!lowest.HasValue || reg.RequiredAt < lowest.Value)
                {
                    lowest = reg.RequiredAt;
                }
            }
        }
        return lowest;
    }

    /// <summary>
    /// 指定されたrequiredAtの登録を取得
    /// </summary>
    private List<DetailRegistration> GetRegistrationsForRequiredAt(int requiredAt)
    {
        var result = new List<DetailRegistration>();
        foreach (var reg in _registrations)
        {
            if (reg.RequiredAt == requiredAt)
            {
                var alreadyActive = false;
                foreach (var active in _activeDetails)
                {
                    if (active.Registration.DetailType == reg.DetailType)
                    {
                        alreadyActive = true;
                        break;
                    }
                }

                if (!alreadyActive)
                {
                    result.Add(reg);
                }
            }
        }
        return result;
    }

    private void TickInstantiating()
    {
        // 現在のグループのNone状態のものをLoadingに遷移
        var pendingDetails = GetDetailsForRequiredAtInPhase(_currentGroupRequiredAt, UnitPhase.None);
        foreach (var active in pendingDetails)
        {
            var oldPhase = active.Instance.Phase;
            active.Instance.OnChangePhase(Self, UnitPhase.None, UnitPhase.Loading);
            CheckAndRaisePhaseChanged(active, oldPhase);
        }

        var stillNone = GetDetailsForRequiredAtInPhase(_currentGroupRequiredAt, UnitPhase.None);
        if (stillNone.Count == 0)
        {
            _internalPhase = UnitInternalPhase.Loading;
        }
    }

    private void TickLoading()
    {
        // 現在のグループのLoadingを進行
        var loadingDetails = GetDetailsForRequiredAtInPhase(_currentGroupRequiredAt, UnitPhase.Loading);
        foreach (var active in loadingDetails)
        {
            var oldPhase = active.Instance.Phase;
            active.Instance.OnUpdatePhase(Self, UnitPhase.Loading);
            CheckAndRaisePhaseChanged(active, oldPhase);
        }

        // 現在のグループが全員Loadedになったか確認
        var currentGroupDetails = GetDetailsForRequiredAt(_currentGroupRequiredAt);
        var allLoaded = true;
        foreach (var active in currentGroupDetails)
        {
            if (active.Instance.Phase < UnitPhase.Loaded)
            {
                allLoaded = false;
                break;
            }
        }

        if (allLoaded && currentGroupDetails.Count > 0)
        {
            // 次のグループがあればInstantiate + Loading開始
            var nextRequiredAt = GetNextRequiredAtToInstantiate();
            if (nextRequiredAt.HasValue && nextRequiredAt.Value <= _targetState)
            {
                InstantiateGroup(nextRequiredAt.Value);
                StartLoadingGroup(nextRequiredAt.Value);
            }

            // 現在のグループのCreatingを開始
            _internalPhase = UnitInternalPhase.Creating;
        }
    }

    /// <summary>
    /// 指定されたrequiredAtのグループのLoadingを開始
    /// </summary>
    private void StartLoadingGroup(int requiredAt)
    {
        var details = GetDetailsForRequiredAtInPhase(requiredAt, UnitPhase.None);
        foreach (var active in details)
        {
            var oldPhase = active.Instance.Phase;
            active.Instance.OnChangePhase(Self, UnitPhase.None, UnitPhase.Loading);
            CheckAndRaisePhaseChanged(active, oldPhase);
        }
    }

    private void TickCreating()
    {
        // 次のグループのLoadingも進行させる（並行）
        TickHigherGroupsLoading();

        // 現在のグループのCreatingを進行
        var currentGroupDetails = GetDetailsForRequiredAt(_currentGroupRequiredAt);

        ActiveDetail detailToProgress = null;
        foreach (var active in currentGroupDetails)
        {
            if (active.Instance.Phase == UnitPhase.Loaded ||
                active.Instance.Phase == UnitPhase.Creating)
            {
                detailToProgress = active;
                break;
            }
        }

        if (detailToProgress != null)
        {
            var oldPhase = detailToProgress.Instance.Phase;
            if (oldPhase == UnitPhase.Loaded)
            {
                detailToProgress.Instance.OnChangePhase(Self, UnitPhase.Loaded, UnitPhase.Creating);
            }
            else
            {
                detailToProgress.Instance.OnUpdatePhase(Self, UnitPhase.Creating);
            }
            CheckAndRaisePhaseChanged(detailToProgress, oldPhase);
        }

        // 現在のグループが全員Readyになったか確認
        currentGroupDetails = GetDetailsForRequiredAt(_currentGroupRequiredAt);
        var allReady = true;
        foreach (var active in currentGroupDetails)
        {
            if (active.Instance.Phase != UnitPhase.Ready)
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            var nextRequiredAt = GetNextHigherRequiredAt(_currentGroupRequiredAt);
            if (nextRequiredAt.HasValue && nextRequiredAt.Value <= _targetState)
            {
                _currentGroupRequiredAt = nextRequiredAt.Value;
                var nextGroupDetails = GetDetailsForRequiredAt(_currentGroupRequiredAt);

                // 次のグループがLoadedになっているか確認
                var nextGroupAllLoaded = true;
                foreach (var active in nextGroupDetails)
                {
                    if (active.Instance.Phase < UnitPhase.Loaded)
                    {
                        nextGroupAllLoaded = false;
                        break;
                    }
                }

                if (nextGroupAllLoaded)
                {
                    // さらに次のグループがあればInstantiate + Loading開始
                    var nextNextRequiredAt = GetNextRequiredAtToInstantiate();
                    if (nextNextRequiredAt.HasValue && nextNextRequiredAt.Value <= _targetState)
                    {
                        InstantiateGroup(nextNextRequiredAt.Value);
                        StartLoadingGroup(nextNextRequiredAt.Value);
                    }

                    _internalPhase = UnitInternalPhase.Creating;
                }
                else
                {
                    _internalPhase = UnitInternalPhase.Loading;
                }
            }
            else
            {
                _internalPhase = UnitInternalPhase.Idle;
            }
        }
    }

    /// <summary>
    /// 現在のグループより高いrequiredAtのグループのLoadingを進行
    /// </summary>
    private void TickHigherGroupsLoading()
    {
        foreach (var active in _activeDetails)
        {
            if (active.Registration.RequiredAt > _currentGroupRequiredAt &&
                active.Instance.Phase == UnitPhase.Loading)
            {
                var oldPhase = active.Instance.Phase;
                active.Instance.OnUpdatePhase(Self, UnitPhase.Loading);
                CheckAndRaisePhaseChanged(active, oldPhase);
            }
        }
    }

    private void TickBackward()
    {
        switch (_internalPhase)
        {
            case UnitInternalPhase.Idle:
                StartUnloadingHighestGroup();
                break;

            case UnitInternalPhase.Unloading:
                TickUnloading();
                break;
        }
    }

    private void StartUnloadingHighestGroup()
    {
        var highestRequiredAt = GetHighestActiveRequiredAtAboveTarget();
        if (!highestRequiredAt.HasValue)
        {
            return;
        }

        _currentGroupRequiredAt = highestRequiredAt.Value;
        var groupDetails = GetDetailsForRequiredAt(_currentGroupRequiredAt);

        foreach (var active in groupDetails)
        {
            if (active.Instance.Phase == UnitPhase.Ready ||
                active.Instance.Phase == UnitPhase.Loaded ||
                active.Instance.Phase == UnitPhase.Creating)
            {
                var oldPhase = active.Instance.Phase;
                active.Instance.OnChangePhase(Self, oldPhase, UnitPhase.Unloading);
                CheckAndRaisePhaseChanged(active, oldPhase);
            }
        }

        _internalPhase = UnitInternalPhase.Unloading;
    }

    private void TickUnloading()
    {
        var currentGroupDetails = GetDetailsForRequiredAt(_currentGroupRequiredAt);

        foreach (var active in currentGroupDetails)
        {
            if (active.Instance.Phase == UnitPhase.Unloading)
            {
                var oldPhase = active.Instance.Phase;
                active.Instance.OnUpdatePhase(Self, UnitPhase.Unloading);
                CheckAndRaisePhaseChanged(active, oldPhase);
            }
        }

        var allUnloaded = true;
        foreach (var active in currentGroupDetails)
        {
            if (active.Instance.Phase != UnitPhase.Unloaded)
            {
                allUnloaded = false;
                break;
            }
        }

        if (allUnloaded)
        {
            var toRemove = new List<ActiveDetail>(currentGroupDetails);
            foreach (var active in toRemove)
            {
                active.Instance.Dispose();
                _activeDetails.Remove(active);
            }

            var nextHighest = GetHighestActiveRequiredAtAboveTarget();
            if (nextHighest.HasValue)
            {
                _currentGroupRequiredAt = nextHighest.Value;
                var nextGroupDetails = GetDetailsForRequiredAt(_currentGroupRequiredAt);

                foreach (var active in nextGroupDetails)
                {
                    if (active.Instance.Phase == UnitPhase.Ready ||
                        active.Instance.Phase == UnitPhase.Loaded ||
                        active.Instance.Phase == UnitPhase.Creating)
                    {
                        var oldPhase = active.Instance.Phase;
                        active.Instance.OnChangePhase(Self, oldPhase, UnitPhase.Unloading);
                        CheckAndRaisePhaseChanged(active, oldPhase);
                    }
                }
            }
            else
            {
                _internalPhase = UnitInternalPhase.Idle;
            }
        }
    }

    private void TickActiveDetails()
    {
        foreach (var active in _activeDetails)
        {
            if (active.Instance.Phase != UnitPhase.Ready)
            {
                var oldPhase = active.Instance.Phase;
                active.Instance.OnUpdatePhase(Self, active.Instance.Phase);
                CheckAndRaisePhaseChanged(active, oldPhase);
            }
        }
    }

    private int GetCurrentEffectiveState()
    {
        var maxRequiredAt = 0;
        foreach (var active in _activeDetails)
        {
            if (active.Registration.RequiredAt > maxRequiredAt)
            {
                maxRequiredAt = active.Registration.RequiredAt;
            }
        }
        return maxRequiredAt;
    }

    private List<ActiveDetail> GetDetailsForRequiredAt(int requiredAt)
    {
        var result = new List<ActiveDetail>();
        foreach (var active in _activeDetails)
        {
            if (active.Registration.RequiredAt == requiredAt)
            {
                result.Add(active);
            }
        }
        return result;
    }

    private List<ActiveDetail> GetDetailsForRequiredAtInPhase(int requiredAt, UnitPhase phase)
    {
        var result = new List<ActiveDetail>();
        foreach (var active in _activeDetails)
        {
            if (active.Registration.RequiredAt == requiredAt && active.Instance.Phase == phase)
            {
                result.Add(active);
            }
        }
        return result;
    }

    private int? GetNextHigherRequiredAt(int current)
    {
        int? next = null;
        foreach (var active in _activeDetails)
        {
            if (active.Registration.RequiredAt > current)
            {
                if (!next.HasValue || active.Registration.RequiredAt < next.Value)
                {
                    next = active.Registration.RequiredAt;
                }
            }
        }
        return next;
    }

    private int? GetHighestActiveRequiredAtAboveTarget()
    {
        int? highest = null;
        foreach (var active in _activeDetails)
        {
            if (active.Registration.RequiredAt > _targetState)
            {
                if (!highest.HasValue || active.Registration.RequiredAt > highest.Value)
                {
                    highest = active.Registration.RequiredAt;
                }
            }
        }
        return highest;
    }

    private void CheckAndRaisePhaseChanged(ActiveDetail active, UnitPhase oldPhase)
    {
        if (active.Instance.Phase != oldPhase)
        {
            RaisePhaseChanged(active.Registration.DetailType, oldPhase, active.Instance.Phase);
        }
    }

    private void RaisePhaseChanged(Type detailType, UnitPhase oldPhase, UnitPhase newPhase)
    {
        var handler = UnitPhaseChanged;
        if (handler != null)
        {
            handler(this, new UnitPhaseChangedEventArgs(detailType, oldPhase, newPhase));
        }
    }

    private enum UnitInternalPhase
    {
        Idle,
        Instantiating,
        Loading,
        Creating,
        Unloading,
    }

    private class DetailRegistration
    {
        public Type DetailType { get; private set; }
        public int RequiredAt { get; private set; }
        public Func<IUnitDetail<TSelf>> Factory { get; private set; }

        public DetailRegistration(Type detailType, int requiredAt, Func<IUnitDetail<TSelf>> factory)
        {
            DetailType = detailType;
            RequiredAt = requiredAt;
            Factory = factory;
        }
    }

    private class ActiveDetail
    {
        public IUnitDetail<TSelf> Instance { get; private set; }
        public DetailRegistration Registration { get; private set; }

        public ActiveDetail(IUnitDetail<TSelf> instance, DetailRegistration registration)
        {
            Instance = instance;
            Registration = registration;
        }
    }
}

/// <summary>
/// 基本的なUnit実装。継承せずに使う場合はこれを使用。
/// </summary>
public class Unit : Unit<Unit>
{
}

}
