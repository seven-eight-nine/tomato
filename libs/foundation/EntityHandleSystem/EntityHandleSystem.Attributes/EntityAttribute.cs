using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// クラスまたは構造体をエンティティ型としてマークし、型固有のHandleとArenaクラスを自動生成します。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class EntityAttribute : Attribute
{
    /// <summary>
    /// エンティティプールの初期容量。デフォルトは256です。
    /// </summary>
    public int InitialCapacity { get; set; } = 256;

    /// <summary>
    /// 生成されるArenaクラスのカスタム名。
    /// nullの場合、"{TypeName}Arena"が使用されます。
    /// </summary>
    public string ArenaName { get; set; } = null;

    /// <summary>
    /// trueに設定すると、エンティティのスナップショット/復元機能が自動生成されます。
    /// </summary>
    public bool Snapshotable { get; set; } = false;
}
