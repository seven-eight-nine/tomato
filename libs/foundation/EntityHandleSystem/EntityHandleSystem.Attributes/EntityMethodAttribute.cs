using System;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// メソッドを生成されるHandle型経由で呼び出し可能にします。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class EntityMethodAttribute : Attribute
{
    /// <summary>
    /// trueの場合、追加で _Unsafe バリアントのメソッドを生成します。
    /// </summary>
    public bool Unsafe { get; set; } = false;
}
