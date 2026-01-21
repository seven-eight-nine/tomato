using System;
using System.Collections.Generic;

namespace Tomato.CombatSystem.Tests.Mocks;

/// <summary>
/// テスト用のAttackInfo実装。
/// </summary>
public class MockAttackInfo : AttackInfo
{
    private readonly Func<IDamageReceiver, bool>? _canTargetFunc;
    private readonly HashSet<IDamageReceiver>? _excludedTargets;

    public MockAttackInfo()
    {
    }

    public MockAttackInfo(Func<IDamageReceiver, bool> canTargetFunc)
    {
        _canTargetFunc = canTargetFunc;
    }

    public MockAttackInfo(HashSet<IDamageReceiver> excludedTargets)
    {
        _excludedTargets = excludedTargets;
    }

    public override bool CanTarget(IDamageReceiver target)
    {
        if (_canTargetFunc != null)
            return _canTargetFunc(target);

        if (_excludedTargets != null)
            return !_excludedTargets.Contains(target);

        // デフォルト: 自分自身以外は攻撃可能
        return target != Attacker;
    }
}
