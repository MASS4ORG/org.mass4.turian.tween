namespace Turian.Tweening;

/// <summary>
/// Builds a timeline of tweens that plays as one: append places a tween after the previous one, join plays it with
/// the previous one, insert places it at a time. Use <see cref="AsTween"/> for loops, delays, easing of the whole
/// timeline, callbacks and awaiting. Set a tween's delay and loops before adding it, and add tweens before the next
/// frame, while they have not started.
/// </summary>
public readonly struct Sequence
{
    readonly Tween tween;

    internal Sequence(Tween tween) => this.tween = tween;

    /// <summary>The sequence as a tween, for its setters, <c>await</c> and <see cref="Tween.Kill"/>.</summary>
    public Tween AsTween() => tween;

    /// <summary>The sequence as a tween.</summary>
    public static implicit operator Tween(Sequence sequence) => sequence.tween;

    /// <summary>Plays <paramref name="child"/> after everything appended so far.</summary>
    public Sequence Append(Tween child)
    {
        tween.Runner.AddChild(tween, child, SequencePlacement.Append, 0f);
        return this;
    }

    /// <summary>Plays <paramref name="child"/> together with the tween appended last.</summary>
    public Sequence Join(Tween child)
    {
        tween.Runner.AddChild(tween, child, SequencePlacement.Join, 0f);
        return this;
    }

    /// <summary>Plays <paramref name="child"/> <paramref name="seconds"/> after the sequence starts.</summary>
    public Sequence Insert(float seconds, Tween child)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        tween.Runner.AddChild(tween, child, SequencePlacement.Insert, seconds);
        return this;
    }

    /// <summary>Waits <paramref name="seconds"/> after everything appended so far.</summary>
    public Sequence AppendInterval(float seconds) => Append(tween.Runner.Delay(seconds));

    /// <summary>Runs <paramref name="action"/> once everything appended so far has played.</summary>
    public Sequence AppendCallback(Action action) => Append(tween.Runner.Delay(0f).OnComplete(action));

    /// <summary>Runs <paramref name="action"/> <paramref name="seconds"/> after the sequence starts.</summary>
    public Sequence InsertCallback(float seconds, Action action) =>
        Insert(seconds, tween.Runner.Delay(0f).OnComplete(action));
}

enum SequencePlacement
{
    Append,
    Join,
    Insert,
}

public sealed partial class TweenRunner
{
    internal void AddChild(Tween sequence, Tween child, SequencePlacement placement, float at)
    {
        ValidateChild(sequence, child);
        ref var parent = ref states[sequence.Index];
        ref var entry = ref states[child.Index];
        var start = placement switch
        {
            SequencePlacement.Append => parent.Cursor,
            SequencePlacement.Join => parent.LastAppendStart,
            _ => at,
        };
        entry.Parent = sequence.Index;
        entry.Start = start;
        // A sequence keeps its length up to date as it grows, so one nested in another places what follows it correctly.
        parent.Duration = MathF.Max(parent.Duration, start + entry.Total);
        if (placement != SequencePlacement.Insert)
        {
            parent.LastAppendStart = start;
            parent.Cursor = MathF.Max(parent.Cursor, start + entry.Total);
        }

        if (parent.LastChild < 0)
        {
            parent.FirstChild = child.Index;
        }
        else
        {
            states[parent.LastChild].NextSibling = child.Index;
        }

        parent.LastChild = child.Index;
    }

    void ValidateChild(Tween sequence, Tween child)
    {
        var parent = State(sequence.Index, sequence.Generation);
        if (parent.Kind != TweenKind.Sequence || parent.Started)
        {
            throw new InvalidOperationException("Tweens join a sequence before it starts.");
        }

        if (child == sequence || !ReferenceEquals(child.Runner, this))
        {
            throw new InvalidOperationException("A sequence takes tweens of its own runner, other than itself.");
        }

        var entry = State(child.Index, child.Generation);
        if (entry.Parent >= 0 || entry.Started)
        {
            throw new InvalidOperationException("Only a new tween in no other sequence joins a sequence.");
        }

        if (!float.IsFinite(entry.Total))
        {
            throw new InvalidOperationException("A tween in a sequence cannot loop forever.");
        }
    }
}
