namespace Turian.Tests;

/// <summary>Killing by id or node, restarting, rewinding, playing backwards, callbacks and speed.</summary>
[Collection(SerialTests.Name)]
public sealed class TweenControlTests : IDisposable
{
    readonly TweenGame game = new();
    float value;

    /// <inheritdoc />
    public void Dispose() => game.Dispose();

    Tween Value(float to = 10f, float duration = 1f) => game.Runner.To(() => value, v => value = v, to, duration);

    /// <summary>Tweens are found and killed by id, by node, linked to a node, or all at once, optionally finishing first.</summary>
    [Fact]
    public void TweensAreKilledByIdNodeOrAll()
    {
        var (node, other) = (game.Spawn(), game.Spawn());
        Value().SetId("fade");
        node.MoveTo(Vector3.One, 1f);
        var linked = Value().SetLink(other);
        Assert.True(game.Runner.IsTweening("fade"));
        Assert.True(node.IsTweening());
        Assert.False(game.Runner.IsTweening("other"));

        Assert.Equal(1, game.Runner.Kill("fade", complete: true));
        Assert.Equal(10f, value);
        Assert.False(game.Runner.IsTweening("fade"));
        Assert.Equal(1, node.KillTweens(complete: true));
        Assert.Equal(Vector3.One, node.Position);

        Assert.True(game.Runner.IsTweening(other));
        other.DestroyImmediate();
        game.Frames(1);
        Assert.False(linked.IsActive);

        Value();
        Value();
        game.Runner.KillAll();
        Assert.Equal(0, game.Runner.ActiveCount);
        Assert.Throws<ArgumentNullException>(() => game.Runner.Kill(null!));
    }

    /// <summary>A tween set to stay can be restarted, rewound, jumped in time and completed again.</summary>
    [Fact]
    public void StayingTweensRestartRewindAndGoTo()
    {
        var completes = 0;
        var tween = Value().SetAutoKill(false).OnComplete(() => completes++);
        game.Frames(5);
        Assert.True(tween.IsActive);
        Assert.True(tween.IsComplete);
        Assert.False(tween.IsPlaying);
        Assert.Equal((10f, 1), (value, completes));

        tween.Restart();
        Assert.Equal(0f, value);
        game.Frames(2);
        Assert.Equal(5f, value, 3);
        game.Frames(2);
        Assert.Equal((10f, 2), (value, completes));

        tween.Rewind();
        game.Frames(3);
        Assert.Equal(0f, value);
        tween.Play();
        game.Frames(1);
        Assert.Equal(2.5f, value, 3);

        tween.GoTo(0.8f);
        Assert.Equal(8f, value, 3);
        Assert.Equal(0.8f, tween.Elapsed, 3);
        tween.Complete();
        Assert.Equal((10f, 3), (value, completes));
        Assert.True(tween.IsActive);
        tween.Kill();
    }

    /// <summary>Playing backwards returns to the start and flips direction at the end of the way back.</summary>
    [Fact]
    public void PlayingBackwardsReturnsToTheStart()
    {
        var tween = Value().SetAutoKill(false);
        game.Frames(5);
        Assert.Equal(10f, value);

        tween.PlayBackwards();
        Assert.True(tween.IsBackwards);
        game.Frames(2);
        Assert.Equal(5f, value, 3);
        game.Frames(2);
        Assert.Equal(0f, value, 3);
        Assert.False(tween.IsBackwards);
        Assert.True(tween.IsActive);

        tween.Flip();
        Assert.True(tween.IsBackwards);
        tween.PlayForward();
        Assert.False(tween.IsBackwards);
        tween.TogglePause();
        Assert.False(tween.IsPlaying);
        tween.Kill();

        var dying = Value();
        game.Frames(2);
        dying.PlayBackwards();
        game.Frames(3);
        Assert.False(dying.IsActive);
        Assert.Equal(0f, value, 3);
    }

    /// <summary>Update, step, complete and kill callbacks fire at their moments.</summary>
    [Fact]
    public void CallbacksFireAtTheirMoments()
    {
        var log = new List<string>();
        var tween = Value(1f, 1f).SetLoops(3).OnUpdate(() => log.Add("u")).OnStepComplete(() => log.Add("s"))
            .OnComplete(() => log.Add("c")).OnKill(() => log.Add("k"));

        game.Frames(12);

        Assert.Equal(3, log.Count(static entry => entry == "s"));
        Assert.Equal(["s", "c", "k"], log.Skip(log.Count - 4).Where(static entry => entry != "u"));
        Assert.Equal(12, log.Count(static entry => entry == "u"));
        Assert.False(tween.IsActive);

        var killed = 0;
        Value().OnKill(() => killed++).Kill();
        Assert.Equal(1, killed);
    }

    /// <summary>A faster tween finishes sooner; a speed of zero or less is refused.</summary>
    [Fact]
    public void SpeedScalesPlayback()
    {
        var tween = Value().SetSpeed(2f);
        game.Frames(2);
        Assert.Equal(10f, value);
        Assert.False(tween.IsActive);
        Assert.Throws<ArgumentOutOfRangeException>(() => Value().SetSpeed(0f));
    }

    /// <summary>Killing a tween from its own completion callback leaves the runner consistent.</summary>
    [Fact]
    public void KillingFromACompletionCallbackIsSafe()
    {
        var tween = default(Tween);
        tween = Value(1f).OnComplete(() => tween.Kill());

        game.Frames(5);
        Assert.Equal(0, game.Runner.ActiveCount);

        Value();
        Value();
        Assert.Equal(2, game.Runner.ActiveCount);
        game.Runner.KillAll();
        Assert.Equal(0, game.Runner.ActiveCount);
    }

    /// <summary>What follows a nested sequence waits for it to finish.</summary>
    [Fact]
    public void ASequenceAfterANestedSequenceWaitsForIt()
    {
        var log = new List<string>();
        var inner = game.Runner.Sequence().AppendInterval(1f);
        game.Runner.Sequence().Append(inner).AppendCallback(() => log.Add("after"));

        game.Frames(3);
        Assert.Empty(log);
        game.Frames(2);
        Assert.Equal(["after"], log);
    }

    /// <summary>Elapsed, duration and progress describe the tween; a sequence lasts as long as its children.</summary>
    [Fact]
    public void ProgressDescribesTheTween()
    {
        var tween = Value(1f, 2f).SetLoops(2).SetDelay(1f);
        game.Frames(2);
        Assert.Equal((2f, 5f), (tween.Duration, tween.FullDuration));
        Assert.Equal(0.5f, tween.Elapsed, 3);
        Assert.Equal(0.1f, tween.Progress, 3);

        var sequence = game.Runner.Sequence().Append(Value(1f, 1f)).Append(Value(1f, 2f));
        Assert.Equal(3f, sequence.AsTween().Duration);
        Assert.Equal(0f, game.Runner.Delay(0f).Progress);
    }

    /// <summary>Everything can be paused and resumed at once.</summary>
    [Fact]
    public void PauseAllHoldsEveryTween()
    {
        var node = game.Spawn();
        node.MoveTo(Vector3.One, 1f);
        Value();

        game.Runner.PauseAll();
        game.Frames(2);
        Assert.Equal((0f, Vector3.Zero), (value, node.Position));

        game.Runner.PlayAll();
        game.Frames(2);
        Assert.Equal(5f, value, 3);
    }

    /// <summary>Controls on a finished tween do nothing or are refused, never corrupting the runner.</summary>
    [Fact]
    public void ControlsOnADeadTweenAreHarmlessOrRefused()
    {
        var tween = Value(1f);
        tween.Kill();

        tween.Complete();
        tween.Restart();
        tween.Rewind();
        Assert.Throws<InvalidOperationException>(() => tween.GoTo(1f));
        Assert.Throws<InvalidOperationException>(() => tween.PlayBackwards());
        Assert.Throws<InvalidOperationException>(() => tween.Flip());
        Assert.Throws<InvalidOperationException>(() => _ = tween.IsBackwards);
        Assert.Equal(0, game.Runner.ActiveCount);
    }

    /// <summary>Rewinding a delayed tween returns to its start values; a tween killed by its update callback is gone.</summary>
    [Fact]
    public void RewindingADelayedTweenAndKillingFromUpdate()
    {
        var delayed = Value().SetDelay(1f).SetAutoKill(false);
        game.Frames(8);
        Assert.Equal(10f, value, 3);

        delayed.Rewind();
        Assert.Equal(0f, value, 3);
        delayed.Kill();

        var tween = default(Tween);
        tween = Value().OnUpdate(() => tween.Kill());
        game.Frames(1);
        Assert.False(tween.IsActive);
        Assert.Equal(0, game.Runner.ActiveCount);
    }

    /// <summary>Controls on a tween inside a sequence act on the whole sequence.</summary>
    [Fact]
    public void ControlsOnAChildActOnItsSequence()
    {
        var child = Value(1f, 1f);
        var sequence = game.Runner.Sequence().Append(child).AsTween().SetAutoKill(false);
        game.Frames(5);
        Assert.True(sequence.IsComplete);

        child.PlayBackwards();
        Assert.True(sequence.IsBackwards);
        game.Frames(5);
        Assert.Equal(0f, value, 3);
        sequence.Kill();
        Assert.False(child.IsActive);
    }
}
