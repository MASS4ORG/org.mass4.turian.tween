namespace Turian.Tweening;

/// <summary>
/// How a tween's progress maps to its value: the CSS keywords, the easings.net curves, the step functions, and
/// <see cref="Bezier"/> for a custom <see cref="CubicBezier"/>.
/// </summary>
public enum Ease
{
    /// <summary>Constant speed.</summary>
    Linear,

    /// <summary>The CSS <c>ease</c> keyword.</summary>
    CssEase,

    /// <summary>The CSS <c>ease-in</c> keyword.</summary>
    CssEaseIn,

    /// <summary>The CSS <c>ease-out</c> keyword.</summary>
    CssEaseOut,

    /// <summary>The CSS <c>ease-in-out</c> keyword.</summary>
    CssEaseInOut,

    /// <summary>Jumps from start to end halfway through.</summary>
    Step,

    /// <summary>Hermite smoothstep.</summary>
    SmoothStep,

    /// <summary>Perlin's smootherstep.</summary>
    SmootherStep,

    /// <summary>Sine ease-in.</summary>
    InSine,

    /// <summary>Sine ease-out.</summary>
    OutSine,

    /// <summary>Sine ease-in-out.</summary>
    InOutSine,

    /// <summary>Quadratic ease-in.</summary>
    InQuad,

    /// <summary>Quadratic ease-out.</summary>
    OutQuad,

    /// <summary>Quadratic ease-in-out.</summary>
    InOutQuad,

    /// <summary>Cubic ease-in.</summary>
    InCubic,

    /// <summary>Cubic ease-out.</summary>
    OutCubic,

    /// <summary>Cubic ease-in-out.</summary>
    InOutCubic,

    /// <summary>Quartic ease-in.</summary>
    InQuart,

    /// <summary>Quartic ease-out.</summary>
    OutQuart,

    /// <summary>Quartic ease-in-out.</summary>
    InOutQuart,

    /// <summary>Quintic ease-in.</summary>
    InQuint,

    /// <summary>Quintic ease-out.</summary>
    OutQuint,

    /// <summary>Quintic ease-in-out.</summary>
    InOutQuint,

    /// <summary>Exponential ease-in.</summary>
    InExpo,

    /// <summary>Exponential ease-out.</summary>
    OutExpo,

    /// <summary>Exponential ease-in-out.</summary>
    InOutExpo,

    /// <summary>Circular ease-in.</summary>
    InCirc,

    /// <summary>Circular ease-out.</summary>
    OutCirc,

    /// <summary>Circular ease-in-out.</summary>
    InOutCirc,

    /// <summary>Pulls back before moving in.</summary>
    InBack,

    /// <summary>Overshoots before settling.</summary>
    OutBack,

    /// <summary>Pulls back, then overshoots.</summary>
    InOutBack,

    /// <summary>Winds up like a spring.</summary>
    InElastic,

    /// <summary>Springs past the end and oscillates into place.</summary>
    OutElastic,

    /// <summary>Springs at both ends.</summary>
    InOutElastic,

    /// <summary>Bounces off the start.</summary>
    InBounce,

    /// <summary>Bounces into the end.</summary>
    OutBounce,

    /// <summary>Bounces at both ends.</summary>
    InOutBounce,

    /// <summary>A custom curve given as a <see cref="CubicBezier"/>.</summary>
    Bezier,
}

/// <summary>A CSS <c>cubic-bezier()</c> timing curve through (0, 0) and (1, 1).</summary>
/// <param name="X1">First control point x, in [0, 1].</param>
/// <param name="Y1">First control point y.</param>
/// <param name="X2">Second control point x, in [0, 1].</param>
/// <param name="Y2">Second control point y.</param>
public readonly record struct CubicBezier(float X1, float Y1, float X2, float Y2)
{
    /// <summary>The curve's value at progress <paramref name="t"/>.</summary>
    /// <param name="t">Progress in [0, 1].</param>
    public float Evaluate(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        // x(u) is monotonic for control x in [0, 1], so bisection with a Newton start converges.
        var (low, high, u) = (0f, 1f, t);
        for (var i = 0; i < 24; i++)
        {
            var error = Coordinate(u, X1, X2) - t;
            if (MathF.Abs(error) < 1e-6f)
            {
                break;
            }

            (low, high) = error > 0f ? (low, u) : (u, high);
            u = (low + high) * 0.5f;
        }

        return Coordinate(u, Y1, Y2);
    }

    static float Coordinate(float u, float p1, float p2)
    {
        var inverse = 1f - u;
        return 3f * inverse * inverse * u * p1 + 3f * inverse * u * u * p2 + u * u * u;
    }
}
