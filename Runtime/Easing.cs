namespace Turian.Tweening;

/// <summary>Evaluates and names the <see cref="Ease"/> curves; every curve maps 0 to 0 and 1 to 1.</summary>
public static class Easing
{
    const float BackPull = 1.70158f;
    const float ElasticPeriod = 0.3f;

    static readonly CubicBezier CssEase = new(0.25f, 0.1f, 0.25f, 1f);
    static readonly CubicBezier CssEaseIn = new(0.42f, 0f, 1f, 1f);
    static readonly CubicBezier CssEaseOut = new(0f, 0f, 0.58f, 1f);
    static readonly CubicBezier CssEaseInOut = new(0.42f, 0f, 0.58f, 1f);

    static readonly Dictionary<string, Ease> CssKeywords = new(StringComparer.Ordinal)
    {
        ["ease"] = Ease.CssEase,
        ["easein"] = Ease.CssEaseIn,
        ["easeout"] = Ease.CssEaseOut,
        ["easeinout"] = Ease.CssEaseInOut,
    };

    /// <summary>The value of <paramref name="ease"/> at progress <paramref name="t"/>.</summary>
    /// <param name="ease">The curve.</param>
    /// <param name="t">Progress in [0, 1].</param>
    /// <param name="bezier">The curve used when <paramref name="ease"/> is <see cref="Ease.Bezier"/>.</param>
    public static float Evaluate(Ease ease, float t, CubicBezier bezier = default) =>
        Evaluate(ease, t, float.NaN, float.NaN, bezier);

    /// <summary>The value of <paramref name="ease"/> at progress <paramref name="t"/>, with the shape of back and elastic curves set.</summary>
    /// <param name="ease">The curve.</param>
    /// <param name="t">Progress in [0, 1].</param>
    /// <param name="overshoot">How far a back curve pulls (default 1.70158), or an elastic curve's amplitude (default 1); NaN keeps the default.</param>
    /// <param name="period">The wavelength of an elastic curve (default 0.3); NaN keeps the default.</param>
    /// <param name="bezier">The curve used when <paramref name="ease"/> is <see cref="Ease.Bezier"/>.</param>
    public static float Evaluate(Ease ease, float t, float overshoot, float period, CubicBezier bezier = default) =>
        ease switch
        {
            Ease.Bezier => bezier.Evaluate(t),
            Ease.Custom => t,
            <= Ease.SmootherStep => Basic(ease, t),
            <= Ease.InOutSine => Sine(ease, t),
            <= Ease.InOutQuint => Polynomial(ease, t),
            <= Ease.InOutExpo => Expo(ease, t),
            <= Ease.InOutCirc => Circ(ease, t),
            _ => Physical(ease, t, overshoot, period),
        };

    static float Basic(Ease ease, float t) => ease switch
    {
        Ease.CssEase => CssEase.Evaluate(t),
        Ease.CssEaseIn => CssEaseIn.Evaluate(t),
        Ease.CssEaseOut => CssEaseOut.Evaluate(t),
        Ease.CssEaseInOut => CssEaseInOut.Evaluate(t),
        Ease.Step => t < 0.5f ? 0f : 1f,
        Ease.SmoothStep => t * t * (3f - 2f * t),
        Ease.SmootherStep => t * t * t * (t * (t * 6f - 15f) + 10f),
        _ => t,
    };

    static float Sine(Ease ease, float t) => ease switch
    {
        Ease.InSine => 1f - MathF.Cos(t * MathF.PI / 2f),
        Ease.OutSine => MathF.Sin(t * MathF.PI / 2f),
        _ => -(MathF.Cos(MathF.PI * t) - 1f) / 2f,
    };

    /// <summary>
    /// Finds a curve by a CSS keyword (<c>ease</c>, <c>ease-in</c>, <c>linear</c>), an easings.net name in any case or
    /// kebab form (<c>ease-out-back</c>, <c>EaseInSine</c>), an <see cref="Ease"/> member name, or
    /// <c>cubic-bezier(x1, y1, x2, y2)</c>; the in-game UI's style sheets use the same names.
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <param name="ease">The curve, or <see cref="Ease.Linear"/> when the name is unknown.</param>
    /// <param name="bezier">The control points when the name is a <c>cubic-bezier()</c>.</param>
    /// <returns>Whether the name was recognized.</returns>
    public static bool TryParse(string? name, out Ease ease, out CubicBezier bezier)
    {
        (ease, bezier) = (Ease.Linear, default);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var text = name.Trim();
        if (text.StartsWith("cubic-bezier(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(')'))
        {
            return TryParseBezier(text[13..^1], out ease, out bezier);
        }

        return TryParseName(text.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant(), out ease);
    }

    static bool TryParseName(string key, out Ease ease)
    {
        if (CssKeywords.TryGetValue(key, out ease))
        {
            return true;
        }

        var member = key.StartsWith("ease", StringComparison.Ordinal) ? key[4..] : key;
        if (member.Length > 0 && char.IsLetter(member[0]) && Enum.TryParse(member, ignoreCase: true, out ease)
            && ease is not (Ease.Bezier or Ease.Custom))
        {
            return true;
        }

        ease = Ease.Linear;
        return false;
    }

    static bool TryParseBezier(string arguments, out Ease ease, out CubicBezier bezier)
    {
        (ease, bezier) = (Ease.Linear, default);
        var parts = arguments.Split(',');
        Span<float> values = stackalloc float[4];
        if (parts.Length != 4)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        if (values[0] is < 0f or > 1f || values[2] is < 0f or > 1f)
        {
            return false;
        }

        (ease, bezier) = (Ease.Bezier, new CubicBezier(values[0], values[1], values[2], values[3]));
        return true;
    }

    static float Polynomial(Ease ease, float t) => ease switch
    {
        Ease.InQuad => In(t, 2),
        Ease.OutQuad => Out(t, 2),
        Ease.InOutQuad => InOut(t, 2),
        Ease.InCubic => In(t, 3),
        Ease.OutCubic => Out(t, 3),
        Ease.InOutCubic => InOut(t, 3),
        Ease.InQuart => In(t, 4),
        Ease.OutQuart => Out(t, 4),
        Ease.InOutQuart => InOut(t, 4),
        Ease.InQuint => In(t, 5),
        Ease.OutQuint => Out(t, 5),
        _ => InOut(t, 5),
    };

    static float In(float t, int power) => MathF.Pow(t, power);

    static float Out(float t, int power) => 1f - MathF.Pow(1f - t, power);

    static float InOut(float t, int power) => t < 0.5f
        ? MathF.Pow(2f, power - 1) * MathF.Pow(t, power)
        : 1f - MathF.Pow(-2f * t + 2f, power) / 2f;

    static float Expo(Ease ease, float t)
    {
        if (t <= 0f || t >= 1f)
        {
            return t <= 0f ? 0f : 1f;
        }

        return ease switch
        {
            Ease.InExpo => MathF.Pow(2f, 10f * t - 10f),
            Ease.OutExpo => 1f - MathF.Pow(2f, -10f * t),
            _ => t < 0.5f ? MathF.Pow(2f, 20f * t - 10f) / 2f : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f,
        };
    }

    static float Circ(Ease ease, float t) => ease switch
    {
        Ease.InCirc => 1f - MathF.Sqrt(1f - t * t),
        Ease.OutCirc => MathF.Sqrt(1f - (t - 1f) * (t - 1f)),
        _ => t < 0.5f
            ? (1f - MathF.Sqrt(1f - 4f * t * t)) / 2f
            : (MathF.Sqrt(1f - MathF.Pow(-2f * t + 2f, 2f)) + 1f) / 2f,
    };

    static float Physical(Ease ease, float t, float overshoot, float period) => ease switch
    {
        Ease.InBack or Ease.OutBack or Ease.InOutBack => Back(ease, t, float.IsNaN(overshoot) ? BackPull : overshoot),
        Ease.InElastic or Ease.OutElastic or Ease.InOutElastic => Elastic(ease, t, overshoot, period),
        Ease.InBounce => 1f - BounceOut(1f - t),
        Ease.OutBounce => BounceOut(t),
        _ => t < 0.5f ? (1f - BounceOut(1f - 2f * t)) / 2f : (1f + BounceOut(2f * t - 1f)) / 2f,
    };

    static float Back(Ease ease, float t, float pull)
    {
        var inOut = pull * 1.525f;
        return ease switch
        {
            Ease.InBack => (pull + 1f) * t * t * t - pull * t * t,
            Ease.OutBack => 1f + (pull + 1f) * MathF.Pow(t - 1f, 3f) + pull * MathF.Pow(t - 1f, 2f),
            _ => t < 0.5f
                ? MathF.Pow(2f * t, 2f) * ((inOut + 1f) * 2f * t - inOut) / 2f
                : (MathF.Pow(2f * t - 2f, 2f) * ((inOut + 1f) * (t * 2f - 2f) + inOut) + 2f) / 2f,
        };
    }

    static float Elastic(Ease ease, float t, float amplitude, float period)
    {
        if (t <= 0f || t >= 1f)
        {
            return t <= 0f ? 0f : 1f;
        }

        var a = float.IsNaN(amplitude) ? 1f : MathF.Max(amplitude, 1f);
        var wave = float.IsNaN(period) || period <= 0f ? ElasticPeriod : period;
        if (ease != Ease.InOutElastic)
        {
            var shift = Shift(wave, a);
            return ease == Ease.InElastic
                ? -a * MathF.Pow(2f, 10f * (t - 1f)) * MathF.Sin((t - 1f - shift) * 2f * MathF.PI / wave)
                : a * MathF.Pow(2f, -10f * t) * MathF.Sin((t - shift) * 2f * MathF.PI / wave) + 1f;
        }

        wave *= 1.5f;
        var x = 2f * t - 1f;
        var phase = (x - Shift(wave, a)) * 2f * MathF.PI / wave;
        return x < 0f
            ? -a * MathF.Pow(2f, 10f * x) * MathF.Sin(phase) / 2f
            : a * MathF.Pow(2f, -10f * x) * MathF.Sin(phase) / 2f + 1f;
    }

    // Where the wave starts, so that it crosses zero at the end of the curve for any amplitude.
    static float Shift(float wave, float amplitude) => wave / (2f * MathF.PI) * MathF.Asin(1f / amplitude);

    static float BounceOut(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1)
        {
            return n1 * t * t;
        }

        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }

        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }

        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }
}
