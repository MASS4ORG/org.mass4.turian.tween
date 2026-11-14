namespace Turian.Tweening;

public readonly partial struct Tween
{
    /// <summary>Seconds played so far, delay included.</summary>
    public float Elapsed => State.Elapsed;

    /// <summary>Seconds one loop lasts; a sequence lasts as long as its children.</summary>
    public float Duration => State.Duration;

    /// <summary>Seconds from start to end with delay and every loop, or infinity for endless loops.</summary>
    public float FullDuration => State.Total;

    /// <summary>How far along the tween is, from 0 to 1; an endless loop reports its current loop.</summary>
    public float Progress
    {
        get
        {
            ref var state = ref State;
            var total = float.IsFinite(state.Total) ? state.Total : state.Delay + state.Duration;
            return total <= 0f ? 0f : Math.Clamp(state.Elapsed / total, 0f, 1f);
        }
    }

    /// <summary>Whether the tween has played every loop; only a tween set to stay can be asked.</summary>
    public bool IsComplete => State.Completed;

    /// <summary>Whether the tween is playing in reverse.</summary>
    public bool IsBackwards => Runner.IsBackwards(Index, Generation);

    /// <summary>Jumps to the end and runs the completion callbacks; endless loops have no end and are left alone.</summary>
    public void Complete() => runner?.Complete(Index, Generation);

    /// <summary>Starts over from the start values and plays forward.</summary>
    public void Restart() => runner?.Restart(Index, Generation, pause: false);

    /// <summary>Returns to the start values and holds there, paused.</summary>
    public void Rewind() => runner?.Restart(Index, Generation, pause: true);

    /// <summary>Jumps to <paramref name="seconds"/> from the start, delay included, keeping the paused state.</summary>
    public Tween GoTo(float seconds)
    {
        Runner.GoTo(Index, Generation, seconds);
        return this;
    }

    /// <summary>Plays the tween from where it is back to its start.</summary>
    public Tween PlayBackwards()
    {
        Runner.PlayBackwards(Index, Generation, backwards: true);
        return this;
    }

    /// <summary>Plays forward again after <see cref="PlayBackwards"/>.</summary>
    public Tween PlayForward()
    {
        Runner.PlayBackwards(Index, Generation, backwards: false);
        return this;
    }

    /// <summary>Reverses the direction the tween is playing in.</summary>
    public Tween Flip()
    {
        Runner.PlayBackwards(Index, Generation, backwards: null);
        return this;
    }

    /// <summary>Pauses a playing tween and resumes a paused one.</summary>
    public Tween TogglePause()
    {
        ref var state = ref State;
        state.Paused = !state.Paused;
        return this;
    }
}
