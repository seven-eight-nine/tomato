using Xunit;

namespace Tomato.FlowTree.Tests;

public class ScopedBlackboardTests
{
    [Fact]
    public void ScopedBlackboard_ReadFromParent()
    {
        var parent = new Blackboard();
        var key = new BlackboardKey<int>(1);
        parent.SetInt(key, 42);

        var scoped = new ScopedBlackboard(parent);

        Assert.Equal(42, scoped.GetInt(key));
    }

    [Fact]
    public void ScopedBlackboard_LocalOverride()
    {
        var parent = new Blackboard();
        var key = new BlackboardKey<int>(1);
        parent.SetInt(key, 42);

        var scoped = new ScopedBlackboard(parent);
        scoped.SetIntLocal(key, 100);

        // ローカル値が優先される
        Assert.Equal(100, scoped.GetInt(key));

        // 親は変更されない
        Assert.Equal(42, parent.GetInt(key));
    }

    [Fact]
    public void ScopedBlackboard_GlobalWrite()
    {
        var parent = new Blackboard();
        var key = new BlackboardKey<int>(1);

        var scoped = new ScopedBlackboard(parent);
        scoped.SetIntGlobal(key, 100);

        // 親に書き込まれる
        Assert.Equal(100, parent.GetInt(key));
        Assert.Equal(100, scoped.GetInt(key));
    }

    [Fact]
    public void ScopedBlackboard_ClearLocal()
    {
        var parent = new Blackboard();
        var key = new BlackboardKey<int>(1);
        parent.SetInt(key, 42);

        var scoped = new ScopedBlackboard(parent);
        scoped.SetIntLocal(key, 100);

        Assert.Equal(100, scoped.GetInt(key));

        scoped.ClearLocal();

        // ローカルがクリアされ、親の値が見える
        Assert.Equal(42, scoped.GetInt(key));
    }

    [Fact]
    public void ScopedBlackboard_FloatValues()
    {
        var parent = new Blackboard();
        var key = new BlackboardKey<float>(1);
        parent.SetFloat(key, 1.5f);

        var scoped = new ScopedBlackboard(parent);

        Assert.Equal(1.5f, scoped.GetFloat(key));

        scoped.SetFloatLocal(key, 3.14f);
        Assert.Equal(3.14f, scoped.GetFloat(key));
        Assert.Equal(1.5f, parent.GetFloat(key));
    }

    [Fact]
    public void ScopedBlackboard_BoolValues()
    {
        var parent = new Blackboard();
        var key = new BlackboardKey<bool>(1);
        parent.SetBool(key, false);

        var scoped = new ScopedBlackboard(parent);

        Assert.False(scoped.GetBool(key));

        scoped.SetBoolLocal(key, true);
        Assert.True(scoped.GetBool(key));
        Assert.False(parent.GetBool(key));
    }

    [Fact]
    public void ScopedBlackboard_DisabledLocalStorage()
    {
        var parent = new Blackboard();
        var key = new BlackboardKey<int>(1);
        parent.SetInt(key, 42);

        var scoped = new ScopedBlackboard(parent, enableLocalStorage: false);

        // ローカル書き込みは無視される
        scoped.SetIntLocal(key, 100);
        Assert.Equal(42, scoped.GetInt(key));

        // グローバル書き込みは機能する
        scoped.SetIntGlobal(key, 200);
        Assert.Equal(200, scoped.GetInt(key));
    }
}
