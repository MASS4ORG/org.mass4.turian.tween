namespace Turian.Tweening;

/// <summary>How a looping tween replays.</summary>
public enum LoopType
{
    /// <summary>Each loop starts again from the start value.</summary>
    Restart,

    /// <summary>Loops alternate forward and backward.</summary>
    Yoyo,

    /// <summary>Each loop continues from where the previous one ended.</summary>
    Incremental,
}

enum TweenKind : byte
{
    Position,
    WorldPosition,
    Orientation,
    Scale,
    Float,
    Vector3,
    Vector4,
    Color,
    Sequence,
    Callback,
    Interval,
    RotateBy,
    Jump,
    Path,
    BlendPosition,
    BlendScale,
    BlendRotation,
    Shake,
    PunchPosition,
    PunchScale,
    PunchRotation,
    SquashStretch,
    ShakeRotation,
    ShakeScale,
}

/// <summary>One pooled tween slot; values travel as <see cref="Vector4"/>, orientations as packed quaternions.</summary>
struct TweenState
{
    public int Generation;
    public bool Alive;
    public bool Started;
    public bool Completed;
    public bool Paused;
    public bool Unscaled;
    public bool Relative;
    public bool HasFrom;
    public TweenKind Kind;
    public Node? Node;
    public Delegate? Getter;
    public Delegate? Setter;
    public Vector4 From;
    public Vector4 To;
    public Vector4 Applied;
    public float Duration;
    public float Delay;
    public float Elapsed;
    public int Loops;
    public int LastLoop;
    public LoopType LoopType;
    public Ease Ease;
    public CubicBezier Bezier;
    public float Frequency;
    public int Seed;
    public float Overshoot;
    public float Period;
    public Func<float, float>? EaseFunction;
    public float Amount;
    public int Mode;
    public Vector3[]? Waypoints;
    public bool Backwards;
    public bool AutoKill;
    public float TimeScale;
    public int StepsFired;
    public object? Id;
    public Node? Link;
    public Action? OnStart;
    public Action? OnUpdate;
    public Action? OnStepComplete;
    public Action? OnComplete;
    public Action? OnKill;
    public TaskCompletionSource? Completion;
    public CancellationToken Cancellation;
    public int Parent;
    public int FirstChild;
    public int LastChild;
    public int NextSibling;
    public float Start;
    public float Cursor;
    public float LastAppendStart;

    /// <summary>Delay plus every loop, or infinity for endless loops.</summary>
    public readonly float Total => Loops < 0 ? float.PositiveInfinity : Delay + Duration * Loops;
}
