namespace Turian.Tests;

/// <summary>The easing curves and their names.</summary>
public sealed class EasingTests
{
    /// <summary>Every curve starts at 0 and ends at 1.</summary>
    [Fact]
    public void EveryCurveRunsFromZeroToOne()
    {
        var bezier = new CubicBezier(0.3f, 0.1f, 0.7f, 1.2f);
        foreach (var ease in Enum.GetValues<Ease>())
        {
            Assert.Equal(0f, Easing.Evaluate(ease, 0f, bezier), 3);
            Assert.Equal(1f, Easing.Evaluate(ease, 1f, bezier), 3);
            Assert.True(float.IsFinite(Easing.Evaluate(ease, 0.37f, bezier)), ease.ToString());
            Assert.True(float.IsFinite(Easing.Evaluate(ease, 0.81f, bezier)), ease.ToString());
        }
    }

    /// <summary>Known midpoints of the standard curves.</summary>
    [Theory]
    [InlineData(Ease.Linear, 0.5f, 0.5f)]
    [InlineData(Ease.InQuad, 0.5f, 0.25f)]
    [InlineData(Ease.OutQuad, 0.5f, 0.75f)]
    [InlineData(Ease.InOutQuad, 0.25f, 0.125f)]
    [InlineData(Ease.InOutQuad, 0.75f, 0.875f)]
    [InlineData(Ease.InCubic, 0.5f, 0.125f)]
    [InlineData(Ease.InOutQuint, 0.5f, 0.5f)]
    [InlineData(Ease.SmoothStep, 0.5f, 0.5f)]
    [InlineData(Ease.SmootherStep, 0.5f, 0.5f)]
    [InlineData(Ease.Step, 0.49f, 0f)]
    [InlineData(Ease.Step, 0.5f, 1f)]
    [InlineData(Ease.InOutSine, 0.5f, 0.5f)]
    [InlineData(Ease.InOutExpo, 0.5f, 0.5f)]
    [InlineData(Ease.InOutCirc, 0.5f, 0.5f)]
    [InlineData(Ease.InOutBounce, 0.5f, 0.5f)]
    [InlineData(Ease.CssEaseInOut, 0.5f, 0.5f)]
    public void StandardCurvesHitTheirKnownValues(Ease ease, float t, float expected) =>
        Assert.Equal(expected, Easing.Evaluate(ease, t), 3);

    /// <summary>Overshooting curves leave [0, 1] on the side they are known for.</summary>
    [Fact]
    public void OvershootingCurvesOvershoot()
    {
        Assert.True(Easing.Evaluate(Ease.InBack, 0.2f) < 0f);
        Assert.True(Easing.Evaluate(Ease.OutBack, 0.8f) > 1f);
        Assert.True(Easing.Evaluate(Ease.OutElastic, 0.1f) > 1f);
        Assert.True(Easing.Evaluate(Ease.InElastic, 0.9f) < 0f);
    }

    /// <summary>A Bézier curve follows its control points, and the CSS keywords are Bézier curves.</summary>
    [Fact]
    public void BezierCurvesFollowTheirControlPoints()
    {
        var linear = new CubicBezier(1f / 3f, 1f / 3f, 2f / 3f, 2f / 3f);
        Assert.Equal(0.42f, linear.Evaluate(0.42f), 3);
        Assert.True(Easing.Evaluate(Ease.CssEaseIn, 0.3f) < 0.3f);
        Assert.True(Easing.Evaluate(Ease.CssEaseOut, 0.3f) > 0.3f);
        Assert.True(Easing.Evaluate(Ease.CssEase, 0.5f) > 0.5f);
    }

    /// <summary>CSS keywords, easings.net names in any form, member names and cubic-bezier() all parse.</summary>
    [Theory]
    [InlineData("linear", Ease.Linear)]
    [InlineData("ease", Ease.CssEase)]
    [InlineData("ease-in", Ease.CssEaseIn)]
    [InlineData("ease-out", Ease.CssEaseOut)]
    [InlineData("ease-in-out", Ease.CssEaseInOut)]
    [InlineData("ease-out-back", Ease.OutBack)]
    [InlineData("EaseInSine", Ease.InSine)]
    [InlineData(" easeInOutBounce ", Ease.InOutBounce)]
    [InlineData("OutElastic", Ease.OutElastic)]
    [InlineData("smoothstep", Ease.SmoothStep)]
    public void NamesParse(string name, Ease expected)
    {
        Assert.True(Easing.TryParse(name, out var ease, out _));
        Assert.Equal(expected, ease);
    }

    /// <summary>A cubic-bezier() reads its four control values.</summary>
    [Fact]
    public void CubicBezierParses()
    {
        Assert.True(Easing.TryParse("cubic-bezier(0.1, 0.2, 0.3, 1.4)", out var ease, out var curve));
        Assert.Equal(Ease.Bezier, ease);
        Assert.Equal(new CubicBezier(0.1f, 0.2f, 0.3f, 1.4f), curve);
    }

    /// <summary>Unknown names, numbers and out-of-range control points are refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    [InlineData("3")]
    [InlineData("bezier")]
    [InlineData("cubic-bezier(1.5, 0, 0.5, 1)")]
    [InlineData("cubic-bezier(0.5, 0, 1)")]
    [InlineData("cubic-bezier(a, 0, 0.5, 1)")]
    public void UnknownNamesAreRefused(string? name)
    {
        Assert.False(Easing.TryParse(name, out var ease, out _));
        Assert.Equal(Ease.Linear, ease);
    }
}
