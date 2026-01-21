using System;

namespace Tomato.HandleSystem;

/// <summary>
/// クラスまたは構造体をハンドル管理可能な型としてマークし、
/// 型固有のHandleとArenaクラスを自動生成します。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class HandleableAttribute : Attribute
{
    /// <summary>
    /// オブジェクトプールの初期容量。デフォルトは256です。
    /// </summary>
    public int InitialCapacity { get; set; } = 256;

    /// <summary>
    /// 生成されるArenaクラスのカスタム名。
    /// nullの場合、"{TypeName}Arena"が使用されます。
    /// </summary>
    public string ArenaName { get; set; } = null;
}
