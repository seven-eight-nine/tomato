using System;
using System.Numerics;
using Xunit;
using Tomato.ActionExecutionSystem;

namespace Tomato.ActionExecutionSystem.Tests;

/// <summary>
/// MotionData テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] MotionFrameを作成できる
/// - [x] LinearMotionDataを作成できる
/// - [x] Duration=0の時はZeroフレームを返す
/// - [x] Duration中の時刻で正しく補間される
/// - [x] Duration外の時刻はクランプされる
/// - [x] ConstantMotionDataは一定値を返す
/// </summary>
public class MotionDataTests
{
    [Fact]
    public void MotionFrame_Zero_ShouldHaveDefaultValues()
    {
        var frame = MotionFrame.Zero;

        Assert.Equal(Vector3.Zero, frame.DeltaPosition);
        Assert.Equal(Quaternion.Identity, frame.DeltaRotation);
        Assert.Equal(0, frame.PoseIndex);
    }

    [Fact]
    public void MotionFrame_ShouldStoreValues()
    {
        var pos = new Vector3(1, 2, 3);
        var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
        var frame = new MotionFrame(pos, rot, 10);

        Assert.Equal(pos, frame.DeltaPosition);
        Assert.Equal(rot, frame.DeltaRotation);
        Assert.Equal(10, frame.PoseIndex);
    }

    [Fact]
    public void LinearMotionData_ShouldBeCreatable()
    {
        var startPos = Vector3.Zero;
        var endPos = new Vector3(10, 0, 0);
        var motion = new LinearMotionData(0.5f, startPos, endPos);

        Assert.NotNull(motion);
        Assert.Equal(0.5f, motion.Duration);
    }

    [Fact]
    public void LinearMotionData_Evaluate_AtStart_ShouldReturnStartPosition()
    {
        var startPos = new Vector3(0, 0, 0);
        var endPos = new Vector3(10, 0, 0);
        var motion = new LinearMotionData(1.0f, startPos, endPos);

        var frame = motion.Evaluate(0f);

        Assert.Equal(startPos, frame.DeltaPosition);
    }

    [Fact]
    public void LinearMotionData_Evaluate_AtEnd_ShouldReturnEndPosition()
    {
        var startPos = new Vector3(0, 0, 0);
        var endPos = new Vector3(10, 0, 0);
        var motion = new LinearMotionData(1.0f, startPos, endPos);

        var frame = motion.Evaluate(1.0f);

        Assert.Equal(endPos, frame.DeltaPosition);
    }

    [Fact]
    public void LinearMotionData_Evaluate_AtMiddle_ShouldInterpolate()
    {
        var startPos = new Vector3(0, 0, 0);
        var endPos = new Vector3(10, 0, 0);
        var motion = new LinearMotionData(1.0f, startPos, endPos);

        var frame = motion.Evaluate(0.5f);

        Assert.Equal(new Vector3(5, 0, 0), frame.DeltaPosition);
    }

    [Fact]
    public void LinearMotionData_Evaluate_BeyondDuration_ShouldClamp()
    {
        var startPos = new Vector3(0, 0, 0);
        var endPos = new Vector3(10, 0, 0);
        var motion = new LinearMotionData(1.0f, startPos, endPos);

        var frame = motion.Evaluate(2.0f);

        Assert.Equal(endPos, frame.DeltaPosition);
    }

    [Fact]
    public void LinearMotionData_Evaluate_NegativeTime_ShouldClampToStart()
    {
        var startPos = new Vector3(0, 0, 0);
        var endPos = new Vector3(10, 0, 0);
        var motion = new LinearMotionData(1.0f, startPos, endPos);

        var frame = motion.Evaluate(-1.0f);

        Assert.Equal(startPos, frame.DeltaPosition);
    }

    [Fact]
    public void ConstantMotionData_ShouldReturnSameValue()
    {
        var position = new Vector3(5, 5, 5);
        var motion = new ConstantMotionData(1.0f, position);

        var frame0 = motion.Evaluate(0f);
        var frame05 = motion.Evaluate(0.5f);
        var frame1 = motion.Evaluate(1.0f);

        Assert.Equal(position, frame0.DeltaPosition);
        Assert.Equal(position, frame05.DeltaPosition);
        Assert.Equal(position, frame1.DeltaPosition);
    }
}
