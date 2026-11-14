namespace Turian.Tweening;

/// <summary>
/// Plays every tween of a gameplay session, as a session-lifetime subsystem ticked once per frame after
/// <see cref="Component.OnUpdate"/>. Tweens live in pooled slots, so a running tween allocates nothing per frame;
/// a tween whose node is destroyed is killed, and ending the session kills them all.
/// </summary>
/// <remarks>Used from the game-loop thread only.</remarks>
public sealed partial class TweenRunner : ITickingSubsystem
{
    readonly GameLoopScheduler? scheduler;
    readonly SimulationClock? clock;
    TweenState[] states = new TweenState[64];
    int[] free = new int[64];
    int freeCount;
    int used;
    double lastUnscaled;

    /// <summary>Creates a runner reading unscaled time from <paramref name="scheduler"/> and freezing <paramref name="clock"/> for hit-stops.</summary>
    /// <param name="scheduler">The session's scheduler, or null to treat scaled time as unscaled.</param>
    /// <param name="clock">The session's clock, or null when hit-stops have nothing to freeze.</param>
    public TweenRunner(GameLoopScheduler? scheduler = null, SimulationClock? clock = null)
    {
        this.scheduler = scheduler;
        this.clock = clock;
        lastUnscaled = scheduler?.UnscaledElapsedSeconds ?? 0d;
    }

    /// <summary>The tween runner the services bound to <paramref name="node"/> resolve.</summary>
    /// <param name="node">A node woken in a running game.</param>
    /// <exception cref="InvalidOperationException">The node has no services, or the tween brick is not installed.</exception>
    public static TweenRunner Of(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Services?.GetService(typeof(TweenRunner)) as TweenRunner
               ?? throw new InvalidOperationException(
                   $"Node '{node.Name}' has no running {nameof(TweenRunner)}: tweens need a running game with the tween brick.");
    }

    /// <inheritdoc />
    public SubsystemTickPhase TickPhase => SubsystemTickPhase.Frame;

    /// <summary>The unscaled seconds of the current frame, which keep passing while game time is paused or frozen.</summary>
    public float UnscaledDeltaTime { get; private set; }

    /// <summary>The unscaled seconds since the session began, which keep passing while game time is paused or frozen.</summary>
    public double UnscaledElapsed { get; private set; }

    /// <summary>How many tweens and sequences are alive, children included.</summary>
    public int ActiveCount { get; private set; }

    /// <inheritdoc />
    public void Start() { }

    /// <summary>Kills every tween and releases a hit-stop, as the session ends.</summary>
    public void Stop()
    {
        for (var i = 0; i < used; i++)
        {
            if (states[i].Alive && states[i].Parent < 0)
            {
                Release(i);
            }
        }

        EndHitStop();
    }

    /// <summary>Advances every playing tween.</summary>
    /// <param name="deltaTime">Scaled seconds since the previous frame.</param>
    public void Tick(float deltaTime)
    {
        UnscaledDeltaTime = UnscaledStep(deltaTime);
        UnscaledElapsed += UnscaledDeltaTime;
        AdvanceHitStop(UnscaledDeltaTime);
        // Tweens created by this frame's callbacks start next frame.
        for (int i = 0, count = used; i < count; i++)
        {
            if (states[i] is { Alive: true, Parent: < 0, Paused: false })
            {
                Step(i, deltaTime);
            }
        }
    }

    void Step(int index, float scaled)
    {
        ref var state = ref states[index];
        if (state.Node is { IsDestroyed: true } || state.Link is { IsDestroyed: true }
            || state.Cancellation.IsCancellationRequested)
        {
            Release(index);
            return;
        }

        var generation = state.Generation;
        var step = (state.Unscaled ? UnscaledDeltaTime : scaled) * state.TimeScale;
        if (state.Backwards)
        {
            StepBackwards(index, step);
            return;
        }

        state.Elapsed += step;
        // A callback may have killed this tween, and its slot may already hold another.
        if (Evaluate(index, state.Elapsed) && IsAlive(index, generation))
        {
            Finish(index);
        }
    }

    // Ends a played-out tween: it dies unless it was told to stay for a restart or a rewind.
    void Finish(int index)
    {
        if (states[index].AutoKill)
        {
            Release(index);
        }
        else
        {
            states[index].Paused = true;
        }
    }

    void StepBackwards(int index, float step)
    {
        var generation = states[index].Generation;
        var position = states[index].Elapsed - step;
        var atStart = position <= states[index].Delay;
        states[index].Elapsed = atStart ? states[index].Delay : position;
        Evaluate(index, states[index].Elapsed);
        if (atStart && IsAlive(index, generation))
        {
            ResetTree(index);
            states[index].Backwards = false;
            Finish(index);
        }
    }

    /// <summary>Tweens a node's local position.</summary>
    public Tween Move(Node node, Vector3 to, float duration) => Create(TweenKind.Position, node, Pack(to), duration);

    /// <summary>Tweens a node's world position.</summary>
    public Tween MoveWorld(Node node, Vector3 to, float duration) =>
        Create(TweenKind.WorldPosition, node, Pack(to), duration);

    /// <summary>Tweens a node's local orientation along the shortest arc.</summary>
    public Tween Rotate(Node node, Quaternion to, float duration) =>
        Create(TweenKind.Orientation, node, new Vector4(to.X, to.Y, to.Z, to.W), duration);

    /// <summary>Tweens a node's local scale.</summary>
    public Tween Scale(Node node, Vector3 to, float duration) => Create(TweenKind.Scale, node, Pack(to), duration);

    /// <summary>Tweens any float through a getter and a setter; pass static or cached delegates to stay allocation-free.</summary>
    public Tween To(Func<float> getter, Action<float> setter, float to, float duration) =>
        Create(TweenKind.Float, null, new Vector4(to, 0f, 0f, 0f), duration, getter, setter);

    /// <summary>Tweens any vector through a getter and a setter.</summary>
    public Tween To(Func<Vector3> getter, Action<Vector3> setter, Vector3 to, float duration) =>
        Create(TweenKind.Vector3, null, Pack(to), duration, getter, setter);

    /// <summary>Tweens any four-component vector through a getter and a setter.</summary>
    public Tween To(Func<Vector4> getter, Action<Vector4> setter, Vector4 to, float duration) =>
        Create(TweenKind.Vector4, null, to, duration, getter, setter);

    /// <summary>Tweens any color through a getter and a setter, channel by channel.</summary>
    public Tween To(Func<Color> getter, Action<Color> setter, Color to, float duration) =>
        Create(TweenKind.Color, null, new Vector4(to.R, to.G, to.B, to.A), duration, getter, setter);

    /// <summary>Waits <paramref name="duration"/> seconds, for a timer or a callback after a delay.</summary>
    public Tween Delay(float duration) => Create(TweenKind.Interval, null, default, duration);

    /// <summary>Starts an empty sequence; add tweens to it before the next frame.</summary>
    public Sequence Sequence() => new(Create(TweenKind.Sequence, null, default, 0f));

    internal bool IsAlive(int index, int generation) =>
        (uint)index < (uint)used && states[index].Alive && states[index].Generation == generation;

    internal ref TweenState State(int index, int generation)
    {
        if (!IsAlive(index, generation))
        {
            throw new InvalidOperationException("The tween has completed or was killed.");
        }

        return ref states[index];
    }

    /// <summary>Kills a tween, first jumping it to its end when <paramref name="complete"/> is set.</summary>
    internal void Kill(int index, int generation, bool complete)
    {
        if (!IsAlive(index, generation))
        {
            return;
        }

        var root = Root(index);
        var rootGeneration = states[root].Generation;
        if (complete)
        {
            JumpToEnd(root);
        }

        if (IsAlive(root, rootGeneration))
        {
            Release(root);
        }
    }

    int Root(int index)
    {
        while (states[index].Parent >= 0)
        {
            index = states[index].Parent;
        }

        return index;
    }

    // Puts a tween at its end and runs its completion; endless loops have no end and stay where they are.
    void JumpToEnd(int root)
    {
        if (!states[root].Started)
        {
            Begin(root);
        }

        if (float.IsFinite(states[root].Total))
        {
            states[root].Elapsed = states[root].Total;
            Evaluate(root, states[root].Total);
        }
    }

    internal Task Completion(int index, int generation)
    {
        ref var state = ref State(index, generation);
        state.Completion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return state.Completion.Task;
    }

    Tween Create(TweenKind kind, Node? node, Vector4 to, float duration, Delegate? getter = null,
        Delegate? setter = null)
    {
        if (!float.IsFinite(duration) || duration < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "A tween lasts a finite, non-negative time.");
        }

        var index = Rent();
        ref var state = ref states[index];
        var generation = state.Generation;
        state = new TweenState
        {
            Generation = generation,
            Alive = true,
            Kind = kind,
            Node = node,
            Getter = getter,
            Setter = setter,
            To = to,
            Duration = duration,
            Loops = 1,
            LastLoop = -1,
            AutoKill = true,
            TimeScale = 1f,
            Overshoot = float.NaN,
            Period = float.NaN,
            Parent = -1,
            FirstChild = -1,
            LastChild = -1,
            NextSibling = -1,
        };
        ActiveCount++;
        return new Tween(this, index, generation);
    }

    int Rent()
    {
        if (freeCount > 0)
        {
            return free[--freeCount];
        }

        if (used == states.Length)
        {
            Array.Resize(ref states, states.Length * 2);
            Array.Resize(ref free, free.Length * 2);
        }

        return used++;
    }

    void Release(int index)
    {
        for (var child = states[index].FirstChild; child >= 0;)
        {
            var next = states[child].NextSibling;
            Release(child);
            child = next;
        }

        ref var state = ref states[index];
        RemoveOffsets(ref state);
        var completion = state.Completion;
        var completed = state.Completed;
        var onKill = state.OnKill;
        state = new TweenState { Generation = state.Generation + 1 };
        free[freeCount++] = index;
        ActiveCount--;
        if (completed)
        {
            completion?.TrySetResult();
        }
        else
        {
            completion?.TrySetCanceled();
        }

        onKill?.Invoke();
    }

    float UnscaledStep(float scaled)
    {
        if (scheduler is null)
        {
            return scaled;
        }

        var now = scheduler.UnscaledElapsedSeconds;
        var step = (float)(now - lastUnscaled);
        lastUnscaled = now;
        return step;
    }

    static Vector4 Pack(Vector3 value) => new(value, 0f);
}
