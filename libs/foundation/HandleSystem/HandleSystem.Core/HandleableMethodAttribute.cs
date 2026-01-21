using System;

namespace Tomato.HandleSystem;

/// <summary>
/// メソッドを生成されるHandle型経由で呼び出し可能にします。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HandleableMethodAttribute : Attribute
{
    /// <summary>
    /// trueの場合、追加で _Unsafe バリアントのメソッドを生成します。
    /// </summary>
    public bool Unsafe { get; set; } = false;
}
