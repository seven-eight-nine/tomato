namespace Tomato.CommandGenerator;

/// <summary>
/// コマンドごとのプール設定（Source Generatorが各コマンドに対して生成）
/// </summary>
/// <typeparam name="T">コマンド型</typeparam>
public static class CommandPoolConfig<T> where T : class
{
    /// <summary>
    /// プールの初期容量
    /// </summary>
    public static int InitialCapacity { get; set; } = 8;
}
