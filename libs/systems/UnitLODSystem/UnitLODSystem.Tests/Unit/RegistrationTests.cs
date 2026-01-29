using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.UnitLODSystem;
using Tomato.UnitLODSystem.Tests.Mocks;

namespace Tomato.UnitLODSystem.Tests.UnitTests
{

public class RegistrationTests
{
    [Fact]
    public void Register_AddsDetailRegistration()
    {
        var unit = new Unit();

        unit.Register<MockUnitDetailA>(1);
        unit.RequestState(1);
        unit.Tick();

        var detail = unit.Get<MockUnitDetailA>();
        Assert.Null(detail);
    }

    [Fact]
    public void Register_MultipleDetails_DifferentRequiredAt()
    {
        var unit = new Unit();

        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(2);

        unit.RequestState(1);

        for (int i = 0; i < 10; i++)
        {
            unit.Tick();
        }

        Assert.NotNull(unit.Get<MockUnitDetailA>());
        Assert.Null(unit.Get<MockUnitDetailB>());
    }

    [Fact]
    public void Register_MultipleDetails_SameRequiredAt()
    {
        var unit = new Unit();

        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(1);

        unit.RequestState(1);

        for (int i = 0; i < 10; i++)
        {
            unit.Tick();
        }

        Assert.NotNull(unit.Get<MockUnitDetailA>());
        Assert.NotNull(unit.Get<MockUnitDetailB>());
    }
}

}
