namespace Turian.Tweening;

/// <summary>
/// A handle to a playing tween or sequence. Setters return the same handle so they chain; a handle whose tween
/// completed or was killed reports <see cref="IsActive"/> false and refuses changes. Awaiting a tween completes when
/// it finishes and is cancelled when it is killed.
/// </summary>
public readonly struct Tween : IEquatable<Tween>
{
    readonly TweenRunner? runner;

    internal Tween(TweenRunner runner, int index, int generation)
    {
        this.runner = runner;
        Index = index;
        Generation = generation;
    }

    internal int Index { get; }

    internal int Generation { get; }

    internal TweenRunner Runner => runner ?? throw new InvalidOperationException("The tween was never created.");

    /// <summary>Whether the tween is still playing or waiting to play.</summary>
    public bool IsActive => runner?.IsAlive(Index, Generation) == true;

    /// <summary>Whether the tween is active and not paused.</summary>
    public bool IsPlaying => IsActive && !State.Paused;

    ref TweenState State => ref Runner.State(Index, Generation);

    /// <summary>Shapes the tween's progress with <paramref name="ease"/>.</summary>
    public Tween SetEase(Ease ease)
    {
        State.Ease = ease;
        return this;
    }

    /// <summary>Shapes the tween's progress with a custom curve.</summary>
    public Tween SetEase(CubicBezier curve)
    {
        ref var state = ref State;
        (state.Ease, state.Bezier) = (Ease.Bezier, curve);
        return this;
    }

    /// <summary>Shapes the tween's progress with a named curve, as <see cref="Easing.TryParse"/> reads it.</summary>
    /// <exception cref="ArgumentException">The name is not a known curve.</exception>
    public Tween SetEase(string name)
    {
        if (!Easing.TryParse(name, out var ease, out var curve))
        {
            throw new ArgumentException($"'{name}' is not an easing curve.", nameof(name));
        }

        ref var state = ref State;
        (state.Ease, state.Bezier) = (ease, curve);
        return this;
    }

    /// <summary>Waits <paramref name="seconds"/> before the tween starts.</summary>
    public Tween SetDelay(float seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        State.Delay = seconds;
        return this;
    }

    /// <summary>Plays the tween <paramref name="loops"/> times, or forever with -1.</summary>
    public Tween SetLoops(int loops, LoopType type = LoopType.Restart)
    {
        if (loops is 0 or < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(loops), "Play at least once, or -1 to loop forever.");
        }

        ref var state = ref State;
        (state.Loops, state.LoopType) = (loops, type);
        return this;
    }

    /// <summary>Treats the end value as an offset from the start value.</summary>
    public Tween SetRelative()
    {
        State.Relative = true;
        return this;
    }

    /// <summary>Advances the tween in real time, so it keeps playing while game time is paused or frozen.</summary>
    public Tween SetUnscaledTime(bool unscaled = true)
    {
        State.Unscaled = unscaled;
        return this;
    }

    /// <summary>Starts from <paramref name="value"/> instead of the current value.</summary>
    public Tween From(float value) => SetFrom(new Vector4(value, 0f, 0f, 0f));

    /// <summary>Starts from <paramref name="value"/> instead of the current value.</summary>
    public Tween From(Vector3 value) => SetFrom(new Vector4(value, 0f));

    /// <summary>Starts from <paramref name="value"/> instead of the current value.</summary>
    public Tween From(Vector4 value) => SetFrom(value);

    /// <summary>Starts from <paramref name="value"/> instead of the current orientation.</summary>
    public Tween From(Quaternion value) => SetFrom(new Vector4(value.X, value.Y, value.Z, value.W));

    /// <summary>Starts from <paramref name="value"/> instead of the current color.</summary>
    public Tween From(Color value) => SetFrom(new Vector4(value.R, value.G, value.B, value.A));

    /// <summary>Runs <paramref name="action"/> when the tween starts, after its delay.</summary>
    public Tween OnStart(Action action)
    {
        State.OnStart = action;
        return this;
    }

    /// <summary>Runs <paramref name="action"/> when the tween has played every loop.</summary>
    public Tween OnComplete(Action action)
    {
        State.OnComplete = action;
        return this;
    }

    /// <summary>Kills the tween on the first frame after <paramref name="token"/> is cancelled.</summary>
    public Tween WithCancellation(CancellationToken token)
    {
        State.Cancellation = token;
        return this;
    }

    /// <summary>Holds the tween where it is.</summary>
    public Tween Pause()
    {
        State.Paused = true;
        return this;
    }

    /// <summary>Resumes a paused tween.</summary>
    public Tween Play()
    {
        State.Paused = false;
        return this;
    }

    /// <summary>Stops the tween, or its whole sequence, removing any offset it applied.</summary>
    /// <param name="complete">Jump to the end first, running completion callbacks.</param>
    public void Kill(bool complete = false) => runner?.Kill(Index, Generation, complete);

    /// <summary>A task that completes with the tween and is cancelled if it is killed first.</summary>
    public Task AsTask() => IsActive ? Runner.Completion(Index, Generation) : Task.CompletedTask;

    /// <summary>Lets game code <c>await</c> a tween.</summary>
    public TaskAwaiter GetAwaiter() => AsTask().GetAwaiter();

    /// <inheritdoc />
    public bool Equals(Tween other) =>
        ReferenceEquals(runner, other.runner) && Index == other.Index && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Tween other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(runner, Index, Generation);

    /// <summary>Whether two handles name the same tween.</summary>
    public static bool operator ==(Tween left, Tween right) => left.Equals(right);

    /// <summary>Whether two handles name different tweens.</summary>
    public static bool operator !=(Tween left, Tween right) => !left.Equals(right);

    Tween SetFrom(Vector4 value)
    {
        ref var state = ref State;
        (state.From, state.HasFrom) = (value, true);
        return this;
    }
}
