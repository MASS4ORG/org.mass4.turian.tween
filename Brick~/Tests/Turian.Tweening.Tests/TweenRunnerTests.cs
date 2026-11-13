namespace Turian.Tests;

/// <summary>Tweens, loops, callbacks and lifetime in a running game.</summary>
[Collection(SerialTests.Name)]
public sealed class TweenRunnerTests : IDisposable
{
    readonly TweenGame game = new();

    /// <inheritdoc />
    public void Dispose() => game.Dispose();

    /// <summary>A node tween passes through its eased values, lands on its target and reports both callbacks once.</summary>
    [Fact]
    public void NodeTweensReachTheirTargets()
    {
        var node = game.Spawn(Vector3.Zero);
        var (starts, completes) = (0, 0);
        var tween = node.MoveTo(new Vector3(4, 0, 0), 1f).OnStart(() => starts++).OnComplete(() => completes++);

        game.Frames(2);
        Assert.Equal(2f, node.Position.X, 3);
        Assert.True(tween.IsPlaying);

        game.Frames(3);
        Assert.Equal(new Vector3(4, 0, 0), node.Position);
        Assert.Equal((1, 1), (starts, completes));
        Assert.False(tween.IsActive);
        Assert.Equal(0, game.Runner.ActiveCount);
    }

    /// <summary>Scale, orientation and world-position tweens drive their node; easing shapes the progress.</summary>
    [Fact]
    public void ScaleOrientationAndWorldPositionTween()
    {
        var parent = game.Spawn(new Vector3(10, 0, 0));
        var child = game.Spawn();
        parent.Children.Add(child);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        child.ScaleTo(new Vector3(3), 1f).SetEase(Ease.InQuad);
        child.RotateTo(turn, 1f);
        child.MoveToWorld(new Vector3(10, 2, 0), 1f);
        game.Frames(2);
        Assert.Equal(1.5f, child.Scale.X, 3);

        game.Frames(2);
        Assert.Equal(new Vector3(3), child.Scale);
        Assert.True(Quaternion.Dot(turn, child.Orientation) > 0.9999f);
        Assert.Equal(new Vector3(10, 2, 0), child.GlobalTransform.Position);
        Assert.Equal(new Vector3(0, 2, 0), child.Position);
    }

    /// <summary>Property tweens drive floats, vectors and colors through their delegates.</summary>
    [Fact]
    public void PropertiesTweenThroughTheirDelegates()
    {
        var (value, vector, quad, color) = (0f, Vector3.Zero, Vector4.Zero, new Color(0, 0, 0));
        game.Runner.To(() => value, v => value = v, 8f, 1f);
        game.Runner.To(() => vector, v => vector = v, Vector3.One, 1f);
        game.Runner.To(() => quad, v => quad = v, new Vector4(2), 1f);
        game.Runner.To(() => color, v => color = v, new Color(1, 0.5f, 0, 0.5f), 1f).From(new Color(0, 0, 0, 1));

        game.Frames(2);
        Assert.Equal(4f, value, 3);
        Assert.Equal(0.75f, color.A, 3);

        game.Frames(2);
        Assert.Equal((8f, Vector3.One, new Vector4(2)), (value, vector, quad));
        Assert.Equal(new Color(1, 0.5f, 0, 0.5f), color);
    }

    /// <summary>Delays wait, relative tweens offset and From replaces the captured start.</summary>
    [Fact]
    public void DelayRelativeAndFrom()
    {
        var node = game.Spawn(new Vector3(1, 0, 0));
        node.MoveTo(new Vector3(2, 0, 0), 1f).SetRelative().SetDelay(0.5f);
        var other = game.Spawn();
        other.ScaleTo(Vector3.One, 1f).From(new Vector3(5));

        game.Frames(2);
        Assert.Equal(1f, node.Position.X, 3);
        game.Frames(4);
        Assert.Equal(3f, node.Position.X, 3);
        Assert.Equal(Vector3.One, other.Scale);
    }

    /// <summary>Restart, yoyo and incremental loops end where each loop type puts them.</summary>
    [Theory]
    [InlineData(LoopType.Restart, 2, 1f)]
    [InlineData(LoopType.Yoyo, 2, 0f)]
    [InlineData(LoopType.Yoyo, 3, 1f)]
    [InlineData(LoopType.Incremental, 3, 3f)]
    public void LoopsEndWhereTheirTypePutsThem(LoopType type, int loops, float end)
    {
        var value = 0f;
        var tween = game.Runner.To(() => value, v => value = v, 1f, 1f).SetLoops(loops, type);

        game.Frames(4 * loops - 2);
        Assert.True(tween.IsActive);
        game.Frames(2);

        Assert.Equal(end, value, 3);
        Assert.False(tween.IsActive);
    }

    /// <summary>An endless loop keeps going until killed; killing it cancels its awaiters.</summary>
    [Fact]
    public void EndlessLoopsPlayUntilKilled()
    {
        var value = 0f;
        var tween = game.Runner.To(() => value, v => value = v, 1f, 0.5f).SetLoops(-1, LoopType.Yoyo);
        var awaited = tween.AsTask();

        game.Frames(40);
        Assert.True(tween.IsActive);

        tween.Kill();
        Assert.True(awaited.IsCanceled);
        Assert.False(tween.IsActive);
        Assert.Throws<InvalidOperationException>(() => tween.SetEase(Ease.InQuad));
    }

    /// <summary>Killing with completion jumps to the end and completes the awaiter.</summary>
    [Fact]
    public void KillingWithCompletionJumpsToTheEnd()
    {
        var node = game.Spawn();
        var completed = false;
        var tween = node.MoveTo(new Vector3(0, 5, 0), 3f).OnComplete(() => completed = true);
        var awaited = tween.AsTask();
        game.Frames(1);

        tween.Kill(complete: true);

        Assert.Equal(new Vector3(0, 5, 0), node.Position);
        Assert.True(completed);
        Assert.True(awaited.IsCompletedSuccessfully);
        Assert.True(tween.AsTask().IsCompletedSuccessfully);
    }

    /// <summary>A tween dies with its node, and a cancelled token kills its tween on the next frame.</summary>
    [Fact]
    public void TweensDieWithTheirNodeOrToken()
    {
        var node = game.Spawn();
        var tween = node.MoveTo(Vector3.One, 2f);
        using var cancel = new CancellationTokenSource();
        var value = 0f;
        var timed = game.Runner.To(() => value, v => value = v, 1f, 2f).WithCancellation(cancel.Token);
        game.Frames(1);

        node.DestroyImmediate();
        cancel.Cancel();
        game.Frames(1);

        Assert.False(tween.IsActive);
        Assert.False(timed.IsActive);
        Assert.Equal(0, game.Runner.ActiveCount);
    }

    /// <summary>A paused tween holds, and an unscaled tween keeps playing while game time is stopped.</summary>
    [Fact]
    public void PausedHoldAndUnscaledTweensIgnoreTimeScale()
    {
        var (scaled, unscaled, paused) = (0f, 0f, 0f);
        game.Runner.To(() => scaled, v => scaled = v, 1f, 1f);
        game.Runner.To(() => unscaled, v => unscaled = v, 1f, 1f).SetUnscaledTime();
        var held = game.Runner.To(() => paused, v => paused = v, 1f, 1f).Pause();

        game.Clock.TimeScale = 0;
        game.Frames(2);
        Assert.Equal((0f, 0.5f, 0f), (scaled, unscaled, paused));
        Assert.False(held.IsPlaying);

        game.Clock.TimeScale = 1;
        held.Play();
        game.Frames(4);
        Assert.Equal((1f, 1f, 1f), (scaled, unscaled, paused));
    }

    /// <summary>Restarting the session kills every tween, and nodes then reach the new session's runner.</summary>
    [Fact]
    public void ASessionRestartKillsItsTweens()
    {
        var node = game.Spawn();
        var first = game.Runner;
        var awaited = node.MoveTo(Vector3.One, 5f).AsTask();

        game.Lifetimes.Restart();
        game.Frames(1);

        Assert.True(awaited.IsCanceled);
        Assert.NotSame(first, game.Runner);
        Assert.Same(game.Runner, TweenRunner.Of(node));
    }

    /// <summary>Misuse is refused with a clear exception.</summary>
    [Fact]
    public void MisuseIsRefused()
    {
        var value = 0f;
        var tween = game.Runner.To(() => value, v => value = v, 1f, 1f);
        Assert.Throws<ArgumentException>(() => tween.SetEase("wobbly"));
        Assert.Throws<ArgumentOutOfRangeException>(() => tween.SetLoops(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tween.SetDelay(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Runner.Delay(float.NaN));
        Assert.Throws<InvalidOperationException>(() => TweenRunner.Of(new Node()));
        Assert.Throws<InvalidOperationException>(() => default(Tween).SetEase(Ease.Linear));
        Assert.False(default(Tween).IsActive);
        Assert.True(default(Tween).AsTask().IsCompletedSuccessfully);
        Assert.Equal(tween, tween.SetEase("ease-out").SetEase(new CubicBezier(0, 0, 1, 1)));
        Assert.NotEqual(tween, game.Runner.Delay(1f));
        Assert.False(tween.Equals("tween"));
        Assert.Equal(tween.GetHashCode(), tween.SetEase(Ease.InQuad).GetHashCode());
    }

    /// <summary>A thousand running tweens allocate nothing per frame once warmed up.</summary>
    [Fact]
    public void ThousandTweensAllocateNothingPerFrame()
    {
        var nodes = Enumerable.Range(0, 1000).Select(_ => game.Spawn()).ToArray();
        foreach (var node in nodes)
        {
            node.MoveTo(new Vector3(100, 0, 0), 1000f).SetEase(Ease.OutBack).SetLoops(-1, LoopType.Yoyo);
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
        Assert.Equal(1000, runner.ActiveCount);
        Assert.True(nodes[0].Position.X > 0f);
    }
}
