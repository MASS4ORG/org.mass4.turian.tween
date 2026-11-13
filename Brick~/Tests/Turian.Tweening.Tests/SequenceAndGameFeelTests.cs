namespace Turian.Tests;

/// <summary>Sequences and the game-feel effects: shake, punch, squash and stretch, hit-stop and trauma shake.</summary>
[Collection(SerialTests.Name)]
public sealed class SequenceAndGameFeelTests : IDisposable
{
    readonly TweenGame game = new();

    /// <inheritdoc />
    public void Dispose() => game.Dispose();

    /// <summary>Append, join, intervals and callbacks play on one timeline, then the sequence completes.</summary>
    [Fact]
    public void SequencesPlayTheirTimeline()
    {
        var node = game.Spawn();
        var (value, log) = (0f, new List<string>());
        var sequence = game.Runner.Sequence()
            .Append(node.MoveTo(new Vector3(1, 0, 0), 1f))
            .Append(node.ScaleTo(new Vector3(2), 1f))
            .Join(game.Runner.To(() => value, v => value = v, 1f, 0.5f))
            .AppendInterval(0.5f)
            .AppendCallback(() => log.Add("end"))
            .InsertCallback(0.5f, () => log.Add("half"));
        var awaited = sequence.AsTween().OnComplete(() => log.Add("done")).AsTask();

        game.Frames(2);
        Assert.Equal(0.5f, node.Position.X, 3);
        Assert.Equal(["half"], log);
        game.Frames(4);
        Assert.Equal((1.5f, 1f), (node.Scale.X, value));
        Assert.True(((Tween)sequence).IsActive);

        game.Frames(4);
        Assert.Equal(new Vector3(2), node.Scale);
        Assert.Equal(["half", "end", "done"], log);
        Assert.True(awaited.IsCompletedSuccessfully);
        Assert.Equal(0, game.Runner.ActiveCount);
    }

    /// <summary>A looping sequence replays its children and callbacks each loop; yoyo plays it back.</summary>
    [Theory]
    [InlineData(LoopType.Restart, 1f)]
    [InlineData(LoopType.Yoyo, 0f)]
    public void LoopingSequencesReplay(LoopType type, float end)
    {
        var (value, calls) = (0f, 0);
        var inner = game.Runner.Sequence().Append(game.Runner.To(() => value, v => value = v, 1f, 1f));
        game.Runner.Sequence().Append(inner).AppendCallback(() => calls++).AsTween().SetLoops(2, type);

        game.Frames(6);
        Assert.Equal(0.5f, value, 3);
        game.Frames(2);

        Assert.Equal(end, value, 3);
        Assert.Equal(type == LoopType.Restart ? 2 : 1, calls);
    }

    /// <summary>Killing a tween inside a sequence kills the whole sequence.</summary>
    [Fact]
    public void KillingAChildKillsItsSequence()
    {
        var value = 0f;
        var child = game.Runner.To(() => value, v => value = v, 1f, 1f);
        var sequence = game.Runner.Sequence().Append(child);

        child.Kill(complete: true);

        Assert.False(((Tween)sequence).IsActive);
        Assert.Equal(1f, value);
    }

    /// <summary>Sequences refuse endless, started, shared or late children.</summary>
    [Fact]
    public void SequencesRefuseInvalidChildren()
    {
        var value = 0f;
        var endless = game.Runner.To(() => value, v => value = v, 1f, 1f).SetLoops(-1);
        var sequence = game.Runner.Sequence();
        Assert.Throws<InvalidOperationException>(() => sequence.Append(endless));
        Assert.Throws<InvalidOperationException>(() => sequence.Append(sequence));
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Insert(-1f, game.Runner.Delay(1f)));

        var shared = game.Runner.Delay(1f);
        sequence.Append(shared);
        Assert.Throws<InvalidOperationException>(() => game.Runner.Sequence().Append(shared));

        var started = game.Runner.Delay(5f);
        game.Frames(1);
        Assert.Throws<InvalidOperationException>(() => game.Runner.Sequence().Append(started));
        Assert.Throws<InvalidOperationException>(() => sequence.Append(game.Runner.Delay(1f)));
        Assert.Throws<InvalidOperationException>(() => new Sequence().Append(started));
        using var other = new TweenGame();
        Assert.Throws<InvalidOperationException>(() => other.Runner.Sequence().Append(game.Runner.Delay(1f)));
    }

    /// <summary>A seeded shake moves the node, replays identically, keeps other movement and leaves no offset.</summary>
    [Fact]
    public void ShakesAreSeededAdditiveAndLeaveNoOffset()
    {
        var (first, second) = (game.Spawn(new Vector3(1, 2, 3)), game.Spawn(new Vector3(1, 2, 3)));
        first.Shake(1f, new Vector3(1, 1, 0), seed: 5);
        second.Shake(1f, new Vector3(1, 1, 0), seed: 5);

        game.Frames(1, 0.3);
        Assert.NotEqual(new Vector3(1, 2, 3), first.Position);
        Assert.Equal(first.Position, second.Position);
        Assert.Equal(3f, first.Position.Z);

        first.Position += new Vector3(0, 10, 0);
        game.Frames(4);

        AssertNear(new Vector3(1, 12, 3), first.Position);
        AssertNear(new Vector3(1, 2, 3), second.Position);
    }

    /// <summary>Punches kick position, scale and rotation and spring each back to where it was.</summary>
    [Fact]
    public void PunchesSpringBack()
    {
        var node = game.Spawn(new Vector3(0, 1, 0));
        node.PunchPosition(new Vector3(0, 1, 0), 1f);
        node.PunchScale(new Vector3(0.5f), 1f, vibrato: 3);
        node.PunchRotation(new Vector3(0, 0, 30), 1f);

        game.Frames(1, 0.1);
        Assert.NotEqual(1f, node.Position.Y);
        Assert.NotEqual(1f, node.Scale.X);
        Assert.NotEqual(Quaternion.Identity, node.Orientation);

        game.Frames(5);
        AssertNear(new Vector3(0, 1, 0), node.Position);
        AssertNear(Vector3.One, node.Scale);
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.Identity, node.Orientation)) > 0.9999f);
    }

    /// <summary>Squash and stretch keeps the node's volume while it wobbles, then restores its scale.</summary>
    [Fact]
    public void SquashAndStretchKeepsVolume()
    {
        var node = game.Spawn();
        node.Scale = new Vector3(2);
        var tween = node.SquashStretch(0.5f, 1f, Vector3.UnitY);

        game.Frames(1, 0.05);
        Assert.True(node.Scale.Y > 2f);
        Assert.Equal(8f, node.Scale.X * node.Scale.Y * node.Scale.Z, 2);

        tween.Kill();
        AssertNear(new Vector3(2), node.Scale);
        Assert.Throws<ArgumentOutOfRangeException>(() => node.SquashStretch(-1f, 1f, Vector3.UnitY));
    }

    /// <summary>A hit-stop freezes game time in real seconds, extends on overlap and restores the time scale.</summary>
    [Fact]
    public void HitStopsFreezeGameTimeThenRestoreIt()
    {
        game.Clock.TimeScale = 0.5;
        game.Runner.HitStop(0.5f);
        Assert.Equal(0d, game.Clock.TimeScale);

        game.Frames(1);
        game.Runner.HitStop(0.6f);
        game.Frames(2);
        Assert.True(game.Runner.IsHitStopping);

        game.Frames(1);
        Assert.False(game.Runner.IsHitStopping);
        Assert.Equal(0.5d, game.Clock.TimeScale);

        game.Runner.HitStop(10f, 0.1f);
        game.Lifetimes.Restart();
        game.Frames(1);
        Assert.Equal(0.5d, game.Clock.TimeScale);

        var detached = new TweenRunner();
        detached.HitStop(1f);
        Assert.False(detached.IsHitStopping);
    }

    /// <summary>Trauma shakes harder the more trauma there is, decays, keeps going during a hit-stop and cleans up.</summary>
    [Fact]
    public void TraumaShakesDecayAndCleanUp()
    {
        var root = new Node { Name = "Level" };
        var camera = new Node { Name = "Camera", Position = new Vector3(0, 0, -10) };
        var shake = new TraumaShakeComponent { Seed = 3, MaxOffset = new Vector3(1, 1, 0) };
        camera.AddComponent(shake);
        root.Children.Add(camera);
        game.Scenes.AdoptScene(Guid.NewGuid(), root);
        root.Awake(null, game.SceneServices);

        shake.AddTrauma(2f);
        Assert.Equal(1f, shake.Trauma);
        game.Runner.HitStop(1f);
        game.Frames(1, 0.33);
        Assert.NotEqual(new Vector3(0, 0, -10), camera.Position);
        Assert.Equal(0.505f, shake.Trauma, 3);

        shake.IsActive = false;
        AssertNear(new Vector3(0, 0, -10), camera.Position);
        Assert.True(Quaternion.Dot(Quaternion.Identity, camera.Orientation) > 0.9999f);

        shake.IsActive = true;
        game.Frames(4);
        Assert.Equal(0f, shake.Trauma);
        AssertNear(new Vector3(0, 0, -10), camera.Position);
    }

    static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"Expected {expected}, got {actual}.");
}
