using System;
using System.Linq;
using System.Text;

namespace Tomato.ActionSelector;

/// <summary>
/// 選択結果のデバッグ支援。
///
/// 優先度一覧表の生成や、アクションが選択されなかった理由の説明を提供。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <remarks>
/// 開発時のデバッグ専用。本番ビルドでは使用しない。
/// </remarks>
public sealed class SelectionDebugger<TCategory> where TCategory : struct, Enum
{
    // ===========================================
    // 優先度テーブル
    // ===========================================

    /// <summary>
    /// 優先度一覧表を生成する。
    /// </summary>
    /// <param name="result">選択結果</param>
    /// <returns>フォーマットされた優先度表</returns>
    /// <remarks>
    /// 出力例:
    /// <code>
    /// Priority   | Category   | Label               | Outcome
    /// -----------|------------|---------------------|--------
    /// (0,0,0)    | FullBody   | EmergencyDodge      | * SELECTED
    /// (0,1,0)    | FullBody   | JustGuard           | CAT FULL
    /// (1,0,0)    | FullBody   | Attack1             | CAT FULL
    /// </code>
    /// </remarks>
    public string FormatPriorityTable(SelectionResult<TCategory, InputState, GameState> result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Priority   | Category   | Label               | Outcome");
        sb.AppendLine("-----------|------------|---------------------|----------");

        foreach (var eval in result.Evaluations.OrderBy(e => e.Priority))
        {
            var outcome = FormatOutcome(eval.Outcome);
            sb.AppendLine($"{eval.Priority,-10} | {eval.Category,-10} | {eval.Label,-19} | {outcome}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 簡易な優先度一覧を生成する（1行形式）。
    /// </summary>
    public string FormatCompact(SelectionResult<TCategory, InputState, GameState> result)
    {
        var winners = result.GetAllRequestedActions().Select(w => w.Label);
        return $"RequestedActions: [{string.Join(", ", winners)}]";
    }

    // ===========================================
    // 理由説明
    // ===========================================

    /// <summary>
    /// 特定のアクションが選択されなかった理由を説明する。
    /// </summary>
    /// <param name="result">選択結果</param>
    /// <param name="label">対象のラベル</param>
    /// <returns>説明文</returns>
    public string ExplainRejection(SelectionResult<TCategory, InputState, GameState> result, string label)
    {
        var eval = result.Evaluations.FirstOrDefault(e => e.Label == label);

        if (eval.Label == null)
            return $"'{label}' は評価対象に含まれていません。";

        return eval.Outcome switch
        {
            EvaluationOutcome.Selected =>
                $"'{label}' は選択されました。",

            EvaluationOutcome.Disabled =>
                $"'{label}' は GetPriority で Disabled を返しました。",

            EvaluationOutcome.InputNotFired =>
                $"'{label}' は Input が null または Input.IsTriggered が false でした。",

            EvaluationOutcome.ConditionFailed =>
                $"'{label}' は Condition.Evaluate が false でした。",

            EvaluationOutcome.CategoryOccupied =>
                $"'{label}' のカテゴリ '{eval.Category}' は既に他のアクションで埋まっていました。",

            EvaluationOutcome.ExclusivityConflict =>
                $"'{label}' の排他カテゴリが既に埋まっていました。",

            _ =>
                $"'{label}' は不明な理由で選択されませんでした: {eval.Outcome}"
        };
    }

    /// <summary>
    /// すべての拒否理由を列挙する。
    /// </summary>
    public string ExplainAllRejections(SelectionResult<TCategory, InputState, GameState> result)
    {
        var sb = new StringBuilder();

        foreach (var eval in result.Evaluations.Where(e => e.Outcome != EvaluationOutcome.Selected))
        {
            sb.AppendLine(ExplainRejection(result, eval.Label));
        }

        return sb.ToString();
    }

    // ===========================================
    // 統計
    // ===========================================

    /// <summary>
    /// 評価統計を取得する。
    /// </summary>
    public EvaluationStats GetStats(SelectionResult<TCategory, InputState, GameState> result)
    {
        var stats = new EvaluationStats();

        foreach (var eval in result.Evaluations)
        {
            stats.Total++;
            switch (eval.Outcome)
            {
                case EvaluationOutcome.Selected:
                    stats.Selected++;
                    break;
                case EvaluationOutcome.Disabled:
                    stats.Disabled++;
                    break;
                case EvaluationOutcome.InputNotFired:
                    stats.InputFailed++;
                    break;
                case EvaluationOutcome.ConditionFailed:
                    stats.ConditionFailed++;
                    break;
                case EvaluationOutcome.CategoryOccupied:
                case EvaluationOutcome.ExclusivityConflict:
                    stats.CategoryConflict++;
                    break;
            }
        }

        return stats;
    }

    // ===========================================
    // ヘルパー
    // ===========================================

    private static string FormatOutcome(EvaluationOutcome outcome)
    {
        return outcome switch
        {
            EvaluationOutcome.Selected => "* SELECTED",
            EvaluationOutcome.Disabled => "DISABLED",
            EvaluationOutcome.InputNotFired => "NO INPUT",
            EvaluationOutcome.ConditionFailed => "COND FAIL",
            EvaluationOutcome.CategoryOccupied => "CAT FULL",
            EvaluationOutcome.ExclusivityConflict => "EXCL FULL",
            _ => "?"
        };
    }
}

/// <summary>
/// 評価統計。
/// </summary>
public struct EvaluationStats
{
    /// <summary>
    /// 総評価数。
    /// </summary>
    public int Total;

    /// <summary>
    /// 選択された数。
    /// </summary>
    public int Selected;

    /// <summary>
    /// 無効化された数。
    /// </summary>
    public int Disabled;

    /// <summary>
    /// 入力失敗の数。
    /// </summary>
    public int InputFailed;

    /// <summary>
    /// 条件失敗の数。
    /// </summary>
    public int ConditionFailed;

    /// <summary>
    /// カテゴリ競合の数。
    /// </summary>
    public int CategoryConflict;

    public override string ToString()
    {
        return $"Total:{Total} Selected:{Selected} Disabled:{Disabled} " +
               $"InputFail:{InputFailed} CondFail:{ConditionFailed} CatConflict:{CategoryConflict}";
    }
}
