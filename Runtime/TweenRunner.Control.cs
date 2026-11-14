namespace Turian.Tweening;

public sealed partial class TweenRunner
{
    /// <summary>Kills every tween tagged <paramref name="id"/> with <see cref="Tween.SetId"/>.</summary>
    /// <param name="id">The tag to look for.</param>
    /// <param name="complete">Jump each tween to its end first, running completion callbacks.</param>
    /// <returns>How many tweens were killed.</returns>
    public int Kill(object id, bool complete = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        return KillWhere(id, null, complete);
    }

    /// <summary>Kills every tween that moves <paramref name="node"/> or is linked to it.</summary>
    /// <param name="node">The node whose tweens end.</param>
    /// <param name="complete">Jump each tween to its end first, running completion callbacks.</param>
    /// <returns>How many tweens were killed.</returns>
    public int KillTweensOf(Node node, bool complete = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        return KillWhere(null, node, complete);
    }

    /// <summary>Kills every tween and sequence.</summary>
    /// <param name="complete">Jump each tween to its end first, running completion callbacks.</param>
    public void KillAll(bool complete = false)
    {
        for (var i = 0; i < used; i++)
        {
            if (states[i].Alive && states[i].Parent < 0)
            {
                Kill(i, states[i].Generation, complete);
            }
        }
    }

    /// <summary>Whether any tween tagged <paramref name="id"/> is alive.</summary>
    public bool IsTweening(object id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Matches(id, null);
    }

    /// <summary>Whether any tween moves <paramref name="node"/> or is linked to it.</summary>
    public bool IsTweening(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Matches(null, node);
    }

    /// <summary>Pauses every tween; <see cref="PlayAll"/> resumes them.</summary>
    public void PauseAll() => SetPaused(true);

    /// <summary>Resumes every paused tween.</summary>
    public void PlayAll() => SetPaused(false);

    internal void Complete(int index, int generation)
    {
        if (!IsAlive(index, generation))
        {
            return;
        }

        var root = Root(index);
        var rootGeneration = states[root].Generation;
        JumpToEnd(root);
        if (IsAlive(root, rootGeneration) && float.IsFinite(states[root].Total))
        {
            Finish(root);
        }
    }

    internal void Restart(int index, int generation, bool pause)
    {
        if (!IsAlive(index, generation))
        {
            return;
        }

        var root = Root(index);
        ResetTree(root);
        states[root].Elapsed = 0f;
        states[root].Backwards = false;
        states[root].Paused = pause;
        if (states[root].Started)
        {
            Evaluate(root, 0f);
        }
    }

    internal void GoTo(int index, int generation, float seconds)
    {
        var root = RootOf(index, generation);
        if (!states[root].Started)
        {
            Begin(root);
        }

        var total = states[root].Total;
        var position = float.IsFinite(total) ? Math.Clamp(seconds, 0f, total) : MathF.Max(seconds, 0f);
        if (position < total)
        {
            states[root].Completed = false;
        }

        states[root].Elapsed = position;
        Evaluate(root, position);
    }

    /// <summary>Plays in reverse, forward again, or the other way round when <paramref name="backwards"/> is null.</summary>
    internal void PlayBackwards(int index, int generation, bool? backwards)
    {
        var root = RootOf(index, generation);
        ref var state = ref states[root];
        state.Backwards = backwards ?? !state.Backwards;
        state.Paused = false;
        if (state.Backwards)
        {
            state.Completed = false;
            if (float.IsFinite(state.Total))
            {
                state.Elapsed = MathF.Min(state.Elapsed, state.Total);
            }
        }
    }

    internal bool IsBackwards(int index, int generation) => states[RootOf(index, generation)].Backwards;

    // The sequence a tween plays in, or the tween itself; refuses a tween that is gone.
    int RootOf(int index, int generation)
    {
        _ = State(index, generation);
        return Root(index);
    }

    bool Matches(object? id, Node? node)
    {
        for (var i = 0; i < used; i++)
        {
            if (states[i].Alive && Matches(in states[i], id, node))
            {
                return true;
            }
        }

        return false;
    }

    static bool Matches(in TweenState state, object? id, Node? node) =>
        id is not null ? id.Equals(state.Id) : ReferenceEquals(state.Node, node) || ReferenceEquals(state.Link, node);

    int KillWhere(object? id, Node? node, bool complete)
    {
        var killed = 0;
        for (var i = 0; i < used; i++)
        {
            if (states[i].Alive && Matches(in states[i], id, node))
            {
                Kill(i, states[i].Generation, complete);
                killed++;
            }
        }

        return killed;
    }

    void SetPaused(bool paused)
    {
        for (var i = 0; i < used; i++)
        {
            if (states[i].Alive && states[i].Parent < 0)
            {
                states[i].Paused = paused;
            }
        }
    }
}
