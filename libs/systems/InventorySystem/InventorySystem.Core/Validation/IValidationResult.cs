using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// バリデーション結果のインターフェース。
/// </summary>
public interface IValidationResult
{
    /// <summary>バリデーションが成功したかどうか</summary>
    bool IsValid { get; }

    /// <summary>失敗理由のリスト</summary>
    IReadOnlyList<ValidationFailureReason> FailureReasons { get; }
}
