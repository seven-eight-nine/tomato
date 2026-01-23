using System;
using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// バリデーション結果の実装。
/// </summary>
public sealed class ValidationResult : IValidationResult
{
    private static readonly ValidationResult _success = new(true, Array.Empty<ValidationFailureReason>());

    /// <summary>バリデーションが成功したかどうか</summary>
    public bool IsValid { get; }

    /// <summary>失敗理由のリスト</summary>
    public IReadOnlyList<ValidationFailureReason> FailureReasons { get; }

    private ValidationResult(bool isValid, IReadOnlyList<ValidationFailureReason> failureReasons)
    {
        IsValid = isValid;
        FailureReasons = failureReasons;
    }

    /// <summary>成功結果を取得する</summary>
    public static ValidationResult Success() => _success;

    /// <summary>単一の失敗理由で失敗結果を作成する</summary>
    public static ValidationResult Fail(ValidationFailureReason reason) =>
        new(false, new[] { reason });

    /// <summary>複数の失敗理由で失敗結果を作成する</summary>
    public static ValidationResult Fail(IReadOnlyList<ValidationFailureReason> reasons) =>
        new(false, reasons);

    /// <summary>失敗コードで失敗結果を作成する</summary>
    public static ValidationResult Fail(ValidationFailureCode code, string? message = null) =>
        Fail(new ValidationFailureReason(code, message));

    public override string ToString() =>
        IsValid ? "ValidationResult(Valid)" : $"ValidationResult(Invalid, Reasons={FailureReasons.Count})";
}
