using System;
using System.Collections.Generic;

namespace Tomato.CombatSystem.Tests.Mocks;

/// <summary>
/// テスト用のIDamageReceiver実装。
/// </summary>
public class MockDamageReceiver : IDamageReceiver
{
    private readonly HitHistory _hitHistory;
    private readonly List<DamageInfo> _receivedDamages = new();

    public string Name { get; }
    public int Health { get; set; } = 100;
    public bool IsDead => Health <= 0;

    public IReadOnlyList<DamageInfo> ReceivedDamages => _receivedDamages;
    public int DamageReceivedCount => _receivedDamages.Count;

    public Func<DamageInfo, DamageResult>? OnDamageCallback { get; set; }

    public MockDamageReceiver(string name, HitHistory? hitHistory = null)
    {
        Name = name;
        _hitHistory = hitHistory ?? new HitHistory();
    }

    public DamageResult OnDamage(DamageInfo damageInfo)
    {
        _receivedDamages.Add(damageInfo);

        if (OnDamageCallback != null)
            return OnDamageCallback(damageInfo);

        // デフォルト: 10ダメージ
        const int damage = 10;
        Health -= damage;

        return new DamageResult
        {
            Applied = true,
            ActualDamage = damage,
            Killed = IsDead,
            Blocked = false
        };
    }

    public HitHistory GetHitHistory() => _hitHistory;

    public bool Equals(IDamageReceiver? other)
    {
        return other is MockDamageReceiver mock && Name == mock.Name;
    }

    public override bool Equals(object? obj) => Equals(obj as IDamageReceiver);

    public override int GetHashCode() => Name.GetHashCode();

    public override string ToString() => $"MockDamageReceiver({Name})";
}
