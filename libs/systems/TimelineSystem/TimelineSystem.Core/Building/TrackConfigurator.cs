namespace Tomato.TimelineSystem;

/// <summary>
/// トラック構築時に型安全なクリップ追加を提供するコンフィギュレーター
/// </summary>
public sealed class TrackConfigurator<T> where T : Track
{
    private readonly T _track;

    public TrackConfigurator(T track) => _track = track;

    /// <summary>
    /// トラックにクリップを追加する。Clip&lt;T&gt;のみ受け付ける。
    /// </summary>
    public TrackConfigurator<T> AddClip(Clip<T> clip)
    {
        _track.AddClip(clip);
        return this;
    }

    /// <summary>
    /// 内部のトラックインスタンスを取得する。
    /// </summary>
    public T Track => _track;
}
