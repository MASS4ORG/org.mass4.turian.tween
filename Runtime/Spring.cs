namespace Turian.Tweening;

/// <summary>
/// Damped spring motion toward a target that may move every frame. Each step solves the oscillator exactly, so it
/// stays stable and frame-rate independent at any time step, and a changed target blends in without a jump.
/// </summary>
public static class Spring
{
    /// <summary>Moves <paramref name="value"/> and <paramref name="velocity"/> toward <paramref name="target"/> by one step.</summary>
    /// <param name="value">The current value, updated.</param>
    /// <param name="velocity">The current speed, updated; start at 0.</param>
    /// <param name="target">The value to settle on.</param>
    /// <param name="frequency">Oscillations per second; higher is stiffer, 0 or less snaps to the target.</param>
    /// <param name="damping">0 never settles, below 1 overshoots and wobbles, 1 settles fastest without overshoot, above 1 creeps.</param>
    /// <param name="deltaTime">Seconds to advance.</param>
    public static void Step(ref float value, ref float velocity, float target, float frequency, float damping,
        float deltaTime)
    {
        if (frequency <= 0f)
        {
            (value, velocity) = (target, 0f);
            return;
        }

        var (offset, speed) = Solve(value - target, velocity, 2f * MathF.PI * frequency, MathF.Max(damping, 0f), deltaTime);
        (value, velocity) = (target + offset, speed);
    }

    /// <summary>Moves <paramref name="value"/> and <paramref name="velocity"/> toward <paramref name="target"/> by one step, per axis.</summary>
    /// <inheritdoc cref="Step(ref float, ref float, float, float, float, float)"/>
    public static void Step(ref Vector3 value, ref Vector3 velocity, Vector3 target, float frequency, float damping,
        float deltaTime)
    {
        var (x, y, z) = (value.X, value.Y, value.Z);
        var (vx, vy, vz) = (velocity.X, velocity.Y, velocity.Z);
        Step(ref x, ref vx, target.X, frequency, damping, deltaTime);
        Step(ref y, ref vy, target.Y, frequency, damping, deltaTime);
        Step(ref z, ref vz, target.Z, frequency, damping, deltaTime);
        (value, velocity) = (new Vector3(x, y, z), new Vector3(vx, vy, vz));
    }

    // The closed-form solution of x'' = -2*zeta*omega*x' - omega^2*x after dt seconds.
    static (float Offset, float Speed) Solve(float x, float v, float omega, float zeta, float dt)
    {
        var decay = MathF.Exp(-zeta * omega * dt);
        if (MathF.Abs(zeta - 1f) < 1e-4f)
        {
            var push = v + omega * x;
            return (decay * (x + push * dt), decay * (v - push * omega * dt));
        }

        var under = zeta < 1f;
        var rate = omega * MathF.Sqrt(MathF.Abs(1f - zeta * zeta));
        var b = (v + zeta * omega * x) / rate;
        var (cos, sin) = under ? (MathF.Cos(rate * dt), MathF.Sin(rate * dt)) : (MathF.Cosh(rate * dt), MathF.Sinh(rate * dt));
        var sign = under ? 1f : -1f;
        return (decay * (x * cos + b * sin),
            decay * ((b * rate - zeta * omega * x) * cos + (-sign * x * rate - zeta * omega * b) * sin));
    }
}
