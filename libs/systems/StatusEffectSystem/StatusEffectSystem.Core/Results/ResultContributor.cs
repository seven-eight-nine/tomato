namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 効果結果に寄与するデリゲート
    /// </summary>
    /// <typeparam name="TResult">結果の型</typeparam>
    /// <param name="result">変更対象の結果</param>
    /// <param name="stacks">現在のスタック数</param>
    public delegate void ResultContributor<TResult>(ref TResult result, int stacks) where TResult : struct;
}
