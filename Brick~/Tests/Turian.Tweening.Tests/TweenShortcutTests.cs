namespace Turian.Tests;

/// <summary>Value, rotate-by, jump, path and blendable tweens, easing shape, rotation and scale shake, and slow motion.</summary>
[Collection(SerialTests.Name)]
public sealed class TweenShortcutTests : IDisposable
{
    readonly TweenGame game = new();

    /// <inheritdoc />
    public void Dispose() => game.Dispose();

    static Quaternion Yaw(float degrees) => Quaternion.CreateFromYawPitchRoll(degrees * MathF.PI / 180f, 0f, 0f);

    static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"Expected {expected}, got {actual}.");

    /// <summary>Value tweens hand a float or a vector from start to end to a setter.</summary>
    [Fact]
    public void ValueTweensHandOverEachValue()
    {
        var (number, vector) = (0f, Vector3.Zero);
        game.Runner.Value(2f, 6f, 1f, v => number = v);
        game.Runner.Value(Vector3.Zero, new Vector3(4), 1f, v => vector = v);

        game.Frames(2);
        Assert.Equal(4f, number, 3);
        Assert.Equal(new Vector3(2), vector);
        game.Frames(2);
        Assert.Equal((6f, new Vector3(4)), (number, vector));
        Assert.Throws<ArgumentNullException>(() => game.Runner.Value(0f, 1f, 1f, null!));
    }

    /// <summary>Rotate-by turns further than a half turn, which a shortest-arc rotation cannot.</summary>
    [Fact]
    public void RotateByGoesBeyondAHalfTurn()
    {
        var node = game.Spawn();
        node.RotateBy(new Vector3(0, 360, 0), 1f);

        game.Frames(1);
        Assert.True(MathF.Abs(Quaternion.Dot(Yaw(90), node.Orientation)) > 0.9999f);
        game.Frames(2);
        Assert.True(MathF.Abs(Quaternion.Dot(Yaw(270), node.Orientation)) > 0.9999f);
        game.Frames(1);
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.Identity, node.Orientation)) > 0.9999f);
    }

    /// <summary>A jump arcs between its start and end and lands exactly.</summary>
    [Fact]
    public void JumpsArcAndLand()
    {
        var node = game.Spawn();
        var tween = node.JumpTo(new Vector3(4, 0, 0), 2f, 2, 1f);

        game.Frames(1);
        AssertNear(new Vector3(1, 2, 0), node.Position);
        game.Frames(1);
        AssertNear(new Vector3(2, 0, 0), node.Position);
        game.Frames(2);
        Assert.Equal(new Vector3(4, 0, 0), node.Position);
        Assert.False(tween.IsActive);
        Assert.Throws<ArgumentOutOfRangeException>(() => node.JumpTo(Vector3.One, 1f, 0, 1f));
    }

    /// <summary>A path starts where the node is, through its waypoints, in straight lines or a smooth curve.</summary>
    [Fact]
    public void PathsVisitTheirWaypoints()
    {
        var linear = game.Spawn();
        var curved = game.Spawn();
        var looped = game.Spawn();
        var waypoints = new[] { new Vector3(2, 0, 0), new Vector3(2, 2, 0) };
        linear.MovePath(waypoints, 1f);
        curved.MovePath(waypoints, 1f, PathType.CatmullRom);
        looped.MovePath(waypoints, 1f, closed: true);

        game.Frames(1);
        AssertNear(new Vector3(1, 0, 0), linear.Position);
        game.Frames(1);
        AssertNear(new Vector3(2, 0, 0), curved.Position);
        game.Frames(1);
        AssertNear(new Vector3(2, 1, 0), linear.Position);
        game.Frames(1);
        AssertNear(new Vector3(2, 2, 0), linear.Position);
        AssertNear(new Vector3(2, 2, 0), curved.Position);
        AssertNear(Vector3.Zero, looped.Position);
        Assert.Throws<ArgumentException>(() => linear.MovePath([], 1f));
    }

    /// <summary>Blendable tweens add to each other and to other movement, and what they moved stays.</summary>
    [Fact]
    public void BlendableTweensAddUp()
    {
        var node = game.Spawn();
        node.MoveBlend(new Vector3(2, 0, 0), 1f);
        node.MoveBlend(new Vector3(0, 3, 0), 1f).SetEase(Ease.InQuad);
        node.ScaleBlend(Vector3.One, 1f);
        node.RotateBlend(new Vector3(0, 90, 0), 1f);

        game.Frames(2);
        node.Position += new Vector3(0, 0, 5);
        game.Frames(2);

        AssertNear(new Vector3(2, 3, 5), node.Position);
        AssertNear(new Vector3(2), node.Scale);
        Assert.True(MathF.Abs(Quaternion.Dot(Yaw(90), node.Orientation)) > 0.9999f);
    }

    /// <summary>A custom function shapes progress, and back and elastic curves take their parameters.</summary>
    [Fact]
    public void CustomEasesAndParameters()
    {
        var value = 0f;
        game.Runner.To(() => value, v => value = v, 10f, 1f).SetEase(static t => t * t);
        game.Frames(2);
        Assert.Equal(2.5f, value, 3);
        Assert.Throws<ArgumentNullException>(() => game.Runner.Delay(1f).SetEase((Func<float, float>)null!));

        Assert.Equal(Easing.Evaluate(Ease.OutBack, 0.5f), Easing.Evaluate(Ease.OutBack, 0.5f, 1.70158f, float.NaN), 5);
        Assert.Equal(0.875f, Easing.Evaluate(Ease.OutBack, 0.5f, 0f, float.NaN), 5);
        Assert.Equal(1.015625f, Easing.Evaluate(Ease.OutElastic, 0.5f), 4);
        Assert.Equal(0.011969f, Easing.Evaluate(Ease.InOutElastic, 0.25f), 4);
        Assert.NotEqual(Easing.Evaluate(Ease.OutElastic, 0.3f), Easing.Evaluate(Ease.OutElastic, 0.3f, 2f, 0.5f));
        foreach (var ease in new[] { Ease.InElastic, Ease.OutElastic, Ease.InOutElastic, Ease.InBack, Ease.OutBack })
        {
            Assert.Equal(0f, Easing.Evaluate(ease, 0f, 2f, 0.5f));
            Assert.Equal(1f, Easing.Evaluate(ease, 1f, 2f, 0.5f));
        }

        var shaped = 0f;
        var tween = game.Runner.To(() => shaped, v => shaped = v, 1f, 1f).SetEase(Ease.OutBack)
            .SetEaseOvershoot(4f).SetEasePeriod(0.2f);
        game.Frames(2);
        Assert.Equal(Easing.Evaluate(Ease.OutBack, 0.5f, 4f, 0.2f), shaped, 4);
        tween.Kill();
        Assert.False(Easing.TryParse("custom", out _, out _));
    }

    /// <summary>Rotation and scale shakes move the node, replay by seed and leave no offset.</summary>
    [Fact]
    public void RotationAndScaleShakes()
    {
        var node = game.Spawn();
        node.ShakeRotation(1f, new Vector3(0, 0, 45), seed: 2);
        node.ShakeScale(1f, new Vector3(0.5f), seed: 2);

        game.Frames(1, 0.3);
        Assert.NotEqual(Quaternion.Identity, node.Orientation);
        Assert.NotEqual(Vector3.One, node.Scale);

        game.Frames(4);
        Assert.True(Quaternion.Dot(Quaternion.Identity, node.Orientation) > 0.9999f);
        AssertNear(Vector3.One, node.Scale);
    }

    /// <summary>A recovering hit-stop eases the time scale back instead of snapping it.</summary>
    [Fact]
    public void SlowMotionEasesBack()
    {
        game.Runner.HitStop(0.5f, 0.2f, 1f);
        Assert.Equal(0.2d, game.Clock.TimeScale, 3);

        game.Frames(2);
        Assert.Equal(0.2d, game.Clock.TimeScale, 3);
        game.Frames(2);
        Assert.Equal(0.6d, game.Clock.TimeScale, 3);
        Assert.True(game.Runner.IsHitStopping);

        game.Frames(2);
        Assert.Equal(1d, game.Clock.TimeScale);
        Assert.False(game.Runner.IsHitStopping);
    }

    /// <summary>Every kind of node tween allocates nothing per frame once running.</summary>
    [Fact]
    public void EveryNodeTweenKindAllocatesNothingPerFrame()
    {
        var waypoints = new[] { new Vector3(5, 0, 0), new Vector3(5, 5, 0), new Vector3(0, 5, 0) };
        var updates = 0;
        foreach (var node in Enumerable.Range(0, 20).Select(_ => game.Spawn()))
        {
            node.RotateBy(new Vector3(0, 720, 0), 100f).SetLoops(-1, LoopType.Yoyo).OnUpdate(() => updates++);
            node.JumpTo(new Vector3(9, 0, 0), 2f, 3, 100f).SetLoops(-1, LoopType.Incremental);
            node.MovePath(waypoints, 100f, PathType.CatmullRom, closed: true).SetLoops(-1);
            node.MoveBlend(Vector3.One, 100f).SetLoops(-1, LoopType.Yoyo).SetEase(static t => t * t);
            node.ScaleBlend(new Vector3(0.1f), 100f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.OutElastic);
            node.RotateBlend(new Vector3(0, 10, 0), 100f).SetLoops(-1, LoopType.Yoyo);
            node.ShakeRotation(100f, Vector3.One).SetLoops(-1);
            node.ShakeScale(100f, new Vector3(0.1f)).SetLoops(-1);
        }

        var runner = game.Runner;
        for (var i = 0; i < 5; i++)
        {
            runner.Tick(0.016f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            runner.Tick(0.016f);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(160, runner.ActiveCount);
        Assert.True(updates > 0);
    }

    /// <summary>Light and camera tweens drive their component and end with its node.</summary>
    [Fact]
    public void LightAndCameraTweens()
    {
        var node = game.Spawn();
        var (light, camera) = (new LightComponent(), new CameraComponent { FieldOfViewDegrees = 60f });
        node.AddComponent(light);
        node.AddComponent(camera);

        var fade = light.IntensityTo(5f, 1f);
        light.ColorTo(new Vector4(1, 0, 0, 1), 1f);
        camera.FieldOfViewTo(90f, 1f);
        game.Frames(2);
        Assert.Equal(3f, light.Intensity, 3);
        Assert.Equal(75f, camera.FieldOfViewDegrees, 3);

        game.Frames(2);
        Assert.Equal((5f, new Vector4(1, 0, 0, 1), 90f), (light.Intensity, light.Color, camera.FieldOfViewDegrees));

        var again = light.IntensityTo(0f, 1f);
        node.DestroyImmediate();
        game.Frames(1);
        Assert.False(again.IsActive);
        Assert.False(fade.IsActive);
        Assert.Throws<InvalidOperationException>(static () => new LightComponent().IntensityTo(1f, 1f));
    }
}
