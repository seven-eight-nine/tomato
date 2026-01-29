using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.UnitLODSystem;
using Tomato.UnitLODSystem.Tests.Mocks;

namespace Tomato.UnitLODSystem.Tests.UnitTests
{

public class GetTests
{
    [Fact]
    public void Get_ReturnsNull_WhenNotRegistered()
    {
        var unit = new Unit();

        var result = unit.Get<MockUnitDetailA>();

        Assert.Null(result);
    }

    [Fact]
    public void Get_ReturnsNull_WhenNotReady()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        unit.RequestState(1);
        unit.Tick();

        var result = unit.Get<MockUnitDetailA>();

        Assert.Null(result);
    }

    [Fact]
    public void Get_ReturnsDetail_WhenReady()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        unit.RequestState(1);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        var result = unit.Get<MockUnitDetailA>();

        Assert.NotNull(result);
        Assert.IsType<MockUnitDetailA>(result);
    }

    [Fact]
    public void Get_ReturnsNull_AfterUnload()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        unit.RequestState(1);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.NotNull(unit.Get<MockUnitDetailA>());

        unit.RequestState(0);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.Null(unit.Get<MockUnitDetailA>());
    }
}

}
