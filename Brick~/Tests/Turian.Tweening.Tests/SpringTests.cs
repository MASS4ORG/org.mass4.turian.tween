namespace Turian.Tests;

/// <summary>Damped spring steps: settling, overshoot, frame-rate independence and snapping.</summary>
public sealed class SpringTests
{
    static (float Peak, float Final) Run(float frequency, float damping, int steps, float dt)
    {
        var (value, velocity, peak) = (0f, 0f, 0f);
        for (var i = 0; i < steps; i++)
        {
            Spring.Step(ref value, ref velocity, 10f, frequency, damping, dt);
            peak = MathF.Max(peak, value);
        }

        return (peak, value);
    }

    /// <summary>A critically damped or overdamped spring settles on its target without overshoot.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    public void DampedSpringsSettleWithoutOvershoot(float damping)
    {
        var (peak, final) = Run(2f, damping, 600, 1f / 60f);

        Assert.True(peak <= 10f + 1e-3f, $"Overshot to {peak}.");
        Assert.Equal(10f, final, 2);
    }

    /// <summary>An underdamped spring overshoots, then settles.</summary>
    [Fact]
    public void UnderdampedSpringsOvershoot()
    {
        var (peak, final) = Run(2f, 0.2f, 1200, 1f / 60f);

        Assert.True(peak > 10.5f);
        Assert.Equal(10f, final, 2);
    }

    /// <summary>One long step matches many short ones.</summary>
    [Theory]
    [InlineData(0.3f)]
    [InlineData(1f)]
    [InlineData(4f)]
    public void StepsAreFrameRateIndependent(float damping)
    {
        var coarse = Run(1.5f, damping, 1, 1f);
        var fine = Run(1.5f, damping, 1000, 0.001f);

        Assert.Equal(fine.Final, coarse.Final, 2);
    }

    /// <summary>No frequency snaps to the target, and vectors spring per axis.</summary>
    [Fact]
    public void SnapAndVectorSprings()
    {
        var (value, velocity) = (3f, 9f);
        Spring.Step(ref value, ref velocity, 7f, 0f, 1f, 0.1f);
        Assert.Equal((7f, 0f), (value, velocity));

        var (position, speed) = (Vector3.Zero, Vector3.Zero);
        for (var i = 0; i < 300; i++)
        {
            Spring.Step(ref position, ref speed, new Vector3(1, -2, 3), 3f, 1f, 1f / 60f);
        }

        Assert.True(Vector3.Distance(new Vector3(1, -2, 3), position) < 1e-3f);
    }
}
