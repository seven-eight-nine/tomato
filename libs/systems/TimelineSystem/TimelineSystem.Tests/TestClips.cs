namespace Tomato.TimelineSystem.Tests;

/// <summary>
/// テスト用トラック
/// </summary>
public class TestTrack : Track
{
}

/// <summary>
/// テスト用Instantクリップ（TestTrack専用）
/// </summary>
public class TestInstantClip : Clip<TestTrack>
{
    private static int _nextId = 1;

    public override ClipType Type => ClipType.Instant;
    public string Name { get; }

    public TestInstantClip(string name, int frame)
        : base(new ClipId(_nextId++), frame, frame)
    {
        Name = name;
    }
}

/// <summary>
/// テスト用Rangeクリップ（TestTrack専用）
/// </summary>
public class TestRangeClip : Clip<TestTrack>
{
    private static int _nextId = 100;

    public override ClipType Type => ClipType.Range;
    public string Name { get; }

    public TestRangeClip(string name, int start, int end)
        : base(new ClipId(_nextId++), start, end)
    {
        Name = name;
    }
}
