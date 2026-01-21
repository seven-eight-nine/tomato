namespace Tomato.SystemPipeline;

/// <summary>
/// ISystem を IExecutable としてラップします。
/// SerialSystemGroup/ParallelSystemGroup 内で ISystem と ISystemGroup を統一的に扱うために使用されます。
/// </summary>
internal sealed class SystemExecutableWrapper : IExecutable
{
    private readonly ISystem _system;

    public SystemExecutableWrapper(ISystem system)
    {
        _system = system;
    }

    public bool IsEnabled
    {
        get => _system.IsEnabled;
        set => _system.IsEnabled = value;
    }

    /// <summary>
    /// 内部の ISystem を取得します。
    /// </summary>
    public ISystem System => _system;

    public void Execute(IEntityRegistry registry, in SystemContext context)
    {
        SystemExecutor.Execute(_system, registry, in context);
    }
}
